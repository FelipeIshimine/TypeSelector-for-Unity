using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace TypeSelector
{
	[CustomPropertyDrawer(typeof(TypeSelectorAttribute))]
	public sealed class TypeSelectorDrawer : PropertyDrawer
	{
		public override VisualElement CreatePropertyGUI(SerializedProperty property)
		{
			return TypeSelectorUI.Build(property, fieldInfo, (TypeSelectorAttribute)attribute);
		}
	}
}