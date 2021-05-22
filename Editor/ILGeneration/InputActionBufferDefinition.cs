using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
namespace NeroWeNeed.InputSystem.Editor.ILGeneration
{
    internal class InputActionBufferDefinition : BaseInputActionDefinition
    {
        public FieldDefinition byteField;
        public InputActionBufferDefinition(ModuleDefinition moduleDefinition, InputActionMap actionMap, InputAction action, string @namespace,HashSet<string> typeNames)
        {
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
            return new FieldDefinition($"InputAction{action.name}TypeHandle", FieldAttributes.Public, moduleDefinition.ImportReference(moduleDefinition.ImportReference(typeof(BufferTypeHandle<>)).MakeGenericInstanceType(typeDefinition)));
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
            var call = new GenericInstanceMethod(moduleDefinition.ImportReference(typeof(ArchetypeChunk).GetMethod(nameof(ArchetypeChunk.GetBufferAccessor), System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance)));
            call.GenericArguments.Add(typeDefinition);
            processor.Emit(OpCodes.Call, moduleDefinition.ImportReference(call));
            processor.Emit(OpCodes.Stloc, variableDefinition);

        }

        public override Instruction ILWriteInputData(ModuleDefinition moduleDefinition, ILProcessor processor, VariableDefinition enumeratorItemVariableDefinition, VariableDefinition accessorVariableDefinition, VariableDefinition deviceFilterAccessorVariableDefinition, ParameterDefinition archetypeChunkParameterDefinition)
        {
            var componentWriteCall = new GenericInstanceMethod(moduleDefinition.ImportReference(typeof(InputUpdateSystemJobUtility).GetMethod(nameof(InputUpdateSystemJobUtility.WriteActionBuffer))));
            componentWriteCall.GenericArguments.Add(typeDefinition);
            processor.Emit(OpCodes.Ldloca, accessorVariableDefinition);
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
