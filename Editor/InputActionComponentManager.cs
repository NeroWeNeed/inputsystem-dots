using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using Unity.Entities;
using UnityEditor;
using UnityEngine;

namespace NeroWeNeed.InputSystem.Editor
{
    public static class InputActionComponentManager
    {
        private static bool initialized = false;
        private static readonly Dictionary<Guid, MappingData> inputActionMapComponents = new Dictionary<Guid, MappingData>();
        public static void Initialize()
        {
            if (!initialized)
            {
                initialized = true;
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                var actionMapComponents = new Dictionary<string, Type>();
                var actionComponents = new Dictionary<string, List<Type>>();
                foreach (var assembly in assemblies)
                {
                    if (assembly.GetCustomAttribute<InputActionAssemblyAttribute>() != null)
                    {
                        foreach (var type in assembly.GetTypes())
                        {
                            var mapAttr = type.GetCustomAttribute<InputActionMapComponentAttribute>();
                            if (mapAttr != null && typeof(IInputActionMapTag).IsAssignableFrom(type))
                            {
                                actionMapComponents[mapAttr.id] = type;
                            }
                            else
                            {
                                var actionAttr = type.GetCustomAttribute<InputActionComponentAttribute>();
                                if (actionAttr != null && typeof(IInputData).IsAssignableFrom(type))
                                {
                                    if (!actionComponents.TryGetValue(actionAttr.actionMapId, out var actionTypes))
                                    {
                                        actionTypes = new List<Type>();
                                        actionComponents[actionAttr.actionMapId] = actionTypes;
                                    }
                                    actionTypes.Add(type);
                                }
                            }
                        }
                    }
                }
                foreach (var actionMapComponent in actionMapComponents)
                {
                    inputActionMapComponents[Guid.ParseExact(actionMapComponent.Key, "B")] = new MappingData(actionMapComponent.Value, actionComponents.TryGetValue(actionMapComponent.Key, out var actionTypes) ? actionTypes.AsReadOnly() : new ReadOnlyCollection<Type>(null));
                }
                initialized = true;
            }
        }
        public static MappingData Get(Guid guid) => inputActionMapComponents[guid];
        public static Type GetActionMapComponent(Guid guid) => inputActionMapComponents[guid].actionMapComponent;
        public static bool TryGetActionMapComponent(Guid guid, out Type result)
        {
            result = null;
            if (inputActionMapComponents.TryGetValue(guid, out var data))
            {
                result = data.actionMapComponent;
                return true;
            }
            else
            {
                return false;
            }
        }
        public static ReadOnlyCollection<Type> GetActionComponents(Guid guid) => inputActionMapComponents[guid].actionComponents;
        public struct MappingData
        {
            public readonly Type actionMapComponent;
            public readonly ReadOnlyCollection<Type> actionComponents;

            public MappingData(Type actionMapComponent, ReadOnlyCollection<Type> actionComponents)
            {
                this.actionMapComponent = actionMapComponent;
                this.actionComponents = actionComponents;
            }
        }
    }
}