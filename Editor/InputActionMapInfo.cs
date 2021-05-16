using System;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace NeroWeNeed.InputSystem.Editor
{
    [Serializable]
    public struct InputActionMapInfo
    {
        public InputActionAsset asset;
        public string id;
    }
    [CustomPropertyDrawer(typeof(InputActionMapInfo))]
    public class InputActionMapInfoDrawer : PropertyDrawer
    {
        private const string Uxml = "Packages/github.neroweneed.inputsystem-dots/Editor/Resources/InputActionMapInfo.uxml";
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            Debug.Log("x");
            var rootElement = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Uxml).CloneTree();
            var actionMapSelector = new PopupField<string>();

            var assetField = rootElement.Q<PropertyField>("asset-field");
            assetField.RegisterValueChangeCallback(evt =>
            {
                var container = ((VisualElement)evt.target).parent.Q<VisualElement>("popup-container");
                UpdateActionMapPopup(container, evt.changedProperty, property.FindPropertyRelative(nameof(InputActionMapInfo.id)));
            });
            UpdateActionMapPopup(rootElement.Q<VisualElement>("popup-container"), property.FindPropertyRelative(nameof(InputActionMapInfo.asset)), property.FindPropertyRelative(nameof(InputActionMapInfo.id)));
            rootElement.BindProperty(property);
            return rootElement;
        }
        private void UpdateActionMapPopup(VisualElement container,SerializedProperty assetProperty,SerializedProperty actionMapProperty) {
            container.Clear();
            if (assetProperty.objectReferenceValue == null)
            {
                actionMapProperty.stringValue = null;
            }
            else
            {
                var asset = (InputActionAsset)assetProperty.objectReferenceValue;
                var ids = asset.actionMaps.Select(actionMap => actionMap.id.ToString("B")).ToList();
                ids.Insert(0, string.Empty);
                var initialIndex = ids.IndexOf(actionMapProperty.stringValue);
                var popup = new PopupField<string>("Action Map",ids, initialIndex >= 0 ? initialIndex : 0, (id) => string.IsNullOrEmpty(id) ? "None" : asset.FindActionMap(id).name, (id) => string.IsNullOrEmpty(id) ? "None" : asset.FindActionMap(id).name);
                container.Add(popup);
                popup.BindProperty(actionMapProperty);
            }
        }
    }
}