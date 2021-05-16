using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;

namespace NeroWeNeed.InputSystem
{
    [UpdateInGroup(typeof(InputSystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(InputActionAssetUnloaderSystem))]
    public class InputActionTraceClearSystem : SystemBase
    {
        protected InputActionTraceSystem inputActionTraceSystem;
        private JobHandle clearHandle;
        protected override void OnCreate()
        {
            base.OnCreate();
            inputActionTraceSystem = World.GetOrCreateSystem<InputActionTraceSystem>();
        }
        protected override void OnUpdate()
        {
            clearHandle.Complete();
            inputActionTraceSystem.ActionTrace.Clear();
        }
        public void AddJobHandle(JobHandle handle)
        {
            clearHandle = JobHandle.CombineDependencies(clearHandle, handle);
        }
    }
}