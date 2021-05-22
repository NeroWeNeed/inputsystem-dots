using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEditor.Compilation;
using NeroWeNeed.InputSystem.Editor.ILGeneration;

namespace NeroWeNeed.InputSystem.Editor
{
    [CustomEditor(typeof(InputSystemAssemblyDefinition))]
    public class InputSystemAssemblyDefinitionEditor : UnityEditor.Editor
    {
        private const string Uxml = "Packages/github.neroweneed.inputsystem-dots/Editor/Resources/InputSystemAssemblyDefinitionEditor.uxml";
        private VisualElement rootElement;
        public override VisualElement CreateInspectorGUI()
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Uxml);
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
    }
}