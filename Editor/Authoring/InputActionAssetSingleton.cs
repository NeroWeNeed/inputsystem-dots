using Unity.Entities;
using UnityEngine;
using UnityEngine.InputSystem;
namespace NeroWeNeed.InputSystem.Editor
{

    public class InputActionAssetSingleton : MonoBehaviour
    {
        public InputActionAsset asset;

/*         public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            dstManager.AddSharedComponentData(entity, new InputActionAssetData(asset));
        } */
    }
}