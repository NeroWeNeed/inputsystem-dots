using Unity.Entities;
using UnityEngine.InputSystem;

namespace NeroWeNeed.InputSystem
{
    public interface IInputData { }
    public interface IInputStateBufferData : IBufferElementData, IInputData { }
    public interface IInputStateComponentData : ISystemStateComponentData, IInputData
    {
        public double StartTime { get; }
        public double Time { get; }
        public InputActionPhase Phase { get; }
        public int DeviceId { get; }
    }
    public interface IInputActionMapTag : IComponentData { }
    public interface IInputStateComponentData<TValue> : IInputStateComponentData where TValue : unmanaged
    {
        public TValue Value { get; set; }
    }
}