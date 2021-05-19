using System;

namespace NeroWeNeed.InputSystem
{
    [AttributeUsage(AttributeTargets.Assembly)]
    public sealed class InputActionAssemblyAttribute : Attribute { }
    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class InputActionMapComponentAttribute : Attribute
    {
        public string id;

        public InputActionMapComponentAttribute(string id)
        {
            this.id = id;
        }
    }
    [AttributeUsage(AttributeTargets.Struct)]
    public sealed class InputActionComponentAttribute : Attribute
    {
        public string actionMapId;
        public string actionId;

        public InputActionComponentAttribute(string actionMapId, string actionId)
        {
            this.actionMapId = actionMapId;
            this.actionId = actionId;
        }
    }

}