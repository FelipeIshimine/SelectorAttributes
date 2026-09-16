using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static DropdownTheme;

internal sealed class MillerColumnsRenderer : IDropdownRenderer
{
    public void Show(Rect screenRect, BuiltDropdown data) => MillerColumnsWindow.Open(screenRect, data);
}

internal sealed class MillerColumnsWindow : EditorWindow
{
    private const float ColumnWidth = 240f;

    internal static void Open(Rect screenRect, BuiltDropdown data)
    {
        var win       = CreateInstance<MillerColumnsWindow>();
        win.hideFlags = HideFlags.DontSave;
        win._root              = data.Root;
        win._title             = data.Title;
        win._callback          = data.Callback;
        win._onCreate          = data.OnCreate;
        win._createLabelFormat = data.CreateLabelFormat;
        win._onItemContext     = data.OnItemContext;
        win.ShowAsDropDown(screenRect, new Vector2(Mathf.Max(screenRect.width, ColumnWidth * 2 + 4), 360));
    }

    private DropdownNode   _root;
    private string         _title;
    private Action<int>    _callback;
    private Action<string> _onCreate;
    private string         _createLabelFormat = "＋ Create \"{0}\"";
    private Action<int>    _onItemContext;

    private readonly List<DropdownNode> _pathFolders = new();
    private readonly List<ListView>     _columnLists = new();
    private int                         _focusedColumn;

    private string             _search = "";
    private bool               _searchFocused = true;
    private bool               _refreshingSelection;
    private readonly List<DropdownNode> _searchResults = new();

    private Label       _titleLabel;
    private TextField   _searchField;
    private ScrollView  _columnsHost;
    private ListView    _searchList;

    private sealed class RowRef
    {
        public DropdownNode Node;
        public int          Column;
    }

    private void CreateGUI()
    {
        var root = rootVisualElement;
        root.style.flexDirection   = FlexDirection.Column;
        root.style.flexGrow        = 1;
        root.style.backgroundColor = C_BG;

        root.Add(BuildHeader());
        root.Add(BuildSearchBar());
        root.Add(BuildColumnsHost());
        root.Add(BuildSearchList());

        RebuildColumns();
        UpdateSearchVisibility();

        root.schedule.Execute(() => _searchField.Focus()).StartingIn(50);
        root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
        root.RegisterCallback<NavigationMoveEvent>(ev => ev.StopPropagation(), TrickleDown.TrickleDown);
        root.RegisterCallback<NavigationSubmitEvent>(ev => ev.StopPropagation(), TrickleDown.TrickleDown);
    }

    private VisualElement BuildHeader()
    {
        var header = new VisualElement();
        header.style.flexDirection     = FlexDirection.Row;
        header.style.alignItems        = Align.Center;
        header.style.minHeight         = 30;
        header.style.paddingLeft       = 8;
        header.style.paddingRight      = 8;
        header.style.paddingTop        = 4;
        header.style.paddingBottom     = 4;
        header.style.backgroundColor   = C_HEADER;
        header.style.borderBottomWidth = 1;
        header.style.borderBottomColor = C_BORDER;

        _titleLabel = new Label(_title ?? string.Empty);
        _titleLabel.style.flexGrow                = 1;
        _titleLabel.style.fontSize                = 12;
        _titleLabel.style.color                   = C_TEXT;
        _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _titleLabel.style.unityTextAlign          = TextAnchor.MiddleCenter;

        header.Add(_titleLabel);
        return header;
    }

