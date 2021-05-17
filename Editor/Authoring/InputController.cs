using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Entities;
using System;
using Unity.Scenes;
using UnityEditor;
using UnityEngine.SceneManagement;

namespace NeroWeNeed.InputSystem.Editor
{
    [RequireComponent(typeof(InputActionAssetProvider))]
    public class InputController : MonoBehaviour, IConvertGameObjectToEntity
    {
        [SerializeField]
        public InputActionMapInfo actionMap;
        [SerializeField]
        private InputActionAssetSingleton assetSingleton;

        private void OnValidate()
        {
            if (assetSingleton == null)
            {
                assetSingleton = FindObjectOfType<InputActionAssetSingleton>();
                if (assetSingleton == null)
                {
                    var go = new GameObject("InputActionAsset", typeof(InputActionAssetSingleton));
                    var component = go.GetComponent<InputActionAssetSingleton>();
                    component.value = actionMap.asset;
                    assetSingleton = component;
                    SceneManager.MoveGameObjectToScene(go, this.gameObject.scene);
                }
            }
            if (assetSingleton.value != actionMap.asset)
            {
                if (assetSingleton.value == null)
                {
                    assetSingleton.value = actionMap.asset;
                }
                else if (actionMap.asset == null)
                {
                    actionMap.asset = assetSingleton.value;
                }
                else
                {
                    Debug.LogError("Multiple InputActionAssets are not supported. Please compact them into one asset.");
                }
            }
        }

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