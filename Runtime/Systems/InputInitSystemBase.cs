using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NeroWeNeed.InputSystem
{
    [UpdateInGroup(typeof(InputInitSystemGroup))]
    public abstract class InputInitSystemBase : SystemBase
    {

        public EntityQuery Query { get; protected set; }
        public ComponentType[] ComponentTypes { get; protected set; }
        
        protected override void OnUpdate()
        {
            
            var entities = Query.ToEntityArray(Allocator.Temp);
            foreach (var componentType in ComponentTypes)
            {
                EntityManager.AddComponent(entities, componentType);
            }
        }
        private void Sample() {
            Vector2 vector2 = new Vector2(-1,-2);
            float2 c = vector2;
        }
    }
}