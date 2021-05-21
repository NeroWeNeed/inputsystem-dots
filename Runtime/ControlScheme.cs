using Unity.Collections;
using Unity.Entities;
using UnityEngine.InputSystem;
namespace NeroWeNeed.InputSystem
{
    public struct ControlScheme
    {
        public BlobArray<DeviceInfo> devices;
        public struct DeviceInfo
        {
            public BlobString path;
            public DeviceFlags flags;
            public bool IsOptional { get => (((byte)flags) & 1) != 0; }
            public bool IsOr { get => (((byte)flags) & 2) != 0; }
        }
        public enum DeviceFlags : byte
        {
            None = 0,
            Optional = 1,
            Or = 2
        }
/*         public static BlobAssetReference<ControlScheme> Create(InputControlScheme scheme,Allocator allocator = Allocator.Persistent) {
            var builder = new BlobBuilder(Allocator.Temp);
            ref ControlScheme root = ref builder.ConstructRoot<ControlScheme>();

            var deviceArray = builder.Allocate<ControlScheme.DeviceInfo>(ref root.devices, scheme.deviceRequirements.Count);
            for (int i = 0; i < scheme.deviceRequirements.Count; i++)
            {
                builder.AllocateString(ref deviceArray[i].path, scheme.deviceRequirements[i].controlPath);
                byte flags = 0;
                flags |= (byte)(scheme.deviceRequirements[i].isOptional ? DeviceFlags.Optional : DeviceFlags.None);
                flags |= (byte)(scheme.deviceRequirements[i].isOR ? DeviceFlags.Or : DeviceFlags.None);
                deviceArray[i].flags = (DeviceFlags)flags;
            }
            var controlScheme = builder.CreateBlobAssetReference<ControlScheme>(allocator);
            builder.Dispose();
            return controlScheme;
        } */
    }
}