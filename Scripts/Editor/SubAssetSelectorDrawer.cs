using System;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

[CustomPropertyDrawer(typeof(SubAssetSelectorAttribute))]
public sealed class SubAssetSelectorDrawer : PropertyDrawer
{
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        return SubAssetSelectorUI.Build(property, fieldInfo);
    }

 
    
}