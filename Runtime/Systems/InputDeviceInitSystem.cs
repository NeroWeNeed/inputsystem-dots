using Unity.Entities;
namespace NeroWeNeed.InputSystem
{
    [UpdateInGroup(typeof(InputInitializationSystemGroup))]
    public class InputDeviceInitSystem : SystemBase
    {
        private EntityCommandBufferSystem entityCommandBufferSystem;
        protected override void OnCreate()
        {
            entityCommandBufferSystem = World.GetOrCreateSystem<EndInitializationEntityCommandBufferSystem>();
        }
        protected override void OnUpdate()
        {
            var ecb = entityCommandBufferSystem.CreateCommandBuffer();
            Entities.WithNone<InputDeviceFilterData>().ForEach((Entity entity, in InputActionAssetData assetData, in InputControlScheme controlScheme) =>
            {
                var filterBuffer = ecb.AddBuffer<InputDeviceFilterData>(entity);
                if (controlScheme.index >= 0)
                {
                    var targetControlScheme = assetData.value.controlSchemes[controlScheme.index];
                    for (int j = 0; j < targetControlScheme.deviceRequirements.Count; j++)
                    {
                        
                        filterBuffer.Add(UnityEngine.InputSystem.InputSystem.FindControl(targetControlScheme.deviceRequirements[j].controlPath).device);
                    }
                }
            }).WithoutBurst().Run();
            Entities.WithNone<InputControlScheme>().WithAll<InputDeviceFilterData>().ForEach((Entity entity) => ecb.RemoveComponent<InputDeviceFilterData>(entity)).WithoutBurst().Run();
            entityCommandBufferSystem.AddJobHandleForProducer(Dependency);
        }
    }
}