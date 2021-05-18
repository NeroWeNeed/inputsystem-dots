using System;
using UnityEngine;

namespace NeroWeNeed.InputSystem.Editor {
    public class InputActionComponentMappingAsset : ScriptableObject
    {
        public string assembly;
        public string assetGuid;
        public InputActionMap[] actionMaps;
        [Serializable]
        public class InputActionMap
        {
            public string id;
            public string component;
            public InputAction[] actions;
            [Serializable]
            public class InputAction
            {
                public string id;
                public string component;
                /*                 [XmlAttribute("valueType")]
                                public string valueType; */
            }
        }
    }
}