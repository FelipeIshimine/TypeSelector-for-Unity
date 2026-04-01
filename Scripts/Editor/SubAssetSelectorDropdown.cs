using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

/// <summary>
/// Searchable dropdown that lists sub-assets of a given type inside a container asset.
///
/// Supports two display modes:
///   Flat          — all items in a single list; shows a subtle type label on the right of each row.
///   GroupedByType — items grouped under their concrete type name; type labels are omitted.
///                   While the user is searching, groups collapse into a flat list.
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
        SubAssetSelectorAttribute.ListMode listMode,
        Action<ScriptableObject> onSelected,
        Type defaultType = null)
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
        window._listMode           = listMode;
        window._onSelected         = onSelected;
        window.LoadSubAssets();
        
        // Ensure default exists BEFORE showing
        if (!IsScenePath(containerAssetPath))
        {
	        SubAssetDefaults.GetOrCreateDefault(containerAssetPath, fieldType, defaultType);
	        window.LoadSubAssets(); // reload after potential creation
        }
        
        window.ShowAsDropDown(screenRect, new Vector2(Mathf.Max(screenRect.width, 260), 300));
    }

    // ── Page enum ─────────────────────────────────────────────────────────────

    private enum Page { SelectAsset, SelectType }

    // ── Group header item ─────────────────────────────────────────────────────

    private readonly struct GroupHeader
    {
        public readonly string Title;
        public GroupHeader(string title) => Title = title;
    }

    // ── State ─────────────────────────────────────────────────────────────────

    private string                             _containerAssetPath;
    private Type                               _fieldType;
    private SubAssetSelectorAttribute.ListMode _listMode;
    private Action<ScriptableObject>           _onSelected;
    private List<ScriptableObject>             _allSubAssets  = new();
    private List<Type>                         _concreteTypes = new();

    private Page   _currentPage          = Page.SelectAsset;
    private string _searchText           = "";
    private string _pendingCreationName  = "";
    private string _savedAssetSearchText = "";

    // _displayItems holds: CreateSentinel, GroupHeader, ScriptableObject (existing), or Type (concrete type).
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

    static readonly Color BackgroundColor  = new(0.18f, 0.18f, 0.18f);
    static readonly Color HeaderColor      = new(0.13f, 0.13f, 0.13f);
    static readonly Color BorderColor      = new(0.09f, 0.09f, 0.09f);
    static readonly Color RowAltColor      = new(0.00f, 0.00f, 0.00f, 0.06f);
    static readonly Color HoverColor       = new(0.28f, 0.28f, 0.28f);
    static readonly Color TextColor        = new(0.85f, 0.85f, 0.85f);
    static readonly Color SubtextColor     = new(0.50f, 0.50f, 0.50f);
    static readonly Color TypeLabelColor   = new(0.40f, 0.40f, 0.40f);
    static Color AccentColor => HoverColor;

    // ── Data loading ──────────────────────────────────────────────────────────

    private void LoadSubAssets()
    {
        if (IsScenePath(_containerAssetPath))
        {
            _allSubAssets = new List<ScriptableObject>();
            return;
        }

        _allSubAssets = AssetDatabase.LoadAllAssetsAtPath(_containerAssetPath)
                                     .Where(asset => asset != null
                                         && !AssetDatabase.IsMainAsset(asset)
                                         && _fieldType.IsAssignableFrom(asset.GetType()))
                                     .Cast<ScriptableObject>()
                                     .OrderBy(asset => asset.name)
                                     .ToList();
    }

    private static bool IsScenePath(string path) =>
        path.EndsWith(".unity", StringComparison.OrdinalIgnoreCase);

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
        // If the ListView somehow receives focus (e.g. user clicks empty space), send it back
        // to the search field. We keep the ListView focusable so selection highlights render.
        _listView.RegisterCallback<FocusInEvent>(_ => FocusSearchField());
        _listView.selectionChanged += _ => _listView.RefreshItems();
        return _listView;
    }

    private VisualElement MakeRow()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems    = Align.Center;
        row.style.paddingLeft   = 10f;
        row.style.paddingRight  = 8f;
        // Rows must not steal keyboard focus — all input stays in the search field.
        row.focusable = false;

        // ── Normal item content (assets / type picker rows) ───────────────────

        var itemContent = new VisualElement { name = "item-content" };
        itemContent.style.flexDirection = FlexDirection.Row;
        itemContent.style.alignItems    = Align.Center;
        itemContent.style.flexGrow      = 1;

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

        // Subtle type annotation, shown in Flat mode only.
        var typeLabel = new Label { name = "type" };
        typeLabel.style.fontSize       = 10f;
        typeLabel.style.color          = TypeLabelColor;
        typeLabel.style.unityTextAlign = TextAnchor.MiddleRight;
        typeLabel.style.marginLeft     = 6f;
        typeLabel.style.maxWidth       = 90f;
        typeLabel.style.overflow       = Overflow.Hidden;
        typeLabel.style.textOverflow   = TextOverflow.Ellipsis;
        typeLabel.style.flexShrink     = 0f;
        typeLabel.style.display        = DisplayStyle.None;

        itemContent.Add(icon);
        itemContent.Add(nameLabel);
        itemContent.Add(typeLabel);

        // ── Group header content (GroupedByType mode) ─────────────────────────

        var headerContent = new VisualElement { name = "header-content" };
        headerContent.style.flexGrow      = 1;
        headerContent.style.flexDirection = FlexDirection.Row;
        headerContent.style.alignItems    = Align.Center;
        headerContent.style.display       = DisplayStyle.None;

        var groupLabel = new Label { name = "group-label" };
        groupLabel.style.fontSize                = 10f;
        groupLabel.style.color                   = SubtextColor;
        groupLabel.style.unityTextAlign          = TextAnchor.MiddleLeft;
        groupLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        groupLabel.style.flexGrow                = 1;

        headerContent.Add(groupLabel);

        row.Add(itemContent);
        row.Add(headerContent);

        // ── Interaction callbacks ─────────────────────────────────────────────

        row.RegisterCallback<PointerEnterEvent>(_ =>
        {
            if (row.userData is int rowIndex && IsSelectableIndex(rowIndex))
                row.style.backgroundColor = HoverColor;
        });
        row.RegisterCallback<PointerLeaveEvent>(_ =>
        {
            if (row.userData is int rowIndex)
                row.style.backgroundColor = RowBackgroundColor(rowIndex);
        });
        row.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0) return;
            if (row.userData is int rowIndex && IsSelectableIndex(rowIndex))
                OnItemClicked(rowIndex);
        });

        return row;
    }

    private void BindRow(VisualElement row, int index)
    {
        row.userData = index;

        var item          = _displayItems[index];
        var itemContent   = row.Q("item-content");
        var headerContent = row.Q("header-content");
        bool isSelected   = _listView.selectedIndex == index;

        if (item is GroupHeader groupHeader)
        {
            itemContent.style.display   = DisplayStyle.None;
            headerContent.style.display = DisplayStyle.Flex;
            row.style.backgroundColor   = HeaderColor;
            row.style.paddingLeft       = 8f;
            row.style.borderTopWidth    = 1f;
            row.style.borderTopColor    = BorderColor;
            row.Q<Label>("group-label").text = groupHeader.Title;
            return;
        }

        itemContent.style.display   = DisplayStyle.Flex;
        headerContent.style.display = DisplayStyle.None;
        row.style.paddingLeft       = 10f;
        row.style.borderTopWidth    = 0f;

        var icon      = row.Q<Image>("icon");
        var nameLabel = row.Q<Label>("name");
        var typeLabel = row.Q<Label>("type");

        if (item == CreateSentinel)
        {
            row.style.backgroundColor = isSelected ? AccentColor : NaturalRowColor(index);
            nameLabel.text            = string.IsNullOrWhiteSpace(_searchText)
                ? "＋  New…  (type a name above)"
                : $"＋  Create \"{_searchText.Trim()}\"";
            nameLabel.style.color     = isSelected ? Color.white : AccentColor;
            icon.image                = EditorGUIUtility.FindTexture("d_CreateAddNew");
            icon.style.display        = icon.image != null ? DisplayStyle.Flex : DisplayStyle.None;
            typeLabel.style.display   = DisplayStyle.None;
            return;
        }

        if (item is ScriptableObject asset)
        {
            row.style.backgroundColor = isSelected ? AccentColor : NaturalRowColor(index);
            bool isDefault = asset.name == SubAssetDefaults.DefaultName;

            nameLabel.text = isDefault ? $"{asset.name}" : asset.name;

            if (isDefault)
            {
	            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            }
            
            nameLabel.style.color     = isSelected ? Color.white : TextColor;

            var assetIcon          = EditorGUIUtility.ObjectContent(asset, asset.GetType()).image as Texture2D;
            icon.image             = assetIcon;
            icon.style.display     = assetIcon != null ? DisplayStyle.Flex : DisplayStyle.None;

            bool showTypeLabel       = _listMode == SubAssetSelectorAttribute.ListMode.Flat;
            typeLabel.style.display  = showTypeLabel ? DisplayStyle.Flex : DisplayStyle.None;
            typeLabel.text           = ObjectNames.NicifyVariableName(asset.GetType().Name);
            typeLabel.style.color    = isSelected ? SubtextColor : TypeLabelColor;
            return;
        }

        if (item is Type concreteType)
        {
            row.style.backgroundColor = isSelected ? AccentColor : NaturalRowColor(index);
            nameLabel.text            = GetTypeDisplayPath(concreteType).Replace("/", " › ");
            nameLabel.style.color     = isSelected ? Color.white : TextColor;

            var typeIcon           = EditorGUIUtility.ObjectContent(null, concreteType).image as Texture2D;
            icon.image             = typeIcon;
            icon.style.display     = typeIcon != null ? DisplayStyle.Flex : DisplayStyle.None;
            typeLabel.style.display = DisplayStyle.None;
        }
    }

    private Color NaturalRowColor(int index) =>
        index % 2 == 0 ? new Color(0f, 0f, 0f, 0f) : RowAltColor;

    // Returns the correct resting background for any row index, including recycled header rows.
    private Color RowBackgroundColor(int index)
    {
        if (index >= 0 && index < _displayItems.Count && _displayItems[index] is GroupHeader)
            return HeaderColor;
        return NaturalRowColor(index);
    }

    // ── Index helpers ─────────────────────────────────────────────────────────

    private bool IsSelectableIndex(int index) =>
        index >= 0
        && index < _displayItems.Count
        && _displayItems[index] is not GroupHeader;

    /// <summary>
    /// Walks the display list starting just past <paramref name="fromExclusive"/> in <paramref name="direction"/>
    /// (+1 or -1) and returns the first index that is selectable (not a GroupHeader). Returns -1 if none found.
    /// </summary>
    private int FindSelectableIndex(int fromExclusive, int direction)
    {
        var index = fromExclusive + direction;
        while (index >= 0 && index < _displayItems.Count)
        {
            if (IsSelectableIndex(index)) return index;
            index += direction;
        }
        return -1;
    }

    // ── Data refresh ──────────────────────────────────────────────────────────

    private void RefreshDisplayList()
    {
        _displayItems.Clear();

        var query = _searchText.Trim().ToLowerInvariant();

        if (_currentPage == Page.SelectAsset)
        {
            // Use groups only when in GroupedByType mode AND the user isn't actively searching.
            // While searching, a flat result set is easier to scan than scattered groups.
            bool useGroups = _listMode == SubAssetSelectorAttribute.ListMode.GroupedByType
                             && string.IsNullOrEmpty(query);

            if (useGroups)
            {
                var groups = _allSubAssets
                    .GroupBy(asset => asset.GetType())
                    .OrderBy(g => ObjectNames.NicifyVariableName(g.Key.Name));

                foreach (var group in groups)
                {
                    _displayItems.Add(new GroupHeader(ObjectNames.NicifyVariableName(group.Key.Name)));
                    foreach (var asset in group)
                        _displayItems.Add(asset);
                }
            }
            else
            {
                foreach (var asset in _allSubAssets)
                {
                    if (string.IsNullOrEmpty(query) || asset.name.ToLowerInvariant().Contains(query))
                        _displayItems.Add(asset);
                }
            }

            _displayItems.Add(CreateSentinel);
        }
        else
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

        if (!string.IsNullOrEmpty(_searchText) && _displayItems.Count > 0)
        {
            var firstSelectable = FindSelectableIndex(-1, 1);
            if (firstSelectable >= 0)
            {
                _listView.selectedIndex = firstSelectable;
                _listView.ScrollToItem(firstSelectable);
            }
        }
        else
        {
            _listView.ClearSelection();
        }
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
        FocusSearchField();
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
        FocusSearchField();
    }

    // ── Interaction ───────────────────────────────────────────────────────────

    private void OnItemClicked(int index)
    {
        var item = _displayItems[index];

        if (item is GroupHeader) return;

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
        if (IsScenePath(_containerAssetPath))
        {
            Debug.LogError("[SubAssetSelector] Cannot create sub-assets inside a scene. " +
                "Use a ScriptableObject asset as the container instead.");
            return;
        }

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
        // Any printable character typed while the search field doesn't have focus
        // redirects input there. This covers the case where the user navigated
        // the list with Up/Down and the ListView somehow took focus.
        if (evt.character != '\0' && !char.IsControl(evt.character) && !SearchFieldContainsFocus())
        {
            _searchField.value += evt.character.ToString();
            FocusSearchField();
            evt.StopPropagation();
            return;
        }

        switch (evt.keyCode)
        {
            case KeyCode.Delete:
            {
                if (_currentPage               == Page.SelectAsset
                    && _listView.selectedIndex >= 0
                    && _listView.selectedIndex < _displayItems.Count
                    && _displayItems[_listView.selectedIndex] is ScriptableObject toDelete)
                {
                    TryDeleteSelected(toDelete);
                }
                evt.StopPropagation();
                return;
            }

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
                var nextIndex = _listView.selectedIndex >= 0
                    ? FindSelectableIndex(_listView.selectedIndex, -1)
                    : FindSelectableIndex(_displayItems.Count, -1);
                if (nextIndex >= 0)
                {
                    _listView.selectedIndex = nextIndex;
                    _listView.ScrollToItem(nextIndex);
                }
                evt.StopPropagation();
                return;
            }

            case KeyCode.DownArrow:
            {
                var nextIndex = _listView.selectedIndex >= 0
                    ? FindSelectableIndex(_listView.selectedIndex, 1)
                    : FindSelectableIndex(-1, 1);
                if (nextIndex >= 0)
                {
                    _listView.selectedIndex = nextIndex;
                    _listView.ScrollToItem(nextIndex);
                }
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
    /// Defers focus to the search field by one frame so it fires after the current event
    /// pipeline (e.g. a PointerDown that triggered navigation) has fully settled.
    /// Direct Focus() calls can be overwritten by the event system restoring focus to the
    /// element that was just clicked, which may no longer be in the hierarchy.
    /// </summary>
    private void FocusSearchField() =>
        rootVisualElement.schedule.Execute(() => _searchField.Focus()).StartingIn(16);

    /// <summary>
    /// Returns true if the search field or any of its descendants currently has keyboard focus.
    /// TextField's actual focus lives on its inner TextElement, so checking the TextField
    /// itself is not sufficient — we walk up from the focused element instead.
    /// </summary>
    private bool SearchFieldContainsFocus()
    {
        var focusedElement = rootVisualElement?.panel?.focusController?.focusedElement as VisualElement;
        while (focusedElement != null)
        {
            if (focusedElement == _searchField) return true;
            focusedElement = focusedElement.parent;
        }
        return false;
    }

    // ── Deletion ──────────────────────────────────────────────────────────────

    private void TryDeleteSelected(ScriptableObject target)
    {
	    if (target.name == SubAssetDefaults.DefaultName)
	    {
		    EditorUtility.DisplayDialog(
			    "Cannot Delete",
			    "The Default sub-asset cannot be deleted.",
			    "OK");
		    return;
	    }
	    
        int refCount = CountReferences(target, out List<string> paths);
        if (refCount > 0)
        {
            var builder = new StringBuilder(
                $"\"{target.name}\" still has {refCount} reference{(refCount == 1 ? "" : "s")} in the container:");

            foreach (string path in paths)
                builder.AppendLine(path);

            builder.AppendLine("\n\nDelete anyway?");

            bool confirmed = EditorUtility.DisplayDialog(
                "Delete Sub-Asset",
                builder.ToString(),
                "Delete",
                "Cancel");

            if (!confirmed) return;
        }

        Undo.DestroyObjectImmediate(target);
        AssetDatabase.SaveAssets();

        LoadSubAssets();
        RefreshDisplayList();
    }

    private int CountReferences(ScriptableObject target, out List<string> list)
    {
        list = new List<string>();
        var objects = _containerAssetPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase)
            ? CollectSceneObjects()
            : CollectAssetObjects();

        int count = 0;
        foreach (var obj in objects)
        {
            if (obj == null || obj == target) continue;

            var serializedObject = new SerializedObject(obj);
            var property         = serializedObject.GetIterator();
            while (property.NextVisible(true))
            {
                if (property.propertyType            == SerializedPropertyType.ObjectReference
                    && property.objectReferenceValue == target)
                {
                    count++;
                    list.Add(property.propertyPath);
                }
            }
        }
        return count;
    }

    private IEnumerable<Object> CollectAssetObjects() =>
        AssetDatabase.LoadAllAssetsAtPath(_containerAssetPath).Where(o => o != null);

    private IEnumerable<Object> CollectSceneObjects()
    {
        for (int i = 0; i < SceneManager.loadedSceneCount; i++)
        {
            var scene = SceneManager.GetSceneAt(i);
            if (scene.path != _containerAssetPath) continue;

            return scene.GetRootGameObjects()
                        .SelectMany(go => go.GetComponentsInChildren<Component>(includeInactive: true))
                        .Cast<Object>();
        }

        return Array.Empty<Object>();
    }

    /// <summary>
    /// Returns the display path for a type in the type-picker list.
    /// Respects the project's [SelectorName] attribute when present;
    /// falls back to a nicified version of the C# type name.
    /// </summary>
    private static string GetTypeDisplayPath(Type type)
    {
        foreach (var attr in type.GetCustomAttributes(inherit: false))
        {
            if (attr.GetType().Name != "SelectorNameAttribute") continue;

            // Read via reflection to avoid a hard assembly dependency on the attribute's assembly.
            var pathProperty = attr.GetType().GetProperty("Path")
                            ?? attr.GetType().GetProperty("Name");
            if (pathProperty?.GetValue(attr) is string path && !string.IsNullOrEmpty(path))
                return path;
        }

        return ObjectNames.NicifyVariableName(type.Name);
    }
}