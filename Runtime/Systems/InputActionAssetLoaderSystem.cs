using Unity.Entities;

namespace NeroWeNeed.InputSystem
{
    [UpdateInGroup(typeof(InputSystemGroup), OrderFirst = true)]
    public class InputActionAssetLoaderSystem : SystemBase
    {
        private EntityQuery query;
        private EntityCommandBufferSystem entityCommandBufferSystem;
        protected override void OnCreate()
        {
            query = GetEntityQuery(ComponentType.ReadOnly<InputActionAssetLoadData>(), ComponentType.Exclude<InputActionAssetData>());
            entityCommandBufferSystem = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
            RequireForUpdate(query);
        }
        protected override void OnUpdate()
        {
            var ecb = entityCommandBufferSystem.CreateCommandBuffer();
            var entity = query.GetSingletonEntity();
            var data = EntityManager.GetSharedComponentData<InputActionAssetLoadData>(entity);
            ecb.AddSharedComponent(entity, new InputActionAssetData(data.value));
            entityCommandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }
}