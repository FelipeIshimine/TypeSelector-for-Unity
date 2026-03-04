// Editor/InterfaceReferenceDrawer.cs
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(InterfaceReference<>), true)]
public class InterfaceReferenceDrawer : PropertyDrawer
{
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        var interfaceType = fieldInfo.FieldType.GetGenericArguments()[0];

        var useObjectProp   = property.FindPropertyRelative(nameof(InterfaceReference<object>.useObject));
        var objectValueProp = property.FindPropertyRelative(nameof(InterfaceReference<object>.objectValue));
        var managedProp     = property.FindPropertyRelative(nameof(InterfaceReference<object>.managedValue));

        var root = new VisualElement { style = { flexDirection = FlexDirection.Column } };

        // ── Header row: always visible ────────────────────────────────
        var header = new VisualElement
        {
            style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginBottom = 2 }
        };

        var nameLabel = new Label(property.displayName)
        {
            style =
            {
                color = new Color(0.7f, 0.7f, 0.7f),
                fontSize = 11,
                marginRight = 4,
                flexShrink = 0
            }
        };

        var inlineContent = new VisualElement { style = { flexGrow = 1, flexShrink = 1 } };

        var toggleIcon = new Image { style = { width = 12, height = 12 } };

        var toggle = new Button
        {
            style =
            {
                width = 22, height = 18,
                marginLeft = 4,
                paddingLeft = 0, paddingRight = 0,
                paddingTop = 0, paddingBottom = 0,
                alignItems = Align.Center,
                justifyContent = Justify.Center,
                backgroundColor = new Color(0.25f, 0.25f, 0.25f),
                borderTopLeftRadius = 3, borderTopRightRadius = 3,
                borderBottomLeftRadius = 3, borderBottomRightRadius = 3,
                borderTopWidth = 1, borderBottomWidth = 1,
                borderLeftWidth = 1, borderRightWidth = 1,
                borderTopColor = new Color(0.5f, 0.5f, 0.5f),
                borderBottomColor = new Color(0.5f, 0.5f, 0.5f),
                borderLeftColor = new Color(0.5f, 0.5f, 0.5f),
                borderRightColor = new Color(0.5f, 0.5f, 0.5f),
            }
        };
        toggle.Add(toggleIcon);

        var normalBg   = new Color(0.25f, 0.25f, 0.25f);
        var hoverBg    = new Color(0.35f, 0.35f, 0.35f);
        var activeBg   = new Color(0.18f, 0.18f, 0.18f);
        var normalTint = new Color(0.6f, 0.6f, 0.6f);
        var hoverTint  = Color.white;

        toggleIcon.tintColor = normalTint;

        toggle.RegisterCallback<MouseEnterEvent>(_ =>
        {
            toggle.style.backgroundColor = hoverBg;
            toggle.style.borderTopColor = toggle.style.borderBottomColor =
            toggle.style.borderLeftColor = toggle.style.borderRightColor = new Color(0.7f, 0.7f, 0.7f);
            toggleIcon.tintColor = hoverTint;
        });
        toggle.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            toggle.style.backgroundColor = normalBg;
            toggle.style.borderTopColor = toggle.style.borderBottomColor =
            toggle.style.borderLeftColor = toggle.style.borderRightColor = new Color(0.5f, 0.5f, 0.5f);
            toggleIcon.tintColor = normalTint;
        });
        toggle.RegisterCallback<MouseDownEvent>(_ =>
        {
            toggle.style.backgroundColor = activeBg;
            toggleIcon.tintColor = normalTint;
        });
        toggle.RegisterCallback<MouseUpEvent>(_ =>
        {
            toggle.style.backgroundColor = hoverBg;
            toggleIcon.tintColor = hoverTint;
        });

        header.Add(nameLabel);
        header.Add(inlineContent);
        header.Add(toggle);
        root.Add(header);

        // ── Expanded content: only used in managed mode ───────────────
        var expandedContent = new VisualElement { style = { flexGrow = 1 } };
        root.Add(expandedContent);

        void Rebuild()
        {
            inlineContent.Clear();
            expandedContent.Clear();

            toggleIcon.image = EditorGUIUtility.IconContent(
                useObjectProp.boolValue ? "d_Linked" : "d_Unlinked"
            ).image;

            if (useObjectProp.boolValue)
            {
                // Object field sits inline next to the label
                var objField = new ObjectField
                {
                    objectType = typeof(UnityEngine.Object),
                    allowSceneObjects = true,
                    value = objectValueProp.objectReferenceValue,
                    style = { flexGrow = 1 }
                };

                objField.RegisterValueChangedCallback(evt =>
                {
                    var resolved = ResolveObject(evt.newValue, interfaceType);

                    if (evt.newValue != null && resolved == null)
                    {
                        Debug.LogWarning($"[InterfaceReference] {evt.newValue.GetType().Name} does not implement {interfaceType.Name}");
                        objField.SetValueWithoutNotify(evt.previousValue);
                        return;
                    }

                    objectValueProp.objectReferenceValue = resolved;
                    objectValueProp.serializedObject.ApplyModifiedProperties();
                    objField.SetValueWithoutNotify(resolved);
                });

                if (objectValueProp.objectReferenceValue != null &&
                    !interfaceType.IsAssignableFrom(objectValueProp.objectReferenceValue.GetType()))
                    objField.style.backgroundColor = new Color(0.6f, 0.1f, 0.1f, 0.3f);

                inlineContent.Add(objField);
            }
            else
            {
                // Managed field expands below the header
                var managedField = new PropertyField(managedProp, "") { style = { flexGrow = 1 } };

                managedField.RegisterCallback<GeometryChangedEvent>(_ =>
                {
                    var inner = managedField.Q<Label>();
                    if (inner != null) inner.style.display = DisplayStyle.None;
                });

                expandedContent.Add(managedField);
                expandedContent.Bind(property.serializedObject);
            }
        }

        toggle.clicked += () =>
        {
            useObjectProp.boolValue = !useObjectProp.boolValue;
            useObjectProp.serializedObject.ApplyModifiedProperties();
            Rebuild();
        };

        Rebuild();
        return root;
    }

    private static UnityEngine.Object ResolveObject(UnityEngine.Object obj, System.Type interfaceType)
    {
        if (obj == null) return null;
        if (interfaceType.IsAssignableFrom(obj.GetType())) return obj;
        if (obj is GameObject go)
        {
            var component = go.GetComponent(interfaceType);
            if (component != null) return component;
        }
        return null;
    }
}