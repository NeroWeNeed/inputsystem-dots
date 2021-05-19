using Unity.Burst;
using Unity.Entities;
using UnityEngine.InputSystem;

namespace NeroWeNeed.InputSystem
{
    [BurstCompile]
    public unsafe static class InputStateExtensions
    {
        public static double Duration<TComponent>(this TComponent self) where TComponent : struct, IInputStateComponentData => self.Time - self.StartTime;
        public static double StartTime<TComponent>(this DynamicBuffer<TComponent> self) where TComponent : struct, IInputStateBufferData => ((InputStateUntypedHeaderData*)self.GetUnsafePtr())->startTime;
        public static double Time<TComponent>(this DynamicBuffer<TComponent> self) where TComponent : struct, IInputStateBufferData => ((InputStateUntypedHeaderData*)self.GetUnsafePtr())->time;
        public static double Duration<TComponent>(this DynamicBuffer<TComponent> self) where TComponent : struct, IInputStateBufferData
        {
            var header = (InputStateUntypedHeaderData*)self.GetUnsafePtr();
            return header->time - header->startTime;
        }
        public static InputActionPhase Phase<TComponent>(this DynamicBuffer<TComponent> self) where TComponent : struct, IInputStateBufferData => ((InputStateUntypedHeaderData*)self.GetUnsafePtr())->phase;
        public static int DeviceId<TComponent>(this DynamicBuffer<TComponent> self) where TComponent : struct, IInputStateBufferData => ((InputStateUntypedHeaderData*)self.GetUnsafePtr())->deviceId;
        public static void* Value<TComponent>(this DynamicBuffer<TComponent> self) where TComponent : struct, IInputStateBufferData
        {
            return ((byte*)self.GetUnsafePtr()) + sizeof(InputStateUntypedHeaderData);
        }
        public static void* Value<TComponent>(this DynamicBuffer<TComponent> self, out int valueSizeInBytes) where TComponent : struct, IInputStateBufferData
        {
            valueSizeInBytes = self.Length - sizeof(InputStateUntypedHeaderData);
            return Value(self);
        }
        public static void Value<TComponent, TResult>(this DynamicBuffer<TComponent> self, out TResult result) where TComponent : struct, IInputStateBufferData where TResult : unmanaged
        {
            result = *(TResult*)Value(self);
        }

        internal struct InputStateUntypedHeaderData
        {
            public double startTime;
            public double time;
            public InputActionPhase phase;
            public int deviceId;
        }
    }
}