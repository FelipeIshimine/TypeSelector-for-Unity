using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using TypeSelector;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

namespace TypeSelector
{

[CustomPropertyDrawer(typeof(TypeSelectorAttribute))]
public class TypeSelectorDrawer : PropertyDrawer
{
    private Editor _editor;

    public override VisualElement CreatePropertyGUI(SerializedProperty property)
	{
		Button typeSelectorBtn;
		Label activeTypeName;
		TypeSelectorAttribute selectorAttribute = (TypeSelectorAttribute)attribute;
		if (SerializationUtility.HasManagedReferencesWithMissingTypes(property.serializedObject.targetObject))
		{
			SerializationUtility.ClearAllManagedReferencesWithMissingTypes(property.serializedObject.targetObject);
		}

		var container = new VisualElement();
		if (property.propertyType != SerializedPropertyType.ManagedReference)
		{
			container.Add(new PropertyField(property));
			container.Add(new Label($"WARNING: Invalid use of {nameof(TypeSelectorAttribute)}")
			{
				style = { color = Color.red }
			});
		}

		var treeAsset =
			AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
				"Packages/com.felipe-ishimine.type-selector/Assets/UI_TypeSelector.uxml");
		container = treeAsset.CloneTree().Q<VisualElement>("Container");
		container.styleSheets.Add(
			AssetDatabase.LoadAssetAtPath<StyleSheet>(
				"Packages/com.felipe-ishimine.type-selector/Assets/ST_TypeSelector.uss"));
		
		switch (selectorAttribute.Mode)
		{
			case DrawMode.Default:
			{
				var propertyField = new PropertyField(property) { style = { flexGrow = 1 } };
				container.Add(propertyField);
				break;
			}
			case DrawMode.Inline:
			{
				property.isExpanded = true;

				VisualElement subContainer = new VisualElement()
				{
					style = { flexDirection = FlexDirection.Row, flexGrow = 1, marginRight = 16 }
				};
				subContainer.Add(new Label(property.displayName));
				var propertyField = new PropertyField(property) { style = { flexGrow = 1 } };
				propertyField.AddToClassList("inline");
				VisualElement contentContainer = new VisualElement()
				{
					style = { flexGrow = 1}
				};
				contentContainer.AddToClassList("no-foldout-container");
				contentContainer.Add(propertyField);
				subContainer.Add(contentContainer);

                
				container.Add(subContainer);
				break;
			}
			case DrawMode.NoFoldout:
			{
				property.isExpanded = true;
				VisualElement subContainer = new VisualElement()
				{
					style = { flexGrow = 1}
				};
	
				subContainer.Add(new Label(property.displayName));


				VisualElement contentContainer = new VisualElement();
				contentContainer.AddToClassList("no-foldout-container");
				subContainer.Add(contentContainer);
				var propertyField = new PropertyField(property) { style = { flexGrow = 1 } };
				propertyField.AddToClassList("no-foldout");
				contentContainer.Add(propertyField);
				
				subContainer.Add(contentContainer);
				container.Add(subContainer);
			}
				break;
			default:
				break;
		}

		typeSelectorBtn = container.Q<Button>("TypeSelector");
		activeTypeName = container.Q<Label>("TypeName");

		activeTypeName.text = GetButtonLabel(property);

		activeTypeName.RemoveFromClassList("none");
		activeTypeName.RemoveFromClassList("show");
		if (property.managedReferenceValue == null)
		{
			activeTypeName.AddToClassList("none");
		}
		else if (selectorAttribute.Mode == DrawMode.Inline)
		{
			activeTypeName.RemoveFromClassList("show");
		}
		else
		{
			activeTypeName.AddToClassList("show");
		}
		
		typeSelectorBtn.clicked += () =>
		{
			SelectorButtonClicked(typeSelectorBtn, property);
		};
		typeSelectorBtn.RemoveFromHierarchy();
		container.Add(typeSelectorBtn);
		return container;
	}

    private string GetButtonLabel(SerializedProperty p)
	{
		var managedReferenceValue = p.managedReferenceValue;
		if (managedReferenceValue != null)
		{
			var type = managedReferenceValue.GetType();
			var value = GetDisplayName(type);
			return value;
		}

		return "-null-";
	}

