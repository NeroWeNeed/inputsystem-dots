using Unity.Collections;
using Unity.Entities;

namespace NeroWeNeed.InputSystem
{
    [UpdateInGroup(typeof(InputDisposeSystemGroup))]
    public abstract class InputDisposeSystemBase : SystemBase
    {
        public EntityQuery Query { get; protected set; }
        public ComponentType[] ComponentTypes { get; protected set; }
        protected override void OnUpdate()
        {
            var entities = Query.ToEntityArray(Allocator.Temp);
            foreach (var componentType in ComponentTypes)
            {
                EntityManager.RemoveComponent(entities, componentType);
            }
        }
        protected override void OnDestroy()
        {
            var entities = Query.ToEntityArray(Allocator.Temp);
            foreach (var componentType in ComponentTypes)
            {
                EntityManager.RemoveComponent(entities, componentType);
            }
        }
    }

}