    private VisualElement BuildSearchBar()
    {
        var bar = new VisualElement();
        bar.style.paddingLeft       = 6; bar.style.paddingRight  = 6;
        bar.style.paddingTop        = 5; bar.style.paddingBottom = 5;
        bar.style.backgroundColor   = C_BG;
        bar.style.borderBottomWidth = 1;
        bar.style.borderBottomColor = C_BORDER;

        _searchField = new TextField();
        _searchField.style.flexGrow = 1;
        _searchField.RegisterCallback<FocusInEvent>(_  => _searchFocused = true);
        _searchField.RegisterCallback<FocusOutEvent>(_ => _searchFocused = false);

        _searchField.RegisterCallbackOnce<AttachToPanelEvent>(_ =>
        {
            var input = _searchField.Q(className: "unity-base-field__input");
            if (input == null) return;
            input.style.backgroundColor = new Color(0.13f, 0.13f, 0.13f);
            input.style.color           = C_TEXT;
            input.style.borderTopWidth  = input.style.borderRightWidth =
                input.style.borderBottomWidth = input.style.borderLeftWidth = 1;
            input.style.borderTopColor  = input.style.borderRightColor =
                input.style.borderBottomColor = input.style.borderLeftColor = C_BORDER;
            SetBorderRadius(input.style, 4);
        });

        var placeholder = new Label("Search...");
        placeholder.style.position       = Position.Absolute;
        placeholder.style.left           = 10;
        placeholder.style.top            = 0;
        placeholder.style.bottom         = 0;
        placeholder.style.fontSize       = 12;
        placeholder.style.color          = C_SUBTEXT;
        placeholder.style.unityTextAlign = TextAnchor.MiddleLeft;
        placeholder.pickingMode          = PickingMode.Ignore;

        _searchField.RegisterValueChangedCallback(e =>
        {
            _search = e.newValue;
            placeholder.style.display = string.IsNullOrEmpty(e.newValue) ? DisplayStyle.Flex : DisplayStyle.None;
            RefreshSearch();
            UpdateSearchVisibility();
        });

        bar.Add(_searchField);
        bar.Add(placeholder);
        return bar;
    }

    private VisualElement BuildColumnsHost()
    {
        _columnsHost = new ScrollView(ScrollViewMode.Horizontal);
        _columnsHost.style.flexGrow      = 1;
        _columnsHost.style.flexDirection = FlexDirection.Row;
        _columnsHost.contentContainer.style.flexDirection = FlexDirection.Row;
        return _columnsHost;
    }

    private VisualElement BuildSearchList()
    {
        _searchList = new ListView
        {
            fixedItemHeight = 28,
            selectionType   = SelectionType.Single,
            makeItem        = MakeRow,
            itemsSource     = _searchResults,
        };
        _searchList.bindItem       = (row, i) => BindRow(row, _searchResults[i], -1, i, _searchList, showFullPath: true);
        _searchList.selectionChanged += _ => RefreshListSafe(_searchList);
        _searchList.style.flexGrow = 1;
        _searchList.style.display  = DisplayStyle.None;
        return _searchList;
    }

    private void RebuildColumns()
    {
        _columnsHost.Clear();
        _columnLists.Clear();

        AddColumn(_root, 0);
        for (int k = 0; k < _pathFolders.Count; k++)
            AddColumn(_pathFolders[k], k + 1);

        _focusedColumn = Mathf.Clamp(_focusedColumn, 0, _columnLists.Count - 1);
        _columnsHost.schedule.Execute(() => _columnsHost.horizontalScroller.value = _columnsHost.horizontalScroller.highValue).StartingIn(0);
    }

    private void AddColumn(DropdownNode owner, int columnIndex)
    {
        var column = new VisualElement();
        column.style.width            = ColumnWidth;
        column.style.flexShrink       = 0;
        column.style.borderRightWidth = 1;
        column.style.borderRightColor = C_BORDER;

        var items = owner.Children;
        var list  = new ListView
        {
            fixedItemHeight = 28,
            selectionType   = SelectionType.Single,
            makeItem        = MakeRow,
            itemsSource     = items,
        };
        int capturedColumn = columnIndex;
        list.bindItem = (row, i) => BindRow(row, items[i], capturedColumn, i, list, showFullPath: false);
        list.selectionChanged += _ => RefreshListSafe(list);
        list.RegisterCallback<PointerDownEvent>(_ => _focusedColumn = capturedColumn, TrickleDown.TrickleDown);
        list.style.flexGrow = 1;

        if (capturedColumn < _pathFolders.Count)
        {
            int active = items.IndexOf(_pathFolders[capturedColumn]);
            if (active >= 0) list.selectedIndex = active;
        }

        _columnLists.Add(list);
        column.Add(list);
        _columnsHost.Add(column);
    }

