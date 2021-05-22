using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine.InputSystem;
namespace NeroWeNeed.InputSystem.Editor.ILGeneration
{
    internal class InputActionComponentDefinition : BaseInputActionDefinition
    {

        public FieldDefinition startTimeField;
        public FieldDefinition timeField;
        public FieldDefinition phaseField;
        public FieldDefinition deviceIdField;
        public FieldDefinition valueField;
        public TypeReference readValueType;
        public TypeReference fieldType;
        public InputActionComponentDefinition(ModuleDefinition moduleDefinition, InputActionMap actionMap, InputAction action, string @namespace, TypeReference fieldType, HashSet<string> typeNames) : this(moduleDefinition, actionMap, action, @namespace, fieldType, fieldType, typeNames) { }
        public InputActionComponentDefinition(ModuleDefinition moduleDefinition, InputActionMap actionMap, InputAction action, string @namespace, TypeReference fieldType, TypeReference readValueType, HashSet<string> typeNames)
        {
            this.readValueType = readValueType;
            this.fieldType = fieldType;
            this.action = action;
            var typeName = $"InputAction_{actionMap.name.Replace(" ", "")}_{action.name.Replace(" ", "")}";
            if (!CodeGenerator.IsValidLanguageIndependentIdentifier(typeName) || typeNames.Contains(typeName))
            {
                typeName = $"InputAction_{actionMap.name.Replace(" ", "")}_{action.id:N}";
                if (!CodeGenerator.IsValidLanguageIndependentIdentifier(typeName) || typeNames.Contains(typeName))
                {
                    typeName = $"InputAction_{actionMap.id:N}_{action.id:N}";
                }
            }
            Debug.Assert(!typeNames.Contains(typeName));
            typeNames.Add(typeName);
            typeDefinition = new TypeDefinition(@namespace, typeName, TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.SequentialLayout | TypeAttributes.Serializable, moduleDefinition.ImportReference(typeof(ValueType)));
            var interfaceType = moduleDefinition.ImportReference(typeof(IInputStateComponentData<>)).MakeGenericInstanceType(fieldType);
            typeDefinition.Interfaces.Add(new InterfaceImplementation(interfaceType));
            var idAttr = new CustomAttribute(moduleDefinition.ImportReference(typeof(InputActionComponentAttribute).GetConstructor(new Type[] { typeof(string), typeof(string) })));
            idAttr.ConstructorArguments.Add(new CustomAttributeArgument(moduleDefinition.TypeSystem.String, actionMap.id.ToString("B")));
            idAttr.ConstructorArguments.Add(new CustomAttributeArgument(moduleDefinition.TypeSystem.String, action.id.ToString("B")));
            typeDefinition.CustomAttributes.Add(idAttr);

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
            CreateProperty(moduleDefinition, "Value", valueField);
            CreateProperty(moduleDefinition, nameof(IInputStateComponentData.StartTime), startTimeField);
            CreateProperty(moduleDefinition, nameof(IInputStateComponentData.Time), timeField);
            CreateProperty(moduleDefinition, nameof(IInputStateComponentData.Phase), phaseField);
            CreateProperty(moduleDefinition, nameof(IInputStateComponentData.DeviceId), deviceIdField);
            moduleDefinition.Types.Add(typeDefinition);
        }

        public override FieldDefinition ILCreateJobField(ModuleDefinition moduleDefinition)
        {
            return new FieldDefinition($"InputAction{action.name}TypeHandle", FieldAttributes.Public, moduleDefinition.ImportReference(moduleDefinition.ImportReference(typeof(ComponentTypeHandle<>)).MakeGenericInstanceType(typeDefinition)));
        }

        public override void ILGetTypeHandle(ModuleDefinition moduleDefinition, ILProcessor processor)
        {
            var call = new GenericInstanceMethod(moduleDefinition.ImportReference(typeof(ComponentSystemBase).GetMethod(nameof(ComponentSystemBase.GetComponentTypeHandle))));
            call.GenericArguments.Add(typeDefinition);
            processor.Emit(OpCodes.Call, call);
        }

