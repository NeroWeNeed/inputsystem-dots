using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Mono.Cecil;
using NeroWeNeed.Commons.AssemblyAnalyzers.Editor;
using NeroWeNeed.Commons.Editor;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
namespace NeroWeNeed.InputSystem.Editor.ILGeneration
{
    public static class InputSystemAssemblyBuilder
    {
        public static bool BuildAssembly(InputSystemAssemblyDefinition asset, out string assemblyPath)
        {
            assemblyPath = $"{asset.assemblyPath}/{asset.assemblyName}.dll";
            if (!Directory.Exists(asset.assemblyPath))
            {
                Directory.CreateDirectory(asset.assemblyPath);
            }
            using var resolver = new DefaultAssemblyResolver();
            resolver.AddSearchDirectory("Library/ScriptAssemblies");
            resolver.AddSearchDirectory($"{EditorApplication.applicationContentsPath}/Managed");
            resolver.AddSearchDirectory($"{EditorApplication.applicationContentsPath}/Managed/UnityEngine");
            using var assemblyDefinition = AssemblyDefinition.CreateAssembly(new AssemblyNameDefinition(asset.assemblyName, new Version(0, 0, 0, 0)), asset.assemblyName, new ModuleParameters { AssemblyResolver = resolver });
            List<InputActionSystemDefinition.JobDefinition> jobDefinitions = new List<InputActionSystemDefinition.JobDefinition>();
            foreach (var actionMap in asset.asset.actionMaps)
            {
                GenerateActionComponents(assemblyDefinition, assemblyDefinition.MainModule, actionMap, asset.assemblyNamespace, jobDefinitions);
            }
            if (jobDefinitions.Count > 0)
            {
                new JobRegistrationClass(assemblyDefinition.MainModule, jobDefinitions);
            }
            if (asset.asset.actionMaps.Count > 0)
            {
                assemblyDefinition.CustomAttributes.Add(new CustomAttribute(assemblyDefinition.MainModule.ImportReference(typeof(InputActionAssemblyAttribute).GetConstructor(Type.EmptyTypes))));
                assemblyDefinition.Write(assemblyPath);
                return true;
            }
            else
            {
                return false;
            }
        }
        private static void GenerateActionComponents(AssemblyDefinition assemblyDefinition, ModuleDefinition moduleDefinition, InputActionMap actionMap, string @namespace, List<InputActionSystemDefinition.JobDefinition> jobDefinitions)
        {
            var actionMapComponent = new InputActionMapComponentDefinition(moduleDefinition, actionMap, @namespace);
            var actionComponents = actionMap.actions.Select(action => CreateInputActionDefinition(moduleDefinition, actionMap, action, @namespace)).ToArray();
            var initSystem = new InputActionInitSystemDefinition(actionMapComponent, actionComponents, actionMap, assemblyDefinition, moduleDefinition, @namespace);
            var disposeSystem = new InputActionDisposeSystemDefinition(actionMapComponent, actionComponents, actionMap, assemblyDefinition, moduleDefinition, @namespace);
            var updateSystem = new InputActionSystemDefinition(actionMapComponent, actionComponents, moduleDefinition, @namespace);
            jobDefinitions.Add(updateSystem.jobTypeDefinition);

        }
        private static BaseInputActionDefinition CreateInputActionDefinition(ModuleDefinition moduleDefinition, InputActionMap actionMap, InputAction action, string @namespace)
        {
            switch (action.expectedControlType)
            {
                case "Vector2":
                    return new InputActionComponentDefinition(moduleDefinition, actionMap, action, @namespace, moduleDefinition.ImportReference(typeof(float2)), moduleDefinition.ImportReference(typeof(Vector2)));
                case "Vector3":
                    return new InputActionComponentDefinition(moduleDefinition, actionMap, action, @namespace, moduleDefinition.ImportReference(typeof(float3)), moduleDefinition.ImportReference(typeof(Vector3)));
                case "Quaternion":
                    return new InputActionComponentDefinition(moduleDefinition, actionMap, action, @namespace, moduleDefinition.ImportReference(typeof(quaternion)), moduleDefinition.ImportReference(typeof(Quaternion)));
                case "Pose":
                    return new InputActionComponentDefinition(moduleDefinition, actionMap, action, @namespace, moduleDefinition.ImportReference(typeof(PoseState)));
                case "Bone":
                    return new InputActionComponentDefinition(moduleDefinition, actionMap, action, @namespace, moduleDefinition.ImportReference(typeof(UnityEngine.InputSystem.XR.Bone)));
                case "Button":
                    return new InputActionComponentDefinition(moduleDefinition, actionMap, action, @namespace, moduleDefinition.TypeSystem.Single);
                case "Touch":
                    return new InputActionComponentDefinition(moduleDefinition, actionMap, action, @namespace, moduleDefinition.ImportReference(typeof(TouchState)));
                case "Integer":
                    return new InputActionComponentDefinition(moduleDefinition, actionMap, action, @namespace, moduleDefinition.TypeSystem.Int32);
                case "Double":
                    return new InputActionComponentDefinition(moduleDefinition, actionMap, action, @namespace, moduleDefinition.TypeSystem.Double);
                case "Analog":
                case "Axis":
                    return new InputActionComponentDefinition(moduleDefinition, actionMap, action, @namespace, moduleDefinition.TypeSystem.Single);
                case "Dpad":
                case "Stick":
                    return new InputActionComponentDefinition(moduleDefinition, actionMap, action, @namespace, moduleDefinition.ImportReference(typeof(float2)), moduleDefinition.ImportReference(typeof(Vector2)));
                default:
                    return new InputActionBufferDefinition(moduleDefinition, actionMap, action, @namespace);
            }
        }
    }


}
