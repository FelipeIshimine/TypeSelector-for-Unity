using System;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

[CustomPropertyDrawer(typeof(ShowEditorAttribute))]
public class ShowEditorDrawer : PropertyDrawer
{
    private UnityEditor.Editor _editor;

    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        var container = new VisualElement();

        Foldout subEditorContainer = new Foldout();
        container.Add(subEditorContainer);

        RefreshSubEditor(property);

        subEditorContainer.TrackPropertyValue(property, RefreshSubEditor);

        var field = new PropertyField(property)
        {
            style = { flexGrow = 1}
        };
        field.Bind(property.serializedObject);

        var toggle = subEditorContainer.Q<Toggle>();
        toggle[0].Add(field);

        return container;


        void RefreshSubEditor(SerializedProperty prop)
        {
            subEditorContainer.Clear();
            var checkmark = subEditorContainer.Q<VisualElement>("unity-checkmark");
            subEditorContainer.value = false;
            checkmark.style.visibility = Visibility.Hidden;
            checkmark.SetEnabled(false);
            
            if (prop.propertyType is SerializedPropertyType.ManagedReference
                or SerializedPropertyType.ObjectReference)
            {
                object obj;

                if (prop.propertyType == SerializedPropertyType.ManagedReference)
                {
                    obj = prop.managedReferenceValue;
                }
                else if (prop.propertyType == SerializedPropertyType.ObjectReference)
                {
                    obj = prop.objectReferenceValue;
                }
                else
                {
                    throw new Exception("Wrong property Type");
                }

                if (obj is Object uObj)
                {
                    if (!_editor || _editor.target != uObj)
                    {
                        Editor.CreateCachedEditor(uObj, null, ref _editor);
                    }

                    if (_editor)
                    {

                        var subEditor = _editor.CreateInspectorGUI();
                        subEditor.Bind(_editor.serializedObject);

                        subEditor.style.borderBottomLeftRadius =
                            subEditor.style.borderBottomRightRadius =
                                subEditor.style.borderTopLeftRadius =
                                    subEditor.style.borderTopRightRadius = 12;

                        subEditor.style.paddingLeft = 18;
                        subEditor.style.paddingRight = 8;
                        subEditor.style.paddingBottom = 8;
                        subEditor.style.paddingTop = 8;

                        subEditor.style.backgroundColor = new StyleColor(new Color(0, 0, 0, .1f));

                        subEditorContainer.Add(subEditor);
                        
                        subEditorContainer.value = false;
                        checkmark.style.visibility = Visibility.Visible;
                        checkmark.SetEnabled(true);
                    }
                }
                else if (_editor)
                {
                    checkmark.style.visibility = Visibility.Hidden;
                    checkmark.SetEnabled(false);
                    subEditorContainer.value = false;
                    
                    Object.DestroyImmediate(_editor);
                    _editor = null;
                }


            }
        }
    }
}

