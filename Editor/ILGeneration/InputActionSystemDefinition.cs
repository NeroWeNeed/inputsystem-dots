using System;
using System.Collections.Generic;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
//using NeroWeNeed.Commons.Editor;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
namespace NeroWeNeed.InputSystem.Editor.ILGeneration
{
    internal class InputActionSystemDefinition
    {
        public TypeDefinition typeDefinition;
        public JobDefinition jobTypeDefinition;
        public BaseInputActionDefinition[] actionComponents;
        public InputActionMapComponentDefinition actionMapComponent;
        public FieldDefinition queryField;
        public FieldDefinition traceSystemField;
        public FieldDefinition traceClearSystemField;
        public InputActionSystemDefinition(
            InputActionMapComponentDefinition actionMapComponentDefinition,
            BaseInputActionDefinition[] actionComponentDefinitions,
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
            processor.Emit(OpCodes.Ldc_I4, actionComponents.Length + 1);
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
            processor.Emit(OpCodes.Dup);
            processor.Emit(OpCodes.Ldc_I4, offset++);
            processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(ComponentType).GetMethod(nameof(ComponentType.ReadOnly), Type.EmptyTypes).MakeGenericMethod(typeof(InputDeviceFilterData))));
            processor.Emit(OpCodes.Stelem_Any, componentTypeReference);
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
                processor.Emit(OpCodes.Ldstr, component.action.id.ToString("B"));
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
            processor.Emit(OpCodes.Ldloc, jobHandleVariable);
            processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(SystemBase).GetProperty("Dependency", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).SetMethod));
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, traceClearSystemField);
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(SystemBase).GetProperty("Dependency", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).GetMethod));
            /* processor.Emit(OpCodes.Ldloc, jobHandleVariable); */
            processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(InputActionCleanupSystem).GetMethod(nameof(InputActionCleanupSystem.AddJobHandle))));
            processor.Emit(OpCodes.Ret);
            processor.Body.OptimizeMacros();
            typeDefinition.Methods.Add(methodDefinition);
        }
        private void InitJobTypeHandles(ModuleDefinition moduleDefinition, ILProcessor processor, VariableDefinition jobVariable)
        {
            //Init Device Filter Handle
            processor.Emit(OpCodes.Ldloca, jobVariable);
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldc_I4_0);
            processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(ComponentSystemBase).GetMethod(nameof(ComponentSystemBase.GetBufferTypeHandle)).MakeGenericMethod(typeof(InputDeviceFilterData))));
            processor.Emit(OpCodes.Stfld, jobTypeDefinition.deviceFilterTypeHandle);
            foreach (var component in actionComponents)
            {
                var jobField = jobTypeDefinition.actionComponents.First(c => c.componentDefinition == component).componentTypeHandleField;
                processor.Emit(OpCodes.Ldloca, jobVariable);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldc_I4_0);
                component.ILGetTypeHandle(moduleDefinition, processor);
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
            public FieldDefinition deviceFilterTypeHandle;
            public struct ComponentInfo
            {
                public BaseInputActionDefinition componentDefinition;
                public FieldDefinition componentTypeHandleField;
            }
            public JobDefinition(ModuleDefinition moduleDefinition, BaseInputActionDefinition[] actionComponentDefinitions, TypeDefinition containerTypeDefinition)
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
                deviceFilterTypeHandle = new FieldDefinition("deviceFilterTypeHandle", FieldAttributes.Public, moduleDefinition.ImportReference(typeof(BufferTypeHandle<InputDeviceFilterData>)));
                typeDefinition.Fields.Add(deviceFilterTypeHandle);
                actionComponents = actionComponentDefinitions.Select(actionComponentDefinition =>
                {
                    var field = actionComponentDefinition.ILCreateJobField(moduleDefinition);
                    typeDefinition.Fields.Add(field);
                    return new ComponentInfo
                    {
                        componentDefinition = actionComponentDefinition,
                        componentTypeHandleField = field
                    };
                }).ToArray();
                Execute(moduleDefinition);
                containerTypeDefinition.NestedTypes.Add(typeDefinition);
            }
            private void IndexJumpTable(
                ModuleDefinition moduleDefinition,
                ILProcessor processor,
                Dictionary<BaseInputActionDefinition, VariableDefinition> nativeArrayVariables,
                VariableDefinition enumeratorItemVariable,
                VariableDefinition indexVariable,
                VariableDefinition deviceFilterVariable,
                ParameterDefinition archetypeParameter
            )
            {
                var head = processor.Create(OpCodes.Nop);
                processor.Append(head);
                var labels = new List<Instruction>();
                var breaks = new List<Instruction>();
                foreach (var component in nativeArrayVariables)
                {
                    var label = processor.Create(OpCodes.Nop);
                    processor.Append(label);
                    labels.Add(label);
                    var lastInstruction = component.Key.ILWriteInputData(moduleDefinition, processor, enumeratorItemVariable, component.Value, deviceFilterVariable, archetypeParameter);
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
                var batchIndexParameter = new ParameterDefinition("batchIndex", ParameterAttributes.None, moduleDefinition.TypeSystem.Int32);
                methodDefinition.Parameters.Add(batchIndexParameter);
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
                var deviceFilterAccessorVariable = new VariableDefinition(moduleDefinition.ImportReference(typeof(BufferAccessor<InputDeviceFilterData>)));
                processor.Body.Variables.Add(deviceFilterAccessorVariable);
                var nativeArrayVariables = new Dictionary<BaseInputActionDefinition, VariableDefinition>();
                //Get Device Filter Accessor
                processor.Emit(OpCodes.Ldarga, archetypeChunkParameter);
                processor.Emit(OpCodes.Ldarg_0);
                processor.Emit(OpCodes.Ldfld, deviceFilterTypeHandle);
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(ArchetypeChunk).GetMethod(nameof(ArchetypeChunk.GetBufferAccessor), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).MakeGenericMethod(typeof(InputDeviceFilterData))));
                processor.Emit(OpCodes.Stloc, deviceFilterAccessorVariable);

                //Get Native Arrays & buffers
                foreach (var component in actionComponents)
                {
                    var accessorVariable = component.componentDefinition.ILCreateAccessVariable(moduleDefinition, processor);
                    processor.Body.Variables.Add(accessorVariable);
                    nativeArrayVariables[component.componentDefinition] = accessorVariable;
                    component.componentDefinition.ILInitAccessVariable(moduleDefinition, processor, accessorVariable, component.componentTypeHandleField, archetypeChunkParameter, batchIndexParameter);
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
                processor.Emit(OpCodes.Ldfld, moduleDefinition.ImportReference(typeof(InputActionEventHeaderData).GetField(nameof(InputActionEventHeaderData.actionId))));
                processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(NativeHashMap<Guid, int>).GetProperty("Item").GetMethod));
                processor.Emit(OpCodes.Stloc, indexVariable);
                IndexJumpTable(moduleDefinition, processor, nativeArrayVariables, enumeratorItemVariable, indexVariable, deviceFilterAccessorVariable, archetypeChunkParameter);
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
}