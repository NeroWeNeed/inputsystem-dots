using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEngine.InputSystem;
using System.Linq;
using System;

namespace NeroWeNeed.InputSystem.Editor
{
    [CustomEditor(typeof(InputController))]
    public class InputControllerEditor : UnityEditor.Editor
    {
        private const string Uxml = "Packages/github.neroweneed.inputsystem-dots/Editor/Resources/InputController.uxml";
        private const string EntryUxml = "Packages/github.neroweneed.inputsystem-dots/Editor/Resources/InputActionMapOptions.Entry.uxml";
        private VisualElement rootElement;
        public override VisualElement CreateInspectorGUI()
        {
            var uxml = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Uxml);
            rootElement = uxml.CloneTree();
            var assetField = rootElement.Q<ObjectField>("asset-field");
            assetField.RegisterValueChangedCallback(evt =>
            {
                var asset = evt.newValue as InputActionAsset;
                UpdateControlSchemeContainer(rootElement.Q<VisualElement>("control-scheme-container"), asset, serializedObject.FindProperty(nameof(InputController.controlScheme)));
                UpdateActionMapContainer(rootElement.Q<VisualElement>("action-map-options-container"), asset, serializedObject.FindProperty(nameof(InputController.actionMapOptions)));
            });
            var asset = serializedObject.FindProperty("asset").objectReferenceValue as InputActionAsset;
            UpdateControlSchemeContainer(rootElement.Q<VisualElement>("control-scheme-container"), asset, serializedObject.FindProperty(nameof(InputController.controlScheme)));
            InitActionMapContainer(rootElement.Q<VisualElement>("action-map-options-container"), asset, serializedObject.FindProperty(nameof(InputController.actionMapOptions)));
            return rootElement;
        }
        private void UpdateActionMapContainer(VisualElement container, InputActionAsset asset, SerializedProperty property)
        {
            container.Clear();
            property.ClearArray();
            if (asset != null)
            {
                property.arraySize = asset.actionMaps.Count;
                serializedObject.ApplyModifiedProperties();
                serializedObject.UpdateIfRequiredOrScript();
                var entryElementTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(EntryUxml);
                var entryCount = property.arraySize;
                for (int i = 0; i < entryCount; i++)
                {
                    var entryElement = entryElementTree.CloneTree();
                    var currentProperty = property.GetArrayElementAtIndex(i);
                    currentProperty.FindPropertyRelative(nameof(InputController.InputActionMapOption.id)).stringValue = asset.actionMaps[i].id.ToString("B");
                    entryElement.Q<Toggle>("action-map-name-single").label = asset.actionMaps[i].name;
                    entryElement.BindProperty(currentProperty);
                    container.Add(entryElement);
                }
                serializedObject.ApplyModifiedProperties();
            }
        }
        private void InitActionMapContainer(VisualElement container, InputActionAsset asset, SerializedProperty property)
        {
            if (asset != null)
            {
                var entryElementTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(EntryUxml);
                var entryCount = property.arraySize;
                for (int i = 0; i < entryCount; i++)
                {
                    var entryElement = entryElementTree.CloneTree();
                    var currentProperty = property.GetArrayElementAtIndex(i);
                    var id = Guid.Parse(currentProperty.FindPropertyRelative("id").stringValue);
                    entryElement.Q<Toggle>("action-map-name-single").label = asset.FindActionMap(id)?.name ?? id.ToString("N");
                    entryElement.BindProperty(currentProperty);
                    container.Add(entryElement);
                }
            }
        }
        private void UpdateControlSchemeContainer(VisualElement container, InputActionAsset asset, SerializedProperty controlSchemeProperty)
        {
            container.Clear();
            if (asset != null)
            {
                serializedObject.UpdateIfRequiredOrScript();
                var controlSchemes = asset.controlSchemes.Select(controlScheme => controlScheme.name).ToList();
                controlSchemes.Insert(0, string.Empty);
                var popup = new PopupField<string>("Control Scheme", controlSchemes, 0, FormatPopupString, FormatPopupString);
                popup.BindProperty(controlSchemeProperty);
                container.Add(popup);
            }
        }
        private static string FormatPopupString(string input) => string.IsNullOrEmpty(input) ? "Any" : input;

    }

}