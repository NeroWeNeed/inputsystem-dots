using Unity.Entities;
using UnityEngine;
namespace NeroWeNeed.InputSystem.Editor
{
    public class InputControllerReference : MonoBehaviour, IConvertGameObjectToEntity
    {
        public InputController inputController;
        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            if (inputController != null)
            {
                var controllerEntity = conversionSystem.TryGetPrimaryEntity(inputController);
                if (controllerEntity != Entity.Null)
                {
                    dstManager.AddComponentData(entity, new InputSystem.InputControllerReference { value = controllerEntity });
                }
            }
        }
    }
}