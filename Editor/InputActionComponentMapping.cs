using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml.Serialization;
using UnityEditor.AssetImporters;
using UnityEngine;
namespace NeroWeNeed.InputSystem.Editor
{
    [ScriptedImporter(1, Extension)]
    public class InputActionComponentMappingAssetImporter : ScriptedImporter
    {
        public const string Extension = "inputactioncomponentmapping";
        public override void OnImportAsset(AssetImportContext ctx)
        {
            var serializer = new XmlSerializer(typeof(InputActionComponentMapping));
            using var fs = File.OpenRead(ctx.assetPath);
            var asset = ((InputActionComponentMapping)serializer.Deserialize(fs)).ToAsset();
            ctx.AddObjectToAsset("Main", asset);
            ctx.SetMainObject(asset);
        }
    }
    [XmlRoot("Components")]
    public class InputActionComponentMapping
    {
        [XmlAttribute]
        public string assembly;
        [XmlAttribute]
        public string assetGuid;

        [XmlArray("InputActionMaps")]
        public List<InputActionMap> actionMaps = new List<InputActionMap>();
        public InputActionComponentMappingAsset ToAsset()
        {
            var asset = ScriptableObject.CreateInstance<InputActionComponentMappingAsset>();
            asset.assetGuid = assetGuid;
            asset.assembly = assembly;
            asset.actionMaps = actionMaps.Select(a => new InputActionComponentMappingAsset.InputActionMap
            {
                id = a.id,
                component = a.component,
                actions = a.actions.Select(b => new InputActionComponentMappingAsset.InputActionMap.InputAction
                {
                    id = b.id,
                    component = b.component
                }).ToArray()
            }).ToArray();
            return asset;
        }
        public class InputActionMap
        {
            [XmlAttribute("id")]
            public string id;
            [XmlAttribute("component")]
            public string component;
            [XmlArray("InputActions")]
            public List<InputAction> actions = new List<InputAction>();

            public class InputAction
            {
                [XmlAttribute("id")]
                public string id;
                [XmlAttribute("component")]
                public string component;
                /*                 [XmlAttribute("valueType")]
                                public string valueType; */
            }
        }


    }
}