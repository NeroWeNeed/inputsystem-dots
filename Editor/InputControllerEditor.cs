using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace NeroWeNeed.InputSystem.Editor
{
    [CustomEditor(typeof(InputController))]
    public class InputControllerEditor : UnityEditor.Editor
    {
        private const string Uxml = "Packages/github.neroweneed.inputsystem-dots/Editor/Resources/InputController.uxml";
        private VisualElement rootElement;
        public override VisualElement CreateInspectorGUI()
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Uxml);
            rootElement = uxml.CloneTree();
            return rootElement;
        }

    }
}