using System;
using Unity.Burst;
using Unity.Collections;
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
    }
    [UpdateInGroup(typeof(InputUpdateSystemGroup))]
    public abstract class InputUpdateSystemBase : SystemBase
    {
        public Guid ActionMapId { get; protected set; }
        public NativeHashMap<Guid, int> ActionIndices { get; protected set; }
        protected override void OnDestroy()
        {
            ActionIndices.Dispose();
        }
    }
}