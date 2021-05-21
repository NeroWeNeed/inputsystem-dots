using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;

namespace NeroWeNeed.InputSystem
{
    [BurstCompile]
    public unsafe static class InputUpdateSystemJobUtility
    {
        [BurstCompile]
        public static void WriteComponents(void* destination, ref NativeInputActionBuffer.ActionEventHandle handle, int count)
        {
            UnsafeUtility.MemCpyReplicate(destination, handle.Data + InputActionEventHeaderData.DataOffset, handle.Header->sizeInBytes - InputActionEventHeaderData.DataOffset, count);
        }
        public static void WriteActionComponent(void* destination, ref BufferAccessor<InputDeviceFilterData> deviceFilters, ref NativeInputActionBuffer.ActionEventHandle handle, int count)
        {
            var componentSize = handle.Header->sizeInBytes - InputActionEventHeaderData.DataOffset;
            for (int i = 0; i < count; i++)
            {
                var writeDestination = ((byte*)destination) + (componentSize * i);
                if (handle.Header->time > ((InputActionHeaderData*)writeDestination)->time)
                {
                    var deviceFilter = deviceFilters[i];
                    if (HasDevice(ref deviceFilter, handle.Header->deviceId))
                    {
                        UnsafeUtility.MemCpy(writeDestination, handle.Data + InputActionEventHeaderData.DataOffset, componentSize);
                    }
                }
            }
        }
        public static void WriteActionBuffer<TBuffer>(ref BufferAccessor<TBuffer> accessor, ref BufferAccessor<InputDeviceFilterData> deviceFilters, ref NativeInputActionBuffer.ActionEventHandle handle, int count) where TBuffer : unmanaged, IInputStateBufferData
        {
            var componentSize = handle.Header->sizeInBytes - InputActionEventHeaderData.DataOffset;
            for (int i = 0; i < count; i++)
            {
                var buffer = accessor[i];
                if (buffer.Length < sizeof(InputActionHeaderData) || handle.Header->time > ((InputActionHeaderData*)buffer.GetUnsafeReadOnlyPtr())->time)
                {
                    var deviceFilter = deviceFilters[i];
                    if (HasDevice(ref deviceFilter, handle.Header->deviceId))
                    {
                        buffer.EnsureCapacity(componentSize);
                        buffer.Length = componentSize;
                        UnsafeUtility.MemCpy(buffer.GetUnsafePtr(), handle.Data + InputActionEventHeaderData.DataOffset, componentSize);
                    }
                }
            }
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool HasDevice(ref DynamicBuffer<InputDeviceFilterData> devices, int deviceId)
        {
            if (devices.Length == 0)
            {
                return true;
            }
            else
            {
                for (int i = 0; i < devices.Length; i++)
                {
                    if (devices[i].id == deviceId)
                    {
                        return true;
                    }
                }
                return false;
            }
        }
        /*         [MethodImpl(MethodImplOptions.AggressiveInlining)]
                public static void HasDevice(ref DynamicBuffer<InputDeviceFilterData> devices, int deviceId, out byte result)
                {
                    if (devices.Length == 0)
                    {
                        result = 1;
                    }
                    else
                    {
                        result = 0;
                        for (int i = 0; i < devices.Length; i++)
                        {
                            if (devices[i].id == deviceId)
                            {
                                result = 1;
                                break;
                            }
                        }
                    }
                } */

        public static void WriteBuffers<TBuffer>(ref BufferAccessor<TBuffer> accessor, ref NativeInputActionBuffer.ActionEventHandle handle, int count) where TBuffer : unmanaged, IInputStateBufferData
        {
            var copySize = handle.Header->sizeInBytes - InputActionEventHeaderData.DataOffset;
            for (int i = 0; i < accessor.Length; i++)
            {
                var buffer = accessor[i];
                buffer.EnsureCapacity(copySize);
                buffer.Length = copySize;
                UnsafeUtility.MemCpy(buffer.GetUnsafePtr(), handle.Data + InputActionEventHeaderData.DataOffset, copySize);
            }
        }
    }
}