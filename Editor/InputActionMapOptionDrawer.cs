using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;
using UnityEditor.UIElements;
using UnityEngine.InputSystem;
using System.Linq;
using System;

namespace NeroWeNeed.InputSystem.Editor
{

    [CustomPropertyDrawer(typeof(InputController.InputActionMapOptions))]
    public class InputActionMapOptionsDrawer : PropertyDrawer
    {
        private const string Uxml = "Packages/github.neroweneed.inputsystem-dots/Editor/Resources/InputActionMapOptions.uxml";
        private const string EntryUxml = "Packages/github.neroweneed.inputsystem-dots/Editor/Resources/InputActionMapOptions.Entry.uxml";
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var rootElement = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Uxml).CloneTree();
            var assetField = rootElement.Q<PropertyField>("asset-field");
            assetField.RegisterValueChangeCallback(evt =>
            {
                UpdateActionMapContainer(rootElement.Q<VisualElement>("popup-container"), evt.changedProperty.objectReferenceValue as InputActionAsset, property.FindPropertyRelative(nameof(InputController.InputActionMapOptions.entries)));
            });
            UpdateActionMapContainer(rootElement.Q<VisualElement>("popup-container"), property.FindPropertyRelative(nameof(InputController.InputActionMapOptions.asset)).objectReferenceValue as InputActionAsset, property.FindPropertyRelative(nameof(InputController.InputActionMapOptions.entries)));
            return rootElement;
        }
        private void UpdateActionMapContainer(VisualElement container, InputActionAsset asset, SerializedProperty entriesProperty)
        {
            container.Clear();
            if (asset != null)
            {
                entriesProperty.serializedObject.UpdateIfRequiredOrScript();
                var entryElementTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(EntryUxml);
                var entryCount = entriesProperty.arraySize;
                for (int i = 0; i < entryCount; i++)
                {
                    var entryElement = entryElementTree.CloneTree();
                    var property = entriesProperty.GetArrayElementAtIndex(i);
                    var id = Guid.Parse(property.FindPropertyRelative("id").stringValue);
                    entryElement.Q<PropertyField>("action-map-name-single").label = asset.actionMaps.FirstOrDefault(actionMap => actionMap.id == id)?.name ?? id.ToString("N");
                    entryElement.BindProperty(property);
                    container.Add(entryElement);
                }
            }
            
            //entriesProperty.serializedObject.ApplyModifiedProperties();
        }
    }

    /*     [CustomPropertyDrawer(typeof(InputController.InputActionMapOptions.Entry))]
        public class InputActionMapOptionsEntryDrawer : PropertyDrawer
        {
            private const string Uxml = "Packages/github.neroweneed.inputsystem-dots/Editor/Resources/InputActionMapOptions.Entry.uxml";
            //private const string EntryUxml = "Packages/github.neroweneed.inputsystem-dots/Editor/Resources/InputActionMapOptions.Entry.uxml";
            public override VisualElement CreatePropertyGUI(SerializedProperty property)
            {

                var rootElement = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(Uxml).CloneTree();
                return rootElement;
            }
        } */

}