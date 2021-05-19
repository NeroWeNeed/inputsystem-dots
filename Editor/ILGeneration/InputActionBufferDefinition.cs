using System;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using NeroWeNeed.Commons.Editor;
using Unity.Entities;
using UnityEngine.InputSystem;
namespace NeroWeNeed.InputSystem.Editor.ILGeneration
{
    internal class InputActionBufferDefinition : BaseInputActionDefinition
    {
        public FieldDefinition byteField;
        public InputActionBufferDefinition(ModuleDefinition moduleDefinition, InputActionMap actionMap, InputAction action, string @namespace)
        {
            this.action = action;
            typeDefinition = new TypeDefinition(@namespace, $"InputAction_{actionMap.name}_{action.name}", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.SequentialLayout | TypeAttributes.Serializable, moduleDefinition.ImportReference(typeof(ValueType)));
            var interfaceType = moduleDefinition.ImportReference(typeof(IInputStateBufferData));
            typeDefinition.Interfaces.Add(new InterfaceImplementation(interfaceType));
            var idAttr = new CustomAttribute(moduleDefinition.ImportReference(typeof(InputActionComponentAttribute).GetConstructor(new Type[] { typeof(string), typeof(string) })));
            idAttr.ConstructorArguments.Add(new CustomAttributeArgument(moduleDefinition.TypeSystem.String, actionMap.id.ToString("B")));
            idAttr.ConstructorArguments.Add(new CustomAttributeArgument(moduleDefinition.TypeSystem.String, action.id.ToString("B")));
            typeDefinition.CustomAttributes.Add(idAttr);
            byteField = new FieldDefinition("value", FieldAttributes.Public, moduleDefinition.TypeSystem.Byte);
            typeDefinition.Fields.Add(byteField);
            moduleDefinition.Types.Add(typeDefinition);
        }
        public override FieldDefinition ILCreateJobField(ModuleDefinition moduleDefinition)
        {
            return new FieldDefinition($"{action.name}TypeHandle", FieldAttributes.Public, moduleDefinition.ImportReference(moduleDefinition.ImportReference(typeof(BufferTypeHandle<>)).MakeGenericInstanceType(typeDefinition)));
        }

        public override void ILGetTypeHandle(ModuleDefinition moduleDefinition, ILProcessor processor)
        {
            var call = new GenericInstanceMethod(moduleDefinition.ImportReference(typeof(ComponentSystemBase).GetMethod(nameof(ComponentSystemBase.GetBufferTypeHandle))));
            call.GenericArguments.Add(typeDefinition);
            processor.Emit(OpCodes.Call, call);
        }

        public override VariableDefinition ILCreateAccessVariable(ModuleDefinition moduleDefinition, ILProcessor processor)
        {
            return new VariableDefinition(moduleDefinition.ImportReference(typeof(BufferAccessor<>)).MakeGenericInstanceType(typeDefinition));
        }

        public override void ILInitAccessVariable(ModuleDefinition moduleDefinition, ILProcessor processor, VariableDefinition variableDefinition, FieldDefinition handleFieldDefinition, ParameterDefinition archetypeChunkParameterDefinition, ParameterDefinition batchIndexParameterDefinition)
        {
            processor.Emit(OpCodes.Ldarga, archetypeChunkParameterDefinition);
            processor.Emit(OpCodes.Ldarg_0);
            processor.Emit(OpCodes.Ldfld, handleFieldDefinition);
            var call = new GenericInstanceMethod(moduleDefinition.ImportReference(typeof(ArchetypeChunk).GetGenericMethod(nameof(ArchetypeChunk.GetBufferAccessor), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)));
            call.GenericArguments.Add(typeDefinition);
            processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(call));
            processor.Emit(OpCodes.Stloc, variableDefinition);

        }

        public override Instruction ILWriteInputData(ModuleDefinition moduleDefinition, ILProcessor processor, VariableDefinition enumeratorItemVariableDefinition, VariableDefinition accessorVariableDefinition, ParameterDefinition archetypeChunkParameterDefinition)
        {
            var componentWriteCall = new GenericInstanceMethod(moduleDefinition.ImportReference(typeof(InputUpdateSystemJobUtility).GetMethod(nameof(InputUpdateSystemJobUtility.WriteBuffers))));
            componentWriteCall.GenericArguments.Add(typeDefinition);

            processor.Emit(OpCodes.Ldloca, accessorVariableDefinition);
            processor.Emit(OpCodes.Ldloca, enumeratorItemVariableDefinition);
            processor.Emit(OpCodes.Ldarga, archetypeChunkParameterDefinition);
            processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(typeof(ArchetypeChunk).GetProperty(nameof(ArchetypeChunk.Count)).GetMethod));
            var lastInstruction = processor.Create(OpCodes.Call, componentWriteCall);
            processor.Append(lastInstruction);
            return lastInstruction;
        }
    }


}