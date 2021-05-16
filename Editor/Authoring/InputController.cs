using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Entities;
using System;

namespace NeroWeNeed.InputSystem.Editor
{
    public class InputController : MonoBehaviour, IConvertGameObjectToEntity
    {
        [SerializeField]
        public InputActionMapInfo actionMap;
        
        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            if (this.actionMap.asset != null)
            {

                var actionMap = this.actionMap.asset.FindActionMap(this.actionMap.id, false);
                if (actionMap != null)
                {
                    dstManager.AddSharedComponentData(entity, new InputActionMapReference(actionMap.id));
                }
            }
        }
    }
}