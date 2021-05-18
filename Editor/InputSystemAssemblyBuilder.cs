using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Xml.Serialization;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using NeroWeNeed.Commons.AssemblyAnalyzers.Editor;
using NeroWeNeed.Commons.Editor;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.Utilities;
using UnityEngine.InputSystem.XR;
using UnityEngine.XR;
namespace NeroWeNeed.InputSystem.Editor
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
            var componentMapping = new InputActionComponentMapping
            {
                assembly = asset.assemblyName,
                assetGuid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(asset.asset))
            };
            foreach (var actionMap in asset.asset.actionMaps)
            {
                GenerateActionComponents(assemblyDefinition, assemblyDefinition.MainModule, actionMap, asset.assemblyNamespace, jobDefinitions, componentMapping);
            }
            if (jobDefinitions.Count > 0)
            {
                new JobRegistrationClass(assemblyDefinition.MainModule, jobDefinitions);
            }
            if (asset.asset.actionMaps.Count > 0)
            {
                assemblyDefinition.Write(assemblyPath);
                var mappingFilePath = $"{asset.assemblyPath}/{asset.assemblyName}.{InputActionComponentMappingAssetImporter.Extension}";
                using (var mappingFile = File.CreateText(mappingFilePath))
                {
                    var serializer = new XmlSerializer(typeof(InputActionComponentMapping));
                    serializer.Serialize(mappingFile, componentMapping);
                }
                AssetDatabase.ImportAsset(mappingFilePath);
                AssetDatabase.Refresh();
                return true;
            }
            else
            {
                return false;
            }
        }
        private static void GenerateActionComponents(AssemblyDefinition assemblyDefinition, ModuleDefinition moduleDefinition, InputActionMap actionMap, string @namespace, List<InputActionSystemDefinition.JobDefinition> jobDefinitions, InputActionComponentMapping mapping)
        {
            var actionMapComponent = new InputActionMapComponentDefinition(moduleDefinition, actionMap, @namespace);

            var inputActionMapMapping = new InputActionComponentMapping.InputActionMap
            {
                component = actionMapComponent.typeDefinition.FullName,
                id = actionMap.id.ToString("B")
            };
            var actionComponents = actionMap.actions.Select(action =>
            {
                var fieldType = GetFieldType(moduleDefinition, action.expectedControlType);
                return fieldType.Item1 == null ? null : new InputActionComponentDefinition(moduleDefinition, actionMap, action, @namespace, fieldType.Item1, fieldType.Item2 ?? fieldType.Item1);
            }).Where(componentDefinition => componentDefinition != null).ToArray();
            foreach (var actionComponent in actionComponents)
            {
                inputActionMapMapping.actions.Add(new InputActionComponentMapping.InputActionMap.InputAction
                {
                    component = actionComponent.typeDefinition.FullName,
                    id = actionComponent.actionId.ToString("B")
                });
            }
            mapping.actionMaps.Add(inputActionMapMapping);
            var initSystem = new InputActionInitSystemDefinition(actionMapComponent, actionComponents, actionMap, assemblyDefinition, moduleDefinition, @namespace);
            var disposeSystem = new InputActionDisposeSystemDefinition(actionMapComponent, actionComponents, actionMap, assemblyDefinition, moduleDefinition, @namespace);
            var updateSystem = new InputActionSystemDefinition(actionMapComponent, actionComponents, moduleDefinition, @namespace);
            jobDefinitions.Add(updateSystem.jobTypeDefinition);

        }
        private static (TypeReference, TypeReference) GetFieldType(ModuleDefinition moduleDefinition, string type)
        {
            switch (type)
            {
                case "Vector2":
                    return (moduleDefinition.ImportReference(typeof(float2)), moduleDefinition.ImportReference(typeof(Vector2)));
                case "Vector3":
                    return (moduleDefinition.ImportReference(typeof(float3)), moduleDefinition.ImportReference(typeof(Vector3)));
                case "Quaternion":
                    return (moduleDefinition.ImportReference(typeof(quaternion)), moduleDefinition.ImportReference(typeof(Quaternion)));
                case "Pose":
                    return (moduleDefinition.ImportReference(typeof(PoseState)), null);
                case "Bone":
                    return (moduleDefinition.ImportReference(typeof(UnityEngine.InputSystem.XR.Bone)), null);
                case "Button":
                    return (moduleDefinition.TypeSystem.Single, null);
                case "Touch":
                    return (moduleDefinition.ImportReference(typeof(TouchState)), null);
                case "Integer":
                    return (moduleDefinition.TypeSystem.Int32, null);
                case "Double":
                    return (moduleDefinition.TypeSystem.Double, null);
                case "Analog":
                    return (moduleDefinition.TypeSystem.Single, null);
                case "Axis":
                    return (moduleDefinition.TypeSystem.Single, null);
                case "Dpad":
                    return (moduleDefinition.ImportReference(typeof(float2)), moduleDefinition.ImportReference(typeof(Vector2)));
                case "Stick":
                    return (moduleDefinition.ImportReference(typeof(float2)), moduleDefinition.ImportReference(typeof(Vector2)));
                default:
                    return (null, null);
            }
        }

        private class InputActionMapComponentDefinition
        {
            public TypeDefinition typeDefinition;
            public FieldDefinition guidField;
            public InputActionMap actionMap;
            public InputActionMapComponentDefinition(ModuleDefinition moduleDefinition, InputActionMap actionMap, string @namespace)
            {
                this.actionMap = actionMap;
                typeDefinition = new TypeDefinition(@namespace, $"InputActionMap_{actionMap.name}", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.SequentialLayout | TypeAttributes.Serializable, moduleDefinition.ImportReference(typeof(ValueType)));
                typeDefinition.Interfaces.Add(new InterfaceImplementation(moduleDefinition.ImportReference(typeof(IComponentData))));
                guidField = new FieldDefinition("Id", FieldAttributes.Public | FieldAttributes.Static | FieldAttributes.InitOnly, moduleDefinition.ImportReference(typeof(Guid)));
                StaticConstructor(moduleDefinition);
                typeDefinition.Fields.Add(guidField);
                moduleDefinition.Types.Add(typeDefinition);
            }
            private void StaticConstructor(ModuleDefinition moduleDefinition)
            {
                MethodDefinition staticConstructor = new MethodDefinition(".cctor", Mono.Cecil.MethodAttributes.Private | Mono.Cecil.MethodAttributes.HideBySig | Mono.Cecil.MethodAttributes.SpecialName | Mono.Cecil.MethodAttributes.RTSpecialName | Mono.Cecil.MethodAttributes.Static, moduleDefinition.TypeSystem.Void);
                typeDefinition.IsBeforeFieldInit = true;
                var processor = staticConstructor.Body.GetILProcessor();
                processor.Emit(OpCodes.Ldstr, actionMap.id.ToString("B"));
                processor.Emit(OpCodes.Ldstr, "B");
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(Guid).GetMethod(nameof(Guid.ParseExact))));
                processor.Emit(OpCodes.Stsfld, guidField);
                processor.Emit(OpCodes.Ret);
                typeDefinition.Methods.Add(staticConstructor);
            }
        }
        private class InputActionComponentDefinition
        {
            public TypeDefinition typeDefinition;
            public FieldDefinition startTimeField;
            public FieldDefinition timeField;
            public FieldDefinition phaseField;
            public FieldDefinition deviceIdField;
            public FieldDefinition valueField;
            public TypeReference readValueType;
            public TypeReference fieldType;
            public Guid actionId;
            public string name;
            public InputActionComponentDefinition(ModuleDefinition moduleDefinition, InputActionMap actionMap, InputAction action, string @namespace, TypeReference fieldType, TypeReference readValueType)
            {
                this.readValueType = readValueType;
                this.fieldType = fieldType;
                typeDefinition = new TypeDefinition(@namespace, $"InputAction_{actionMap.name}_{action.name}", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.SequentialLayout | TypeAttributes.Serializable, moduleDefinition.ImportReference(typeof(ValueType)));
                var interfaceType = moduleDefinition.ImportReference(typeof(IInputStateComponentData<>)).MakeGenericInstanceType(fieldType);
                typeDefinition.Interfaces.Add(new InterfaceImplementation(interfaceType));

                startTimeField = new FieldDefinition("startTime", FieldAttributes.Public, moduleDefinition.TypeSystem.Double);
                typeDefinition.Fields.Add(startTimeField);
                timeField = new FieldDefinition("time", FieldAttributes.Public, moduleDefinition.TypeSystem.Double);
                typeDefinition.Fields.Add(timeField);
                phaseField = new FieldDefinition("phase", FieldAttributes.Public, moduleDefinition.ImportReference(typeof(InputActionPhase)));
                typeDefinition.Fields.Add(phaseField);
                deviceIdField = new FieldDefinition("deviceId", FieldAttributes.Public, moduleDefinition.TypeSystem.Int32);
                typeDefinition.Fields.Add(deviceIdField);
                valueField = new FieldDefinition("value", FieldAttributes.Public, fieldType);
                typeDefinition.Fields.Add(valueField);
                actionId = action.id;
                name = action.name;
                var valueProperty = new PropertyDefinition("Value", PropertyAttributes.None, fieldType);
                //Getter
                var getter = new MethodDefinition("get_Value", MethodAttributes.ReuseSlot | MethodAttributes.Virtual | MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName, fieldType);
                var getterProcessor = getter.Body.GetILProcessor();
                getterProcessor.Emit(OpCodes.Ldarg_0);
                getterProcessor.Emit(OpCodes.Ldfld, valueField);
                getterProcessor.Emit(OpCodes.Ret);
                typeDefinition.Methods.Add(getter);
                valueProperty.GetMethod = getter;
                //Setter
                var setter = new MethodDefinition("set_Value", MethodAttributes.ReuseSlot | MethodAttributes.Virtual | MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName, moduleDefinition.TypeSystem.Void);
                var setterParameter = new ParameterDefinition(fieldType);
                setter.Parameters.Add(setterParameter);
                var setterProcessor = setter.Body.GetILProcessor();
                setterProcessor.Emit(OpCodes.Ldarg_0);
                setterProcessor.Emit(OpCodes.Ldarg_1);
                setterProcessor.Emit(OpCodes.Stfld, valueField);
                setterProcessor.Emit(OpCodes.Ret);
                typeDefinition.Methods.Add(setter);
                valueProperty.SetMethod = setter;
                typeDefinition.Properties.Add(valueProperty);
                moduleDefinition.Types.Add(typeDefinition);
            }
        }
        private class InputActionSystemDefinition
        {
            public TypeDefinition typeDefinition;
            public JobDefinition jobTypeDefinition;
            public InputActionComponentDefinition[] actionComponents;
            public InputActionMapComponentDefinition actionMapComponent;
            public FieldDefinition queryField;
            public FieldDefinition traceSystemField;
            public FieldDefinition traceClearSystemField;
            public InputActionSystemDefinition(
                InputActionMapComponentDefinition actionMapComponentDefinition,
                InputActionComponentDefinition[] actionComponentDefinitions,
                ModuleDefinition moduleDefinition,
                string @namespace)
            {
                this.actionComponents = actionComponentDefinitions;
                this.actionMapComponent = actionMapComponentDefinition;
                this.typeDefinition = new TypeDefinition(@namespace, $"InputUpdateSystem_{actionMapComponentDefinition.actionMap.name}", TypeAttributes.Public | TypeAttributes.Class, moduleDefinition.ImportReference(typeof(InputUpdateSystemBase)));
                this.jobTypeDefinition = new JobDefinition(moduleDefinition, actionComponents, typeDefinition);
                var groupAttr = new CustomAttribute(moduleDefinition.ImportReference(typeof(UpdateInGroupAttribute).GetConstructor(new Type[] { typeof(Type) })));
                groupAttr.ConstructorArguments.Add(new CustomAttributeArgument(moduleDefinition.TypeSystem.TypedReference, moduleDefinition.ImportReference(typeof(InputUpdateSystemGroup))));
                typeDefinition.CustomAttributes.Add(groupAttr);
                traceSystemField = new FieldDefinition("inputActionProcessorSystem", FieldAttributes.Family, moduleDefinition.ImportReference(typeof(InputActionProcessorSystem)));
                typeDefinition.Fields.Add(traceSystemField);
                traceClearSystemField = new FieldDefinition("inputActionTraceClearSystem", FieldAttributes.Family, moduleDefinition.ImportReference(typeof(InputActionCleanupSystem)));
                typeDefinition.Fields.Add(traceClearSystemField);
                queryField = new FieldDefinition("query", FieldAttributes.Family, moduleDefinition.ImportReference(typeof(EntityQuery)));
                typeDefinition.Fields.Add(queryField);
                Constructor(moduleDefinition);
                OnCreate(moduleDefinition);
                OnUpdate(moduleDefinition);
                moduleDefinition.Types.Add(typeDefinition);

            }
            private void Constructor(ModuleDefinition moduleDefinition)
            {
                var ctor = new MethodDefinition(".ctor", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.HideBySig | Mono.Cecil.MethodAttributes.SpecialName | Mono.Cecil.MethodAttributes.RTSpecialName, moduleDefinition.TypeSystem.Void);
                var il = ctor.Body.GetILProcessor();
                il.Emit(OpCodes.Ret);
                typeDefinition.Methods.Add(ctor);
            }
            private void OnCreate(ModuleDefinition moduleDefinition)
            {
                var methodDefinition = new MethodDefinition("OnCreate", MethodAttributes.Family | MethodAttributes.HideBySig | MethodAttributes.Virtual, moduleDefinition.TypeSystem.Void);
                var baseType = typeDefinition.BaseType.Resolve();
                methodDefinition.Body.InitLocals = true;
                var processor = methodDefinition.Body.GetILProcessor();
                processor.Body.SimplifyMacros();
                var actionIndicesVariable = new VariableDefinition(moduleDefinition.ImportReference(typeof(NativeHashMap<Guid, int>)));
                var actionIndicesSetter = moduleDefinition.ImportReference(typeof(NativeHashMap<Guid, int>).GetProperty("Item").SetMethod);
                processor.Body.Variables.Add(actionIndicesVariable);
                //Calls
                var parseGuid = moduleDefinition.ImportReference(typeof(Guid).GetMethod(nameof(Guid.ParseExact)));
                //Init Query field
                processor.Emit(OpCodes.Ldarg_0);
                var getEntityQueryCall = moduleDefinition.ImportReference(typeof(SystemBase).GetMethod("GetEntityQuery", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, new Type[] { typeof(ComponentType[]) }, null));
                var componentTypeReference = moduleDefinition.ImportReference(typeof(ComponentType));
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldc_I4, actionComponents.Length);
                processor.Emit(OpCodes.Newarr, componentTypeReference);
                int offset = 0;
                foreach (var component in actionComponents)
                {
                    var componentTypeReadWrite = new GenericInstanceMethod(moduleDefinition.ImportReference(typeof(ComponentType).GetMethod(nameof(ComponentType.ReadWrite), Type.EmptyTypes)));
                    componentTypeReadWrite.GenericArguments.Add(component.typeDefinition);
                    processor.Emit(OpCodes.Dup);
                    processor.Emit(OpCodes.Ldc_I4, offset++);
                    processor.Emit(OpCodes.Call, componentTypeReadWrite);
                    processor.Emit(OpCodes.Stelem_Any, componentTypeReference);
                }
                processor.Emit(OpCodes.Call, getEntityQueryCall);
                processor.Emit(OpCodes.Stfld, queryField);
                //Init Input Trace
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(ComponentSystemBase).GetProperty(nameof(ComponentSystemBase.World), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).GetMethod));
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(World).GetMethod(nameof(World.GetOrCreateSystem), Type.EmptyTypes).MakeGenericMethod(typeof(InputActionProcessorSystem))));
                processor.Emit(OpCodes.Stfld, traceSystemField);
                //Init Input Trace Clear System
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(ComponentSystemBase).GetProperty(nameof(ComponentSystemBase.World), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).GetMethod));
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(World).GetMethod(nameof(World.GetOrCreateSystem), Type.EmptyTypes).MakeGenericMethod(typeof(InputActionCleanupSystem))));
                processor.Emit(OpCodes.Stfld, traceClearSystemField);
                //Init Action Map ID
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldstr, actionMapComponent.actionMap.id.ToString("B"));
                processor.Emit(OpCodes.Ldstr, "B");
                processor.Emit(OpCodes.Call, parseGuid);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(baseType.Properties.First(p => p.Name == nameof(InputUpdateSystemBase.ActionMapId)).SetMethod));
                //Init Action Indices
                processor.Emit(OpCodes.Ldc_I4, actionComponents.Length);
                processor.Emit(OpCodes.Ldc_I4, (int)Allocator.Persistent);
                processor.Emit(OpCodes.Newobj, moduleDefinition.ImportReference(typeof(NativeHashMap<Guid, int>).GetConstructor(new Type[] { typeof(int), typeof(Allocator) })));
                processor.Emit(OpCodes.Stloc, actionIndicesVariable);

                int index = 0;
                foreach (var component in actionComponents)
                {
                    processor.Emit(OpCodes.Ldloca, actionIndicesVariable);
                    processor.Emit(OpCodes.Ldstr, component.actionId.ToString("B"));
                    processor.Emit(OpCodes.Ldstr, "B");
                    processor.Emit(OpCodes.Call, parseGuid);
                    processor.Emit(OpCodes.Ldc_I4, index++);
                    processor.Emit(OpCodes.Call, actionIndicesSetter);
                }
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldloc, actionIndicesVariable);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(baseType.Properties.First(p => p.Name == "ActionIndices").SetMethod));
                //Require for Updates
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldfld, queryField);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(SystemBase).GetMethod(nameof(SystemBase.RequireForUpdate))));
                processor.Emit(OpCodes.Ret);
                processor.Body.OptimizeMacros();
                typeDefinition.Methods.Add(methodDefinition);
            }

            private void OnUpdate(ModuleDefinition moduleDefinition)
            {
                var methodDefinition = new MethodDefinition("OnUpdate", MethodAttributes.Family | MethodAttributes.HideBySig | MethodAttributes.Virtual, moduleDefinition.TypeSystem.Void);
                var baseType = typeDefinition.BaseType.Resolve();
                methodDefinition.Body.InitLocals = true;
                var processor = methodDefinition.Body.GetILProcessor();
                var jobVariable = new VariableDefinition(jobTypeDefinition.typeDefinition);
                processor.Body.Variables.Add(jobVariable);
                var jobHandleVariable = new VariableDefinition(moduleDefinition.ImportReference(typeof(JobHandle)));
                processor.Body.Variables.Add(jobHandleVariable);
                processor.Emit(OpCodes.Ldloca, jobVariable);
                processor.Emit(OpCodes.Initobj, jobTypeDefinition.typeDefinition);
                //Handles
                processor.Emit(OpCodes.Ldloca, jobVariable);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldfld, traceSystemField);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(InputActionProcessorSystem).GetProperty(nameof(InputActionProcessorSystem.Handles)).GetMethod));
                processor.Emit(OpCodes.Stfld, jobTypeDefinition.handlesField);
                //ID
                processor.Emit(OpCodes.Ldloca, jobVariable);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(baseType.Properties.First(p => p.Name == nameof(InputUpdateSystemBase.ActionMapId)).GetMethod));
                processor.Emit(OpCodes.Stfld, jobTypeDefinition.guidField);
                //componentMap
                processor.Emit(OpCodes.Ldloca, jobVariable);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(baseType.Properties.First(p => p.Name == nameof(InputUpdateSystemBase.ActionIndices)).GetMethod));
                processor.Emit(OpCodes.Stfld, jobTypeDefinition.componentMapField);
                InitJobTypeHandles(moduleDefinition, processor, jobVariable);
                //Schedule
                processor.Emit(OpCodes.Ldloc, jobVariable);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldfld, queryField);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(SystemBase).GetProperty("Dependency", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetMethod));
                var scheduleCall = new GenericInstanceMethod(moduleDefinition.ImportReference(typeof(JobEntityBatchExtensions).GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public).First(m => m.Name == nameof(JobEntityBatchExtensions.Schedule) && m.GetParameters().Length == 3)));
                scheduleCall.GenericArguments.Add(jobTypeDefinition.typeDefinition);
                processor.Emit(OpCodes.Call, scheduleCall);
                processor.Emit(OpCodes.Stloc, jobHandleVariable);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldfld, traceClearSystemField);
                processor.Emit(OpCodes.Ldloc, jobHandleVariable);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(InputActionCleanupSystem).GetMethod(nameof(InputActionCleanupSystem.AddJobHandle))));
                processor.Emit(OpCodes.Ret);
                processor.Body.OptimizeMacros();
                typeDefinition.Methods.Add(methodDefinition);
            }
            private void InitJobTypeHandles(ModuleDefinition moduleDefinition, ILProcessor processor, VariableDefinition jobVariable)
            {
                foreach (var component in actionComponents)
                {
                    var jobField = jobTypeDefinition.actionComponents.First(c => c.componentDefinition == component).componentTypeHandleField;
                    processor.Emit(OpCodes.Ldloca, jobVariable);
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldc_I4_0);
                    var call = new GenericInstanceMethod(moduleDefinition.ImportReference(typeof(ComponentSystemBase).GetMethod(nameof(ComponentSystemBase.GetComponentTypeHandle))));
                    call.GenericArguments.Add(component.typeDefinition);
                    processor.Emit(OpCodes.Call, call);
                    processor.Emit(OpCodes.Stfld, jobField);
                }
            }
            public class JobDefinition
            {
                public TypeDefinition typeDefinition;
                public ComponentInfo[] actionComponents;
                public FieldDefinition handlesField;
                public FieldDefinition guidField;
                public FieldDefinition componentMapField;
                public struct ComponentInfo
                {
                    public InputActionComponentDefinition componentDefinition;
                    public FieldDefinition componentTypeHandleField;
                }
                public JobDefinition(ModuleDefinition moduleDefinition, InputActionComponentDefinition[] actionComponentDefinitions, TypeDefinition containerTypeDefinition)
                {
                    typeDefinition = new TypeDefinition(null, "ProcessJob", TypeAttributes.NestedPublic | TypeAttributes.Sealed | TypeAttributes.SequentialLayout | TypeAttributes.Serializable, moduleDefinition.ImportReference(typeof(ValueType)));
                    typeDefinition.Interfaces.Add(new InterfaceImplementation(moduleDefinition.ImportReference(typeof(IJobEntityBatch))));
                    typeDefinition.CustomAttributes.Add(new CustomAttribute(moduleDefinition.ImportReference(typeof(BurstCompileAttribute).GetConstructor(Type.EmptyTypes))));
                    handlesField = new FieldDefinition("handles", FieldAttributes.Public, moduleDefinition.ImportReference(typeof(NativeMultiHashMap<Guid, NativeInputActionBuffer.ActionEventHandle>)));
                    handlesField.CustomAttributes.Add(new CustomAttribute(moduleDefinition.ImportReference(typeof(ReadOnlyAttribute).GetConstructor(Type.EmptyTypes))));
                    typeDefinition.Fields.Add(handlesField);
                    guidField = new FieldDefinition("guid", FieldAttributes.Public, moduleDefinition.ImportReference(typeof(Guid)));
                    typeDefinition.Fields.Add(guidField);
                    componentMapField = new FieldDefinition("componentMap", FieldAttributes.Public, moduleDefinition.ImportReference(typeof(NativeHashMap<Guid, int>)));
                    componentMapField.CustomAttributes.Add(new CustomAttribute(moduleDefinition.ImportReference(typeof(ReadOnlyAttribute).GetConstructor(Type.EmptyTypes))));
                    typeDefinition.Fields.Add(componentMapField);
                    actionComponents = actionComponentDefinitions.Select(a =>
                    {
                        var fieldType = moduleDefinition.ImportReference(moduleDefinition.ImportReference(typeof(ComponentTypeHandle<>)).MakeGenericInstanceType(a.typeDefinition));
                        var field = new FieldDefinition($"{a.name}TypeHandle", FieldAttributes.Public, fieldType);
                        typeDefinition.Fields.Add(field);
                        return new ComponentInfo
                        {
                            componentDefinition = a,
                            componentTypeHandleField = field
                        };
                    }).ToArray();
                    Execute(moduleDefinition);
                    containerTypeDefinition.NestedTypes.Add(typeDefinition);
                }
                private void IndexJumpTable(
                    ModuleDefinition moduleDefinition,
                    ILProcessor processor,
                    Dictionary<InputActionComponentDefinition, VariableDefinition> nativeArrayVariables,
                    VariableDefinition enumeratorItemVariable,
                    VariableDefinition indexVariable,
                    ParameterDefinition archetypeParameter
                )
                {
                    var head = processor.Create(OpCodes.Nop);
                    processor.Append(head);
                    var labels = new List<Instruction>();
                    var breaks = new List<Instruction>();
                    var componentWriteCall = moduleDefinition.ImportReference(typeof(InputUpdateSystemJobUtility).GetMethod(nameof(InputUpdateSystemJobUtility.WriteComponents)));
                    foreach (var component in nativeArrayVariables)
                    {
                        var label = processor.Create(OpCodes.Nop);
                        processor.Append(label);
                        labels.Add(label);
                        processor.Emit(OpCodes.Ldloc, component.Value);
                        var unsafePtrCall = new GenericInstanceMethod(moduleDefinition.ImportReference(typeof(NativeArrayUnsafeUtility).GetMethod(nameof(NativeArrayUnsafeUtility.GetUnsafePtr))));
                        unsafePtrCall.GenericArguments.Add(component.Key.typeDefinition);
                        processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(unsafePtrCall));
                        processor.Emit(OpCodes.Ldloca, enumeratorItemVariable);
                        processor.Emit(OpCodes.Ldarga, archetypeParameter);
                        processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(ArchetypeChunk).GetProperty(nameof(ArchetypeChunk.Count)).GetMethod));
                        var lastInstruction = processor.Create(OpCodes.Call, componentWriteCall);
                        processor.Append(lastInstruction);
                        breaks.Add(lastInstruction);
                    }
                    var switchOut = processor.Create(OpCodes.Nop);
                    processor.Append(switchOut);
                    for (int i = 0; i < breaks.Count; i++)
                    {
                        processor.InsertAfter(breaks[i], processor.Create(OpCodes.Br, switchOut));
                    }
                    var postHead = processor.Create(OpCodes.Ldloc, indexVariable);
                    processor.InsertAfter(head, postHead);
                    head = postHead;
                    postHead = processor.Create(OpCodes.Switch, labels.ToArray());
                    processor.InsertAfter(head, postHead);
                    head = postHead;
                    postHead = processor.Create(OpCodes.Br, switchOut);
                    processor.InsertAfter(head, postHead);
                    head = postHead;
                }
                private void Execute(ModuleDefinition moduleDefinition)
                {
                    var methodDefinition = new MethodDefinition(nameof(IJobEntityBatch.Execute), MethodAttributes.Public | MethodAttributes.ReuseSlot | MethodAttributes.HideBySig | MethodAttributes.Virtual, moduleDefinition.TypeSystem.Void);
                    var archetypeChunkParameter = new ParameterDefinition("batchInChunk", ParameterAttributes.None, moduleDefinition.ImportReference(typeof(ArchetypeChunk)));
                    methodDefinition.Parameters.Add(archetypeChunkParameter);
                    methodDefinition.Parameters.Add(new ParameterDefinition("batchIndex", ParameterAttributes.None, moduleDefinition.TypeSystem.Int32));
                    methodDefinition.Body.InitLocals = true;
                    var processor = methodDefinition.Body.GetILProcessor();
                    processor.Body.SimplifyMacros();
                    //Variables
                    var enumeratorVariable = new VariableDefinition(moduleDefinition.ImportReference(typeof(NativeMultiHashMap<Guid, NativeInputActionBuffer.ActionEventHandle>.Enumerator)));
                    processor.Body.Variables.Add(enumeratorVariable);
                    var enumeratorItemVariable = new VariableDefinition(moduleDefinition.ImportReference(typeof(NativeInputActionBuffer.ActionEventHandle)));
                    processor.Body.Variables.Add(enumeratorItemVariable);
                    var indexVariable = new VariableDefinition(moduleDefinition.TypeSystem.Int32);
                    processor.Body.Variables.Add(indexVariable);
                    var nativeArrayVariables = new Dictionary<InputActionComponentDefinition, VariableDefinition>();
                    //Get Native Arrays
                    foreach (var component in actionComponents)
                    {
                        var nativeArrayType = moduleDefinition.ImportReference(typeof(NativeArray<>)).MakeGenericInstanceType(component.componentDefinition.typeDefinition);
                        var nativeArrayVariable = new VariableDefinition(nativeArrayType);
                        processor.Body.Variables.Add(nativeArrayVariable);
                        nativeArrayVariables[component.componentDefinition] = nativeArrayVariable;
                        processor.Emit(OpCodes.Ldarga, archetypeChunkParameter);
                        processor.Emit(OpCodes.Ldarg_0);
                        processor.Emit(OpCodes.Ldfld, component.componentTypeHandleField);
                        var call = new GenericInstanceMethod(moduleDefinition.ImportReference(typeof(ArchetypeChunk).GetGenericMethod(nameof(ArchetypeChunk.GetNativeArray), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)));
                        call.GenericArguments.Add(component.componentDefinition.typeDefinition);
                        processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(call));
                        processor.Emit(OpCodes.Stloc, nativeArrayVariable);
                        /* //GetUnsafePtr, might throw error
                        var unsafePtrCall = new GenericInstanceMethod(moduleDefinition.ImportReference(typeof(NativeArrayUnsafeUtility).GetMethod(nameof(NativeArrayUnsafeUtility.GetUnsafePtr))));
                        unsafePtrCall.GenericArguments.Add(component.componentDefinition.typeDefinition);
                        processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(unsafePtrCall));
                        processor.Emit(OpCodes.Ldarg_0);
                        processor.Emit(OpCodes.Ldflda, component.componentValueField);
                        var addressOf = new GenericInstanceMethod(moduleDefinition.ImportReference(typeof(UnsafeUtility).GetMethod(nameof(UnsafeUtility.AddressOf))));
                        addressOf.GenericArguments.Add(component.componentValueField.FieldType);
                        processor.Emit(OpCodes.Call, addressOf);
                        processor.Emit(OpCodes.Sizeof, component.componentValueField.FieldType);
                        processor.Emit(OpCodes.Ldarga, archetypeChunkParameter);
                        processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(ArchetypeChunk).GetProperty(nameof(ArchetypeChunk.Count)).GetMethod));
                        processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(UnsafeUtility).GetMethod(nameof(UnsafeUtility.MemCpyReplicate)))); */
                    }
                    //handles.GetValuesForKey
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldflda, handlesField);
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldfld, guidField);
                    processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(NativeMultiHashMap<Guid, NativeInputActionBuffer.ActionEventHandle>).GetMethod(nameof(NativeMultiHashMap<Guid, NativeInputActionBuffer.ActionEventHandle>.GetValuesForKey))));
                    var whileStart = processor.Create(OpCodes.Stloc, enumeratorVariable);
                    processor.Append(whileStart);
                    //Get Current
                    var whileContentStart = processor.Create(OpCodes.Ldloca, enumeratorVariable);
                    processor.Append(whileContentStart);

                    processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(NativeMultiHashMap<Guid, NativeInputActionBuffer.ActionEventHandle>.Enumerator).GetProperty(nameof(NativeMultiHashMap<Guid, NativeInputActionBuffer.ActionEventHandle>.Enumerator.Current)).GetMethod));
                    processor.Emit(OpCodes.Stloc, enumeratorItemVariable);
                    //Get Index
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldflda, componentMapField);
                    processor.Emit(OpCodes.Ldloca, enumeratorItemVariable);
                    processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(NativeInputActionBuffer.ActionEventHandle).GetProperty(nameof(NativeInputActionBuffer.ActionEventHandle.Header)).GetMethod));
                    processor.Emit(OpCodes.Ldfld, moduleDefinition.ImportReference(typeof(InputActionHeaderData).GetField(nameof(InputActionHeaderData.actionId))));
                    processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(NativeHashMap<Guid, int>).GetProperty("Item").GetMethod));
                    processor.Emit(OpCodes.Stloc, indexVariable);
                    IndexJumpTable(moduleDefinition, processor, nativeArrayVariables, enumeratorItemVariable, indexVariable, archetypeChunkParameter);
                    //Loop Condition
                    var whileCondition = processor.Create(OpCodes.Ldloca, enumeratorVariable);
                    processor.Append(whileCondition);
                    processor.InsertAfter(whileStart, processor.Create(OpCodes.Br, whileCondition));
                    processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(NativeMultiHashMap<Guid, NativeInputActionBuffer.ActionEventHandle>.Enumerator).GetMethod(nameof(NativeMultiHashMap<Guid, NativeInputActionBuffer.ActionEventHandle>.Enumerator.MoveNext))));
                    processor.Emit(OpCodes.Brtrue, whileContentStart);
                    processor.Emit(OpCodes.Ret);
                    processor.Body.OptimizeMacros();
                    typeDefinition.Methods.Add(methodDefinition);
                }
            }
        }
        private class InputActionInitSystemDefinition
        {
            public TypeDefinition typeDefinition;
            public InputActionMapComponentDefinition actionMapComponentDefinition;
            public InputActionComponentDefinition[] componentDefinitions;
            public InputActionMap actionMap;
            public InputActionInitSystemDefinition(
                InputActionMapComponentDefinition actionMapComponentDefinition,
                InputActionComponentDefinition[] componentDefinitions,
                InputActionMap actionMap,
                AssemblyDefinition assemblyDefinition,
                ModuleDefinition moduleDefinition,
                string @namespace)
            {
                this.componentDefinitions = componentDefinitions;
                this.actionMapComponentDefinition = actionMapComponentDefinition;
                this.actionMap = actionMap;
                typeDefinition = new TypeDefinition(@namespace, $"InputInitSystem_{actionMap.name}", TypeAttributes.Public | TypeAttributes.Class, moduleDefinition.ImportReference(typeof(InputInitSystemBase)));
                Constructor(moduleDefinition);
                OnCreate(moduleDefinition);
                moduleDefinition.Types.Add(typeDefinition);
            }
            private void Constructor(ModuleDefinition moduleDefinition)
            {
                var ctor = new MethodDefinition(".ctor", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.HideBySig | Mono.Cecil.MethodAttributes.SpecialName | Mono.Cecil.MethodAttributes.RTSpecialName, moduleDefinition.TypeSystem.Void);
                var il = ctor.Body.GetILProcessor();
                il.Emit(OpCodes.Ret);
                typeDefinition.Methods.Add(ctor);
            }
            private void OnCreate(ModuleDefinition moduleDefinition)
            {
                var methodDefinition = new MethodDefinition("OnCreate", MethodAttributes.Family | MethodAttributes.HideBySig | MethodAttributes.Virtual, moduleDefinition.TypeSystem.Void);
                methodDefinition.Body.InitLocals = true;
                //References
                var componentTypeReference = moduleDefinition.ImportReference(typeof(ComponentType));
                var processor = methodDefinition.Body.GetILProcessor();
                //Calls
                var getEntityQueryCall = moduleDefinition.ImportReference(typeof(SystemBase).GetMethod("GetEntityQuery", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, new Type[] { typeof(EntityQueryDesc[]) }, null));
                //Variables 
                var queryVariable = new VariableDefinition(moduleDefinition.ImportReference(typeof(EntityQuery)));
                processor.Body.Variables.Add(queryVariable);
                processor.Body.SimplifyMacros();
                //Init Components
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldc_I4, componentDefinitions.Length);
                processor.Emit(OpCodes.Newarr, moduleDefinition.ImportReference(typeof(ComponentType)));
                for (int i = 0; i < componentDefinitions.Length; i++)
                {
                    var componentTypeReadWrite = new GenericInstanceMethod(moduleDefinition.ImportReference(typeof(ComponentType).GetMethod(nameof(ComponentType.ReadWrite), Type.EmptyTypes)));
                    componentTypeReadWrite.GenericArguments.Add(componentDefinitions[i].typeDefinition);
                    processor.Emit(OpCodes.Dup);
                    processor.Emit(OpCodes.Ldc_I4, i);
                    processor.Emit(OpCodes.Call, componentTypeReadWrite);
                    processor.Emit(OpCodes.Stelem_Any, componentTypeReference);
                }
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(InputInitSystemBase).GetProperty("ComponentTypes").SetMethod));
                //Init Query
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldc_I4_1);
                processor.Emit(OpCodes.Newarr, moduleDefinition.ImportReference(typeof(EntityQueryDesc)));
                processor.Emit(OpCodes.Dup);
                processor.Emit(OpCodes.Ldc_I4_0);
                processor.Emit(OpCodes.Newobj, moduleDefinition.ImportReference(typeof(EntityQueryDesc).GetConstructor(Type.EmptyTypes)));
                processor.Emit(OpCodes.Dup);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(InputInitSystemBase).GetProperty("ComponentTypes").GetMethod));
                processor.Emit(OpCodes.Stfld, moduleDefinition.ImportReference(typeof(EntityQueryDesc).GetField(nameof(EntityQueryDesc.None))));
                processor.Emit(OpCodes.Dup);
                processor.Emit(OpCodes.Ldc_I4_1);
                processor.Emit(OpCodes.Newarr, moduleDefinition.ImportReference(typeof(ComponentType)));
                processor.Emit(OpCodes.Dup);
                processor.Emit(OpCodes.Ldc_I4_0);
                var call = new GenericInstanceMethod(moduleDefinition.ImportReference(typeof(ComponentType).GetMethod(nameof(ComponentType.ReadWrite), Type.EmptyTypes)));
                call.GenericArguments.Add(actionMapComponentDefinition.typeDefinition);

                processor.Emit(OpCodes.Call, call);
                processor.Emit(OpCodes.Stelem_Any, componentTypeReference);
                processor.Emit(OpCodes.Stfld, moduleDefinition.ImportReference(typeof(EntityQueryDesc).GetField(nameof(EntityQueryDesc.All))));
                processor.Emit(OpCodes.Stelem_Any, moduleDefinition.ImportReference(typeof(EntityQueryDesc)));
                processor.Emit(OpCodes.Call, getEntityQueryCall);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(InputInitSystemBase).GetProperty("Query").SetMethod));
                processor.Emit(OpCodes.Ret);
                processor.Body.OptimizeMacros();
                typeDefinition.Methods.Add(methodDefinition);
            }
        }
        private class InputActionDisposeSystemDefinition
        {
            public TypeDefinition typeDefinition;
            public InputActionMapComponentDefinition actionMapComponentDefinition;
            public InputActionComponentDefinition[] componentDefinitions;
            public InputActionMap actionMap;
            public InputActionDisposeSystemDefinition(
                InputActionMapComponentDefinition actionMapComponentDefinition,
                InputActionComponentDefinition[] componentDefinitions,
                InputActionMap actionMap,
                AssemblyDefinition assemblyDefinition,
                ModuleDefinition moduleDefinition,
                string @namespace)
            {
                this.actionMapComponentDefinition = actionMapComponentDefinition;

                this.componentDefinitions = componentDefinitions;
                this.actionMap = actionMap;
                typeDefinition = new TypeDefinition(@namespace, $"InputDisposeSystem_{actionMap.name}", TypeAttributes.Public | TypeAttributes.Class, moduleDefinition.ImportReference(typeof(InputDisposeSystemBase)));
                Constructor(moduleDefinition);
                OnCreate(moduleDefinition);
                moduleDefinition.Types.Add(typeDefinition);
            }
            private void Constructor(ModuleDefinition moduleDefinition)
            {
                var ctor = new MethodDefinition(".ctor", Mono.Cecil.MethodAttributes.Public | Mono.Cecil.MethodAttributes.HideBySig | Mono.Cecil.MethodAttributes.SpecialName | Mono.Cecil.MethodAttributes.RTSpecialName, moduleDefinition.TypeSystem.Void);
                var il = ctor.Body.GetILProcessor();
                il.Emit(OpCodes.Ret);
                typeDefinition.Methods.Add(ctor);
            }
            private void OnCreate(ModuleDefinition moduleDefinition)
            {
                var methodDefinition = new MethodDefinition("OnCreate", MethodAttributes.Family | MethodAttributes.HideBySig | MethodAttributes.Virtual, moduleDefinition.TypeSystem.Void);
                methodDefinition.Body.InitLocals = true;
                //References
                var componentTypeReference = moduleDefinition.ImportReference(typeof(ComponentType));
                var processor = methodDefinition.Body.GetILProcessor();
                //Calls
                var getEntityQueryCall = moduleDefinition.ImportReference(typeof(SystemBase).GetMethod("GetEntityQuery", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, new Type[] { typeof(EntityQueryDesc[]) }, null));
                processor.Body.SimplifyMacros();
                //Init Components
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldc_I4, componentDefinitions.Length);
                processor.Emit(OpCodes.Newarr, moduleDefinition.ImportReference(typeof(ComponentType)));
                for (int i = 0; i < componentDefinitions.Length; i++)
                {
                    var componentTypeReadWrite = new GenericInstanceMethod(moduleDefinition.ImportReference(typeof(ComponentType).GetMethod(nameof(ComponentType.ReadWrite), Type.EmptyTypes)));
                    componentTypeReadWrite.GenericArguments.Add(componentDefinitions[i].typeDefinition);
                    processor.Emit(OpCodes.Dup);
                    processor.Emit(OpCodes.Ldc_I4, i);
                    processor.Emit(OpCodes.Call, componentTypeReadWrite);
                    processor.Emit(OpCodes.Stelem_Any, componentTypeReference);
                }
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(InputDisposeSystemBase).GetProperty("ComponentTypes").SetMethod));
                //Init Query
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldc_I4_1);
                processor.Emit(OpCodes.Newarr, moduleDefinition.ImportReference(typeof(EntityQueryDesc)));
                processor.Emit(OpCodes.Dup);
                processor.Emit(OpCodes.Ldc_I4_0);
                processor.Emit(OpCodes.Newobj, moduleDefinition.ImportReference(typeof(EntityQueryDesc).GetConstructor(Type.EmptyTypes)));
                processor.Emit(OpCodes.Dup);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(InputDisposeSystemBase).GetProperty("ComponentTypes").GetMethod));
                processor.Emit(OpCodes.Stfld, moduleDefinition.ImportReference(typeof(EntityQueryDesc).GetField(nameof(EntityQueryDesc.Any))));
                processor.Emit(OpCodes.Dup);
                processor.Emit(OpCodes.Ldc_I4_1);
                processor.Emit(OpCodes.Newarr, moduleDefinition.ImportReference(typeof(ComponentType)));
                processor.Emit(OpCodes.Dup);
                processor.Emit(OpCodes.Ldc_I4_0);
                var call = new GenericInstanceMethod(moduleDefinition.ImportReference(typeof(ComponentType).GetMethod(nameof(ComponentType.ReadWrite), Type.EmptyTypes)));
                call.GenericArguments.Add(actionMapComponentDefinition.typeDefinition);

                processor.Emit(OpCodes.Call, call);
                processor.Emit(OpCodes.Stelem_Any, componentTypeReference);
                processor.Emit(OpCodes.Stfld, moduleDefinition.ImportReference(typeof(EntityQueryDesc).GetField(nameof(EntityQueryDesc.None))));
                processor.Emit(OpCodes.Stelem_Any, moduleDefinition.ImportReference(typeof(EntityQueryDesc)));
                processor.Emit(OpCodes.Call, getEntityQueryCall);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(InputDisposeSystemBase).GetProperty("Query").SetMethod));
                processor.Emit(OpCodes.Ret);
                processor.Body.OptimizeMacros();
                typeDefinition.Methods.Add(methodDefinition);
            }
        }
        private class JobRegistrationClass
        {
            public TypeDefinition typeDefinition;
            public List<InputActionSystemDefinition.JobDefinition> jobDefinitions;
            public JobRegistrationClass(ModuleDefinition moduleDefinition, List<InputActionSystemDefinition.JobDefinition> jobDefinitions)
            {
                this.typeDefinition = new TypeDefinition(null, $"__JobReflectionData__{Guid.NewGuid():N}", TypeAttributes.Abstract | TypeAttributes.Sealed | TypeAttributes.Class | TypeAttributes.NotPublic, moduleDefinition.TypeSystem.Object);
                this.jobDefinitions = jobDefinitions;
                EarlyInit(moduleDefinition, CreateJobReflectionData(moduleDefinition));
                moduleDefinition.Types.Add(typeDefinition);
            }
            private MethodDefinition CreateJobReflectionData(ModuleDefinition moduleDefinition)
            {
                var methodDefinition = new MethodDefinition("CreateJobReflectionData", MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig, moduleDefinition.TypeSystem.Void);
                methodDefinition.Body.InitLocals = true;
                var processor = methodDefinition.Body.GetILProcessor();
                processor.Body.SimplifyMacros();
                foreach (var jobDefinition in jobDefinitions)
                {
                    var earlyJobInitCall = new GenericInstanceMethod(moduleDefinition.ImportReference(typeof(JobEntityBatchExtensions).GetMethod(nameof(JobEntityBatchExtensions.EarlyJobInit))));
                    earlyJobInitCall.GenericArguments.Add(jobDefinition.typeDefinition);
                    var earlyJobInitAddress = processor.Create(OpCodes.Call, earlyJobInitCall);
                    processor.Append(earlyJobInitAddress);
                    var catchStart = processor.Create(OpCodes.Nop);
                    processor.Append(catchStart);
                    processor.Emit(OpCodes.Ldtoken, jobDefinition.typeDefinition);
                    processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(Type).GetMethod(nameof(Type.GetTypeFromHandle))));
                    var creationFailedAddress = processor.Create(OpCodes.Call, moduleDefinition.ImportReference(typeof(EarlyInitHelpers).GetMethod(nameof(EarlyInitHelpers.JobReflectionDataCreationFailed))));
                    processor.Append(creationFailedAddress);
                    var leave = processor.Create(OpCodes.Nop);
                    processor.Append(leave);
                    var tryLeave = processor.Create(OpCodes.Leave, leave);
                    processor.InsertAfter(earlyJobInitAddress, tryLeave);
                    var catchLeave = processor.Create(OpCodes.Leave, leave);
                    processor.InsertAfter(creationFailedAddress, catchLeave);
                    processor.Body.ExceptionHandlers.Add(new ExceptionHandler(ExceptionHandlerType.Catch)
                    {
                        TryStart = earlyJobInitAddress,
                        TryEnd = catchStart,
                        HandlerStart = catchStart,
                        HandlerEnd = catchLeave,
                        CatchType = moduleDefinition.ImportReference(typeof(Exception))
                    });
                }
                processor.Emit(OpCodes.Ret);
                processor.Body.OptimizeMacros();
                typeDefinition.Methods.Add(methodDefinition);
                return methodDefinition;
            }
            private void EarlyInit(ModuleDefinition moduleDefinition, MethodDefinition createJobReflectionData)
            {
                var methodDefinition = new MethodDefinition("EarlyInit", MethodAttributes.Public | MethodAttributes.Static | MethodAttributes.HideBySig, moduleDefinition.TypeSystem.Void);
                methodDefinition.CustomAttributes.Add(new CustomAttribute(moduleDefinition.ImportReference(typeof(InitializeOnLoadMethodAttribute).GetConstructor(Type.EmptyTypes))));
                methodDefinition.Body.InitLocals = true;
                var processor = methodDefinition.Body.GetILProcessor();
                processor.Body.SimplifyMacros();
                processor.Emit(OpCodes.Ldnull);

                processor.Emit(OpCodes.Ldftn, createJobReflectionData);
                processor.Emit(OpCodes.Newobj, moduleDefinition.ImportReference(typeof(EarlyInitHelpers.EarlyInitFunction).GetConstructor(new Type[] { typeof(object), typeof(IntPtr) })));
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(EarlyInitHelpers).GetMethod(nameof(EarlyInitHelpers.AddEarlyInitFunction))));
                processor.Emit(OpCodes.Ret);
                processor.Body.OptimizeMacros();
                typeDefinition.Methods.Add(methodDefinition);
            }

        }
    }

}
