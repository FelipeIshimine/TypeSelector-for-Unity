using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Searchable dropdown that lists sub-assets of a given type inside a container asset.
///
/// Page flow:
///   SelectAsset → type a name → ＋ Create →
///     if concrete type: create immediately
///     if abstract type: navigate to SelectType page (in-place, no second popup)
///
/// Navigation mirrors AdvancedDropdownBuilder: Backspace (empty search) or Escape goes back.
/// </summary>
internal sealed class SubAssetSelectorDropdown : EditorWindow
{
    // ── Factory ───────────────────────────────────────────────────────────────

    internal static void Open(
        Rect worldBound,
        string containerAssetPath,
        Type fieldType,
        Action<ScriptableObject> onSelected)
    {
        // worldBound is in EditorWindow-local space; convert to screen space.
        var screenRect = worldBound;
        if (focusedWindow != null)
        {
            screenRect.x += focusedWindow.position.x;
            screenRect.y += focusedWindow.position.y;
        }

        var window = CreateInstance<SubAssetSelectorDropdown>();
        window.hideFlags           = HideFlags.DontSave;
        window._containerAssetPath = containerAssetPath;
        window._fieldType          = fieldType;
        window._onSelected         = onSelected;
        window.LoadSubAssets();
        window.ShowAsDropDown(screenRect, new Vector2(Mathf.Max(screenRect.width, 260), 300));
    }

    // ── Page enum ─────────────────────────────────────────────────────────────

    private enum Page { SelectAsset, SelectType }

    // ── State ─────────────────────────────────────────────────────────────────

    private string                   _containerAssetPath;
    private Type                     _fieldType;
    private Action<ScriptableObject> _onSelected;
    private List<ScriptableObject>   _allSubAssets     = new();
    private List<Type>               _concreteTypes    = new();

    private Page   _currentPage          = Page.SelectAsset;
    private string _searchText           = "";
    private string _pendingCreationName  = "";
    private string _savedAssetSearchText = ""; // restored when navigating back

    // _displayItems holds: CreateSentinel, ScriptableObject (existing), or Type (concrete type).
    private readonly List<object> _displayItems = new();

    // Sentinel that represents the "＋ Create" row — identity-compared, never null.
    private static readonly object CreateSentinel = new();

    // ── UI refs ───────────────────────────────────────────────────────────────

    private Label     _titleLabel;
    private Button    _backButton;
    private TextField _searchField;
    private Label     _searchPlaceholder;
    private ListView  _listView;

    // ── Visual theme (matches AdvancedDropdownBuilder's DropdownWindow) ───────

    static readonly Color BackgroundColor = new(0.18f, 0.18f, 0.18f);
    static readonly Color HeaderColor     = new(0.13f, 0.13f, 0.13f);
    static readonly Color BorderColor     = new(0.09f, 0.09f, 0.09f);
    static readonly Color RowAltColor     = new(0.00f, 0.00f, 0.00f, 0.06f);
    static readonly Color HoverColor      = new(0.28f, 0.28f, 0.28f);
    static readonly Color TextColor       = new(0.85f, 0.85f, 0.85f);
    static readonly Color SubtextColor    = new(0.50f, 0.50f, 0.50f);
    static readonly Color AccentColor     = new(0.25f, 0.49f, 0.96f);
    static readonly Color CreateRowColor  = new(0.20f, 0.27f, 0.40f, 1.00f);

    // ── Data loading ──────────────────────────────────────────────────────────

    private void LoadSubAssets()
    {
        _allSubAssets = AssetDatabase.LoadAllAssetsAtPath(_containerAssetPath)
            .Where(asset => asset != null
                         && !AssetDatabase.IsMainAsset(asset)
                         && _fieldType.IsAssignableFrom(asset.GetType()))
            .Cast<ScriptableObject>()
            .OrderBy(asset => asset.name)
            .ToList();
    }

