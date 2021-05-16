using UnityEditor;
using UnityEngine.InputSystem;

namespace NeroWeNeed.InputSystem.Editor
{

    public class InputActionAssetSingletonConversionSystem : GameObjectConversionSystem
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((InputActionAssetSingleton asset) =>
            {
                var root = this.GetPrimaryEntity(asset);
                if (asset.asset != null)
                {
                    DeclareAssetDependency(asset.gameObject, asset.asset);
                    DstEntityManager.AddSharedComponentData(root, new InputActionAssetLoadData(asset.asset));
                }

            });
        }
    }
}