using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine.InputSystem;
using static Unity.Entities.TypeManager;

namespace NeroWeNeed.InputSystem
{
    public interface IInputStateComponentData : ISystemStateComponentData { }
    public interface IInputStateComponentData<TValue> : IInputStateComponentData where TValue : unmanaged
    {
        public TValue Value { get; set; }
    }


    public struct InputActionAssetData : ISystemStateSharedComponentData, IEquatable<InputActionAssetData>
    {
        public InputActionAsset value;
        public InputActionAssetData(string json)
        {
            value = InputActionAsset.FromJson(json);
        }
        public InputActionAssetData(InputActionAsset asset)
        {
            value = asset;
        }
        public bool Equals(InputActionAssetData other)
        {
            return value == other.value;
        }

        public override int GetHashCode()
        {
            return -1584136870 + EqualityComparer<InputActionAsset>.Default.GetHashCode(value);
        }
    }
    public struct InputActionAssetLoadRequest : IComponentData
    {
        public Guid value;
#if UNITY_EDITOR
        public InputActionAssetLoadRequest(InputActionAsset asset)
        {
            value = Guid.Empty;
            var path = UnityEditor.AssetDatabase.GetAssetPath(asset);
            if (path != null)
            {
                var guid = UnityEditor.AssetDatabase.AssetPathToGUID(path);
                if (!string.IsNullOrEmpty(guid)) {
                    value = Guid.Parse(guid);
                }
            }
        }
#endif
    }
    [Serializable]
    public struct InputActionMapReference : ISharedComponentData, IEquatable<InputActionMapReference>
    {
        public Guid value;

        public InputActionMapReference(Guid value)
        {

            this.value = value;
        }

        public bool Equals(InputActionMapReference other)
        {
            return value.Equals(other.value);
        }

        public override int GetHashCode()
        {
            return -1584136870 + value.GetHashCode();
        }
    }
}