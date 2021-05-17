using Unity.Entities;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;

namespace NeroWeNeed.InputSystem
{
 /*    [UpdateInGroup(typeof(InputAssetLoaderSystemGroup))]
    public class InputActionAssetRawLoaderSystem : SystemBase
    {
        private EntityQuery query;
        private EntityCommandBufferSystem entityCommandBufferSystem;
        protected override void OnCreate()
        {
            query = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[] { ComponentType.ReadOnly<InputActionAssetLoadRaw>() },
                None = new ComponentType[] { ComponentType.ReadWrite<InputActionAssetData>() },
                Options = EntityQueryOptions.FilterWriteGroup
            });
            entityCommandBufferSystem = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
            RequireForUpdate(query);
        }
        protected override void OnUpdate()
        {
            var ecb = entityCommandBufferSystem.CreateCommandBuffer();
            var entity = query.GetSingletonEntity();
            var data = EntityManager.GetSharedComponentData<InputActionAssetLoadRaw>(entity);
            ecb.AddSharedComponent(entity, new InputActionAssetData(data.value));
            entityCommandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }
    [UpdateInGroup(typeof(InputAssetLoaderSystemGroup))]
    public class InputActionAssetLoaderSystem : SystemBase
    {
        private EntityQuery query;
        private EntityCommandBufferSystem entityCommandBufferSystem;
        protected override void OnCreate()
        {
            query = GetEntityQuery(new EntityQueryDesc
            {
                All = new ComponentType[] { ComponentType.ReadOnly<InputActionAssetLoadAddress>() },
                None = new ComponentType[] { ComponentType.ReadWrite<InputActionAssetData>() },
                Options = EntityQueryOptions.FilterWriteGroup
            });
            entityCommandBufferSystem = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
            RequireForUpdate(query);
        }
        protected override void OnUpdate()
        {
            var ecb = entityCommandBufferSystem.CreateCommandBuffer();
            var entity = query.GetSingletonEntity();
            var data = EntityManager.GetSharedComponentData<InputActionAssetLoadAddress>(entity);
            var handle = Addressables.LoadAssetAsync<InputActionAsset>(data.value);
            var asset = handle.WaitForCompletion();
            ecb.AddSharedComponent(entity, new InputActionAssetData(asset));
            Addressables.Release(handle);
            entityCommandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    } */
    [UpdateInGroup(typeof(InputAssetLoaderSystemGroup))]
    public class InputActionAssetGuidLoaderSystem : SystemBase
    {
        private EntityQuery query;
        private EntityCommandBufferSystem entityCommandBufferSystem;
        protected override void OnCreate()
        {
            /*             query = GetEntityQuery(new EntityQueryDesc
                        {
                            All = new ComponentType[] { ComponentType.ReadOnly<InputActionAssetLoadGuid>() },
                            None = new ComponentType[] { ComponentType.ReadWrite<InputActionAssetData>() }
                            //Options = EntityQueryOptions.FilterWriteGroup
                        }); */
            query = GetEntityQuery(ComponentType.ReadOnly<InputActionAssetLoadRequest>(), ComponentType.Exclude<InputActionAssetData>());
            entityCommandBufferSystem = World.GetOrCreateSystem<BeginSimulationEntityCommandBufferSystem>();
            RequireForUpdate(query);
        }
        protected override void OnUpdate()
        {
            var ecb = entityCommandBufferSystem.CreateCommandBuffer();
            Entities.ForEach((Entity entity, in InputActionAssetLoadRequest data) =>
            {
                var handle = Addressables.LoadAssetAsync<InputActionAsset>(data.value.ToString("N"));
                var asset = handle.WaitForCompletion();
                Debug.Log(asset);
                ecb.AddSharedComponent(entity, new InputActionAssetData(asset));
                Addressables.Release(handle);
            }).WithoutBurst().Run();
            entityCommandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }
}