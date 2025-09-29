// TypeSelectorDrawer.cs (fixed for arrays/lists/elements)
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TypeSelector
{
    [CustomPropertyDrawer(typeof(TypeSelectorAttribute))]
    public class TypeSelectorDrawer : PropertyDrawer
    {
        private static readonly Dictionary<string, Type[]> GenericCandidatesCache = new();

        static TypeSelectorDrawer()
        {
            AssemblyReloadEvents.beforeAssemblyReload += () => GenericCandidatesCache.Clear();
        }

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var selectorAttribute = (TypeSelectorAttribute)attribute;

            if (SerializationUtility.HasManagedReferencesWithMissingTypes(property.serializedObject.targetObject))
                SerializationUtility.ClearAllManagedReferencesWithMissingTypes(property.serializedObject.targetObject);

            var root = new VisualElement();

            // Show warning only if this isn't a managed reference AND not a supported collection
            if (property.propertyType != SerializedPropertyType.ManagedReference && !IsCollectionField(fieldInfo))
            {
                var warningBox = new VisualElement { style = { flexDirection = FlexDirection.Column, marginBottom = 4 } };
                warningBox.Add(new PropertyField(property));
                warningBox.Add(new Label($"WARNING: Invalid use of {nameof(TypeSelectorAttribute)} — requires [SerializeReference]")
                {
                    style = { color = Color.red }
                });
                root.Add(warningBox);
            }

            // Collections -> build per-element interface
            if (property.isArray && IsCollectionField(fieldInfo))
            {
                root.Add(BuildCollectionGUI(property));
                return root;
            }

            // Single field (original behaviour)
            var treeAsset =
                AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Packages/com.felipe-ishimine.selector-attributes/Assets/UI_TypeSelector.uxml");
            var container = treeAsset?.CloneTree().Q<VisualElement>("Container") ?? new VisualElement();
            var ss = AssetDatabase.LoadAssetAtPath<StyleSheet>("Packages/com.felipe-ishimine.selector-attributes/Assets/ST_TypeSelector.uss");
            if (ss != null) container.styleSheets.Add(ss);

            switch (selectorAttribute.Mode)
            {
                case DrawMode.Default:
                    container.Add(new PropertyField(property) { style = { flexGrow = 1 } });
                    break;
                case DrawMode.Inline:
                    property.isExpanded = true;
                    var inlineParent = new VisualElement()
                    {
	                    style =
	                    {
		                    flexDirection = FlexDirection.Row,
		                    flexGrow = 1,
		                    marginRight = 16 
	                    }
                    };
                    inlineParent.Add(new Label(property.displayName)
                    {
		                  style  =
		                  {
			                  width = Length.Percent(30),
			                  maxWidth = 220,
			                  flexGrow = 0,
			                  flexShrink = 0,
		                  }
                    });
                    
                    var contentContainer = new VisualElement()
                    {
	                    style =
	                    {
		                    flexGrow = 1,
		                    flexShrink = 0,
		                    width = Length.Percent(70),
	                    }
                    };
                    
                    var propertyField = new PropertyField(property)
                    {
	                    style =
	                    {
		                    flexGrow = 1,
		                    flexShrink = 1,
	                    }
                    };
                    propertyField.AddToClassList("inline");
                
                    contentContainer.AddToClassList("no-foldout-container");
                    contentContainer.Add(propertyField);
                    inlineParent.Add(contentContainer);
                    container.Add(inlineParent);
                    break;
                case DrawMode.NoFoldout:
                    property.isExpanded = true;
                    var sub = new VisualElement() { style = { flexGrow = 1 } };
                    sub.Add(new Label(property.displayName));
                    var content = new VisualElement();
                    content.AddToClassList("no-foldout-container");
                    sub.Add(content);
                    var pf = new PropertyField(property) { style = { flexGrow = 1 } };
                    pf.AddToClassList("no-foldout");
                    content.Add(pf);
                    sub.Add(content);
                    container.Add(sub);
                    content.style.display = property.hasVisibleChildren ? DisplayStyle.Flex : DisplayStyle.None;
                    break;
            }

            root.Add(container);

            var typeSelectorBtn = container.Q<Button>("TypeSelector") ?? new Button() { text = "Select Type" };
            var activeTypeName = container.Q<Label>("TypeName") ?? new Label();

            activeTypeName.text = GetButtonLabel(property);
            activeTypeName.RemoveFromClassList("none");
            activeTypeName.RemoveFromClassList("show");
            if (property.managedReferenceValue == null)
                activeTypeName.AddToClassList("none");
            else if (selectorAttribute.Mode == DrawMode.Inline)
                activeTypeName.RemoveFromClassList("show");
            else
                activeTypeName.AddToClassList("show");

            typeSelectorBtn.clicked += () => SelectorButtonClicked_ForProperty(typeSelectorBtn, property);
            typeSelectorBtn.RemoveFromHierarchy();
            container.Add(typeSelectorBtn);

            return root;
        }

        // ---------- Collection UI ----------
        private VisualElement BuildCollectionGUI(SerializedProperty collectionProperty)
        {
            var container = new VisualElement();
            var header = new Foldout() { text = collectionProperty.displayName, value = true };
            container.Add(header);

            var content = new VisualElement() { style = { flexDirection = FlexDirection.Column } };
            header.Add(content);

            var sizeRow = new VisualElement() { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
            sizeRow.Add(new Label("Size:"));
            var sizeField = new IntegerField() { value = collectionProperty.arraySize, style = { width = 60 } };
            sizeField.RegisterValueChangedCallback(evt =>
            {
                int newSize = Mathf.Max(0, evt.newValue);
                if (newSize == collectionProperty.arraySize) return;
                collectionProperty.arraySize = newSize;
                collectionProperty.serializedObject.ApplyModifiedProperties();
                content.Clear();
                content.Add(sizeRow);
                BuildElementRows(collectionProperty, content);
            });
            sizeRow.Add(sizeField);
            var addBtn = new Button(() =>
            {
                collectionProperty.arraySize++;
                collectionProperty.serializedObject.ApplyModifiedProperties();
                content.Clear();
                content.Add(sizeRow);
                BuildElementRows(collectionProperty, content);
                sizeField.SetValueWithoutNotify(collectionProperty.arraySize);
            }) { text = "Add" };
            sizeRow.Add(addBtn);

            content.Add(sizeRow);
            BuildElementRows(collectionProperty, content);
            return container;
        }

        private void BuildElementRows(SerializedProperty collectionProperty, VisualElement parent)
        {
            int count = collectionProperty.arraySize;
            for (int i = 0; i < count; i++)
            {
                var elementProp = collectionProperty.GetArrayElementAtIndex(i);
                var row = new VisualElement() { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 2 } };

                var label = new Label($"[{i}]") { style = { width = 30 } };
                row.Add(label);

                var elementField = new PropertyField(elementProp);
                elementField.style.flexGrow = 1;
                row.Add(elementField);

                // Button shows actual instance type (if any) or "-null-"
                var typeBtn = new Button() { text = GetButtonLabel(elementProp), style = { width = 140, marginLeft = 4 } };
                // IMPORTANT: capture elementProp/collectionProperty to avoid closure mixups
                typeBtn.clicked += () => SelectorButtonClicked_ForElement(typeBtn, elementProp, collectionProperty);
                row.Add(typeBtn);

                var removeBtn = new Button(() =>
                {
                    collectionProperty.DeleteArrayElementAtIndex(i);
                    collectionProperty.serializedObject.ApplyModifiedProperties();
                    parent.Clear();
                    // Rebuild the sizeRow + rows
                    var sizeRow = new VisualElement() { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };
                    sizeRow.Add(new Label("Size:"));
                    var sizeField2 = new IntegerField() { value = collectionProperty.arraySize, style = { width = 60 } };
                    sizeField2.RegisterValueChangedCallback(evt =>
                    {
                        int newSize = Mathf.Max(0, evt.newValue);
                        if (newSize == collectionProperty.arraySize) return;
                        collectionProperty.arraySize = newSize;
                        collectionProperty.serializedObject.ApplyModifiedProperties();
                        parent.Clear();
                        parent.Add(sizeRow);
                        BuildElementRows(collectionProperty, parent);
                    });
                    sizeRow.Add(sizeField2);
                    var addBtn2 = new Button(() =>
                    {
                        collectionProperty.arraySize++;
                        collectionProperty.serializedObject.ApplyModifiedProperties();
                        parent.Clear();
                        parent.Add(sizeRow);
                        BuildElementRows(collectionProperty, parent);
                    }) { text = "Add" };
                    sizeRow.Add(addBtn2);

                    parent.Add(sizeRow);
                    BuildElementRows(collectionProperty, parent);
                }) { text = "Remove", style = { width = 70, marginLeft = 4 } };
                row.Add(removeBtn);

                parent.Add(row);
            }
        }

        // ---------- Selectors ----------
        private void SelectorButtonClicked_ForProperty(Button typeBtn, SerializedProperty property)
        {
            var targetType = GetTargetType(fieldInfo); // declared field type (could be constructed generic)
            ShowTypeDropdownForProperty(typeBtn.worldBound, targetType, chosenType =>
            {
                if (chosenType == null) property.managedReferenceValue = null;
                else property.managedReferenceValue = Activator.CreateInstance(chosenType);

                property.serializedObject.ApplyModifiedProperties();
                if (typeBtn.parent != null) typeBtn.parent.Bind(property.serializedObject);
            });
        }

        private void SelectorButtonClicked_ForElement(Button typeBtn, SerializedProperty elementProperty, SerializedProperty collectionProperty)
        {
            // 1) Determine element declared type from the field (T in T[] or List<T>)
            var elementDeclaredType = GetElementTargetType(fieldInfo);
            // 2) If declared type couldn't be resolved (weird custom collection), try to infer from existing element instance typename
            if (elementDeclaredType == null)
            {
                var full = elementProperty.managedReferenceFullTypename;
                if (!string.IsNullOrEmpty(full))
                {
                    // managedReferenceFullTypename format: "AssemblyName TypeFullName"
                    var parts = full.Split(' ');
                    var typeName = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : parts[0];
                    elementDeclaredType = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(typeName)).FirstOrDefault(t => t != null);
                }
            }

            // 3) Fallback to object if nothing resolved (shouldn't happen for Pred<T>[] or List<Pred<T>>)
            if (elementDeclaredType == null)
            {
                Debug.LogWarning("[TypeSelector] Could not resolve element declared type; falling back to object.");
                elementDeclaredType = typeof(object);
            }
            Debug.Log(elementDeclaredType.Name);

            // Now show the type list for the ELEMENT TYPE (this is the critical fix)
            ShowTypeDropdownForProperty(typeBtn.worldBound, elementDeclaredType, chosenType =>
            {
                if (chosenType == null) elementProperty.managedReferenceValue = null;
                else elementProperty.managedReferenceValue = Activator.CreateInstance(chosenType);

                collectionProperty.serializedObject.ApplyModifiedProperties();
                if (typeBtn.parent != null) typeBtn.parent.Bind(collectionProperty.serializedObject);
                // update button text to reflect current selection/instance
                typeBtn.text = GetButtonLabel(elementProperty);
            });
        }

        // ---------- Dropdown and candidate building ----------
        private void ShowTypeDropdownForProperty(Rect worldRect, Type targetType, Action<Type> onSelect)
        {
            if (targetType == null)
            {
                onSelect?.Invoke(null);
                return;
            }

            if (targetType.IsArray)
            {
	            targetType = targetType.GetElementType();
            }
            else if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
            {
	            targetType = targetType.GetGenericArguments()[0];
            }

            var unityObjectType = typeof(UnityEngine.Object);
            var types = new List<Type>();

            // If target is a constructed generic (Pred<Context>) handle generic definition + nested generic defs
            if (targetType.IsGenericType && !targetType.IsGenericTypeDefinition)
            {
                var genericDef = targetType.GetGenericTypeDefinition();
                var targetArgs = targetType.GetGenericArguments();
                string cacheKey = (genericDef.FullName ?? genericDef.Name) + ":" + string.Join(",", targetArgs.Select(a => a.FullName ?? a.Name));

                if (!GenericCandidatesCache.TryGetValue(cacheKey, out var cached))
                {
                    var list = new List<Type>();
                    foreach (var candidate in TypeCache.GetTypesDerivedFrom(genericDef))
                    {
                        try
                        {
                            Type constructed = candidate;
                            if (candidate.IsGenericTypeDefinition)
                            {
                                constructed = candidate.MakeGenericType(targetArgs);
                            }

                            if (constructed.IsAbstract || constructed.IsGenericTypeDefinition || unityObjectType.IsAssignableFrom(constructed))
                                continue;

                            if (!targetType.IsAssignableFrom(constructed))
                                continue;

                            list.Add(constructed);
                        }
                        catch
                        {
                            // ignore construction failures
                        }
                    }

                    cached = list.ToArray();
                    GenericCandidatesCache[cacheKey] = cached;
                }

                types.AddRange(cached);

                if (!targetType.IsAbstract && !targetType.IsGenericTypeDefinition && !unityObjectType.IsAssignableFrom(targetType))
                    types.Add(targetType);
            }
            else
            {
                foreach (var t in TypeCache.GetTypesDerivedFrom(targetType))
                {
                    if (!t.IsAbstract && !t.IsGenericTypeDefinition && !unityObjectType.IsAssignableFrom(t))
                        types.Add(t);
                }

                if (!targetType.IsAbstract && !targetType.IsGenericTypeDefinition)
                    types.Add(targetType);
            }

            var typesArray = types.ToArray();
            (string path, Type type)[] pairs = new (string path, Type type)[typesArray.Length + 1];

            for (int i = 0; i < pairs.Length - 1; i++)
            {
                var type = typesArray[i];
                string path = null;
                foreach (object customAttribute in type.GetCustomAttributes(typeof(SelectorNameAttribute), false))
                {
                    if (customAttribute is SelectorNameAttribute dropdownPathAttribute) path = dropdownPathAttribute.Name;
                }

                pairs[i] = string.IsNullOrEmpty(path) ? new(SelectorName.GetDisplayName(type), type) : new(path, type);
            }

            pairs[^1].path = "-null-";
            Array.Sort(pairs, (a, b) => string.Compare(a.path, b.path, StringComparison.Ordinal));

            new AdvancedDropdownBuilder()
                .WithTitle($"{targetType.Name} Types")
                .AddElements(pairs.Select(x => x.path), out List<int> indices)
                .SetCallback(index => onSelect?.Invoke(pairs[indices[index]].type))
                .Build()
                .Show(worldRect);
        }

        // ---------- Helpers ----------
        private Type GetTargetType(FieldInfo fi) => fi.FieldType;

        private Type GetElementTargetType(FieldInfo fi)
        {
            var ft = fi.FieldType;
            if (ft.IsArray)
            {
	            Debug.Log($"IS ARRAY {ft.GetElementType().Name}");
	            return ft.GetElementType();
            }
            if (ft.IsGenericType)
            {
                var genArgs = ft.GetGenericArguments();
                Debug.Log($"IS GENERIC {genArgs}");
                if (genArgs.Length == 1)
                {
	                return genArgs[0];
                }
                
                
                var iface = ft.GetInterfaces().FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
                if (iface != null) return iface.GetGenericArguments()[0];
            }
            return null;
        }

        private bool IsCollectionField(FieldInfo fi)
        {
            if (fi == null) return false;
            var ft = fi.FieldType;
            if (ft.IsArray) return true;
            if (ft.IsGenericType)
            {
                if (ft.GetGenericArguments().Length == 1) return true;
                if (ft.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))) return true;
            }
            return false;
        }

        private string GetButtonLabel(SerializedProperty p)
        {
            var managedReferenceValue = p.managedReferenceValue;
            if (managedReferenceValue != null) return SelectorName.GetDisplayName(managedReferenceValue.GetType());
            return "-null-";
        }

        
    }
}