	private string GetDisplayName(Type type)
	{
		if (type == null)
		{
			return "NULL";
		}

		var typeFullName = type.FullName;
		var typeNamespace = type.Namespace;

		string resultName;
		if (type.GetCustomAttribute(typeof(TypeSelectorNameAttribute), false) is TypeSelectorNameAttribute
		    nameAttribute)
		{
			resultName = Path.GetFileName(nameAttribute.Name);
		}
		else if (!string.IsNullOrEmpty(typeFullName) && !string.IsNullOrEmpty(typeNamespace))
		{
			resultName = typeFullName.Replace(typeNamespace, string.Empty).Replace(".", string.Empty);
		}
		else if (!string.IsNullOrEmpty(typeFullName))
		{
			resultName = type.FullName;
		}
		else
		{
			resultName = type.Name;
		}

		return resultName;

	}

	private Type GetTargetType()
	{
		Type targetType;
		if (fieldInfo.FieldType.IsConstructedGenericType)
		{
			targetType = fieldInfo.FieldType.GetGenericArguments()[0];
		}
		else
		{
			targetType = fieldInfo.FieldType;
		}

		return targetType;
	}


	private void SelectorButtonClicked(Button typeBtn, SerializedProperty property)
	{

		var targetType = GetTargetType();
		var baseTypeName = GetDisplayName(targetType);

		var types = new List<Type>();

		var unityObjectType = typeof(UnityEngine.Object);

		foreach (var type in TypeCache.GetTypesDerivedFrom(targetType))
		{
			if (!type.IsAbstract && !type.IsGenericTypeDefinition && !unityObjectType.IsAssignableFrom(type))
			{
				types.Add(type);
			}
		}

		if (!targetType.IsAbstract && !targetType.IsGenericTypeDefinition)
		{
			types.Add(targetType);
		}

		var typesArray = types.ToArray();
		(string path, Type type)[] pairs = new (string path, Type type)[typesArray.Length + 1];

		for (int i = 0; i < pairs.Length - 1; i++)
		{
			var type = typesArray[i];

			string path = null;

			foreach (object customAttribute in type.GetCustomAttributes(typeof(TypeSelectorNameAttribute), false))
			{
				if (customAttribute is TypeSelectorNameAttribute dropdownPathAttribute)
				{
					path = dropdownPathAttribute.Name;
				}
			}

			if (string.IsNullOrEmpty(path))
			{
				pairs[i] = new(GetDisplayName(type), type);
			}
			else
			{
				pairs[i] = new(path, type);
			}
		}

		var rect = typeBtn.worldBound;
		rect.width = Mathf.Max(200, rect.width);

		pairs[^1].path = "-null-";

		Array.Sort(pairs, (a, b) => string.Compare(a.path, b.path, StringComparison.Ordinal));

		new AdvancedDropdownBuilder()
			.WithTitle($"{targetType.Name} Types")
			.AddElements(pairs.Select(x => x.path), out List<int> indices)
			.SetCallback(index =>
				OnTypeSelected(indices[index], typeBtn, property, pairs.Select(x => x.type).ToArray()))
			.Build()
			.Show(rect);
	}

	void OnTypeSelected(int index, Button typeBtn, SerializedProperty property, Type[] typesArray)
	{
		var type = typesArray[index];
		if (type != null)
		{
			property.managedReferenceValue = Activator.CreateInstance(typesArray[index]);
		}
		else
		{
			if (property.managedReferenceValue == null)
			{
				return;
			}

			property.managedReferenceValue = null;
		}

		property.serializedObject.ApplyModifiedProperties();
		typeBtn.text = GetButtonLabel(property);
		if (typeBtn.parent != null)
		{
			typeBtn.parent.Bind(property.serializedObject);
		}
	}

	public IEnumerable<SerializedProperty> GetChildrenProperties(SerializedProperty property)
	{
		// Copy the property to iterate over its children
		var iterator = property.Copy();
		var endProperty = property.GetEndProperty();

		// Move to the first child property
		iterator.NextVisible(true);

		while (!SerializedProperty.EqualContents(iterator, endProperty))
		{
			yield return iterator.Copy();
			iterator.NextVisible(false); // Move to the next sibling property
		}
	}
}
}