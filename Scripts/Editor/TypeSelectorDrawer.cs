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

        // ── Palette ──────────────────────────────────────────────────────────
        private static readonly Color PanelBg        = new(0.14f, 0.14f, 0.14f, 1f);
        private static readonly Color PanelBorder     = new(0.25f, 0.25f, 0.25f, 1f);
        private static readonly Color RowBgEven       = new(0.17f, 0.17f, 0.17f, 1f);
        private static readonly Color RowBgOdd        = new(0.20f, 0.20f, 0.20f, 1f);
        private static readonly Color AccentBlue      = new(0.22f, 0.50f, 0.90f, 1f);
        private static readonly Color AccentBlueDark  = new(0.16f, 0.38f, 0.70f, 1f);
        private static readonly Color AccentGreen     = new(0.22f, 0.70f, 0.44f, 1f);
        private static readonly Color AccentGreenDark = new(0.16f, 0.52f, 0.32f, 1f);
        private static readonly Color AccentRed       = new(0.80f, 0.26f, 0.26f, 1f);
        private static readonly Color AccentRedDark   = new(0.60f, 0.18f, 0.18f, 1f);
        private static readonly Color HeaderBg        = new(0.19f, 0.22f, 0.28f, 1f);
        private static readonly Color LabelMuted      = new(0.65f, 0.65f, 0.65f, 1f);
        private static readonly Color LabelBright     = new(0.90f, 0.90f, 0.90f, 1f);
        private static readonly Color IndexLabel      = new(0.45f, 0.72f, 1.00f, 1f);
        // ─────────────────────────────────────────────────────────────────────

        static TypeSelectorDrawer()
        {
            AssemblyReloadEvents.beforeAssemblyReload += () => GenericCandidatesCache.Clear();
        }

        // ── Helpers ───────────────────────────────────────────────────────────
        private static void SetRadius(IStyle s, float r)
        {
            s.borderTopLeftRadius     = r;
            s.borderTopRightRadius    = r;
            s.borderBottomLeftRadius  = r;
            s.borderBottomRightRadius = r;
        }

        private static void SetBorderColor(IStyle s, Color c)
        {
            s.borderTopColor    = c;
            s.borderRightColor  = c;
            s.borderBottomColor = c;
            s.borderLeftColor   = c;
        }

        private static void SetBorderWidth(IStyle s, float w)
        {
            s.borderTopWidth    = w;
            s.borderRightWidth  = w;
            s.borderBottomWidth = w;
            s.borderLeftWidth   = w;
        }

        private static void SetPadding(IStyle s, float v, float h)
        {
            s.paddingTop    = v; s.paddingBottom = v;
            s.paddingLeft   = h; s.paddingRight  = h;
        }

        private static Button StyledButton(string label, Color bg, Color border, float width = -1)
        {
            var btn = new Button { text = label };
            btn.style.backgroundColor = bg;
            SetBorderColor(btn.style, border);
            SetBorderWidth(btn.style, 1f);
            SetRadius(btn.style, 4f);
            SetPadding(btn.style, 3f, 8f);
            btn.style.color      = LabelBright;
            btn.style.fontSize   = 11;
            btn.style.unityFontStyleAndWeight = FontStyle.Bold;
            //btn.style.cursor     = new StyleCursor(new Cursor());
            if (width > 0) btn.style.width = width;
            return btn;
        }
        // ─────────────────────────────────────────────────────────────────────

        public override VisualElement CreatePropertyGUI(SerializedProperty property)
        {
            var selectorAttribute = (TypeSelectorAttribute)attribute;

            if (SerializationUtility.HasManagedReferencesWithMissingTypes(property.serializedObject.targetObject))
                SerializationUtility.ClearAllManagedReferencesWithMissingTypes(property.serializedObject.targetObject);

            var root = new VisualElement();

            // Show warning only if this isn't a managed reference AND not a supported collection
            if (property.propertyType != SerializedPropertyType.ManagedReference && !IsCollectionField(fieldInfo))
            {
                var warningBox = new VisualElement();
                warningBox.style.flexDirection  = FlexDirection.Column;
                warningBox.style.marginBottom   = 4;
                warningBox.style.backgroundColor = new Color(0.45f, 0.18f, 0.10f, 0.6f);
                SetRadius(warningBox.style, 4f);
                SetPadding(warningBox.style, 4f, 6f);
                SetBorderColor(warningBox.style, AccentRed);
                SetBorderWidth(warningBox.style, 1f);

                warningBox.Add(new PropertyField(property));
                var warnLabel = new Label($"⚠  Invalid use of {nameof(TypeSelectorAttribute)} — requires [SerializeReference]");
                warnLabel.style.color    = new Color(1f, 0.55f, 0.45f);
                warnLabel.style.fontSize = 10;
                warnLabel.style.unityFontStyleAndWeight = FontStyle.Italic;
                warnLabel.style.marginTop = 2;
                warningBox.Add(warnLabel);
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
                            flexGrow      = 1,
                            marginRight   = 16
                        }
                    };
                    inlineParent.Add(new Label(property.displayName)
                    {
                        style =
                        {
                            width     = Length.Percent(30),
                            maxWidth  = 220,
                            flexGrow  = 0,
                            flexShrink = 0,
                        }
                    });

                    var contentContainer = new VisualElement()
                    {
                        style =
                        {
                            flexGrow   = 1,
                            flexShrink = 0,
                            width      = Length.Percent(70),
                        }
                    };

                    var propertyField = new PropertyField(property)
                    {
                        style =
                        {
                            flexGrow   = 1,
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

            var typeSelectorBtn = container.Q<Button>("TypeSelector") ?? StyledButton("Select Type", AccentBlueDark, AccentBlue);

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
            // Outer panel
            var panel = new VisualElement();
            panel.style.marginTop    = 4;
            panel.style.marginBottom = 4;
            panel.style.backgroundColor = PanelBg;
            SetRadius(panel.style, 6f);
            SetBorderColor(panel.style, PanelBorder);
            SetBorderWidth(panel.style, 1f);
            panel.style.overflow = Overflow.Hidden;

            // Header foldout
            var header = new Foldout { text = collectionProperty.displayName, value = true };
            header.style.backgroundColor = HeaderBg;
            SetPadding(header.style, 4f, 8f);
            panel.Add(header);

            var content = new VisualElement();
            content.style.flexDirection = FlexDirection.Column;
            content.style.paddingBottom = 6;
            header.Add(content);

            // ── Size row ─────────────────────────────────────────────────────
            var sizeRow = BuildSizeRow(collectionProperty, content);
            content.Add(sizeRow);

            BuildElementRows(collectionProperty, content);
            return panel;
        }

        /// Extracted helper so both initial build and rebuild can reuse it.
        private VisualElement BuildSizeRow(SerializedProperty collectionProperty, VisualElement content)
        {
            var sizeRow = new VisualElement();
            sizeRow.style.flexDirection  = FlexDirection.Row;
            sizeRow.style.alignItems     = Align.Center;
            sizeRow.style.marginTop      = 6;
            sizeRow.style.marginBottom   = 4;
            sizeRow.style.paddingLeft    = 8;
            sizeRow.style.paddingRight   = 8;

            var sizeLabel = new Label("Size");
            sizeLabel.style.color    = LabelMuted;
            sizeLabel.style.fontSize = 11;
            sizeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            sizeLabel.style.width    = 36;
            sizeRow.Add(sizeLabel);

            var sizeField = new IntegerField { value = collectionProperty.arraySize };
            sizeField.style.width           = 52;
            sizeField.style.marginLeft      = 4;
            sizeField.style.backgroundColor = PanelBorder;
            SetRadius(sizeField.style, 3f);
            sizeField.RegisterValueChangedCallback(evt =>
            {
                int newSize = Mathf.Max(0, evt.newValue);
                if (newSize == collectionProperty.arraySize) return;
                collectionProperty.arraySize = newSize;
                collectionProperty.serializedObject.ApplyModifiedProperties();
                RebuildContent(collectionProperty, content);
            });
            sizeRow.Add(sizeField);

            var addBtn = StyledButton("＋ Add", AccentGreenDark, AccentGreen);
            addBtn.style.marginLeft = 6;
            addBtn.clicked += () =>
            {
                collectionProperty.arraySize++;
                collectionProperty.serializedObject.ApplyModifiedProperties();
                RebuildContent(collectionProperty, content);
                sizeField.SetValueWithoutNotify(collectionProperty.arraySize);
            };
            sizeRow.Add(addBtn);

            return sizeRow;
        }

        /// Clears and rebuilds the entire content area (size row + element rows).
        private void RebuildContent(SerializedProperty collectionProperty, VisualElement content)
        {
            content.Clear();
            content.Add(BuildSizeRow(collectionProperty, content));
            BuildElementRows(collectionProperty, content);
        }

        private void BuildElementRows(SerializedProperty collectionProperty, VisualElement parent)
        {
            int count = collectionProperty.arraySize;
            for (int i = 0; i < count; i++)
            {
                var elementProp = collectionProperty.GetArrayElementAtIndex(i);

                // Alternating row background
                var row = new VisualElement();
                row.style.flexDirection  = FlexDirection.Row;
                row.style.alignItems     = Align.Center;
                row.style.marginTop      = 1;
                row.style.paddingTop     = 3;
                row.style.paddingBottom  = 3;
                row.style.paddingLeft    = 8;
                row.style.paddingRight   = 8;
                row.style.backgroundColor = (i % 2 == 0) ? RowBgEven : RowBgOdd;

                // Index badge
                var label = new Label($"[{i}]");
                label.style.color    = IndexLabel;
                label.style.fontSize = 11;
                label.style.unityFontStyleAndWeight = FontStyle.Bold;
                label.style.width    = 32;
                label.style.flexShrink = 0;
                row.Add(label);

                // Property field
                var elementField = new PropertyField(elementProp);
                elementField.style.flexGrow = 1;
                row.Add(elementField);

                // Type selector button (accent blue)
                var typeBtn = StyledButton(GetButtonLabel(elementProp), AccentBlueDark, AccentBlue, 130f);
                typeBtn.style.marginLeft = 6;
                typeBtn.clicked += () => SelectorButtonClicked_ForElement(typeBtn, elementProp, collectionProperty);
                row.Add(typeBtn);

                // Separator + remove button
                var sep = new VisualElement();
                sep.style.width           = 1;
                sep.style.height          = Length.Percent(70);
                sep.style.backgroundColor = PanelBorder;
                sep.style.marginLeft      = 6;
                sep.style.marginRight     = 2;
                sep.style.alignSelf       = Align.Center;
                row.Add(sep);

                var removeBtn = StyledButton("✕", AccentRedDark, AccentRed, 28f);
                removeBtn.style.marginLeft = 2;
                int capturedIndex = i;
                removeBtn.clicked += () =>
                {
                    collectionProperty.DeleteArrayElementAtIndex(capturedIndex);
                    collectionProperty.serializedObject.ApplyModifiedProperties();
                    RebuildContent(collectionProperty, parent);
                };
                row.Add(removeBtn);

                parent.Add(row);
            }

            // Empty state hint
            if (count == 0)
            {
                var empty = new Label("— empty —");
                empty.style.color     = LabelMuted;
                empty.style.fontSize  = 11;
                empty.style.unityFontStyleAndWeight = FontStyle.Italic;
                empty.style.alignSelf = Align.Center;
                empty.style.marginTop = 6;
                empty.style.marginBottom = 6;
                parent.Add(empty);
            }
        }

        // ---------- Selectors ----------
        private void SelectorButtonClicked_ForProperty(Button typeBtn, SerializedProperty property)
        {
            var targetType = GetTargetType(fieldInfo);
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
            var elementDeclaredType = GetElementTargetType(fieldInfo);
            if (elementDeclaredType == null)
            {
                var full = elementProperty.managedReferenceFullTypename;
                if (!string.IsNullOrEmpty(full))
                {
                    var parts    = full.Split(' ');
                    var typeName = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : parts[0];
                    elementDeclaredType = AppDomain.CurrentDomain.GetAssemblies()
                        .Select(a => a.GetType(typeName)).FirstOrDefault(t => t != null);
                }
            }

            if (elementDeclaredType == null)
            {
                Debug.LogWarning("[TypeSelector] Could not resolve element declared type; falling back to object.");
                elementDeclaredType = typeof(object);
            }
            Debug.Log(elementDeclaredType.Name);

            ShowTypeDropdownForProperty(typeBtn.worldBound, elementDeclaredType, chosenType =>
            {
                if (chosenType == null) elementProperty.managedReferenceValue = null;
                else elementProperty.managedReferenceValue = Activator.CreateInstance(chosenType);

                collectionProperty.serializedObject.ApplyModifiedProperties();
                if (typeBtn.parent != null) typeBtn.parent.Bind(collectionProperty.serializedObject);
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
            var types           = new List<Type>();

            if (targetType.IsGenericType && !targetType.IsGenericTypeDefinition)
            {
                var genericDef  = targetType.GetGenericTypeDefinition();
                var targetArgs  = targetType.GetGenericArguments();
                string cacheKey = (genericDef.FullName ?? genericDef.Name) + ":" +
                                  string.Join(",", targetArgs.Select(a => a.FullName ?? a.Name));

                if (!GenericCandidatesCache.TryGetValue(cacheKey, out var cached))
                {
                    var list = new List<Type>();
                    foreach (var candidate in TypeCache.GetTypesDerivedFrom(genericDef))
                    {
                        try
                        {
                            Type constructed = candidate;
                            if (candidate.IsGenericTypeDefinition)
                                constructed = candidate.MakeGenericType(targetArgs);

                            if (constructed.IsAbstract || constructed.IsGenericTypeDefinition ||
                                unityObjectType.IsAssignableFrom(constructed)) continue;

                            if (!targetType.IsAssignableFrom(constructed)) continue;

                            list.Add(constructed);
                        }
                        catch { /* ignore construction failures */ }
                    }
                    cached = list.ToArray();
                    GenericCandidatesCache[cacheKey] = cached;
                }
                types.AddRange(cached);

                if (!targetType.IsAbstract && !targetType.IsGenericTypeDefinition &&
                    !unityObjectType.IsAssignableFrom(targetType))
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
                    if (customAttribute is SelectorNameAttribute dropdownPathAttribute)
                        path = dropdownPathAttribute.Name;
                }
                pairs[i] = string.IsNullOrEmpty(path)
                    ? new(SelectorName.GetDisplayName(type), type)
                    : new(path, type);
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
                if (genArgs.Length == 1) return genArgs[0];

                var iface = ft.GetInterfaces().FirstOrDefault(i =>
                    i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
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
                if (ft.GetInterfaces().Any(i => i.IsGenericType &&
                    i.GetGenericTypeDefinition() == typeof(IEnumerable<>))) return true;
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