    private void RefreshListSafe(ListView list)
    {
        if (_refreshingSelection) return;
        _refreshingSelection = true;
        list.RefreshItems();
        _refreshingSelection = false;
    }

    private VisualElement MakeRow()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems    = Align.Center;
        row.style.paddingLeft   = 10;
        row.style.paddingRight  = 8;
        row.userData            = new RowRef();

        var iconImg = new Image { name = "icon" };
        iconImg.style.width       = 16;
        iconImg.style.height      = 16;
        iconImg.style.marginRight = 6;
        iconImg.style.flexShrink  = 0;

        var label = new Label { name = "label" };
        label.style.flexGrow       = 1;
        label.style.fontSize       = 12;
        label.style.color          = C_TEXT;
        label.style.unityTextAlign = TextAnchor.MiddleLeft;

        var baseLabel = new Label { name = "base" };
        baseLabel.style.fontSize       = 9;
        baseLabel.style.color          = C_RIGHT;
        baseLabel.style.unityTextAlign = TextAnchor.MiddleRight;
        baseLabel.style.marginLeft     = 8;
        baseLabel.style.flexShrink     = 0;
        baseLabel.style.display        = DisplayStyle.None;

        var arrow = new Label("›") { name = "arrow" };
        arrow.style.fontSize = 14;
        arrow.style.color    = C_SUBTEXT;
        arrow.style.display  = DisplayStyle.None;

        row.RegisterCallback<PointerEnterEvent>(_ => row.style.backgroundColor = C_HOVER);
        row.RegisterCallback<PointerLeaveEvent>(_ => ApplyRowBackground(row));

        row.RegisterCallback<PointerDownEvent>(e =>
        {
            if (!(row.userData is RowRef r) || r.Node == null) return;

            if (e.button == 1)
            {
                if (r.Node.IsLeaf && _onItemContext != null) { _onItemContext(r.Node.Index); e.StopPropagation(); }
                return;
            }

            if (e.button == 0) OnItemClicked(r.Node, r.Column);
        });

