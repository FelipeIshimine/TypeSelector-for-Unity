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

namespace TypeSelector
{
	[CustomPropertyDrawer(typeof(TypeSelectorAttribute))]
	public class TypeSelectorDrawer : PropertyDrawer
	{
		private Button typeSelectorBtn;
		private Label activeTypeName;

		public override VisualElement CreatePropertyGUI(SerializedProperty property)
		{
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
					"Packages/ishimine.type-selector/Assets/UI_TypeSelector.uxml");
			container = treeAsset.CloneTree().Q<VisualElement>("Container");
			container.styleSheets.Add(
				AssetDatabase.LoadAssetAtPath<StyleSheet>(
					"Packages/ishimine.type-selector/Assets/ST_TypeSelector.uss"));


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
					container.Add(new Label(property.displayName));
					container.Add(new VisualElement()
					{
						style =
						{
							backgroundColor = new Color(0, 0, 0, .1f), width = 1, height = Length.Percent(100),
							alignSelf = Align.Center, marginTop = 12, marginBottom = 4
						}
					});

					VisualElement subContainer = new VisualElement()
					{
						style = { alignItems = Align.Stretch, flexGrow = 1, marginRight = 18 }
					};
					foreach (SerializedProperty childProperty in GetChildrenProperties(property))
					{
						subContainer.Add(new PropertyField(childProperty)
						{
							style = { flexGrow = 1 }
						});
					}

					container.Add(subContainer);
					break;
				}
				case DrawMode.NoFoldout:
					VisualElement sContainre = new VisualElement()
					{
						style = { alignItems = Align.Stretch, flexGrow = 1 }
					};
					sContainre.Add(new Label(property.displayName));

					if (property.hasVisibleChildren)
					{
						VisualElement propsContainer = new VisualElement()
						{
							style = { alignItems = Align.Stretch, flexGrow = 1 }
						};
						propsContainer.AddToClassList("no-foldout-container");
						foreach (SerializedProperty childProperty in GetChildrenProperties(property))
						{
							propsContainer.Add(new PropertyField(childProperty)
							{
								style = { flexGrow = 1 }
							});
						}

						sContainre.Add(propsContainer);
					}

					container.Add(sContainre);
					break;
				default:
					break;
			}

			typeSelectorBtn = container.Q<Button>("TypeSelector");
			activeTypeName = container.Q<Label>("TypeName");

			activeTypeName.text = GetButtonLabel(property);

			typeSelectorBtn.clicked += () => SelectorButtonClicked(typeSelectorBtn, property);
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

			return "-Select Type-";
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

			Array.Sort(pairs, (a, b) => String.Compare(a.path, b.path, StringComparison.InvariantCulture));

			new AdvancedDropdownBuilder()
				.WithTitle($"{baseTypeName} Types")
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
			typeBtn.parent.Bind(property.serializedObject);
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