    private void EnsureConcreteTypesLoaded()
    {
        if (_concreteTypes.Count > 0) return;

        _concreteTypes = TypeCache.GetTypesDerivedFrom(_fieldType)
            .Where(t => !t.IsAbstract && !t.IsGenericTypeDefinition)
            .OrderBy(GetTypeDisplayPath)
            .ToList();
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void CreateGUI()
    {
        var root = rootVisualElement;
        root.style.flexDirection   = FlexDirection.Column;
        root.style.flexGrow        = 1;
        root.style.backgroundColor = BackgroundColor;

        root.Add(BuildHeader());
        root.Add(BuildSearchBar());
        root.Add(BuildList());

        RefreshDisplayList();

        root.schedule.Execute(() => _searchField.Focus()).StartingIn(50);
        root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
    }

    // ── Header ────────────────────────────────────────────────────────────────

    private VisualElement BuildHeader()
    {
        var header = new VisualElement();
        header.style.backgroundColor   = HeaderColor;
        header.style.flexDirection     = FlexDirection.Row;
        header.style.alignItems        = Align.Center;
        header.style.paddingTop        = 6f;
        header.style.paddingBottom     = 6f;
        header.style.paddingLeft       = 6f;
        header.style.paddingRight      = 10f;
        header.style.borderBottomWidth = 1f;
        header.style.borderBottomColor = BorderColor;

        _backButton = new Button(NavigateBackToAssetPage) { text = "‹" };
        _backButton.style.width          = 22f;
        _backButton.style.height         = 20f;
        _backButton.style.fontSize       = 14f;
        _backButton.style.paddingLeft    = 0f;
        _backButton.style.paddingRight   = 0f;
        _backButton.style.paddingTop     = 0f;
        _backButton.style.paddingBottom  = 0f;
        _backButton.style.marginRight    = 4f;
        _backButton.style.color          = TextColor;
        _backButton.style.unityTextAlign = TextAnchor.MiddleCenter;
        _backButton.style.display        = DisplayStyle.None;

        _titleLabel = new Label(BuildAssetPageTitle());
        _titleLabel.style.flexGrow                = 1;
        _titleLabel.style.fontSize                = 11f;
        _titleLabel.style.color                   = TextColor;
        _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _titleLabel.style.unityTextAlign          = TextAnchor.MiddleLeft;

        header.Add(_backButton);
        header.Add(_titleLabel);
        return header;
    }

    // ── Search bar ────────────────────────────────────────────────────────────

    private VisualElement BuildSearchBar()
    {
        var bar = new VisualElement();
        bar.style.paddingTop        = 5f;
        bar.style.paddingBottom     = 5f;
        bar.style.paddingLeft       = 8f;
        bar.style.paddingRight      = 8f;
        bar.style.borderBottomWidth = 1f;
        bar.style.borderBottomColor = BorderColor;
        bar.style.position          = Position.Relative;

        _searchField = new TextField();
        _searchField.style.flexGrow = 1;
        _searchField.style.height   = 22f;

        _searchPlaceholder = new Label("Search or type a new name…");
        _searchPlaceholder.style.position       = Position.Absolute;
        _searchPlaceholder.style.left           = 18f;
        _searchPlaceholder.style.top            = 0f;
        _searchPlaceholder.style.bottom         = 0f;
        _searchPlaceholder.style.fontSize       = 11f;
        _searchPlaceholder.style.color          = SubtextColor;
        _searchPlaceholder.style.unityTextAlign = TextAnchor.MiddleLeft;
        _searchPlaceholder.pickingMode          = PickingMode.Ignore;

        _searchField.RegisterValueChangedCallback(evt =>
        {
            _searchText = evt.newValue;
            _searchPlaceholder.style.display = string.IsNullOrEmpty(evt.newValue)
                ? DisplayStyle.Flex
                : DisplayStyle.None;
            RefreshDisplayList();
        });

        bar.Add(_searchField);
        bar.Add(_searchPlaceholder);
        return bar;
    }

    // ── List ──────────────────────────────────────────────────────────────────

    private VisualElement BuildList()
    {
        _listView = new ListView
        {
            fixedItemHeight = 28,
            selectionType   = SelectionType.Single,
            makeItem        = MakeRow,
            bindItem        = BindRow,
        };
        _listView.style.flexGrow = 1;
        return _listView;
    }

    private VisualElement MakeRow()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems    = Align.Center;
        row.style.paddingLeft   = 10f;
        row.style.paddingRight  = 8f;

        var icon = new Image { name = "icon" };
        icon.style.width       = 16f;
        icon.style.height      = 16f;
        icon.style.marginRight = 6f;
        icon.style.flexShrink  = 0f;

        var nameLabel = new Label { name = "name" };
        nameLabel.style.flexGrow       = 1;
        nameLabel.style.fontSize       = 12f;
        nameLabel.style.color          = TextColor;
        nameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
        nameLabel.style.overflow       = Overflow.Hidden;
        nameLabel.style.textOverflow   = TextOverflow.Ellipsis;

        row.RegisterCallback<PointerEnterEvent>(_ => row.style.backgroundColor = HoverColor);
        row.RegisterCallback<PointerLeaveEvent>(_ =>
        {
            if (row.userData is int rowIndex)
                row.style.backgroundColor = NaturalRowColor(rowIndex);
        });
        row.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0) return;
            if (row.userData is int rowIndex && rowIndex >= 0 && rowIndex < _displayItems.Count)
                OnItemClicked(rowIndex);
        });

        row.Add(icon);
        row.Add(nameLabel);
        return row;
    }

    private void BindRow(VisualElement row, int index)
    {
        row.userData = index;

        var item      = _displayItems[index];
        var icon      = row.Q<Image>("icon");
        var nameLabel = row.Q<Label>("name");

        if (item == CreateSentinel)
        {
            row.style.backgroundColor = CreateRowColor;
            nameLabel.style.color     = AccentColor;
            nameLabel.text = string.IsNullOrWhiteSpace(_searchText)
                ? "＋  New…  (type a name above)"
                : $"＋  Create \"{_searchText.Trim()}\"";

            icon.image         = EditorGUIUtility.FindTexture("d_CreateAddNew");
            icon.style.display = icon.image != null ? DisplayStyle.Flex : DisplayStyle.None;
        }
        else if (item is ScriptableObject asset)
        {
            row.style.backgroundColor = NaturalRowColor(index);
            nameLabel.style.color     = TextColor;
            nameLabel.text            = asset.name;

            var assetIcon = EditorGUIUtility.ObjectContent(asset, asset.GetType()).image as Texture2D;
            icon.image         = assetIcon;
            icon.style.display = assetIcon != null ? DisplayStyle.Flex : DisplayStyle.None;
        }
        else if (item is Type concreteType)
        {
            row.style.backgroundColor = NaturalRowColor(index);
            nameLabel.style.color     = TextColor;
            // Replace "/" separators from SelectorName paths with a readable arrow.
            nameLabel.text = GetTypeDisplayPath(concreteType).Replace("/", " › ");

            var typeIcon = EditorGUIUtility.ObjectContent(null, concreteType).image as Texture2D;
            icon.image         = typeIcon;
            icon.style.display = typeIcon != null ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    private Color NaturalRowColor(int index)
    {
        // The create sentinel keeps its tinted background even after hover-leave.
        if (_displayItems.Count > index && _displayItems[index] == CreateSentinel)
            return CreateRowColor;
        return index % 2 == 0 ? new Color(0f, 0f, 0f, 0f) : RowAltColor;
    }

    // ── Data refresh ──────────────────────────────────────────────────────────

    private void RefreshDisplayList()
    {
        _displayItems.Clear();

        var query = _searchText.Trim().ToLowerInvariant();

        if (_currentPage == Page.SelectAsset)
        {
            _displayItems.Add(CreateSentinel);
            foreach (var asset in _allSubAssets)
            {
                if (string.IsNullOrEmpty(query) || asset.name.ToLowerInvariant().Contains(query))
                    _displayItems.Add(asset);
            }
        }
        else // SelectType
        {
            foreach (var concreteType in _concreteTypes)
            {
                var displayPath = GetTypeDisplayPath(concreteType);
                if (string.IsNullOrEmpty(query) || displayPath.ToLowerInvariant().Contains(query))
                    _displayItems.Add(concreteType);
            }
        }

        _listView.itemsSource = _displayItems;
        _listView.Rebuild();
        _listView.ClearSelection();
    }

    // ── Navigation ────────────────────────────────────────────────────────────

    private void NavigateToTypePage(string pendingName)
    {
        EnsureConcreteTypesLoaded();

        if (_concreteTypes.Count == 0)
        {
            Debug.LogError($"[SubAssetSelector] No concrete types found derived from '{_fieldType.Name}'. " +
                           "At least one non-abstract subclass must exist in the project.");
            return;
        }

        _pendingCreationName  = pendingName;
        _savedAssetSearchText = _searchText;
        _currentPage          = Page.SelectType;

        _titleLabel.text          = $"Type for \"{pendingName}\"";
        _backButton.style.display = DisplayStyle.Flex;
        _searchPlaceholder.text   = "Search types…";

        _searchText = "";
        _searchField.SetValueWithoutNotify("");
        _searchPlaceholder.style.display = DisplayStyle.Flex;

        RefreshDisplayList();
        _searchField.Focus();
    }

    private void NavigateBackToAssetPage()
    {
        _currentPage = Page.SelectAsset;

        _titleLabel.text          = BuildAssetPageTitle();
        _backButton.style.display = DisplayStyle.None;
        _searchPlaceholder.text   = "Search or type a new name…";

        _searchText = _savedAssetSearchText;
        _searchField.SetValueWithoutNotify(_savedAssetSearchText);
        _searchPlaceholder.style.display = string.IsNullOrEmpty(_savedAssetSearchText)
            ? DisplayStyle.Flex
            : DisplayStyle.None;

        RefreshDisplayList();
        _searchField.Focus();
    }

    // ── Interaction ───────────────────────────────────────────────────────────

    private void OnItemClicked(int index)
    {
        var item = _displayItems[index];

        if (item == CreateSentinel)
        {
            HandleCreateNew();
            return;
        }

        if (item is ScriptableObject selectedAsset)
        {
            _onSelected?.Invoke(selectedAsset);
            Close();
            return;
        }

        if (item is Type selectedType)
        {
            CreateAndAssign(_pendingCreationName, selectedType);
        }
    }

    private void HandleCreateNew()
    {
        var newName = _searchText.Trim();
        if (string.IsNullOrEmpty(newName))
        {
            _searchField.Focus();
            return;
        }

        if (_fieldType.IsAbstract)
        {
            NavigateToTypePage(newName);
            return;
        }

        CreateAndAssign(newName, _fieldType);
    }

    private void CreateAndAssign(string name, Type type)
    {
        var newAsset  = ScriptableObject.CreateInstance(type);
        newAsset.name = name;

        Undo.RegisterCreatedObjectUndo(newAsset, $"Create {name}");
        AssetDatabase.AddObjectToAsset(newAsset, _containerAssetPath);
        AssetDatabase.SaveAssets();

        _onSelected?.Invoke(newAsset);
        Close();
    }

    // ── Keyboard ──────────────────────────────────────────────────────────────

    private void OnKeyDown(KeyDownEvent evt)
    {
        switch (evt.keyCode)
        {
            case KeyCode.Escape:
                if (_currentPage == Page.SelectType)
                    NavigateBackToAssetPage();
                else
                    Close();
                evt.StopPropagation();
                return;

            // Empty search + Backspace on the type page → go back.
            // Mirrors the AdvancedDropdownBuilder folder-navigation pattern.
            case KeyCode.Backspace when string.IsNullOrEmpty(_searchText) && _currentPage == Page.SelectType:
                NavigateBackToAssetPage();
                evt.StopPropagation();
                return;

            case KeyCode.UpArrow:
            {
                var nextIndex = Mathf.Max(0, _listView.selectedIndex - 1);
                if (_listView.selectedIndex < 0 && _displayItems.Count > 0) nextIndex = 0;
                _listView.selectedIndex = nextIndex;
                _listView.ScrollToItem(nextIndex);
                evt.StopPropagation();
                return;
            }

            case KeyCode.DownArrow:
            {
                var currentIndex = _listView.selectedIndex;
                var nextIndex    = currentIndex < _displayItems.Count - 1 ? currentIndex + 1 : currentIndex;
                if (currentIndex < 0 && _displayItems.Count > 0) nextIndex = 0;
                _listView.selectedIndex = nextIndex;
                _listView.ScrollToItem(nextIndex);
                evt.StopPropagation();
                return;
            }

            case KeyCode.Return:
            case KeyCode.KeypadEnter:
            {
                var selectedIndex = _listView.selectedIndex;
                if (selectedIndex >= 0 && selectedIndex < _displayItems.Count)
                {
                    OnItemClicked(selectedIndex);
                }
                else if (_currentPage == Page.SelectAsset && !string.IsNullOrWhiteSpace(_searchText))
                {
                    // Enter with text but nothing explicitly selected → create.
                    HandleCreateNew();
                }
                evt.StopPropagation();
                return;
            }
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private string BuildAssetPageTitle() =>
        $"Select {ObjectNames.NicifyVariableName(_fieldType.Name)}";

    /// <summary>
    /// Returns the display path for a type in the type-picker list.
    /// Respects the project's [SelectorName] attribute when present;
    /// falls back to a nicified version of the C# type name.
    /// </summary>
    private static string GetTypeDisplayPath(Type type)
    {
        foreach (var attribute in type.GetCustomAttributes(inherit: false))
        {
            if (attribute.GetType().Name != "SelectorNameAttribute") continue;

            // Read via reflection to avoid a hard assembly dependency on the attribute's assembly.
            var pathProperty = attribute.GetType().GetProperty("Path")
                            ?? attribute.GetType().GetProperty("Name");
            if (pathProperty?.GetValue(attribute) is string path && !string.IsNullOrEmpty(path))
                return path;
        }

        return ObjectNames.NicifyVariableName(type.Name);
    }
}