        row.Add(iconImg);
        row.Add(label);
        row.Add(baseLabel);
        row.Add(arrow);
        return row;
    }

    private void BindRow(VisualElement row, DropdownNode node, int column, int index, ListView list, bool showFullPath)
    {
        var r = (RowRef)row.userData;
        r.Node   = node;
        r.Column = column;

        var iconImg = row.Q<Image>("icon");
        var label   = row.Q<Label>("label");
        var arrow   = row.Q<Label>("arrow");
        var baseLbl = row.Q<Label>("base");

        label.text  = showFullPath && node.FullPath != null ? node.FullPath : node.Label;
        row.tooltip = node.Tooltip ?? string.Empty;

        iconImg.image         = node.Icon;
        iconImg.style.display = node.Icon != null ? DisplayStyle.Flex : DisplayStyle.None;

        arrow.style.display = node.IsFolder ? DisplayStyle.Flex : DisplayStyle.None;

        bool showBase = node.IsLeaf && !string.IsNullOrEmpty(node.RightText);
        baseLbl.text          = showBase ? node.RightText : string.Empty;
        baseLbl.style.display = showBase ? DisplayStyle.Flex : DisplayStyle.None;

        row.style.backgroundColor = ResolveRowBackground(node, column, index, list);
    }

    private Color ResolveRowBackground(DropdownNode node, int column, int index, ListView list)
    {
        bool isActiveFolder = column >= 0 && column < _pathFolders.Count && _pathFolders[column] == node;
        if (isActiveFolder) return new Color(C_ACCENT.r, C_ACCENT.g, C_ACCENT.b, 0.18f);
        if (index == list.selectedIndex) return C_HOVER;
        return index % 2 == 0 ? C_TRANSPARENT : C_ROW_ALT;
    }

    private void ApplyRowBackground(VisualElement row)
    {
        if (!(row.userData is RowRef r) || r.Node == null) return;
        int column = r.Column;
        var list   = column < 0 ? _searchList : _columnLists[column];
        int index  = (list.itemsSource as IList<DropdownNode>)?.IndexOf(r.Node) ?? -1;
        row.style.backgroundColor = ResolveRowBackground(r.Node, column, index, list);
    }

    private void OnItemClicked(DropdownNode node, int column)
    {
        if (node.IsCreate)
        {
            var text = _search.Trim();
            var onCreate = _onCreate;
            Close();
            onCreate?.Invoke(text);
            return;
        }

        if (node.IsLeaf)
        {
            _callback?.Invoke(node.Index);
            Close();
            return;
        }

        Drill(column, node);
    }

    private void Drill(int column, DropdownNode folder)
    {
        if (_pathFolders.Count > column)
            _pathFolders.RemoveRange(column, _pathFolders.Count - column);
        _pathFolders.Add(folder);
        _focusedColumn = column + 1;
        RebuildColumns();
        FocusColumn(_focusedColumn);
    }

    private void GoBack()
    {
        int target = _focusedColumn - 1;
        DropdownNode keep = target >= 0 && target < _pathFolders.Count ? _pathFolders[target] : null;
        if (_pathFolders.Count > target)
            _pathFolders.RemoveRange(target, _pathFolders.Count - target);
        _focusedColumn = target;
        RebuildColumns();
        FocusColumn(_focusedColumn, keep);
    }

    private void FocusColumn(int column, DropdownNode preferred = null)
    {
        if (column < 0 || column >= _columnLists.Count) return;
        var list = _columnLists[column];
        var items = (List<DropdownNode>)list.itemsSource;
        int idx = preferred != null ? items.IndexOf(preferred) : -1;
        if (idx >= 0) list.selectedIndex = idx;
        else if (items.Count > 0 && list.selectedIndex < 0) list.selectedIndex = 0;
        list.Focus();
        if (list.selectedIndex >= 0) list.ScrollToItem(list.selectedIndex);
    }

    private void RefreshSearch()
    {
        _searchResults.Clear();

        if (!string.IsNullOrWhiteSpace(_search))
        {
            var query  = _search.ToLowerInvariant();
            var scored = new List<(DropdownNode node, int score)>();
            DropdownSearch.CollectScoredLeaves(_root, query, scored);
            scored.Sort((a, b) => b.score.CompareTo(a.score));
            foreach (var (node, _) in scored)
                _searchResults.Add(node);

            if (_onCreate != null)
            {
                var typed = _search.Trim();
                bool hasExact = _searchResults.Any(n => n.IsLeaf && string.Equals(n.Label, typed, StringComparison.OrdinalIgnoreCase));
                if (typed.Length > 0 && !hasExact)
                    _searchResults.Add(new DropdownNode { Label = string.Format(_createLabelFormat, typed), IsCreate = true });
            }
        }

        _searchList.Rebuild();
        _searchList.ClearSelection();
        if (_searchResults.Count > 0) _searchList.selectedIndex = 0;
    }

    private void UpdateSearchVisibility()
    {
        bool searching = !string.IsNullOrWhiteSpace(_search);
        _columnsHost.style.display = searching ? DisplayStyle.None : DisplayStyle.Flex;
        _searchList.style.display  = searching ? DisplayStyle.Flex : DisplayStyle.None;
    }

    private void OnKeyDown(KeyDownEvent e)
    {
        if (e.keyCode == KeyCode.Escape)
        {
            Close();
            e.StopPropagation();
            return;
        }

        bool searching = !string.IsNullOrWhiteSpace(_search);

        if (_searchFocused)
        {
            switch (e.keyCode)
            {
                case KeyCode.DownArrow:
                    EnterList(searching);
                    e.StopPropagation();
                    return;

                case KeyCode.UpArrow:
                    e.StopPropagation();
                    return;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (searching && _searchResults.Count > 0)
                    {
                        int i = _searchList.selectedIndex;
                        if (i >= 0 && i < _searchResults.Count) OnItemClicked(_searchResults[i], -1);
                        e.StopPropagation();
                    }
                    return;
            }
            return;
        }

        if (searching)
        {
            switch (e.keyCode)
            {
                case KeyCode.UpArrow:
                case KeyCode.DownArrow:
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    HandleListKey(e, _searchList, _searchResults, n => OnItemClicked(n, -1));
                    return;
                default:
                    if (DropdownKeys.IsTypingChar(e)) FocusSearchWithChar(e);
                    return;
            }
        }

        switch (e.keyCode)
        {
            case KeyCode.LeftArrow when _focusedColumn > 0:
            case KeyCode.Backspace when _focusedColumn > 0:
                GoBack();
                e.StopPropagation();
                return;

            case KeyCode.RightArrow:
            {
                var list = _columnLists[_focusedColumn];
                var items = (List<DropdownNode>)list.itemsSource;
                int sel = list.selectedIndex;
                if (sel >= 0 && sel < items.Count && items[sel].IsFolder)
                    Drill(_focusedColumn, items[sel]);
                e.StopPropagation();
                return;
            }

            case KeyCode.UpArrow:
            case KeyCode.DownArrow:
            case KeyCode.Return:
            case KeyCode.KeypadEnter:
            {
                var list = _columnLists[_focusedColumn];
                var items = (List<DropdownNode>)list.itemsSource;
                HandleListKey(e, list, items, n => OnItemClicked(n, _focusedColumn));
                return;
            }

            default:
                if (DropdownKeys.IsTypingChar(e)) FocusSearchWithChar(e);
                return;
        }
    }

    private void EnterList(bool searching)
    {
        var list  = searching ? _searchList : _columnLists[_focusedColumn];
        var items = (List<DropdownNode>)list.itemsSource;
        if (items.Count == 0) return;
        int sel  = list.selectedIndex;
        int next = sel < 0 ? 0 : Mathf.Min(sel + 1, items.Count - 1);
        list.selectedIndex = next;
        list.ScrollToItem(next);
        list.Focus();
    }

    private void FocusSearchWithChar(KeyDownEvent e)
    {
        _searchField.value += e.character;
        _searchField.Focus();
        e.StopPropagation();
    }

    private void HandleListKey(KeyDownEvent e, ListView list, List<DropdownNode> items, Action<DropdownNode> activate)
    {
        switch (e.keyCode)
        {
            case KeyCode.UpArrow:
            {
                int next = Mathf.Max(0, list.selectedIndex - 1);
                if (list.selectedIndex < 0 && items.Count > 0) next = 0;
                list.selectedIndex = next;
                list.ScrollToItem(next);
                e.StopPropagation();
                return;
            }
            case KeyCode.DownArrow:
            {
                int cur  = list.selectedIndex;
                int next = cur < items.Count - 1 ? cur + 1 : cur;
                if (cur < 0 && items.Count > 0) next = 0;
                list.selectedIndex = next;
                list.ScrollToItem(next);
                e.StopPropagation();
                return;
            }
            case KeyCode.Return:
            case KeyCode.KeypadEnter:
            {
                int i = list.selectedIndex;
                if (i >= 0 && i < items.Count) activate(items[i]);
                e.StopPropagation();
                return;
            }
        }
    }
}
