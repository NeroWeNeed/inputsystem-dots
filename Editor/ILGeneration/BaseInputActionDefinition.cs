using Mono.Cecil;
using Mono.Cecil.Cil;
using UnityEngine.InputSystem;
namespace NeroWeNeed.InputSystem.Editor.ILGeneration
{
    internal abstract class BaseInputActionDefinition
    {
        public TypeDefinition typeDefinition;
        public InputAction action;
        public abstract void ILGetTypeHandle(ModuleDefinition moduleDefinition, ILProcessor processor);
        public abstract VariableDefinition ILCreateAccessVariable(ModuleDefinition moduleDefinition, ILProcessor processor);
        public abstract FieldDefinition ILCreateJobField(ModuleDefinition moduleDefinition);
        public abstract void ILInitAccessVariable(ModuleDefinition moduleDefinition, ILProcessor processor, VariableDefinition variableDefinition, FieldDefinition handleFieldDefinition, ParameterDefinition archetypeChunkParameterDefinition, ParameterDefinition batchIndexParameterDefinition);
        public abstract Instruction ILWriteInputData(ModuleDefinition moduleDefinition, ILProcessor processor, VariableDefinition enumeratorItemVariableDefinition, VariableDefinition accessorVariableDefinition,VariableDefinition deviceFilterAccessorVariableDefinition, ParameterDefinition archetypeChunkParameterDefinition);
    }


}
