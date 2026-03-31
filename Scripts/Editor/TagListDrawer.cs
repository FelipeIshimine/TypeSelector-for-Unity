using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace PillsList.Editor
{
    [CustomPropertyDrawer(typeof(TagList<>), true)]
    public sealed class TagListDrawer : PropertyDrawer
    {
        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var propertyPath = property.propertyPath;
            var serializedObject = property.serializedObject;
            var objectType = GetElementType();
            var theme = Theme.Create();

            SerializedProperty GetRootProperty()
            {
                return serializedObject.FindProperty(propertyPath);
            }

            SerializedProperty GetArrayProperty()
            {
                return ResolveArrayProperty(GetRootProperty());
            }

            var root = new VisualElement();
            root.style.marginBottom = 4;
            root.style.flexDirection = FlexDirection.Row;
            root.style.alignItems = Align.FlexStart;
            root.AddToClassList("unity-base-field");
            root.AddToClassList("unity-base-field__aligned");

            var title = new Label(property.displayName);
            title.AddToClassList("unity-base-field__label");
            title.style.unityFontStyleAndWeight = FontStyle.Normal;
            title.style.color = theme.TitleText;
            title.style.flexShrink = 0;
            root.Add(title);

            var pillsSurface = new VisualElement();
            pillsSurface.style.flexGrow = 1;
            pillsSurface.style.flexShrink = 1;
            pillsSurface.style.minWidth = 0;
            pillsSurface.style.borderTopLeftRadius = 6;
            pillsSurface.style.borderTopRightRadius = 6;
            pillsSurface.style.borderBottomLeftRadius = 6;
            pillsSurface.style.borderBottomRightRadius = 6;
            pillsSurface.style.paddingLeft = 4;
            pillsSurface.style.paddingRight = 4;
            pillsSurface.style.paddingTop = 4;
            pillsSurface.style.paddingBottom = 0;
            root.Add(pillsSurface);

            void ApplyPillsSurfaceChrome()
            {
	            return;
                if (EditorGUIUtility.isProSkin)
                {
                    pillsSurface.style.backgroundColor = ParseHtmlColor("#2A2A2A", theme.SurfaceBackground);
                    pillsSurface.style.borderTopWidth = 1;
                    pillsSurface.style.borderRightWidth = 1;
                    pillsSurface.style.borderBottomWidth = 3;
                    pillsSurface.style.borderLeftWidth = 1;
                    pillsSurface.style.borderTopColor = ParseHtmlColor("#0D0D0D", theme.SurfaceBorder);
                    pillsSurface.style.borderRightColor = ParseHtmlColor("#212121", theme.SurfaceBorder);
                    pillsSurface.style.borderBottomColor = ParseHtmlColor("#212121", theme.SurfaceBorder);
                    pillsSurface.style.borderLeftColor = ParseHtmlColor("#212121", theme.SurfaceBorder);
                    return;
                }

                pillsSurface.style.backgroundColor = theme.SurfaceBackground;
                pillsSurface.style.borderTopWidth = 1;
                pillsSurface.style.borderRightWidth = 1;
                pillsSurface.style.borderBottomWidth = 1;
                pillsSurface.style.borderLeftWidth = 1;
                pillsSurface.style.borderTopColor = theme.SurfaceBorder;
                pillsSurface.style.borderRightColor = theme.SurfaceBorder;
                pillsSurface.style.borderBottomColor = theme.SurfaceBorder;
                pillsSurface.style.borderLeftColor = theme.SurfaceBorder;
            }

            ApplyPillsSurfaceChrome();

            var pillsContainer = new VisualElement();
            pillsContainer.style.flexDirection = FlexDirection.Row;
            pillsContainer.style.flexWrap = Wrap.Wrap;
            pillsContainer.style.alignItems = Align.FlexStart;
            pillsContainer.style.justifyContent = Justify.FlexEnd;
            pillsSurface.Add(pillsContainer);

            var addButton = CreateInlineAddButton(theme);
            var reorderablePills = new List<VisualElement>();
            List<AssetChoice> assetChoices = new List<AssetChoice>();
            VisualElement draggedPill = null;
            int draggedIndex = -1;

            void ResetDraggedPill()
            {
                if (draggedPill != null)
                {
                    draggedPill.style.opacity = 1f;
                    draggedPill = null;
                }

                draggedIndex = -1;
            }

            int GetDropIndex(Vector2 position)
            {
                for (var index = 0; index < reorderablePills.Count; index++)
                {
                    var pill = reorderablePills[index];
                    if (pill == null || pill == draggedPill || !pill.worldBound.Contains(position))
                    {
                        continue;
                    }

                    if (pill.userData is int pillIndex)
                    {
                        return pillIndex;
                    }
                }

                return draggedIndex;
            }

            bool TryReorderChoice(int fromIndex, int toIndex)
            {
                if (fromIndex < 0 || toIndex < 0 || fromIndex == toIndex)
                {
                    return false;
                }

                serializedObject.Update();
                var arrayProperty = GetArrayProperty();
                if (arrayProperty != null)
                {
                    if (fromIndex >= arrayProperty.arraySize || toIndex >= arrayProperty.arraySize)
                    {
                        return false;
                    }

                    arrayProperty.MoveArrayElement(fromIndex, toIndex);
                    serializedObject.ApplyModifiedProperties();
                    return true;
                }

                if (!TryGetRuntimeList(serializedObject, propertyPath, out var runtimeList)
                    || fromIndex >= runtimeList.Count
                    || toIndex >= runtimeList.Count)
                {
                    return false;
                }

                Undo.RecordObject(serializedObject.targetObject, "Reorder Pill List Item");
                var item = runtimeList[fromIndex];
                runtimeList.RemoveAt(fromIndex);
                runtimeList.Insert(toIndex, item);
                EditorUtility.SetDirty(serializedObject.targetObject);
                serializedObject.Update();
                return true;
            }

            void RegisterPillDrag(VisualElement pill, int index)
            {
                pill.userData = index;
                reorderablePills.Add(pill);

                pill.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 0 || evt.target is Button)
                    {
                        return;
                    }

                    ResetDraggedPill();
                    draggedPill = pill;
                    draggedIndex = index;
                    draggedPill.style.opacity = 0.55f;
                });
            }

            void RebuildAssetChoices()
            {
                assetChoices = FindAssetsForType(objectType);
                addButton.SetEnabled(assetChoices.Count > 0);
                addButton.tooltip = assetChoices.Count > 0
                    ? $"Add {objectType.Name}"
                    : $"No {objectType.Name} assets found";
            }

            void CloseAddPopup()
            {
                addButton.style.backgroundColor = theme.PillBackground;
                addButton.style.borderTopColor = theme.PillBorder;
                addButton.style.borderRightColor = theme.PillBorder;
                addButton.style.borderBottomColor = theme.PillBorder;
                addButton.style.borderLeftColor = theme.PillBorder;
            }

            bool TryAddChoice(UnityEngine.Object asset)
            {
                return TryAddAsset(serializedObject, GetArrayProperty, asset)
                    || TryAddRuntimeAsset(serializedObject, propertyPath, asset);
            }

            bool TryReplaceChoice(int index, UnityEngine.Object asset)
            {
                serializedObject.Update();

                var arrayProperty = GetArrayProperty();
                if (arrayProperty != null)
                {
                    if (index < 0 || index >= arrayProperty.arraySize)
                    {
                        return false;
                    }

                    arrayProperty.GetArrayElementAtIndex(index).objectReferenceValue = asset;
                    serializedObject.ApplyModifiedProperties();
                    return true;
                }

                if (!TryGetRuntimeList(serializedObject, propertyPath, out var runtimeList)
                    || index < 0
                    || index >= runtimeList.Count)
                {
                    return false;
                }

                Undo.RecordObject(serializedObject.targetObject, "Replace Pill List Item");
                runtimeList[index] = asset;
                EditorUtility.SetDirty(serializedObject.targetObject);
                serializedObject.Update();
                return true;
            }

            void Refresh()
            {
                serializedObject.Update();
                RebuildAssetChoices();
                CloseAddPopup();
                ResetDraggedPill();
                reorderablePills.Clear();
                pillsContainer.Clear();

                var arrayProperty = GetArrayProperty();
                if (arrayProperty != null)
                {
                    title.tooltip = FormatCount(arrayProperty.arraySize);
                    AddSerializedPills(arrayProperty, pillsContainer, theme, Refresh, RegisterPillDrag, RegisterPillReplace);
                }
                else if (TryGetRuntimeList(serializedObject, propertyPath, out var runtimeList))
                {
                    title.tooltip = FormatCount(runtimeList.Count);
                    AddRuntimePills(runtimeList, pillsContainer, serializedObject, theme, Refresh, RegisterPillDrag, RegisterPillReplace);
                }
                else
                {
                    title.tooltip = "Unavailable";
                    pillsContainer.Add(CreateInfoLabel("Could not resolve the list data for this field.", theme));
                }

                pillsContainer.Add(addButton);
            }

            void ShowAssetDropdown(Rect anchor, string title, Action<UnityEngine.Object> onSelect)
            {
                RebuildAssetChoices();

                if (assetChoices.Count == 0 || root.panel == null)
                {
                    return;
                }

                var dropdownItems = new List<AdvancedDropdownPath>(assetChoices.Count);
                for (var index = 0; index < assetChoices.Count; index++)
                {
                    var choice = assetChoices[index];
                    dropdownItems.Add(new AdvancedDropdownPath(
                        choice.Name,
                        AssetPreview.GetMiniThumbnail(choice.Asset),
                        choice.PathLabel));
                }

                var builder = new AdvancedDropdownBuilder()
                    .WithTitle(title)
                    .SetCallback(index =>
                    {
                        if (index < 0 || index >= assetChoices.Count)
                        {
                            return;
                        }

                        onSelect?.Invoke(assetChoices[index].Asset);
                    });

                builder.AddElements(dropdownItems, out _);
                builder.Build().Show(anchor);
            }

            void OpenAddPopup()
            {
                ShowAssetDropdown(
                    addButton.worldBound,
                    $"Add {ObjectNames.NicifyVariableName(objectType.Name)}",
                    asset =>
                    {
                        if (!TryAddChoice(asset))
                        {
                            return;
                        }

                        Refresh();
                    });
            }

            void RegisterPillReplace(VisualElement pill, int index, UnityEngine.Object reference)
            {
                pill.RegisterCallback<PointerDownEvent>(evt =>
                {
                    if (evt.button != 1 || evt.target is Button)
                    {
                        return;
                    }

                    ResetDraggedPill();
                    evt.StopPropagation();
                    ShowAssetDropdown(
                        pill.worldBound,
                        $"Replace {ObjectNames.NicifyVariableName(objectType.Name)}",
                        asset =>
                        {
                            if (asset == reference || !TryReplaceChoice(index, asset))
                            {
                                return;
                            }

                            Refresh();
                        });
                });
            }

            addButton.clicked += OpenAddPopup;
            root.TrackSerializedObjectValue(serializedObject, _ => Refresh());
            root.RegisterCallback<PointerUpEvent>(evt =>
            {
                if (draggedPill == null)
                {
                    return;
                }

                var sourceIndex = draggedIndex;
                var targetIndex = GetDropIndex(evt.position);
                ResetDraggedPill();

                if (!TryReorderChoice(sourceIndex, targetIndex))
                {
                    return;
                }

                Refresh();
                evt.StopPropagation();
            });
            root.RegisterCallback<MouseLeaveEvent>(_ => ResetDraggedPill());
            root.RegisterCallback<DetachFromPanelEvent>(_ =>
            {
                ResetDraggedPill();
                CloseAddPopup();
            });

            Refresh();
            return root;
        }

        private Type GetElementType()
        {
            var type = fieldInfo.FieldType;

            while (type != null)
            {
                if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(TagList<>))
                {
                    return type.GetGenericArguments()[0];
                }

                type = type.BaseType;
            }

            return typeof(ScriptableObject);
        }

        private bool TryGetRuntimeList(SerializedObject serializedObject, string propertyPath, out IList runtimeList)
        {
            runtimeList = null;

            if (serializedObject.isEditingMultipleObjects || serializedObject.targetObject == null || fieldInfo == null)
            {
                return false;
            }

            if (!TryGetFieldOwner(serializedObject.targetObject, propertyPath, out var fieldOwner) || fieldOwner == null)
            {
                return false;
            }

            if (fieldInfo.DeclaringType != null && !fieldInfo.DeclaringType.IsInstanceOfType(fieldOwner))
            {
                return false;
            }

            var fieldValue = fieldInfo.GetValue(fieldOwner);
            if (fieldValue == null)
            {
                if (fieldInfo.FieldType.IsAbstract || fieldInfo.FieldType.ContainsGenericParameters)
                {
                    return false;
                }

                if (!typeof(IList).IsAssignableFrom(fieldInfo.FieldType))
                {
                    return false;
                }

                var constructor = fieldInfo.FieldType.GetConstructor(Type.EmptyTypes);
                if (constructor == null)
                {
                    return false;
                }

                fieldValue = constructor.Invoke(null);
                fieldInfo.SetValue(fieldOwner, fieldValue);
                EditorUtility.SetDirty(serializedObject.targetObject);
                serializedObject.Update();
            }

            runtimeList = fieldValue as IList;
            return runtimeList != null;
        }

        private static bool TryGetFieldOwner(object targetObject, string propertyPath, out object fieldOwner)
        {
            fieldOwner = targetObject;
            if (fieldOwner == null || string.IsNullOrEmpty(propertyPath))
            {
                return false;
            }

            var normalizedPath = propertyPath.Replace(".Array.data[", "[");
            var pathElements = normalizedPath.Split('.');
            for (var index = 0; index < pathElements.Length - 1; index++)
            {
                if (!TryGetPathElementValue(fieldOwner, pathElements[index], out fieldOwner) || fieldOwner == null)
                {
                    return false;
                }
            }

            return true;
        }

        private static bool TryGetPathElementValue(object source, string pathElement, out object value)
        {
            value = null;
            if (source == null || string.IsNullOrEmpty(pathElement))
            {
                return false;
            }

            var bracketIndex = pathElement.IndexOf('[');
            if (bracketIndex < 0)
            {
                return TryGetMemberValue(source, pathElement, out value);
            }

            var memberName = pathElement.Substring(0, bracketIndex);
            if (!TryGetMemberValue(source, memberName, out var collection) || collection == null)
            {
                return false;
            }

            var endBracketIndex = pathElement.IndexOf(']', bracketIndex + 1);
            if (endBracketIndex <= bracketIndex + 1)
            {
                return false;
            }

            if (!int.TryParse(pathElement.Substring(bracketIndex + 1, endBracketIndex - bracketIndex - 1), out var itemIndex))
            {
                return false;
            }

            return TryGetIndexedValue(collection, itemIndex, out value);
        }

        private static bool TryGetMemberValue(object source, string memberName, out object value)
        {
            value = null;
            var type = source.GetType();
            while (type != null)
            {
                var field = type.GetField(memberName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (field != null)
                {
                    value = field.GetValue(source);
                    return true;
                }

                var property = type.GetProperty(memberName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (property != null)
                {
                    value = property.GetValue(source, null);
                    return true;
                }

                type = type.BaseType;
            }

            return false;
        }

        private static bool TryGetIndexedValue(object collection, int index, out object value)
        {
            value = null;

            if (collection is IList list)
            {
                if (index < 0 || index >= list.Count)
                {
                    return false;
                }

                value = list[index];
                return true;
            }

            if (collection is Array array)
            {
                if (index < 0 || index >= array.Length)
                {
                    return false;
                }

                value = array.GetValue(index);
                return true;
            }

            return false;
        }
        private bool TryAddAsset(
            SerializedObject serializedObject,
            Func<SerializedProperty> getArrayProperty,
            UnityEngine.Object asset)
        {
            serializedObject.Update();

            var arrayProperty = getArrayProperty();
            if (arrayProperty == null)
            {
                return false;
            }

            var newIndex = arrayProperty.arraySize;
            arrayProperty.InsertArrayElementAtIndex(newIndex);
            arrayProperty.GetArrayElementAtIndex(newIndex).objectReferenceValue = asset;
            serializedObject.ApplyModifiedProperties();
            return true;
        }

        private bool TryAddRuntimeAsset(SerializedObject serializedObject, string propertyPath, UnityEngine.Object asset)
        {
            if (!TryGetRuntimeList(serializedObject, propertyPath, out var runtimeList))
            {
                return false;
            }

            Undo.RecordObject(serializedObject.targetObject, "Add Pill List Item");
            runtimeList.Add(asset);
            EditorUtility.SetDirty(serializedObject.targetObject);
            serializedObject.Update();
            return true;
        }

        private static SerializedProperty ResolveArrayProperty(SerializedProperty property)
        {
            if (property == null)
            {
                return null;
            }

            if (property.isArray && property.propertyType != SerializedPropertyType.String)
            {
                return property;
            }

            var directArray = property.FindPropertyRelative("Array");
            if (directArray != null && directArray.isArray && directArray.propertyType != SerializedPropertyType.String)
            {
                return directArray;
            }

            var iterator = property.Copy();
            var endProperty = property.GetEndProperty();
            var enterChildren = true;

            while (iterator.NextVisible(enterChildren) && !SerializedProperty.EqualContents(iterator, endProperty))
            {
                if (iterator.depth == property.depth + 1 && iterator.isArray && iterator.propertyType != SerializedPropertyType.String)
                {
                    return iterator.Copy();
                }

                enterChildren = false;
            }

            return null;
        }

        private static void AddSerializedPills(
            SerializedProperty arrayProperty,
            VisualElement pillsContainer,
            Theme theme,
            Action refresh,
            Action<VisualElement, int> registerReorder,
            Action<VisualElement, int, UnityEngine.Object> registerReplace)
        {
            if (arrayProperty.arraySize == 0)
            {
                pillsContainer.Add(CreateInfoLabel("No items", theme));
                return;
            }

            for (var index = 0; index < arrayProperty.arraySize; index++)
            {
                var element = arrayProperty.GetArrayElementAtIndex(index);
                var pill = CreateSerializedPill(arrayProperty, element, index, theme, refresh);
                registerReorder?.Invoke(pill, index);
                registerReplace?.Invoke(pill, index, element.objectReferenceValue);
                pillsContainer.Add(pill);
            }
        }

        private static void AddRuntimePills(
            IList runtimeList,
            VisualElement pillsContainer,
            SerializedObject serializedObject,
            Theme theme,
            Action refresh,
            Action<VisualElement, int> registerReorder,
            Action<VisualElement, int, UnityEngine.Object> registerReplace)
        {
            if (runtimeList.Count == 0)
            {
                pillsContainer.Add(CreateInfoLabel("No items", theme));
                return;
            }

            for (var index = 0; index < runtimeList.Count; index++)
            {
                var reference = runtimeList[index] as UnityEngine.Object;
                var pill = CreateRuntimePill(runtimeList, serializedObject, reference, index, theme, refresh);
                registerReorder?.Invoke(pill, index);
                registerReplace?.Invoke(pill, index, reference);
                pillsContainer.Add(pill);
            }
        }
        private static VisualElement CreateSerializedPill(
            SerializedProperty arrayProperty,
            SerializedProperty element,
            int index,
            Theme theme,
            Action refresh)
        {
            var reference = element.objectReferenceValue;
            var arrayPath = arrayProperty.propertyPath;
            var pill = CreatePillVisual(reference, theme, () =>
            {
                var currentArray = arrayProperty.serializedObject.FindProperty(arrayPath);
                if (currentArray == null || index < 0 || index >= currentArray.arraySize)
                {
                    return;
                }

                currentArray.serializedObject.Update();
                var currentElement = currentArray.GetArrayElementAtIndex(index);
                var requiresSecondDelete = currentElement.propertyType == SerializedPropertyType.ObjectReference
                    && currentElement.objectReferenceValue != null;
                currentArray.DeleteArrayElementAtIndex(index);
                if (requiresSecondDelete && index < currentArray.arraySize)
                {
                    currentArray.DeleteArrayElementAtIndex(index);
                }

                currentArray.serializedObject.ApplyModifiedProperties();
                refresh();
            });

            RegisterPing(pill, reference);
            return pill;
        }

        private static VisualElement CreateRuntimePill(
            IList runtimeList,
            SerializedObject serializedObject,
            UnityEngine.Object reference,
            int index,
            Theme theme,
            Action refresh)
        {
            var pill = CreatePillVisual(reference, theme, () =>
            {
                if (index < 0 || index >= runtimeList.Count)
                {
                    return;
                }

                Undo.RecordObject(serializedObject.targetObject, "Remove Pill List Item");
                runtimeList.RemoveAt(index);
                EditorUtility.SetDirty(serializedObject.targetObject);
                serializedObject.Update();
                refresh();
            });

            RegisterPing(pill, reference);
            return pill;
        }

        private static VisualElement CreatePillVisual(UnityEngine.Object reference, Theme theme, Action onRemove)
        {
            var pillName = reference == null ? "Missing" : reference.name;
            var accentColor = GetPillAccentColor(pillName);
            var pillBackground = theme.PillBackground;
            var pillBorder = theme.PillBorder;
            var pillHoverBackground = theme.PillHoverBackground;
            var pillHoverBorder = theme.PillHoverBorder;

            var pill = new VisualElement();
            pill.style.flexDirection = FlexDirection.Row;
            pill.style.alignItems = Align.Center;
            pill.style.backgroundColor = pillBackground;
            pill.style.borderTopWidth = 1;
            pill.style.borderRightWidth = 1;
            pill.style.borderBottomWidth = 1;
            pill.style.borderLeftWidth = 1;
            pill.style.borderTopColor = pillBorder;
            pill.style.borderRightColor = pillBorder;
            pill.style.borderBottomColor = pillBorder;
            pill.style.borderLeftColor = pillBorder;
            pill.style.borderTopLeftRadius = 8;
            pill.style.borderTopRightRadius = 8;
            pill.style.borderBottomLeftRadius = 8;
            pill.style.borderBottomRightRadius = 8;
            pill.style.paddingLeft = 6;
            pill.style.paddingRight = 3;
            pill.style.paddingTop = 1;
            pill.style.paddingBottom = 1;
            pill.style.marginLeft = 4;
            pill.style.marginBottom = 4;
            pill.style.minHeight = 18;

            var label = new Label(pillName);
            label.style.color = theme.PillText;
            label.style.fontSize = 10;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.flexShrink = 0;
            pill.Add(label);

            var actionSlot = new VisualElement();
            actionSlot.style.position = Position.Relative;
            actionSlot.style.width = 14;
            actionSlot.style.height = 14;
            actionSlot.style.marginLeft = 4;
            actionSlot.style.flexShrink = 0;

            var chipIndicator = CreatePillIndicator(accentColor);
            actionSlot.Add(chipIndicator);

            var removeButton = CreateRemoveButton(theme, onRemove);
            actionSlot.Add(removeButton);
            pill.Add(actionSlot);

            pill.RegisterCallback<MouseEnterEvent>(_ =>
            {
                pill.style.backgroundColor = pillHoverBackground;
                pill.style.borderTopColor = pillHoverBorder;
                pill.style.borderRightColor = pillHoverBorder;
                pill.style.borderBottomColor = pillHoverBorder;
                pill.style.borderLeftColor = pillHoverBorder;
                chipIndicator.style.display = DisplayStyle.None;
                removeButton.style.display = DisplayStyle.Flex;
            });
            pill.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                pill.style.backgroundColor = pillBackground;
                pill.style.borderTopColor = pillBorder;
                pill.style.borderRightColor = pillBorder;
                pill.style.borderBottomColor = pillBorder;
                pill.style.borderLeftColor = pillBorder;
                chipIndicator.style.display = DisplayStyle.Flex;
                removeButton.style.display = DisplayStyle.None;
            });

            return pill;
        }

        private static Color GetPillAccentColor(string text)
        {
            unchecked
            {
                uint hash = 2166136261;
                for (var index = 0; index < text.Length; index++)
                {
                    hash ^= text[index];
                    hash *= 16777619;
                }

                var huePalette = new[]
                {
                    0.00f,
                    0.03f,
                    0.07f,
                    0.11f,
                    0.16f,
                    0.20f,
                    0.25f,
                    0.30f,
                    0.36f,
                    0.42f,
                    0.49f,
                    0.56f,
                    0.62f,
                    0.69f,
                    0.76f,
                    0.84f,
                    0.91f,
                    0.96f,
                };
                var paletteIndex = (int)(hash % (uint)huePalette.Length);
                var hueOffset = ((hash / (uint)huePalette.Length) % 100u) / 100f;
                var saturationOffset = ((hash >> 8) & 0xFFu) / 255f;
                var valueOffset = ((hash >> 16) & 0xFFu) / 255f;
                var hue = Mathf.Repeat(huePalette[paletteIndex] + Mathf.Lerp(-0.02f, 0.02f, hueOffset), 1f);
                var saturation = EditorGUIUtility.isProSkin
                    ? Mathf.Lerp(0.50f, 0.68f, saturationOffset)
                    : Mathf.Lerp(0.40f, 0.60f, saturationOffset);
                var value = EditorGUIUtility.isProSkin
                    ? Mathf.Lerp(0.74f, 0.88f, valueOffset)
                    : Mathf.Lerp(0.60f, 0.76f, valueOffset);
                return Color.HSVToRGB(hue, saturation, value);
            }
        }

        private static Color BlendColor(Color baseColor, Color accentColor, float amount)
        {
            return new Color(
                Mathf.Lerp(baseColor.r, accentColor.r, amount),
                Mathf.Lerp(baseColor.g, accentColor.g, amount),
                Mathf.Lerp(baseColor.b, accentColor.b, amount),
                baseColor.a);
        }

        private static Color ParseHtmlColor(string htmlString, Color fallback)
        {
            return ColorUtility.TryParseHtmlString(htmlString, out var color)
                ? color
                : fallback;
        }

        private static Button CreateInlineAddButton(Theme theme)
        {
            var addButton = new Button
            {
                text = "+"
            };
            addButton.style.minWidth = 18;
            addButton.style.width = 22;
            addButton.style.height = 18;
            addButton.style.paddingLeft = 0;
            addButton.style.paddingRight = 0;
            addButton.style.paddingTop = 0;
            addButton.style.paddingBottom = 0;
            addButton.style.marginLeft = 4;
            addButton.style.marginBottom = 0;
            addButton.style.marginTop = 0;
            addButton.style.backgroundColor = theme.PillBackground;
            addButton.style.borderTopWidth = 1;
            addButton.style.borderRightWidth = 1;
            addButton.style.borderBottomWidth = 1;
            addButton.style.borderLeftWidth = 1;
            addButton.style.borderTopColor = theme.PillBorder;
            addButton.style.borderRightColor = theme.PillBorder;
            addButton.style.borderBottomColor = theme.PillBorder;
            addButton.style.borderLeftColor = theme.PillBorder;
            addButton.style.borderTopLeftRadius = 7;
            addButton.style.borderTopRightRadius = 7;
            addButton.style.borderBottomLeftRadius = 7;
            addButton.style.borderBottomRightRadius = 7;
            addButton.style.color = theme.PillText;
            addButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            addButton.style.fontSize = 12;
            addButton.RegisterCallback<MouseEnterEvent>(_ =>
            {
                if (addButton.enabledSelf)
                {
                    addButton.style.backgroundColor = theme.PillHoverBackground;
                    addButton.style.borderTopColor = theme.PillHoverBorder;
                    addButton.style.borderRightColor = theme.PillHoverBorder;
                    addButton.style.borderBottomColor = theme.PillHoverBorder;
                    addButton.style.borderLeftColor = theme.PillHoverBorder;
                }
            });
            addButton.RegisterCallback<MouseLeaveEvent>(_ =>
            {
                if (addButton.enabledSelf)
                {
                    addButton.style.backgroundColor = theme.PillBackground;
                    addButton.style.borderTopColor = theme.PillBorder;
                    addButton.style.borderRightColor = theme.PillBorder;
                    addButton.style.borderBottomColor = theme.PillBorder;
                    addButton.style.borderLeftColor = theme.PillBorder;
                }
            });
            return addButton;
        }

        private static VisualElement CreatePillIndicator(Color accentColor)
        {
            var indicator = new VisualElement();
            indicator.style.width = 14;
            indicator.style.height = 14;
            indicator.style.alignItems = Align.Center;
            indicator.style.justifyContent = Justify.Center;
            indicator.pickingMode = PickingMode.Ignore;

            var dot = new VisualElement();
            dot.style.width = 6;
            dot.style.height = 6;
            dot.style.backgroundColor = accentColor;
            dot.style.borderTopLeftRadius = 3;
            dot.style.borderTopRightRadius = 3;
            dot.style.borderBottomLeftRadius = 3;
            dot.style.borderBottomRightRadius = 3;
            dot.style.opacity = 0.95f;
            dot.pickingMode = PickingMode.Ignore;

            indicator.Add(dot);
            return indicator;
        }

        private static Button CreateRemoveButton(Theme theme, Action onClick)
        {
            var removeButton = new Button(onClick)
            {
                text = "x"
            };
            removeButton.style.alignItems = Align.Center;
            removeButton.style.justifyContent = Justify.Center;
            removeButton.style.minWidth = 14;
            removeButton.style.height = new StyleLength(Length.Pixels(14));
            removeButton.style.width = new StyleLength(Length.Pixels(14));
            removeButton.style.paddingLeft = 0;
            removeButton.style.paddingRight = 0;
            removeButton.style.paddingTop = 0;
            removeButton.style.paddingBottom = 1;
            removeButton.style.marginLeft = 0;
            removeButton.style.marginRight = 0;
            removeButton.style.marginTop = 0;
            removeButton.style.marginBottom = 0;
            
            removeButton.style.backgroundColor = theme.RemoveButtonHoverBackground;
            removeButton.style.color = theme.RemoveButtonTextHover;
            removeButton.style.borderTopWidth = 0;
            removeButton.style.borderRightWidth = 0;
            removeButton.style.borderBottomWidth = 0;
            removeButton.style.borderLeftWidth = 0;
            removeButton.style.borderTopLeftRadius = 4;
            removeButton.style.borderTopRightRadius = 4;
            removeButton.style.borderBottomLeftRadius = 4;
            removeButton.style.borderBottomRightRadius = 4;
            removeButton.style.fontSize = 8;
            removeButton.style.unityFontStyleAndWeight = FontStyle.Bold;
            removeButton.style.unityTextAlign = TextAnchor.MiddleCenter;
            
            
            removeButton.style.position = Position.Absolute;
            removeButton.style.alignSelf = Align.Center;
            removeButton.style.left = 0;
            removeButton.style.top = 0;
            removeButton.style.bottom = 0;
            removeButton.style.display = DisplayStyle.None;
            return removeButton;
        }

        private static Label CreateInfoLabel(string text, Theme theme)
        {
            var infoLabel = new Label(text);
            infoLabel.style.color = theme.SecondaryText;
            infoLabel.style.alignSelf = Align.Center;
            infoLabel.style.fontSize = 11;
            infoLabel.style.paddingLeft = 4;
            infoLabel.style.paddingBottom = 1;
            infoLabel.style.marginLeft = 4;
            return infoLabel;
        }

        private static void RegisterPing(VisualElement pill, UnityEngine.Object reference)
        {
            pill.RegisterCallback<ClickEvent>(evt =>
            {
                if (evt.button != 0 || evt.target is Button || reference == null)
                {
                    return;
                }

                EditorGUIUtility.PingObject(reference);
                Selection.activeObject = reference;
            });
        }

        private static List<AssetChoice> FindAssetsForType(Type objectType)
        {
            var choices = new List<AssetChoice>();
            var guids = AssetDatabase.FindAssets($"t:{objectType.Name}");

            for (var index = 0; index < guids.Length; index++)
            {
                var path = AssetDatabase.GUIDToAssetPath(guids[index]);
                var asset = AssetDatabase.LoadAssetAtPath(path, objectType) as UnityEngine.Object;
                if (asset == null || !objectType.IsInstanceOfType(asset))
                {
                    continue;
                }

                choices.Add(new AssetChoice(asset.name, GetPathLabel(path), asset));
            }

            choices.Sort((left, right) =>
            {
                var titleCompare = string.Compare(left.Name, right.Name, StringComparison.OrdinalIgnoreCase);
                if (titleCompare != 0)
                {
                    return titleCompare;
                }

                return string.Compare(left.PathLabel, right.PathLabel, StringComparison.OrdinalIgnoreCase);
            });

            return choices;
        }

        private static string GetPathLabel(string assetPath)
        {
            var directory = Path.GetDirectoryName(assetPath);
            if (string.IsNullOrEmpty(directory))
            {
                return "Assets";
            }

            return directory.Replace('\\', '/');
        }

        private static string FormatCount(int count)
        {
            return count == 1 ? "1 item" : $"{count} items";
        }

        private readonly struct AssetChoice
        {
            public AssetChoice(string name, string pathLabel, UnityEngine.Object asset)
            {
                Name = name;
                PathLabel = pathLabel;
                Asset = asset;
            }

            public string Name { get; }
            public string PathLabel { get; }
            public UnityEngine.Object Asset { get; }
        }

        private readonly struct Theme
        {
            public static Theme Create()
            {
                if (EditorGUIUtility.isProSkin)
                {
                    return new Theme(
                        new Color(0.13f, 0.13f, 0.13f, 1f),
                        new Color(0.09f, 0.09f, 0.09f, 1f),
                        new Color(0.88f, 0.88f, 0.88f, 1f),
                        new Color(0.60f, 0.60f, 0.60f, 1f),
                        new Color(0.22f, 0.24f, 0.27f, 1f),
                        new Color(0.29f, 0.32f, 0.37f, 1f),
                        new Color(0.38f, 0.43f, 0.50f, 1f),
                        new Color(0.46f, 0.53f, 0.62f, 1f),
                        new Color(0.79f, 0.83f, 0.88f, 1f),
                        new Color(0.32f, 0.36f, 0.42f, 1f),
                        new Color(0.43f, 0.49f, 0.57f, 1f),
                        new Color(0.72f, 0.76f, 0.82f, 1f),
                        new Color(0.94f, 0.97f, 1.00f, 1f),
                        new Color(0.20f, 0.22f, 0.25f, 1f),
                        new Color(0.31f, 0.35f, 0.40f, 1f),
                        new Color(0.92f, 0.95f, 0.99f, 1f),
                        new Color(0.22f, 0.25f, 0.30f, 1f),
                        new Color(0.28f, 0.33f, 0.40f, 1f),
                        new Color(0.73f, 0.80f, 0.90f, 1f),
                        new Color(0.92f, 0.95f, 0.99f, 1f),
                        new Color(0.66f, 0.71f, 0.78f, 1f));
                }

                return new Theme(
                    new Color(1.00f, 1.00f, 1.00f, 1f),
                    new Color(0.69f, 0.69f, 0.69f, 1f),
                    new Color(0.15f, 0.15f, 0.15f, 1f),
                    new Color(0.45f, 0.45f, 0.45f, 1f),
                    new Color(0.96f, 0.96f, 0.96f, 1f),
                    new Color(0.90f, 0.90f, 0.90f, 1f),
                    new Color(0.76f, 0.76f, 0.76f, 1f),
                    new Color(0.61f, 0.70f, 0.81f, 1f),
                    new Color(0.38f, 0.43f, 0.50f, 1f),
                    new Color(0.84f, 0.89f, 0.95f, 1f),
                    //new Color(0.86f, 0.90f, 0.95f, 1f),
                    //new Color(0.72f, 0.79f, 0.88f, 1f),
                    new Color(0.84f, 0.89f, 0.95f, 1f),
                    new Color(0.43f, 0.43f, 0.43f, 1f),
                    new Color(0.22f, 0.27f, 0.34f, 1f),
                    new Color(0.98f, 0.98f, 0.98f, 1f),
                    new Color(0.82f, 0.82f, 0.82f, 1f),
                    new Color(0.18f, 0.18f, 0.18f, 1f),
                    new Color(0.93f, 0.95f, 0.98f, 1f),
                    new Color(0.86f, 0.90f, 0.95f, 1f),
                    new Color(0.45f, 0.56f, 0.72f, 1f),
                    new Color(0.16f, 0.20f, 0.26f, 1f),
                    new Color(0.43f, 0.47f, 0.53f, 1f));
            }

            private Theme(
                Color surfaceBackground,
                Color surfaceBorder,
                Color titleText,
                Color secondaryText,
                Color pillBackground,
                Color pillHoverBackground,
                Color pillBorder,
                Color pillHoverBorder,
                Color pillText,
                Color removeButtonBackground,
                Color removeButtonHoverBackground,
                Color removeButtonText,
                Color removeButtonTextHover,
                Color popupBackground,
                Color popupBorder,
                Color popupHeaderText,
                Color popupRowBackground,
                Color popupRowHoverBackground,
                Color popupRowAccent,
                Color popupRowText,
                Color popupRowSubtext)
            {
                SurfaceBackground = surfaceBackground;
                SurfaceBorder = surfaceBorder;
                TitleText = titleText;
                SecondaryText = secondaryText;
                PillBackground = pillBackground;
                PillHoverBackground = pillHoverBackground;
                PillBorder = pillBorder;
                PillHoverBorder = pillHoverBorder;
                PillText = pillText;
                RemoveButtonBackground = removeButtonBackground;
                //RemoveButtonHoverBackground = removeButtonHoverBackground;
                RemoveButtonText = removeButtonText;
                RemoveButtonTextHover = removeButtonTextHover;
                PopupBackground = popupBackground;
                PopupBorder = popupBorder;
                PopupHeaderText = popupHeaderText;
                PopupRowBackground = popupRowBackground;
                PopupRowHoverBackground = popupRowHoverBackground;
                PopupRowAccent = popupRowAccent;
                PopupRowText = popupRowText;
                PopupRowSubtext = popupRowSubtext;
            }

            public Color SurfaceBackground { get; }
            public Color SurfaceBorder { get; }
            public Color TitleText { get; }
            public Color SecondaryText { get; }
            public Color PillBackground { get; }
            public Color PillHoverBackground { get; }
            public Color PillBorder { get; }
            public Color PillHoverBorder { get; }
            public Color PillText { get; }
            public Color RemoveButtonBackground { get; }
            public Color RemoveButtonHoverBackground => Color.clear;
            public Color RemoveButtonText { get; }
            public Color RemoveButtonTextHover { get; }
            public Color PopupBackground { get; }
            public Color PopupBorder { get; }
            public Color PopupHeaderText { get; }
            public Color PopupRowBackground { get; }
            public Color PopupRowHoverBackground { get; }
            public Color PopupRowAccent { get; }
            public Color PopupRowText { get; }
            public Color PopupRowSubtext { get; }
        }
    }
}
