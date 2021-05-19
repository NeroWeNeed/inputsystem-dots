using System;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
using Mono.Cecil.Rocks;
using Unity.Entities;
using Unity.Jobs;
using UnityEditor;
namespace NeroWeNeed.InputSystem.Editor.ILGeneration
{
    internal class JobRegistrationClass
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

