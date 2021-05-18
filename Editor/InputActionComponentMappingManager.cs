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
    public static class InputActionComponentMappingManager
    {
        private static bool initialized = false;
        private static Dictionary<Guid, MappingData> inputActionMapComponents = new Dictionary<Guid, MappingData>();
        public static void Initialize()
        {
            if (!initialized)
            {
                foreach (var mapping in AssetDatabase.FindAssets($"t:{nameof(InputActionComponentMappingAsset)}").Select(a => AssetDatabase.LoadAssetAtPath<InputActionComponentMappingAsset>(AssetDatabase.GUIDToAssetPath(a))))
                {
                    var assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(assembly => assembly.GetName().Name == mapping.assembly);
                    if (assembly != null)
                    {
                        foreach (var actionMap in mapping.actionMaps)
                        {
                            inputActionMapComponents[Guid.ParseExact(actionMap.id, "B")] = new MappingData
                            {
                                inputActionMapComponent = assembly.GetType(actionMap.component),
                                inputActionComponents = actionMap.actions.Select(a => assembly.GetType(a.component)).ToList().AsReadOnly()
                            };
                        }
                    }
                }
                initialized = true;
            }
        }
        public static MappingData Get(Guid guid) => inputActionMapComponents[guid];
        public static Type GetActionMapComponent(Guid guid) => inputActionMapComponents[guid].inputActionMapComponent;
        public static bool TryGetActionMapComponent(Guid guid, out Type result)
        {
            result = null;
            if (inputActionMapComponents.TryGetValue(guid, out var data))
            {
                result = data.inputActionMapComponent;
                return true;
            }
            else
            {
                return false;
            }
        }
        public static ReadOnlyCollection<Type> GetActionComponents(Guid guid) => inputActionMapComponents[guid].inputActionComponents;
        public struct MappingData
        {
            public Type inputActionMapComponent;
            public ReadOnlyCollection<Type> inputActionComponents;
        }
    }
}