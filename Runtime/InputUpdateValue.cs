using UnityEngine.InputSystem;

namespace NeroWeNeed.InputSystem
{
    public struct InputUpdateValue<TValue> where TValue : unmanaged
    {
        public TValue value;
        public double startTime;
        public double time;
        public InputActionPhase phase;
        public int deviceId;

        public InputUpdateValue(TValue value, double startTime, double time, InputActionPhase phase,int deviceId)
        {
            this.value = value;
            this.startTime = startTime;
            this.time = time;
            this.phase = phase;
            this.deviceId = deviceId;
        }
    }
}