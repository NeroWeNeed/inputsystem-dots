using System;
using Unity.Entities;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
namespace NeroWeNeed.InputSystem.Editor
{
    public class InputActionAssetSingleton : MonoBehaviour, IConvertGameObjectToEntity
    {
        public InputActionAsset value;
        //public LoadType loadType;
        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            if (value != null)
            {
                conversionSystem.DeclareAssetDependency(this.gameObject, value);
                var guid = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings.DefaultGroup.GetAssetEntry(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value))).guid;
                dstManager.AddComponentData(entity, new InputActionAssetLoadRequest { value = Guid.Parse(guid) });
/*                 switch (loadType)
                {
                    case LoadType.Raw:
                        dstManager.AddSharedComponentData(entity, new InputActionAssetLoadRaw(value));
                        break;
                    case LoadType.Address:
                        var address = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings.DefaultGroup.GetAssetEntry(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value))).address;
                        dstManager.AddSharedComponentData(entity, new InputActionAssetLoadAddress { value = address });
                        break;
                    case LoadType.Guid:
                        var guid = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings.DefaultGroup.GetAssetEntry(AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(value))).guid;
                        dstManager.AddComponentData(entity, new InputActionAssetLoadGuid { value = Guid.Parse(guid) });
                        break;
                } */

            }
        }
/*         public enum LoadType
        {
            Raw, Address, Guid
        } */
    }
}