using System;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Unity.Entities;
using UnityEngine.InputSystem;
namespace NeroWeNeed.InputSystem.Editor.ILGeneration
{
    internal class InputActionDisposeSystemDefinition
    {
        public TypeDefinition typeDefinition;
        public InputActionMapComponentDefinition actionMapComponentDefinition;
        public BaseInputActionDefinition[] componentDefinitions;
        public InputActionMap actionMap;
        public InputActionDisposeSystemDefinition(
            InputActionMapComponentDefinition actionMapComponentDefinition,
            BaseInputActionDefinition[] componentDefinitions,
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


}
