using UnityEngine;
using UnityEngine.InputSystem;
using Unity.Entities;
using System;
using Unity.Scenes;
using UnityEditor;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Linq;
using Unity.Mathematics;

namespace NeroWeNeed.InputSystem.Editor
{
    public class InputController : MonoBehaviour, IConvertGameObjectToEntity
    {
        [SerializeField]
        private InputActionMapOptions options;
        private void OnValidate()
        {
            if (options.asset != null)
            {
                if (options.entries == null)
                {
                    options.entries = options.asset.actionMaps.Select(actionMap => new InputActionMapOptions.Entry(actionMap)).ToArray();
                }
                else
                {
                    var info = new Dictionary<Guid, InputActionMapOptions.Entry>();
                    foreach (var item in options.asset.actionMaps)
                    {
                        info[item.id] = new InputActionMapOptions.Entry(item);
                    }
                    foreach (var item in options.entries)
                    {
                        if (info.ContainsKey(item.Guid))
                        {
                            info[item.Guid] = item;
                        }
                    }
                    options.entries = info.Values.ToArray();
                }
            }
            else
            {
                options.entries = Array.Empty<InputActionMapOptions.Entry>();
            }
            EditorUtility.SetDirty(this);
        }

        public void Convert(Entity entity, EntityManager dstManager, GameObjectConversionSystem conversionSystem)
        {
            if (options.asset != null)
            {
                conversionSystem.DeclareAssetDependency(gameObject, options.asset);
                var assetPath = AssetDatabase.GetAssetPath(options.asset);
                if (assetPath != null)
                {
                    var guid = UnityEditor.AddressableAssets.AddressableAssetSettingsDefaultObject.Settings.DefaultGroup.GetAssetEntry(AssetDatabase.AssetPathToGUID(assetPath))?.guid;
                    if (guid != null)
                    {
                        dstManager.AddSharedComponentData(entity, new InputActionAssetRequest(Guid.Parse(guid)));
                        if (options.entries != null)
                        {
                            InputActionComponentMappingManager.Initialize();
                            foreach (var entry in options.entries.Where(entry => entry.enabledByDefault))
                            {
                                if (InputActionComponentMappingManager.TryGetActionMapComponent(entry.Guid, out var componentType))
                                {
                                    dstManager.AddComponent(entity, componentType);
                                }
                            }
                        }
                    }
                }
            }

        }
        [Serializable]
        public struct InputActionMapOptions
        {
            public InputActionAsset asset;
            public Entry[] entries;


            [Serializable]
            public struct Entry
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
                public Entry(InputActionMap actionMap)
                {
                    id = actionMap.id.ToString("B");
                    enabledByDefault = true;
                }
            }
        }
    }
}