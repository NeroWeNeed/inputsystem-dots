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
        public InputActionAssetData(string json) {
            value = InputActionAsset.FromJson(json);
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
    public struct InputActionAssetLoadData : ISharedComponentData, IEquatable<InputActionAssetLoadData>
    {
        public string value;

        public InputActionAssetLoadData(InputActionAsset asset)
        {
            this.value = asset.ToJson();
        }

        public bool Equals(InputActionAssetLoadData other)
        {
            return value == other.value;
        }

        public override int GetHashCode()
        {
            return 2046966715 + EqualityComparer<string>.Default.GetHashCode(value);
        }
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