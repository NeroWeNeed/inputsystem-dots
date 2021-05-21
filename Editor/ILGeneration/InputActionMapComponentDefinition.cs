using System;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Unity.Entities;
using UnityEngine.InputSystem;
namespace NeroWeNeed.InputSystem.Editor.ILGeneration
{
    internal class InputActionMapComponentDefinition
    {
        public TypeDefinition typeDefinition;
        public FieldDefinition guidField;
        public InputActionMap actionMap;
        public InputActionMapComponentDefinition(ModuleDefinition moduleDefinition, InputActionMap actionMap, string @namespace)
        {
            this.actionMap = actionMap;
            typeDefinition = new TypeDefinition(@namespace, $"InputActionMap_{actionMap.name}", TypeAttributes.Public | TypeAttributes.Sealed | TypeAttributes.SequentialLayout | TypeAttributes.Serializable, moduleDefinition.ImportReference(typeof(ValueType)));
            typeDefinition.Interfaces.Add(new InterfaceImplementation(moduleDefinition.ImportReference(typeof(IInputActionMapTag))));
            var idAttr = new CustomAttribute(moduleDefinition.ImportReference(typeof(InputActionMapComponentAttribute).GetConstructor(new Type[] { typeof(string) })));
            idAttr.ConstructorArguments.Add(new CustomAttributeArgument(moduleDefinition.TypeSystem.String, actionMap.id.ToString("B")));
            typeDefinition.CustomAttributes.Add(idAttr);
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


}
