using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
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
            List<InputActionSystemJobDefinition> jobDefinitions = new List<InputActionSystemJobDefinition>();
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
                assemblyDefinition.Write(assemblyPath);
                return true;
            }
            else
            {
                return false;
            }
        }
        private static void GenerateActionComponents(AssemblyDefinition assemblyDefinition, ModuleDefinition moduleDefinition, InputActionMap actionMap, string @namespace, List<InputActionSystemJobDefinition> jobDefinitions)
        {
            var components = actionMap.actions.Select(action =>
            {
                var fieldType = GetFieldType(moduleDefinition, action.expectedControlType);
                return fieldType.Item1 == null ? null : new InputActionComponentDefinition(moduleDefinition, actionMap, action, @namespace, fieldType.Item1, fieldType.Item2 ?? fieldType.Item1);
            }).Where(componentDefinition => componentDefinition != null).ToArray();
            var updateSystem = new InputActionSystemDefinition(components, actionMap, assemblyDefinition, moduleDefinition, @namespace);
            var initSystem = new InputActionInitSystemDefinition(components, actionMap, assemblyDefinition, moduleDefinition, @namespace);
            var disposeSystem = new InputActionDisposeSystemDefinition(components, actionMap, assemblyDefinition, moduleDefinition, @namespace);

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

        private class InputActionComponentDefinition
        {
            public TypeDefinition typeDefinition;
            public FieldDefinition valueField;
            public FieldDefinition startTimeField;
            public FieldDefinition timeField;
            public FieldDefinition phaseField;
            public FieldDefinition deviceIdField;
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
                valueField = new FieldDefinition("value", FieldAttributes.Public, fieldType);
                typeDefinition.Fields.Add(valueField);
                startTimeField = new FieldDefinition("startTime", FieldAttributes.Public, moduleDefinition.TypeSystem.Double);
                typeDefinition.Fields.Add(startTimeField);
                timeField = new FieldDefinition("time", FieldAttributes.Public, moduleDefinition.TypeSystem.Double);
                typeDefinition.Fields.Add(timeField);
                phaseField = new FieldDefinition("phase", FieldAttributes.Public, moduleDefinition.ImportReference(typeof(InputActionPhase)));
                typeDefinition.Fields.Add(phaseField);
                deviceIdField = new FieldDefinition("deviceId", FieldAttributes.Public, moduleDefinition.TypeSystem.Int32);
                typeDefinition.Fields.Add(deviceIdField);
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
            public InputActionSystemJobDefinition jobTypeDefinition;
            public InputActionComponentDefinition[] components;
            public FieldDefinition queryField;
            public FieldDefinition traceSystemField;
            public FieldDefinition traceClearSystemField;
            public Guid actionMapId;
            public InputActionSystemDefinition(
                InputActionComponentDefinition[] componentDefinitions,
                InputActionMap actionMap,
                AssemblyDefinition assemblyDefinition,
                ModuleDefinition moduleDefinition,
                string @namespace)
            {
                this.components = componentDefinitions;
                this.actionMapId = actionMap.id;
                this.jobTypeDefinition = new InputActionSystemJobDefinition(moduleDefinition, components, actionMap, @namespace);
                this.typeDefinition = new TypeDefinition(@namespace, $"InputUpdateSystem_{actionMap.name}", TypeAttributes.Public | TypeAttributes.Class, moduleDefinition.ImportReference(typeof(InputUpdateSystemBase)));
                this.typeDefinition.NestedTypes.Add(jobTypeDefinition.typeDefinition);
                var groupAttr = new CustomAttribute(moduleDefinition.ImportReference(typeof(UpdateInGroupAttribute).GetConstructor(new Type[] { typeof(Type) })));
                groupAttr.ConstructorArguments.Add(new CustomAttributeArgument(moduleDefinition.TypeSystem.TypedReference, moduleDefinition.ImportReference(typeof(InputUpdateSystemGroup))));
                typeDefinition.CustomAttributes.Add(groupAttr);
                traceSystemField = new FieldDefinition("inputActionTraceSystem", FieldAttributes.Family, moduleDefinition.ImportReference(typeof(InputActionTraceSystem)));
                typeDefinition.Fields.Add(traceSystemField);
                traceClearSystemField = new FieldDefinition("inputActionTraceClearSystem", FieldAttributes.Family, moduleDefinition.ImportReference(typeof(InputActionTraceClearSystem)));
                typeDefinition.Fields.Add(traceClearSystemField);
                queryField = new FieldDefinition("query", FieldAttributes.Family, moduleDefinition.ImportReference(typeof(EntityQuery)));
                typeDefinition.Fields.Add(queryField);
                Constructor(moduleDefinition);
                //QueryProperty(moduleDefinition);
                OnCreate(moduleDefinition);
                OnUpdate(moduleDefinition);
                OnStartRunning(moduleDefinition);
                OnStopRunning(moduleDefinition);
                moduleDefinition.Types.Add(typeDefinition);
                var attr = new CustomAttribute(moduleDefinition.ImportReference(typeof(RegisterGenericJobTypeAttribute).GetConstructor(new Type[] { typeof(Type) })));
                attr.ConstructorArguments.Add(new CustomAttributeArgument(moduleDefinition.TypeSystem.TypedReference, (TypeReference)jobTypeDefinition.typeDefinition));
                assemblyDefinition.CustomAttributes.Add(attr);
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
                //Init Query field
                processor.Emit(OpCodes.Ldarg_0);
                var getEntityQueryCall = moduleDefinition.ImportReference(typeof(SystemBase).GetMethod("GetEntityQuery", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, null, new Type[] { typeof(ComponentType[]) }, null));
                var componentTypeReference = moduleDefinition.ImportReference(typeof(ComponentType));
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldc_I4, components.Length);
                processor.Emit(OpCodes.Newarr, componentTypeReference);
                int offset = 0;
                foreach (var component in components)
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
                //processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(baseType.Properties.First(p => p.Name == "Query").SetMethod).MakeHostInstanceGeneric(jobTypeDefinition.typeDefinition));
                //Init Input Trace
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(ComponentSystemBase).GetProperty(nameof(ComponentSystemBase.World), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).GetMethod));
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(World).GetMethod(nameof(World.GetOrCreateSystem), Type.EmptyTypes).MakeGenericMethod(typeof(InputActionTraceSystem))));
                processor.Emit(OpCodes.Stfld, traceSystemField);
                //Init Input Trace Clear System
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(ComponentSystemBase).GetProperty(nameof(ComponentSystemBase.World), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).GetMethod));
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(World).GetMethod(nameof(World.GetOrCreateSystem), Type.EmptyTypes).MakeGenericMethod(typeof(InputActionTraceClearSystem))));
                processor.Emit(OpCodes.Stfld, traceClearSystemField);
                /* processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Newobj, moduleDefinition.ImportReference(typeof(InputActionTrace).GetConstructor(Type.EmptyTypes)));


                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(baseType.Properties.First(p => p.Name == "ActionTrace").SetMethod)); */
                //Init Action Map ID
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldstr, actionMapId.ToString("B"));
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(baseType.Properties.First(p => p.Name == "ActionMapId").SetMethod));
                //Init Action Indices

                processor.Emit(OpCodes.Ldc_I4, components.Length);
                processor.Emit(OpCodes.Ldc_I4, (int)Allocator.Persistent);
                processor.Emit(OpCodes.Newobj, moduleDefinition.ImportReference(typeof(NativeHashMap<Guid, int>).GetConstructor(new Type[] { typeof(int), typeof(Allocator) })));
                processor.Emit(OpCodes.Stloc, actionIndicesVariable);
                var parseGuid = moduleDefinition.ImportReference(typeof(Guid).GetMethod(nameof(Guid.ParseExact)));
                int index = 0;
                foreach (var component in components)
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
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(SystemBase).GetMethod(nameof(SystemBase.RequireSingletonForUpdate)).MakeGenericMethod(typeof(InputActionAssetData))));
                processor.Emit(OpCodes.Ret);
                processor.Body.OptimizeMacros();
                typeDefinition.Methods.Add(methodDefinition);
            }
            private void OnStartRunning(ModuleDefinition moduleDefinition)
            {
                var methodDefinition = new MethodDefinition("OnStartRunning", MethodAttributes.Family | MethodAttributes.HideBySig | MethodAttributes.Virtual, moduleDefinition.TypeSystem.Void);
                var baseType = typeDefinition.BaseType.Resolve();
                methodDefinition.Body.InitLocals = true;
                var processor = methodDefinition.Body.GetILProcessor();
                processor.Body.SimplifyMacros();
                var getSingletonEntity = moduleDefinition.ImportReference(typeof(SystemBase).GetMethod(nameof(SystemBase.GetSingletonEntity)).MakeGenericMethod(typeof(InputActionAssetData)));
                var entityManager = moduleDefinition.ImportReference(typeof(SystemBase).GetProperty(nameof(SystemBase.EntityManager)).GetMethod);
                var componentObject = moduleDefinition.ImportReference(typeof(EntityManager).GetMethod(nameof(EntityManager.GetSharedComponentData), new Type[] { typeof(Entity) }).MakeGenericMethod(typeof(InputActionAssetData)));
                var startActionTrace = moduleDefinition.ImportReference(typeof(InputUpdateSystemBase).GetMethod("StartActionTrace", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));
                var entityManagerLoc = new VariableDefinition(moduleDefinition.ImportReference(typeof(EntityManager)));
                processor.Body.Variables.Add(entityManagerLoc);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Call, entityManager);
                processor.Emit(OpCodes.Stloc, entityManagerLoc);
                processor.Emit(OpCodes.Ldloca, entityManagerLoc);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Call, getSingletonEntity);
                processor.Emit(OpCodes.Call, componentObject);
                processor.Emit(OpCodes.Call, startActionTrace);
                processor.Emit(OpCodes.Ret);
                processor.Body.OptimizeMacros();
                typeDefinition.Methods.Add(methodDefinition);
            }
            private void OnUpdate(ModuleDefinition moduleDefinition)
            {
                var methodDefinition = new MethodDefinition("OnUpdate", MethodAttributes.Family | MethodAttributes.HideBySig | MethodAttributes.Virtual, moduleDefinition.TypeSystem.Void);
                methodDefinition.Body.InitLocals = true;
                var baseType = typeDefinition.BaseType.Resolve();
                var processor = methodDefinition.Body.GetILProcessor();
                //Variables
                var jobVariable = new VariableDefinition(jobTypeDefinition.typeDefinition);
                processor.Body.Variables.Add(jobVariable);
                var traceVariable = new VariableDefinition(moduleDefinition.ImportReference(typeof(InputActionTrace)));
                processor.Body.Variables.Add(traceVariable);
                var eventPtrEnumerator = new VariableDefinition(moduleDefinition.ImportReference(typeof(IEnumerator<InputActionTrace.ActionEventPtr>)));
                processor.Body.Variables.Add(eventPtrEnumerator);
                var eventPtrEnumeratorItem = new VariableDefinition(moduleDefinition.ImportReference(typeof(InputActionTrace.ActionEventPtr)));
                processor.Body.Variables.Add(eventPtrEnumeratorItem);
                var actionIndices = new VariableDefinition(moduleDefinition.ImportReference(typeof(NativeHashMap<Guid, int>)));
                processor.Body.Variables.Add(actionIndices);
                var jobHandle = new VariableDefinition(moduleDefinition.ImportReference(typeof(JobHandle)));
                processor.Body.Variables.Add(jobHandle);
                var indexVariable = new VariableDefinition(moduleDefinition.TypeSystem.Int32);
                processor.Body.Variables.Add(indexVariable);
                var actionVariable = new VariableDefinition(moduleDefinition.ImportReference(typeof(InputAction)));
                processor.Body.Variables.Add(actionVariable);
                var startTimeVariable = new VariableDefinition(moduleDefinition.TypeSystem.Double);
                processor.Body.Variables.Add(startTimeVariable);
                var timeVariable = new VariableDefinition(moduleDefinition.TypeSystem.Double);
                processor.Body.Variables.Add(timeVariable);
                var phaseVariable = new VariableDefinition(moduleDefinition.ImportReference(typeof(InputActionPhase)));
                processor.Body.Variables.Add(phaseVariable);
                var deviceIdVariable = new VariableDefinition(moduleDefinition.TypeSystem.Int32);
                processor.Body.Variables.Add(deviceIdVariable);
                //Create Job
                processor.Emit(OpCodes.Ldloca, jobVariable);
                processor.Emit(OpCodes.Initobj, jobTypeDefinition.typeDefinition);
                InitJobTypeHandles(moduleDefinition, processor, jobVariable);
                //Iterate over action events
                //Enumerator
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldfld, traceSystemField);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(InputActionTraceSystem).GetProperty(nameof(InputActionTraceSystem.ActionTrace)).GetMethod));
                //processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(baseType.Properties.First(p => p.Name == "ActionTrace").GetMethod));
                processor.Emit(OpCodes.Stloc, traceVariable);
                processor.Emit(OpCodes.Ldloc, traceVariable);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(InputActionTrace).GetMethod(nameof(InputActionTrace.GetEnumerator), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)));
                var forEachLoopStart = processor.Create(OpCodes.Stloc, eventPtrEnumerator); ;
                processor.Append(forEachLoopStart);
                var forEachLoopHead = processor.Create(OpCodes.Ldloc, eventPtrEnumerator);
                processor.Append(forEachLoopHead);
                processor.Emit(OpCodes.Callvirt, moduleDefinition.ImportReference(typeof(IEnumerator<InputActionTrace.ActionEventPtr>).GetProperty(nameof(IEnumerator.Current)).GetMethod));
                processor.Emit(OpCodes.Stloc, eventPtrEnumeratorItem);
                //index variable
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldloca, eventPtrEnumeratorItem);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(InputActionTrace.ActionEventPtr).GetProperty("action").GetMethod));
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(InputAction).GetProperty("id").GetMethod));
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(InputUpdateSystemBase).GetMethod(nameof(InputUpdateSystemBase.GetActionIndex))));
                processor.Emit(OpCodes.Stloc, indexVariable);
                //Action
                processor.Emit(OpCodes.Ldloca, eventPtrEnumeratorItem);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(InputActionTrace.ActionEventPtr).GetProperty("action").GetMethod));
                processor.Emit(OpCodes.Stloc, actionVariable);
                //Start Time
                processor.Emit(OpCodes.Ldloca, eventPtrEnumeratorItem);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(InputActionTrace.ActionEventPtr).GetProperty("startTime").GetMethod));
                processor.Emit(OpCodes.Stloc, startTimeVariable);
                //Time
                processor.Emit(OpCodes.Ldloca, eventPtrEnumeratorItem);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(InputActionTrace.ActionEventPtr).GetProperty("time").GetMethod));
                processor.Emit(OpCodes.Stloc, timeVariable);
                //Phase
                processor.Emit(OpCodes.Ldloca, eventPtrEnumeratorItem);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(InputActionTrace.ActionEventPtr).GetProperty("phase").GetMethod));
                processor.Emit(OpCodes.Stloc, phaseVariable);
                //Device Id
                processor.Emit(OpCodes.Ldloca, eventPtrEnumeratorItem);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(InputActionTrace.ActionEventPtr).GetProperty("action").GetMethod));
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(InputAction).GetProperty("activeControl").GetMethod));
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(InputControl).GetProperty("device").GetMethod));
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(UnityEngine.InputSystem.InputDevice).GetProperty("deviceId").GetMethod));
                processor.Emit(OpCodes.Stloc, deviceIdVariable);
                //Jump Table
                InitJobValueFields(moduleDefinition, processor, jobVariable, indexVariable, actionVariable, startTimeVariable, timeVariable, phaseVariable, deviceIdVariable);
                var forEachLoopEnd = processor.Create(OpCodes.Ldloc, eventPtrEnumerator);
                processor.Append(forEachLoopEnd);
                processor.Emit(OpCodes.Callvirt, moduleDefinition.ImportReference(typeof(IEnumerator).GetMethod(nameof(IEnumerator.MoveNext))));
                processor.Emit(OpCodes.Brtrue, forEachLoopHead);
                processor.InsertAfter(forEachLoopStart, processor.Create(OpCodes.Br, forEachLoopEnd));
                //Schedule
                processor.Emit(OpCodes.Ldloc, jobVariable);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldfld, queryField);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(SystemBase).GetProperty("Dependency", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetMethod));
                var scheduleCall = new GenericInstanceMethod(moduleDefinition.ImportReference(typeof(JobEntityBatchExtensions).GetMethods(System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public).First(m => m.Name == nameof(JobEntityBatchExtensions.Schedule) && m.GetParameters().Length == 3)));
                scheduleCall.GenericArguments.Add(jobTypeDefinition.typeDefinition);
                processor.Emit(OpCodes.Call, scheduleCall);
                processor.Emit(OpCodes.Stloc, jobHandle);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldfld, traceClearSystemField);
                processor.Emit(OpCodes.Ldloc, jobHandle);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(InputActionTraceClearSystem).GetMethod(nameof(InputActionTraceClearSystem.AddJobHandle))));
                //processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(JobHandle).GetMethod(nameof(JobHandle.Complete))));
                processor.Emit(OpCodes.Ret);
                processor.Body.OptimizeMacros();
                typeDefinition.Methods.Add(methodDefinition);
            }
            private void OnStopRunning(ModuleDefinition moduleDefinition)
            {
                var methodDefinition = new MethodDefinition("OnStopRunning", MethodAttributes.Family | MethodAttributes.HideBySig | MethodAttributes.Virtual, moduleDefinition.TypeSystem.Void);
                var baseType = typeDefinition.BaseType.Resolve();
                methodDefinition.Body.InitLocals = true;
                var processor = methodDefinition.Body.GetILProcessor();
                processor.Body.SimplifyMacros();
                var getSingletonEntity = moduleDefinition.ImportReference(typeof(SystemBase).GetMethod(nameof(SystemBase.GetSingletonEntity)).MakeGenericMethod(typeof(InputActionAssetData)));
                var entityManager = moduleDefinition.ImportReference(typeof(SystemBase).GetProperty(nameof(SystemBase.EntityManager)).GetMethod);
                var componentObject = moduleDefinition.ImportReference(typeof(EntityManager).GetMethod(nameof(EntityManager.GetSharedComponentData), new Type[] { typeof(Entity) }).MakeGenericMethod(typeof(InputActionAssetData)));
                var startActionTrace = moduleDefinition.ImportReference(typeof(InputUpdateSystemBase).GetMethod("StopActionTrace", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance));
                var entityManagerLoc = new VariableDefinition(moduleDefinition.ImportReference(typeof(EntityManager)));
                processor.Body.Variables.Add(entityManagerLoc);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Call, entityManager);
                processor.Emit(OpCodes.Stloc, entityManagerLoc);
                processor.Emit(OpCodes.Ldloca, entityManagerLoc);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Call, getSingletonEntity);
                processor.Emit(OpCodes.Call, componentObject);
                processor.Emit(OpCodes.Call, startActionTrace);
                processor.Emit(OpCodes.Ret);
                processor.Body.OptimizeMacros();
                typeDefinition.Methods.Add(methodDefinition);
            }
            private void InitJobTypeHandles(ModuleDefinition moduleDefinition, ILProcessor processor, VariableDefinition jobVariable)
            {
                foreach (var component in components)
                {
                    var jobField = jobTypeDefinition.components.First(c => c.componentDefinition == component).componentTypeHandleField;
                    processor.Emit(OpCodes.Ldloca, jobVariable);
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldc_I4_0);
                    var call = new GenericInstanceMethod(moduleDefinition.ImportReference(typeof(ComponentSystemBase).GetMethod(nameof(ComponentSystemBase.GetComponentTypeHandle))));
                    call.GenericArguments.Add(component.typeDefinition);
                    processor.Emit(OpCodes.Call, call);
                    processor.Emit(OpCodes.Stfld, jobField);
                }
            }
            private void InitJobValueFields(
                            ModuleDefinition moduleDefinition,
                            ILProcessor processor,
                            VariableDefinition jobVariable,
                            VariableDefinition indexVariable,
                            VariableDefinition actionVariable,
                            VariableDefinition startTimeVariable,
                            VariableDefinition timeVariable,
                            VariableDefinition phaseVariable,
                            VariableDefinition deviceIdVariable
                            )
            {
                var head = processor.Create(OpCodes.Nop);
                processor.Append(head);
                var labels = new List<Instruction>();
                var breaks = new List<Instruction>();
                foreach (var component in components)
                {
                    var label = processor.Create(OpCodes.Nop);
                    processor.Append(label);
                    labels.Add(label);
                    var jobFieldInfo = jobTypeDefinition.components.First(c => c.componentDefinition == component);
                    var valueInfo = moduleDefinition.ImportReference(typeof(InputUpdateValue<>)).MakeGenericInstanceType(jobFieldInfo.componentDefinition.valueField.FieldType);
                    processor.Emit(OpCodes.Ldloca, jobVariable);
                    processor.Emit(OpCodes.Ldflda, jobFieldInfo.componentValueField);
                    processor.Emit(OpCodes.Ldloc, actionVariable);
                    var readValueCall = new GenericInstanceMethod(moduleDefinition.ImportReference(typeof(InputAction).GetMethod(nameof(InputAction.ReadValue))));
                    readValueCall.GenericArguments.Add(jobFieldInfo.componentDefinition.readValueType);
                    processor.Emit(OpCodes.Call, readValueCall);
                    if (component.fieldType != component.readValueType)
                    {
                        var implicitCast = new MethodReference("op_Implicit", component.fieldType, component.fieldType);
                        implicitCast.Parameters.Add(new ParameterDefinition(component.readValueType));
                        processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(implicitCast));
                    }
                    var field = moduleDefinition.ImportReference(jobFieldInfo.componentValueField.FieldType.Resolve().Fields.First(f => f.Name == "value"));
                    field.DeclaringType = jobFieldInfo.componentValueField.FieldType;
                    processor.Emit(OpCodes.Stfld, field);
                    processor.Emit(OpCodes.Ldloca, jobVariable);
                    processor.Emit(OpCodes.Ldflda, jobFieldInfo.componentValueField);
                    processor.Emit(OpCodes.Ldloc, startTimeVariable);
                    processor.Emit(OpCodes.Stfld, new FieldReference("startTime", moduleDefinition.TypeSystem.Double, jobFieldInfo.componentValueField.FieldType));
                    processor.Emit(OpCodes.Ldloca, jobVariable);
                    processor.Emit(OpCodes.Ldflda, jobFieldInfo.componentValueField);
                    processor.Emit(OpCodes.Ldloc, timeVariable);
                    processor.Emit(OpCodes.Stfld, new FieldReference("time", moduleDefinition.TypeSystem.Double, jobFieldInfo.componentValueField.FieldType));
                    processor.Emit(OpCodes.Ldloca, jobVariable);
                    processor.Emit(OpCodes.Ldflda, jobFieldInfo.componentValueField);
                    processor.Emit(OpCodes.Ldloc, phaseVariable);
                    processor.Emit(OpCodes.Stfld, new FieldReference("phase", moduleDefinition.ImportReference(typeof(InputActionPhase)), jobFieldInfo.componentValueField.FieldType));
                    processor.Emit(OpCodes.Ldloca, jobVariable);
                    processor.Emit(OpCodes.Ldflda, jobFieldInfo.componentValueField);
                    processor.Emit(OpCodes.Ldloc, deviceIdVariable);
                    var lastInstruction = processor.Create(OpCodes.Stfld, new FieldReference("deviceId", moduleDefinition.TypeSystem.Int32, jobFieldInfo.componentValueField.FieldType));
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
        }

        private class InputActionSystemJobDefinition
        {
            public TypeDefinition typeDefinition;
            public ComponentInfo[] components;
            public struct ComponentInfo
            {
                public InputActionComponentDefinition componentDefinition;
                public FieldDefinition componentTypeHandleField;
                public FieldDefinition componentValueField;
            }
            public InputActionSystemJobDefinition(ModuleDefinition moduleDefinition, InputActionComponentDefinition[] componentDefinitions, InputActionMap actionMap, string @namespace)
            {
                typeDefinition = new TypeDefinition(null, "Job", TypeAttributes.NestedPublic | TypeAttributes.SequentialLayout | TypeAttributes.Serializable | TypeAttributes.Sealed, moduleDefinition.ImportReference(typeof(ValueType)));
                typeDefinition.Interfaces.Add(new InterfaceImplementation(moduleDefinition.ImportReference(typeof(IJobEntityBatch))));
                typeDefinition.CustomAttributes.Add(new CustomAttribute(moduleDefinition.ImportReference(typeof(BurstCompileAttribute).GetConstructor(Type.EmptyTypes))));
                components = componentDefinitions.Select(d =>
                {
                    var handleType = moduleDefinition.ImportReference(typeof(ComponentTypeHandle<>)).MakeGenericInstanceType(d.typeDefinition);
                    var valueType = moduleDefinition.ImportReference(typeof(InputUpdateValue<>)).MakeGenericInstanceType(d.valueField.FieldType);
                    var r = new ComponentInfo
                    {
                        componentDefinition = d,
                        componentTypeHandleField = new FieldDefinition($"{d.name}TypeHandle", FieldAttributes.Public, moduleDefinition.ImportReference(handleType)),
                        componentValueField = new FieldDefinition($"{d.name}Value", FieldAttributes.Public, moduleDefinition.ImportReference(valueType))
                    };
                    typeDefinition.Fields.Add(r.componentTypeHandleField);
                    typeDefinition.Fields.Add(r.componentValueField);
                    return r;
                }).ToArray();
                Execute(moduleDefinition);
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
                foreach (var component in components)
                {
                    var nativeArrayType = moduleDefinition.ImportReference(typeof(NativeArray<>)).MakeGenericInstanceType(component.componentDefinition.typeDefinition);
                    processor.Emit(OpCodes.Ldarga, archetypeChunkParameter);
                    processor.Emit(OpCodes.Ldarg_0);
                    processor.Emit(OpCodes.Ldfld, component.componentTypeHandleField);
                    var call = new GenericInstanceMethod(moduleDefinition.ImportReference(typeof(ArchetypeChunk).GetGenericMethod(nameof(ArchetypeChunk.GetNativeArray), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)));
                    call.GenericArguments.Add(component.componentDefinition.typeDefinition);
                    processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(call));
                    //GetUnsafePtr, might throw error
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
                    processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(UnsafeUtility).GetMethod(nameof(UnsafeUtility.MemCpyReplicate))));
                }
                processor.Emit(OpCodes.Ret);
                processor.Body.OptimizeMacros();
                typeDefinition.Methods.Add(methodDefinition);
            }
        }

        private class InputActionInitSystemDefinition
        {
            public TypeDefinition typeDefinition;

            public InputActionComponentDefinition[] componentDefinitions;
            public InputActionMap actionMap;
            public InputActionInitSystemDefinition(
                InputActionComponentDefinition[] componentDefinitions,
                InputActionMap actionMap,
                AssemblyDefinition assemblyDefinition,
                ModuleDefinition moduleDefinition,
                string @namespace)
            {
                this.componentDefinitions = componentDefinitions;
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
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(ComponentType).GetMethod(nameof(ComponentType.ReadWrite), Type.EmptyTypes).MakeGenericMethod(typeof(InputActionMapReference))));
                processor.Emit(OpCodes.Stelem_Any, componentTypeReference);
                processor.Emit(OpCodes.Stfld, moduleDefinition.ImportReference(typeof(EntityQueryDesc).GetField(nameof(EntityQueryDesc.All))));
                processor.Emit(OpCodes.Stelem_Any, moduleDefinition.ImportReference(typeof(EntityQueryDesc)));
                processor.Emit(OpCodes.Call, getEntityQueryCall);
                processor.Emit(OpCodes.Stloc, queryVariable);
                processor.Emit(OpCodes.Ldloca, queryVariable);
                processor.Emit(OpCodes.Ldstr, actionMap.id.ToString("B"));
                processor.Emit(OpCodes.Ldstr, "B");
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(Guid).GetMethod(nameof(Guid.ParseExact))));
                processor.Emit(OpCodes.Newobj, moduleDefinition.ImportReference(typeof(InputActionMapReference).GetConstructor(new Type[] { typeof(Guid) })));
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(EntityQuery).GetMethod(nameof(EntityQuery.AddSharedComponentFilter)).MakeGenericMethod(typeof(InputActionMapReference))));
                processor.Emit(OpCodes.Ldloc, queryVariable);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(InputInitSystemBase).GetProperty("Query").SetMethod));
                processor.Emit(OpCodes.Ret);
                processor.Body.OptimizeMacros();
                typeDefinition.Methods.Add(methodDefinition);
            }
        }

        private class InputActionDisposeSystemDefinition
        {
            public TypeDefinition typeDefinition;

            public InputActionComponentDefinition[] componentDefinitions;
            public InputActionMap actionMap;
            public InputActionDisposeSystemDefinition(
                InputActionComponentDefinition[] componentDefinitions,
                InputActionMap actionMap,
                AssemblyDefinition assemblyDefinition,
                ModuleDefinition moduleDefinition,
                string @namespace)
            {
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
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(ComponentType).GetMethod(nameof(ComponentType.ReadWrite), Type.EmptyTypes).MakeGenericMethod(typeof(InputActionMapReference))));
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
            public List<InputActionSystemJobDefinition> jobDefinitions;
            public JobRegistrationClass(ModuleDefinition moduleDefinition, List<InputActionSystemJobDefinition> jobDefinitions)
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
