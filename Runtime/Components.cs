using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine.InputSystem;
using static Unity.Entities.TypeManager;

namespace NeroWeNeed.InputSystem
{
    public struct InputStateControlScheme : IComponentData
    {
        public BlobAssetReference<ControlScheme> value;
    }
    [InternalBufferCapacity(4)]
    public struct InputStateDeviceFilter : ISystemStateBufferElementData
    {
        public int id;
    }

    public struct InputControllerReference : IComponentData
    {
        public Entity value;
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
    public struct InputActionAssetRequest : ISharedComponentData, IEquatable<InputActionAssetRequest>
    {
        public Guid value;

        public InputActionAssetRequest(Guid value)
        {
            this.value = value;
        }
#if UNITY_EDITOR
        public InputActionAssetRequest(InputActionAsset asset)
        {
            value = Guid.Empty;
            var path = UnityEditor.AssetDatabase.GetAssetPath(asset);
            if (path != null)
            {
                var guid = UnityEditor.AssetDatabase.AssetPathToGUID(path);
                if (!string.IsNullOrEmpty(guid))
                {
                    value = Guid.Parse(guid);
                }
            }
        }
#endif
        public bool Equals(InputActionAssetRequest other)
        {
            return value.Equals(other.value);
        }

        public override int GetHashCode()
        {
            return -1584136870 + value.GetHashCode();
        }


    }
}