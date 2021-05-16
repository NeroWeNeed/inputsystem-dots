using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace NeroWeNeed.InputSystem.Editor
{
    using System;
    using System.IO;
    using System.Linq;
    using UnityEditor;
    using UnityEngine;
    using UnityEngine.InputSystem;

    [CreateAssetMenu(fileName = "InputSystemAssemblyDefinition", menuName = "Input System Assembly Definition", order = 0)]
    public class InputSystemAssemblyDefinition : ScriptableObject
    {
        public InputActionAsset asset;
        public string assemblyPath;
        public string assemblyNamespace;
        public string assemblyName;

        public List<InputActionMapInfo> inputActionMapInfo;
        private void OnValidate()
        {
            if (string.IsNullOrWhiteSpace(assemblyPath))
            {
                var path = AssetDatabase.GetAssetPath(this);
                if (path != null)
                {
                    assemblyPath = path.Substring(0, path.LastIndexOf('/'));
                }
            }
            if (assemblyPath.EndsWith("/"))
            {
                assemblyPath = assemblyPath.Substring(0, assemblyPath.Length - 1);
            }
            if (string.IsNullOrWhiteSpace(assemblyName))
            {
                assemblyName = $"{Application.productName}.InputSystem.{asset.name}";
            }
            if (asset != null)
            {
                if (inputActionMapInfo == null)
                {
                    inputActionMapInfo = asset.actionMaps.Select(actionMap => new InputActionMapInfo(actionMap)).ToList();
                }
                else
                {
                    var info = new Dictionary<Guid, InputActionMapInfo>();
                    foreach (var item in asset.actionMaps)
                    {
                        info[item.id] = new InputActionMapInfo(item);
                    }
                    foreach (var item in inputActionMapInfo)
                    {
                        if (info.ContainsKey(item.Guid)) {
                            info[item.Guid] = item;
                        }
                    }
                    inputActionMapInfo.Clear();
                    inputActionMapInfo.AddRange(info.Values);
                }

            }
        }
        [Serializable]
        public struct InputActionMapInfo : IEquatable<InputActionMapInfo>
        {
            public string id;
            private Guid guid;
            public Guid Guid
            {
                get
                {
                    if (guid == Guid.Empty && !string.IsNullOrEmpty(id))
                    {
                        if (!Guid.TryParseExact(id, "B", out guid))
                        {
                            id = null;
                        }
                    }
                    return guid;
                }
            }
            public InputActionMapSourceType sourceType;
            public InputActionMapInfo(InputActionMap inputActionMap, InputActionMapSourceType sourceType = InputActionMapSourceType.Single)
            {
                id = inputActionMap.id.ToString("B");
                this.sourceType = sourceType;
            }
            public InputActionMapInfo(string guid, InputActionMapSourceType sourceType = InputActionMapSourceType.Single)
            {
                this.id = guid;
                this.sourceType = sourceType;
            }

            public bool Equals(InputActionMapInfo other)
            {
                return id == other.id;
            }

            public override int GetHashCode()
            {
                int hashCode = -1298651620;
                hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(id);
                return hashCode;
            }
        }
    }

}