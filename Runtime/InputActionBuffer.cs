using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine.InputSystem;

namespace NeroWeNeed.InputSystem
{
    [NativeContainer]
    public unsafe struct NativeInputActionBuffer : IDisposable, IEnumerable<NativeInputActionBuffer.ActionEventHandle>, IEnumerable
    {
        public struct ActionEventHandle
        {
            internal byte* data;
            public byte* Data { get => data; }
            public InputActionHeaderData* Header { get => (InputActionHeaderData*)data; }
            public void* Value { get => (InputActionHeaderData*)(data + sizeof(InputActionHeaderData)); }
        }
        public struct Enumerator : IEnumerator<ActionEventHandle>, IEnumerator
        {
            private byte* data;
            private ActionEventHandle current;
            public int length;
            public int offset;
            public ActionEventHandle Current => current;
            object IEnumerator.Current => current;
            public Enumerator(ref NativeInputActionBuffer buffer)
            {
                data = buffer.data;
                offset = 0;
                length = buffer.currentOffset;
                current = default;
            }

            public void Dispose() { }
            public bool MoveNext()
            {
                if (offset >= length)
                {
                    return false;
                }
                current = new ActionEventHandle
                {
                    data = data + offset
                };
                offset += ((InputActionHeaderData*)(data + offset))->sizeInBytes;
                return true;
            }

            public void Reset()
            {
                offset = 0;
                current = default;
            }
        }
        private struct StateIndex
        {
            public int offset;
            public int length;

            public StateIndex(int offset, int length)
            {
                this.offset = offset;
                this.length = length;
            }
        }
        private UnsafeHashMap<Guid, StateIndex> indices;
        private static int staticSafetyId;
        [NativeDisableUnsafePtrRestriction]
        private byte* data;
        private int capacity;
        private int currentOffset;
        internal Allocator allocator;
        internal AtomicSafetyHandle m_Safety;

        [NativeSetClassTypeToNullOnSchedule]
        internal DisposeSentinel m_DisposeSentinel;
        public int Capacity { get => capacity; }
        public bool IsCreated { get => capacity > 0; }
        [BurstDiscard]
        private static void InitStaticSafetyId(ref AtomicSafetyHandle handle)
        {
            if (staticSafetyId == 0)
            {
                staticSafetyId = AtomicSafetyHandle.NewStaticSafetyId<NativeInputActionBuffer>();
            }
            AtomicSafetyHandle.SetStaticSafetyId(ref handle, staticSafetyId);
        }
        public NativeInputActionBuffer(int initialCapacity = 512, Allocator allocator = Allocator.Persistent)
        {
            initialCapacity = math.ceilpow2(initialCapacity);
            capacity = initialCapacity;
            currentOffset = 0;
            this.allocator = allocator;
            m_Safety = default;
            m_DisposeSentinel = default;
            data = (byte*)IntPtr.Zero.ToPointer();
            indices = new UnsafeHashMap<Guid, StateIndex>(8, allocator);
            Allocate(initialCapacity, allocator, ref this);
        }
        public void ClearAction(InputAction action)
        {
            if (indices.TryGetValue(action.id, out var index))
            {
                UnsafeUtility.MemClear(data + index.offset, index.length);
            }
        }
        [WriteAccessRequired]
        public void WriteAction(InputAction.CallbackContext context)
        {
            if (indices.TryGetValue(context.action.id, out var index))
            {
                var header = new InputActionHeaderData
                {
                    sizeInBytes = index.length,
                    actionMapId = context.action.actionMap.id,
                    actionId = context.action.id,
                    startTime = context.startTime,
                    time = context.time,
                    phase = context.phase,
                    deviceId = context.control.device.deviceId
                };
                UnsafeUtility.CopyStructureToPtr(ref header, data + index.offset);
                context.ReadValue(data + index.offset + sizeof(InputActionHeaderData), capacity - (index.offset + sizeof(InputActionHeaderData)));
            }
            else
            {
                EnsureCapacity(context);
                index = new StateIndex(currentOffset, sizeof(InputActionHeaderData) + context.valueSizeInBytes);
                indices[context.action.id] = index;
                var header = new InputActionHeaderData
                {
                    sizeInBytes = index.length,
                    actionMapId = context.action.actionMap.id,
                    actionId = context.action.id,
                    startTime = context.startTime,
                    time = context.time,
                    phase = context.phase,
                    deviceId = context.control.device.deviceId
                };
                UnsafeUtility.CopyStructureToPtr(ref header, data + currentOffset);
                context.ReadValue(data + currentOffset + sizeof(InputActionHeaderData), capacity - (currentOffset + sizeof(InputActionHeaderData)));
                currentOffset += index.length;
            }
        }
        private static void Allocate(int initialCapacity, Allocator allocator, ref NativeInputActionBuffer buffer)
        {
            buffer.data = (byte*)UnsafeUtility.Malloc(initialCapacity, 4, allocator);
            buffer.allocator = allocator;
            DisposeSentinel.Create(out buffer.m_Safety, out buffer.m_DisposeSentinel, 1, allocator);
            InitStaticSafetyId(ref buffer.m_Safety);
        }
        [WriteAccessRequired]
        public void Clear()
        {
            currentOffset = 0;
        }
        [WriteAccessRequired]
        public void ClearAndDefault()
        {
            Clear();
            UnsafeUtility.MemClear(data, capacity);
        }
        private void EnsureCapacity(InputAction.CallbackContext context)
        {
            AtomicSafetyHandle.CheckWriteAndBumpSecondaryVersion(m_Safety);
            var size = sizeof(InputActionHeaderData) + context.valueSizeInBytes;
            if (currentOffset + size > capacity)
            {
                var newDataSize = capacity * 2;
                var newData = UnsafeUtility.Malloc(newDataSize, 4, allocator);
                UnsafeUtility.MemCpy(newData, data, capacity);
                UnsafeUtility.Free(data, allocator);
                capacity = newDataSize;
                data = (byte*)newData;
            }
        }
        [WriteAccessRequired]

        public void Dispose()
        {
            DisposeSentinel.Dispose(ref m_Safety, ref m_DisposeSentinel);
            capacity = 0;
            currentOffset = 0;
            indices.Dispose();
            UnsafeUtility.Free(data, allocator);
        }
        public NativeInputActionBuffer.Enumerator GetEnumerator()
        {
            return new Enumerator(ref this);
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return new Enumerator(ref this);
        }

        IEnumerator<ActionEventHandle> IEnumerable<ActionEventHandle>.GetEnumerator()
        {
            return new Enumerator(ref this);
        }
    }
}