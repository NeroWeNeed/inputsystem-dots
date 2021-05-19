using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.LowLevel.Unsafe;
using Unity.Mathematics;

namespace NeroWeNeed.InputSystem
{
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