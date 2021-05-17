using Unity.Entities;

namespace NeroWeNeed.InputSystem
{
    [UpdateInGroup(typeof(InputSystemGroup), OrderLast = true)]
    public class InputActionAssetUnloaderSystem : SystemBase
    {
        private EntityQuery query;
        protected override void OnCreate()
        {
            base.OnCreate();
            query = GetEntityQuery(ComponentType.ReadOnly<InputActionAssetData>(), ComponentType.Exclude<InputActionAssetLoadRequest>());
        }
        protected override void OnDestroy()
        {
            EntityManager.RemoveComponent(query, ComponentType.ReadOnly<InputActionAssetData>());
        }
        protected override void OnUpdate()
        {
            EntityManager.RemoveComponent(query, ComponentType.ReadOnly<InputActionAssetData>());
        }
    }
}