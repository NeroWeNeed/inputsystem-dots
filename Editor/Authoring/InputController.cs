using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Entities;
using System;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

namespace NeroWeNeed.InputSystem.Editor
{
    public class InputController : MonoBehaviour, IConvertGameObjectToEntity
    {
        public InputActionAsset asset;
        public string controlScheme;
        public List<InputActionMapOption> actionMapOptions;
        private void OnValidate()
        {
            if (asset != null)
            {
                if (actionMapOptions == null)
                {
                    actionMapOptions = asset.actionMaps.Select(actionMap => new InputActionMapOption(actionMap)).ToList();
                }
                else
                {
                    var actionMapOptionInfo = new Dictionary<Guid, InputActionMapOption>();
                    foreach (var actionMap in asset.actionMaps)
                    {
                        actionMapOptionInfo.Add(actionMap.id, default);
                    }
                    for (int i = 0; i < actionMapOptions.Count; i++)
                    {
                        actionMapOptionInfo[actionMapOptions[i].Guid] = actionMapOptions[i];
                    }
                    var result = actionMapOptionInfo.Values.ToList();
                    if (!result.SequenceEqual(actionMapOptions))
                    {
                        EditorUtility.SetDirty(this);
                    }
                    actionMapOptions = result;
                }
            }
        }

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            if (asset != null)
            {
                conversionSystem.DeclareAssetDependency(gameObject, asset);
                var assetPath = AssetDatabase.GetAssetPath(asset);
                if (assetPath != null)
                {
                    var guid = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings.DefaultGroup.GetAssetEntry(AssetDatabase.AssetPathToGUID(assetPath))?.guid;
                    if (guid != null)
                    {
                        dstManager.AddSharedComponentData(entity, new InputActionAssetRequest(Guid.Parse(guid)));
                        if (actionMapOptions != null)
                        {
                            InputActionComponentManager.Initialize();
                            foreach (var entry in actionMapOptions.Where(entry => entry.enabledByDefault))
                            {
                                if (InputActionComponentManager.TryGetActionMapComponent(entry.Guid, out var componentType))
                                {
                                    dstManager.AddComponent(entity, componentType);
                                }
                            }
                        }
                        if (!string.IsNullOrEmpty(controlScheme))
                        {
                            dstManager.AddComponentData(entity, new InputControlScheme(asset.FindControlSchemeIndex(controlScheme)));
                        }
                        else
                        {
                            dstManager.AddComponentData(entity, new InputControlScheme(-1));
                        }
                    }
                }
            }

        }
        [Serializable]
        public struct InputActionMapOption
        {

            public string id;
            private Guid guid;
            public Guid Guid
            {
                get
                {
                    if (guid == Guid.Empty && !string.IsNullOrEmpty(id))
                    {
                        if (!Guid.TryParseExact(id, "B", out guid))
                        {
                            id = null;
                        }
                    }
                    return guid;
                }
            }
            public bool enabledByDefault;
            public InputActionMapOption(InputActionMap actionMap)
            {
                id = actionMap.id.ToString("B");
                enabledByDefault = false;
            }
        }
    }
}