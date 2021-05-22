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
        private const string EntryUxml = "Packages/github.neroweneed.inputsystem-dots/Editor/Resources/InputController.InputActionMapOption.uxml";
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
                rootElement.Query<VisualElement>(null, "requires-asset").ForEach(element =>
                {
                    var display = element.style.display;
                    display.value = asset != null ? DisplayStyle.Flex : DisplayStyle.None;
                    element.style.display = display;
                });
            });
            var asset = serializedObject.FindProperty("asset").objectReferenceValue as InputActionAsset;
            UpdateControlSchemeContainer(rootElement.Q<VisualElement>("control-scheme-container"), asset, serializedObject.FindProperty(nameof(InputController.controlScheme)));
            InitActionMapContainer(rootElement.Q<VisualElement>("action-map-options-container"), asset, serializedObject.FindProperty(nameof(InputController.actionMapOptions)));
            rootElement.Query<VisualElement>(null, "requires-asset").ForEach(element =>
            {
                var display = element.style.display;
                display.value = asset != null ? DisplayStyle.Flex : DisplayStyle.None;
                element.style.display = display;
            });
            rootElement.Q<Button>("enable-all").clicked += OnEnableAll;
            rootElement.Q<Button>("disable-all").clicked += OnDisableAll;
            return rootElement;
        }
        private void OnEnableAll()
        {
            SetInputActionMapEnableState(true);
        }
        private void OnDisableAll()
        {
            SetInputActionMapEnableState(false);
        }
        private void SetInputActionMapEnableState(bool state)
        {
            var property = serializedObject.FindProperty(nameof(InputController.actionMapOptions));
            for (int i = 0; i < property.arraySize; i++)
            {
                property.GetArrayElementAtIndex(i).FindPropertyRelative(nameof(InputController.InputActionMapOption.enabledByDefault)).boolValue = state;
            }
            serializedObject.ApplyModifiedProperties();
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
                    var id = currentProperty.FindPropertyRelative("id").stringValue;
                    if (Guid.TryParse(id, out var guid))
                    {
                        entryElement.Q<Toggle>("action-map-name-single").label = asset.FindActionMap(guid)?.name ?? guid.ToString("N");
                        entryElement.BindProperty(currentProperty);
                        container.Add(entryElement);
                    }


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