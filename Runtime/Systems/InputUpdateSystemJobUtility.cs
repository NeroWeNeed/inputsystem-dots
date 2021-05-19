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
            UnsafeUtility.MemCpyReplicate(destination, handle.Data + InputActionHeaderData.DataOffset, handle.Header->sizeInBytes - InputActionHeaderData.DataOffset,count);
        }
        [BurstCompile]
        public static void WriteComponentsWithDeviceDiscrimination(void* destination, ref NativeInputActionBuffer.ActionEventHandle handle, int count)
        {
            UnsafeUtility.MemCpyReplicate(destination, handle.Data + InputActionHeaderData.DataOffset, handle.Header->sizeInBytes - InputActionHeaderData.DataOffset, count);
        }
        [BurstCompile]
        public static void WriteBuffers<TBuffer>(ref BufferAccessor<TBuffer> accessor, ref NativeInputActionBuffer.ActionEventHandle handle, int count) where TBuffer : unmanaged, IInputStateBufferData
        {
            var copySize = handle.Header->sizeInBytes - InputActionHeaderData.DataOffset;
            for (int i = 0; i < accessor.Length;i++) {
                var buffer = accessor[i];
                buffer.EnsureCapacity(copySize);
                buffer.Length = copySize;
                UnsafeUtility.MemCpy(buffer.GetUnsafePtr(), handle.Data + InputActionHeaderData.DataOffset, copySize);
            }
        }
    }
}