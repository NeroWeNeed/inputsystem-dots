using System;
using Unity.Collections;
using Unity.Entities;
namespace NeroWeNeed.InputSystem
{
    [UpdateInGroup(typeof(InputInitSystemGroup))]
    public abstract class InputInitSystemBase : SystemBase
    {

        public EntityQuery Query { get; protected set; }
        public ComponentType[] ComponentTypes { get; protected set; }
        public Guid ActionMapId { get; protected set; }
        protected override void OnUpdate()
        {
            var entities = Query.ToEntityArray(Allocator.Temp);

            var chunks = Query.CreateArchetypeChunkArray(Allocator.Temp);
            for (int i = 0; i < chunks.Length; i++)
            {
                var actionMap = chunks[i].GetSharedComponentData(GetSharedComponentTypeHandle<InputActionAssetData>(), EntityManager).value?.FindActionMap(ActionMapId);
                if (!actionMap.enabled) {
                    actionMap.Enable();
                }
            }
            foreach (var componentType in ComponentTypes)
            {
                EntityManager.AddComponent(entities, componentType);
            }
        }
    }
}