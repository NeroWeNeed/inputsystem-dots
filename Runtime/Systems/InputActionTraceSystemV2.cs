using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Utilities;

namespace NeroWeNeed.InputSystemV2
{
    //Components
    public struct InputActionAssetData : ISharedComponentData, IEquatable<InputActionAssetData>
    {
        public InputActionAsset value;

        public bool Equals(InputActionAssetData other)
        {
            return value == other.value;
        }

        public override int GetHashCode()
        {
            return -1584136870 + EqualityComparer<InputActionAsset>.Default.GetHashCode(value);
        }
    }
    public struct InputActionDataBuffer : IBufferElementData
    {
        public byte value;
    }
    public struct InputActionMapData : IBufferElementData, IEquatable<Guid>
    {
        public Guid value;

        public bool Equals(Guid other)
        {
            return value.Equals(other);
        }
    }
    public struct InputActionAssetRequest : IComponentData
    {
        public Entity value;
    }
    public struct InputActionMapRequest : IComponentData, IEquatable<InputActionMapRequest>
    {
        public Guid value;

        public bool Equals(InputActionMapRequest other)
        {
            return value.Equals(other.value);
        }

        public override int GetHashCode()
        {
            return -1584136870 + value.GetHashCode();
        }
    }
    //Systems
    public sealed class InputActionBufferUpdateSystem : SystemBase
    {
        private NativeList<byte> inputBuffer;
        private InputActionTrace inputActionTrace;
        private EntityQuery query;
        protected override void OnCreate()
        {
            base.OnCreate();
            query = GetEntityQuery(ComponentType.ReadWrite<InputActionDataBuffer>());
            inputActionTrace = new InputActionTrace();
            RequireForUpdate(query);
            RequireSingletonForUpdate<InputActionAssetData>();
            RequireSingletonForUpdate<InputActionDataBuffer>();
            RequireSingletonForUpdate<InputActionMapData>();
        }
        protected override void OnUpdate()
        {
            var entity = query.GetSingletonEntity();
            var buffer = GetBuffer<InputActionDataBuffer>(entity);
            UnityEngine.InputSystem.InputSystem.Update();
            var enumerator = inputActionTrace.GetEnumerator();
            inputActionTrace.buffer.bufferPtr.data
            while (enumerator.MoveNext())
            {
                var current = enumerator.Current;
current.ReadValue()

            }
            inputActionTrace.Clear();
        }
        protected override void OnDestroy()
        {
            inputBuffer.Dispose();
            base.OnDestroy();
        }
    }


}