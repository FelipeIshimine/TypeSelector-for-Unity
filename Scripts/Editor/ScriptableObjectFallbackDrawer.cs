using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(ScriptableObject), true)]
public class ScriptableObjectFallbackDrawer : PropertyDrawer
{
    // One shared instance of the real drawer — we just borrow its CreateProperty method.
    private readonly AssetSelectorDrawer _inner = new();

    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        if (property.propertyType != SerializedPropertyType.ObjectReference)
            return base.CreatePropertyGUI(property);

        var fieldType = GetConcreteFieldType();

        // Only act as the fallback for ScriptableObject fields.
        // If the field already has an AssetSelectorAttribute, that drawer takes priority
        // (Unity picks the most specific drawer first), so we won't reach this code.
        if (!typeof(ScriptableObject).IsAssignableFrom(fieldType))
            return base.CreatePropertyGUI(property);

        return _inner.CreateProperty(
            property,
            fieldType,
            AssetSelectorAttribute.GroupMode.None   // sensible default; change to None if you prefer flat list
        );
    }

    // IMGUI fallback — keeps the default look when UI Toolkit isn't used.
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        => EditorGUI.PropertyField(position, property, label);

    private Type GetConcreteFieldType()
    {
        if (fieldInfo == null) return typeof(ScriptableObject);

        var t = fieldInfo.FieldType;

        // Unwrap List<T> / T[] so we get the element type
        if (t.IsArray)
            t = t.GetElementType() ?? t;
        else if (t.IsGenericType)
            t = t.GetGenericArguments()[0];

        return typeof(ScriptableObject).IsAssignableFrom(t) ? t : typeof(ScriptableObject);
    }
}