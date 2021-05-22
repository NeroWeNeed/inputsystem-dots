using System.Collections.Generic;
using Unity.Entities;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;

namespace NeroWeNeed.InputSystem
{
    [UpdateInGroup(typeof(InputAssetLoaderSystemGroup))]
    public class InputActionAssetInitSystem : SystemBase
    {
        private EntityQuery query;
        private EntityCommandBufferSystem entityCommandBufferSystem;
        private readonly List<InputActionAssetRequest> loadRequests = new List<InputActionAssetRequest>();
        protected override void OnCreate()
        {
            query = GetEntityQuery(ComponentType.ReadOnly<InputActionAssetRequest>(), ComponentType.Exclude<InputActionAssetData>());
            entityCommandBufferSystem = World.GetOrCreateSystem<EndInitializationEntityCommandBufferSystem>();
            RequireForUpdate(query);
        }
        protected override void OnUpdate()
        {
            var ecb = entityCommandBufferSystem.CreateCommandBuffer();
            loadRequests.Clear();
            EntityManager.GetAllUniqueSharedComponentData<InputActionAssetRequest>(loadRequests);
            for (int i = 1; i < loadRequests.Count; i++)
            {
                var key = loadRequests[i].value;
                var handle = Addressables.LoadAssetAsync<InputActionAsset>(key.ToString("N"));
                var asset = handle.WaitForCompletion();
                var component = new InputActionAssetData(asset);
                var query2 = GetEntityQuery(new EntityQueryDesc
                {
                    All = new ComponentType[] { ComponentType.ReadOnly<InputActionAssetRequest>() },
                    None = new ComponentType[] { ComponentType.ReadWrite<InputActionAssetData>() }
                });
                query2.AddSharedComponentFilter(new InputActionAssetRequest(key));
                ecb.AddSharedComponent(query2, component);
                Addressables.Release(handle);
            }
            entityCommandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }
}