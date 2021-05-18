using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.InputSystem;
using UnityEngine.ResourceManagement.AsyncOperations;

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
        private readonly List<InputActionAssetLoadRequest> loadRequests = new List<InputActionAssetLoadRequest>();
        protected override void OnCreate()
        {
            query = GetEntityQuery(ComponentType.ReadOnly<InputActionAssetLoadRequest>(), ComponentType.Exclude<InputActionAssetData>());
            entityCommandBufferSystem = World.GetOrCreateSystem<EndInitializationEntityCommandBufferSystem>();
            RequireForUpdate(query);
        }
        protected override void OnUpdate()
        {
            var ecb = entityCommandBufferSystem.CreateCommandBuffer();
            loadRequests.Clear();
            EntityManager.GetAllUniqueSharedComponentData<InputActionAssetLoadRequest>(loadRequests);
            for (int i = 1; i < loadRequests.Count; i++)
            {
                var key = loadRequests[i].value;
                var handle = Addressables.LoadAssetAsync<InputActionAsset>(key.ToString("N"));
                var asset = handle.WaitForCompletion();
                var component = new InputActionAssetData(asset);
                var query2 = GetEntityQuery(new EntityQueryDesc
                {
                    All = new ComponentType[] { ComponentType.ReadOnly<InputActionAssetLoadRequest>() },
                    None = new ComponentType[] { ComponentType.ReadWrite<InputActionAssetData>() }
                });
                query2.AddSharedComponentFilter(new InputActionAssetLoadRequest(key));
                ecb.AddSharedComponent(query2, component);
                asset.Enable();
                Addressables.Release(handle);
            }
            entityCommandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }
}