using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEditor.Compilation;
using Unity.Entities.CodeGeneratedJobForEach;
using UnityEngine.InputSystem;

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
            var assetField = rootElement.Q<PropertyField>("asset-field");
            assetField.RegisterValueChangeCallback(evt => generateButton.SetEnabled(evt.changedProperty.objectReferenceValue != null));
            generateButton.SetEnabled(serializedObject.FindProperty(assetField.bindingPath).objectReferenceValue != null);
            generateButton.clicked += OnGenerate;
            var inputActionMapContainer = rootElement.Q<VisualElement>("inputActionMapInfoContainer");
            var inputActionMapInfoProperty = serializedObject.FindProperty(nameof(InputSystemAssemblyDefinition.inputActionMapInfo));
            var arraySize = inputActionMapInfoProperty.arraySize;
            var asset = serializedObject.FindProperty(assetField.bindingPath).objectReferenceValue as InputActionAsset;
            if (asset != null)
            {
                for (int i = 0; i < arraySize; i++)
                {
                    var element = inputActionMapInfoUxml.CloneTree();
                    inputActionMapContainer.Add(element);
                    var property = inputActionMapInfoProperty.GetArrayElementAtIndex(i);
                    element.BindProperty(property);
                    var label = element.Q<Label>("name");
                    label.text = asset?.FindActionMap(property.FindPropertyRelative("id").stringValue, false)?.name;
                }

            }
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
    }
}