        public override VariableDefinition ILCreateAccessVariable(ModuleDefinition moduleDefinition, ILProcessor processor)
        {
            return new VariableDefinition(moduleDefinition.ImportReference(typeof(NativeArray<>)).MakeGenericInstanceType(typeDefinition));
        }
        private void CreateProperty(ModuleDefinition moduleDefinition, string propertyName, FieldDefinition fieldDefinition)
        {
            var property = new PropertyDefinition(propertyName, PropertyAttributes.None, fieldDefinition.FieldType);
            //Getter
            var getter = new MethodDefinition($"get_{propertyName}", MethodAttributes.ReuseSlot | MethodAttributes.Virtual | MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName, fieldDefinition.FieldType);
            var getterProcessor = getter.Body.GetILProcessor();
            getterProcessor.Emit(OpCodes.Ldarg_0);
            getterProcessor.Emit(OpCodes.Ldfld, fieldDefinition);
            getterProcessor.Emit(OpCodes.Ret);
            typeDefinition.Methods.Add(getter);
            property.GetMethod = getter;
            //Setter
            var setter = new MethodDefinition($"set_{propertyName}", MethodAttributes.ReuseSlot | MethodAttributes.Virtual | MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName, moduleDefinition.TypeSystem.Void);
            var setterParameter = new ParameterDefinition(fieldDefinition.FieldType);
            setter.Parameters.Add(setterParameter);
            var setterProcessor = setter.Body.GetILProcessor();
            setterProcessor.Emit(OpCodes.Ldarg_0);
            setterProcessor.Emit(OpCodes.Ldarg_1);
            setterProcessor.Emit(OpCodes.Stfld, fieldDefinition);
            setterProcessor.Emit(OpCodes.Ret);
            typeDefinition.Methods.Add(setter);
            property.SetMethod = setter;
            typeDefinition.Properties.Add(property);
        }

        public override void ILInitAccessVariable(ModuleDefinition moduleDefinition, ILProcessor processor, VariableDefinition accessorVariableDefinition, FieldDefinition handleFieldDefinition, ParameterDefinition archetypeChunkParameterDefinition, ParameterDefinition batchIndexParameterDefinition)
        {
            processor.Emit(OpCodes.Ldarga, archetypeChunkParameterDefinition);
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, handleFieldDefinition);
            var call = new GenericInstanceMethod(moduleDefinition.ImportReference(typeof(ArchetypeChunk).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance).First(method => method.Name == nameof(ArchetypeChunk.GetNativeArray) && method.IsGenericMethod)));
            call.GenericArguments.Add(typeDefinition);
            processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(call));
            processor.Emit(OpCodes.Stloc, accessorVariableDefinition);
        }
        public override Instruction ILWriteInputData(ModuleDefinition moduleDefinition, ILProcessor processor, VariableDefinition enumeratorItemVariableDefinition, VariableDefinition accessorVariableDefinition, VariableDefinition deviceFilterAccessorVariableDefinition, ParameterDefinition archetypeChunkParameterDefinition)
        {
            //var componentWriteCall = moduleDefinition.ImportReference(typeof(InputUpdateSystemJobUtility).GetMethod(nameof(InputUpdateSystemJobUtility.WriteComponents)));
            var componentWriteCall = moduleDefinition.ImportReference(typeof(InputUpdateSystemJobUtility).GetMethod(nameof(InputUpdateSystemJobUtility.WriteActionComponent)));
            var unsafePtrCall = new GenericInstanceMethod(moduleDefinition.ImportReference(typeof(NativeArrayUnsafeUtility).GetMethod(nameof(NativeArrayUnsafeUtility.GetUnsafePtr))));
            unsafePtrCall.GenericArguments.Add(typeDefinition);
            processor.Emit(OpCodes.Ldloc, accessorVariableDefinition);
            processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(unsafePtrCall));
            processor.Emit(OpCodes.Ldloca, deviceFilterAccessorVariableDefinition);
            processor.Emit(OpCodes.Ldloca, enumeratorItemVariableDefinition);
            processor.Emit(OpCodes.Ldarga, archetypeChunkParameterDefinition);
            processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(ArchetypeChunk).GetProperty(nameof(ArchetypeChunk.Count)).GetMethod));
            var lastInstruction = processor.Create(OpCodes.Call, componentWriteCall);
            processor.Append(lastInstruction);
            return lastInstruction;
        }
    }
}
