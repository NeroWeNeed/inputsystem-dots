using Unity.Entities;

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
    }
}