using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEditor.Compilation;
using Unity.Entities.CodeGeneratedJobForEach;
using UnityEngine.InputSystem;
using System.Runtime.InteropServices;
using UnityEditor.AssetImporters;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using NeroWeNeed.InputSystem.Editor.ILGeneration;

namespace NeroWeNeed.InputSystem.Editor
{
    [CustomEditor(typeof(InputSystemAssemblyDefinition))]
    public class InputSystemAssemblyDefinitionEditor : UnityEditor.Editor
    {
        private const string Uxml = "Packages/github.neroweneed.inputsystem-dots/Editor/Resources/InputSystemAssemblyDefinitionEditor.uxml";
        private const string InputActionMapUxml = "Packages/github.neroweneed.inputsystem-dots/Editor/Resources/InputSystemAssemblyDefinition.InputActionMapInfo.uxml";
        private VisualElement rootElement;
        public override VisualElement CreateInspectorGUI()
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Uxml);
            var inputActionMapInfoUxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(InputActionMapUxml);
            rootElement = uxml.CloneTree();
            var generateButton = rootElement.Q<Button>("generate");
            var assetField = rootElement.Q<ObjectField>("asset-field");
            assetField.RegisterValueChangedCallback(evt => generateButton.SetEnabled(evt.newValue != null));
            generateButton.SetEnabled(serializedObject.FindProperty(assetField.bindingPath).objectReferenceValue != null);
            generateButton.clicked += OnGenerate;
            return rootElement;
        }
        private void OnGenerate()
        {
            if (InputSystemAssemblyBuilder.BuildAssembly((InputSystemAssemblyDefinition)serializedObject.targetObject, out string assemblyPath))
            {
                AssetDatabase.ImportAsset(assemblyPath);
                AssetDatabase.Refresh();
                CompilationPipeline.RequestScriptCompilation();
            }
        }
        /// <summary>
        /// Post Processor for managing InputSystem Assemblies if corresponding InputActionAssets are modified or deleted.
        /// </summary>
        class InputActionPostProcessor : AssetPostprocessor
        {
            static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
            {
                var inputSystemAssemblyDefinitionGuids = AssetDatabase.FindAssets($"t:{nameof(InputSystemAssemblyDefinition)}");
                if (inputSystemAssemblyDefinitionGuids.Length > 0)
                {
                    var inputSystemAssemblyDefinitions = inputSystemAssemblyDefinitionGuids.Select(guid => AssetDatabase.LoadAssetAtPath<InputSystemAssemblyDefinition>(AssetDatabase.GUIDToAssetPath(guid))).Where(asset => asset.asset != null).ToDictionary(asset => AssetDatabase.GetAssetPath(asset.asset), asset => asset);
                    var assembliesToDelete = new HashSet<string>();
                    var assembliesToRebuild = new HashSet<InputSystemAssemblyDefinition>();
                    bool requestRefresh = false;
                    foreach (var item in deletedAssets.Where(asset => asset.EndsWith(".inputactions")))
                    {
                        if (inputSystemAssemblyDefinitions.TryGetValue(item, out var inputSystemAssemblyDefinition))
                        {
                            assembliesToDelete.Add(inputSystemAssemblyDefinition.assemblyPath);
                        }
                    }
                    foreach (var item in importedAssets.Where(asset => asset.EndsWith(".inputactions")))
                    {
                        if (inputSystemAssemblyDefinitions.TryGetValue(item, out var inputSystemAssemblyDefinition))
                        {
                            assembliesToRebuild.Add(inputSystemAssemblyDefinition);
                        }
                    }
                    if (assembliesToRebuild.Count > 0)
                    {
                        var importPaths = new List<string>();
                        foreach (var item in assembliesToRebuild)
                        {
                            if (InputSystemAssemblyBuilder.BuildAssembly(item, out var assemblyPath))
                            {
                                importPaths.Add(assemblyPath);
                            }
                            else if (File.Exists(assemblyPath))
                            {
                                assembliesToDelete.Add(assemblyPath);
                            }
                        }
                        foreach (var importPath in importPaths)
                        {
                            AssetDatabase.ImportAsset(importPath);
                        }
                        requestRefresh = true;
                    }
                    if (assembliesToDelete.Count > 0)
                    {
                        var failedPaths = new List<string>();
                        if (!AssetDatabase.DeleteAssets(assembliesToDelete.ToArray(), failedPaths))
                        {
                            foreach (var failedPath in failedPaths)
                            {
                                Debug.LogError($"Failed to Delete InputSystem Assembly: {failedPath}");
                            }
                        }
                        requestRefresh = true;
                    }
                    if (requestRefresh)
                    {
                        AssetDatabase.Refresh();
                        CompilationPipeline.RequestScriptCompilation();
                    }
                }

            }
        }
    }
}