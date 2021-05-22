using Unity.Entities;
using Unity.Jobs;
namespace NeroWeNeed.InputSystem
{
    [UpdateInGroup(typeof(InputSystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(InputActionAssetUnloaderSystem))]
    public class InputActionCleanupSystem : SystemBase
    {
        protected InputActionProcessorSystem inputActionProcessorSystem;
        private JobHandle clearHandle;
        protected override void OnCreate()
        {
            base.OnCreate();
            inputActionProcessorSystem = World.GetOrCreateSystem<InputActionProcessorSystem>();
        }
        protected override void OnUpdate()
        {
            clearHandle.Complete();
            inputActionProcessorSystem.Handles.Clear();
        }
        public void AddJobHandle(JobHandle handle)
        {
            clearHandle = JobHandle.CombineDependencies(clearHandle, handle);
        }
    }
}