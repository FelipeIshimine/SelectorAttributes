using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

internal sealed class CascadingFlyoutsRenderer : IDropdownRenderer
{
    public void Show(Rect screenRect, BuiltDropdown data) => FlyoutChain.Open(screenRect, data);
}

internal sealed class FlyoutChain
{
    internal const float PanelWidth     = 240f;
    internal const float RowHeight       = 28f;
    internal const float MaxHeight       = 340f;
    internal const float SearchBarHeight = 34f;

    private readonly BuiltDropdown      _data;
    private readonly List<FlyoutWindow> _levels = new();

    private bool _disposed;

    internal BuiltDropdown Data       => _data;
    internal bool          IsDisposed => _disposed;

    internal static void Open(Rect screenRect, BuiltDropdown data)
    {
        var chain = new FlyoutChain(data);
        var rect  = new Rect(screenRect.x, screenRect.yMax, Mathf.Max(screenRect.width, PanelWidth), 0);
        chain.OpenLevel(0, data.Root, rect);
    }

    private FlyoutChain(BuiltDropdown data)
    {
        _data = data;
    }

    private void OpenLevel(int level, DropdownNode owner, Rect rect)
    {
        var win = ScriptableObject.CreateInstance<FlyoutWindow>();
        win.hideFlags = HideFlags.DontSave;
        win.Init(this, level, owner);

        int   count   = owner.Children.Count;
        float searchH = level == 0 ? SearchBarHeight : 0f;
        float height  = Mathf.Min(count * RowHeight + searchH + 8f, MaxHeight);
        rect.height   = Mathf.Max(height, RowHeight + searchH + 8f);

        win.position = rect;
        win.ShowPopup();
        win.Focus();
        _levels.Add(win);
    }

    internal void OpenChild(int parentLevel, DropdownNode folder, Rect rowScreen)
    {
        if (_disposed) return;
        CloseDeeperThan(parentLevel);

        int   count  = folder.Children.Count;
        float height = Mathf.Min(count * RowHeight + 8f, MaxHeight);

        float screenW = Screen.currentResolution.width;
        float screenH = Screen.currentResolution.height;

        float x = rowScreen.xMax - 2f;
        if (x + PanelWidth > screenW) x = rowScreen.x - PanelWidth + 2f;
        float y = Mathf.Min(rowScreen.y, Mathf.Max(0f, screenH - height));

        OpenLevel(parentLevel + 1, folder, new Rect(x, y, PanelWidth, height));
    }

    internal void CloseDeeperThan(int level)
    {
        for (int i = _levels.Count - 1; i > level; i--)
        {
            if (_levels[i] != null) _levels[i].Close();
            _levels.RemoveAt(i);
        }
        if (!_disposed && _levels.Count > 0 && _levels[^1] != null)
            _levels[^1].Focus();
    }

    internal void SelectLeaf(int index)
    {
        var cb = _data.Callback;
        CloseAll();
        cb?.Invoke(index);
    }

    internal void Create(string text)
    {
        var oc = _data.OnCreate;
        CloseAll();
        oc?.Invoke(text);
    }

    internal bool Contains(EditorWindow w) => w != null && _levels.Contains(w as FlyoutWindow);

    internal void CloseAll()
    {
        if (_disposed) return;
        _disposed = true;
        for (int i = _levels.Count - 1; i >= 0; i--)
            if (_levels[i] != null) _levels[i].Close();
        _levels.Clear();
    }
}

internal sealed class FlyoutWindow : EditorWindow
{
    private FlyoutChain  _chain;
    private int          _level;
    private DropdownNode _owner;

    private IVisualElementScheduledItem _pendingOpen;

    private ScrollView          _scroll;
    private readonly List<VisualElement> _rowEls = new();
    private readonly List<DropdownNode>  _nodes  = new();
    private int  _selected = -1;

    private TextField _searchField;
    private ListView  _resultsList;
    private readonly List<DropdownNode> _searchResults = new();
    private string _search = "";
    private bool   _searchFocused;
    private bool   _initialFocusDone;
    private bool   _refreshingSelection;

    private bool IsRoot => _level == 0;

    internal void Init(FlyoutChain chain, int level, DropdownNode owner)
    {
        _chain = chain;
        _level = level;
        _owner = owner;
    }

    private void CreateGUI()
    {
        var root = rootVisualElement;
        DropdownTheme.ApplyPanel(root);

        root.focusable = true;
        root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
        root.RegisterCallback<NavigationMoveEvent>(ev => ev.StopPropagation(), TrickleDown.TrickleDown);
        root.RegisterCallback<NavigationSubmitEvent>(ev => ev.StopPropagation(), TrickleDown.TrickleDown);

        if (IsRoot) root.Add(BuildSearchBar());

        _scroll = new ScrollView(ScrollViewMode.Vertical);
        _scroll.style.flexGrow = 1;
        for (int i = 0; i < _owner.Children.Count; i++)
        {
            var node = _owner.Children[i];
            var row  = BuildRow(node, i);
            _rowEls.Add(row);
            _nodes.Add(node);
            _scroll.Add(row);
        }
        root.Add(_scroll);

        if (IsRoot) root.Add(BuildResultsList());

        if (_nodes.Count > 0) SetSelected(0);

        FocusInner();
    }

    private VisualElement BuildSearchBar()
    {
        var bar = new VisualElement();
        bar.AddToClassList("dropdown-searchbar");

        _searchField = new TextField();
        _searchField.style.flexGrow = 1;
        _searchField.RegisterCallback<FocusInEvent>(_  => _searchFocused = true);
        _searchField.RegisterCallback<FocusOutEvent>(_ => _searchFocused = false);

        var placeholder = new Label("Search...");
        placeholder.AddToClassList("dropdown-placeholder");
        placeholder.pickingMode = PickingMode.Ignore;

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

    private ListView BuildResultsList()
    {
        _resultsList = new ListView
        {
            fixedItemHeight = 28,
            selectionType   = SelectionType.Single,
            makeItem        = MakeResultRow,
            itemsSource     = _searchResults,
        };
        _resultsList.bindItem = BindResultRow;
        _resultsList.AddToClassList("dropdown-list");
        _resultsList.style.display = DisplayStyle.None;
        _resultsList.selectionChanged += _ =>
        {
            if (_refreshingSelection) return;
            _refreshingSelection = true;
            _resultsList.RefreshItems();
            _refreshingSelection = false;
        };
        return _resultsList;
    }

    private VisualElement MakeResultRow()
    {
        var row = new VisualElement();
        row.AddToClassList("dropdown-row");

        var iconImg = new Image { name = "icon" };
        iconImg.AddToClassList("dropdown-icon");

        var label = new Label { name = "label" };
        label.AddToClassList("dropdown-label");

        var baseLabel = new Label { name = "base" };
        baseLabel.AddToClassList("dropdown-right");
        baseLabel.style.display = DisplayStyle.None;

        row.RegisterCallback<PointerDownEvent>(e =>
        {
            if (!(row.userData is int idx) || idx < 0 || idx >= _searchResults.Count) return;
            var node = _searchResults[idx];

            if (e.button == 1)
            {
                if (node.IsLeaf && _chain.Data.OnItemContext != null) { _chain.Data.OnItemContext(node.Index); e.StopPropagation(); }
                return;
            }

            if (e.button == 0) ActivateResult(node);
        });

        row.Add(iconImg);
        row.Add(label);
        row.Add(baseLabel);
        return row;
    }

    private void BindResultRow(VisualElement row, int index)
    {
        row.userData = index;
        var node    = _searchResults[index];
        var iconImg = row.Q<Image>("icon");
        var label   = row.Q<Label>("label");
        var baseLbl = row.Q<Label>("base");

        label.text  = node.IsCreate ? node.Label : (node.FullPath ?? node.Label);
        row.tooltip = node.Tooltip ?? string.Empty;

        iconImg.image         = node.Icon;
        iconImg.style.display = node.Icon != null ? DisplayStyle.Flex : DisplayStyle.None;

        bool showBase = node.IsLeaf && !string.IsNullOrEmpty(node.RightText);
        baseLbl.text          = showBase ? node.RightText : string.Empty;
        baseLbl.style.display = showBase ? DisplayStyle.Flex : DisplayStyle.None;

        row.EnableInClassList("dropdown-row--selected", index == _resultsList.selectedIndex);
    }

    private void ActivateResult(DropdownNode node)
    {
        if (node.IsCreate) _chain.Create(_search.Trim());
        else               _chain.SelectLeaf(node.Index);
    }

    private void RefreshSearch()
    {
        _searchResults.Clear();

        if (!string.IsNullOrWhiteSpace(_search))
        {
            var query  = _search.ToLowerInvariant();
            var scored = new List<(DropdownNode node, int score)>();
            DropdownSearch.CollectScoredLeaves(_owner, query, scored);
            scored.Sort((a, b) => b.score.CompareTo(a.score));
            foreach (var (node, _) in scored)
                _searchResults.Add(node);

            if (_chain.Data.OnCreate != null)
            {
                var typed = _search.Trim();
                bool hasExact = _searchResults.Any(n => n.IsLeaf && string.Equals(n.Label, typed, StringComparison.OrdinalIgnoreCase));
                if (typed.Length > 0 && !hasExact)
                    _searchResults.Add(new DropdownNode { Label = string.Format(_chain.Data.CreateLabelFormat, typed), IsCreate = true });
            }
        }

        _resultsList.Rebuild();
        _resultsList.ClearSelection();
        if (_searchResults.Count > 0) _resultsList.selectedIndex = 0;
    }

    private void UpdateSearchVisibility()
    {
        bool searching = !string.IsNullOrWhiteSpace(_search);
        if (searching) _chain.CloseDeeperThan(0);
        _scroll.style.display      = searching ? DisplayStyle.None : DisplayStyle.Flex;
        _resultsList.style.display = searching ? DisplayStyle.Flex : DisplayStyle.None;

        int   rows = searching ? _searchResults.Count : _nodes.Count;
        float h    = Mathf.Clamp(rows * FlyoutChain.RowHeight + FlyoutChain.SearchBarHeight + 8f,
                                 FlyoutChain.RowHeight + FlyoutChain.SearchBarHeight + 8f,
                                 FlyoutChain.MaxHeight);
        var p = position;
        p.height = h;
        position = p;
    }

    private void OnFocus() => FocusInner();

    private void FocusInner()
    {
        var root = rootVisualElement;
        if (root == null) return;
        root.schedule.Execute(() =>
        {
            if (IsRoot && !_initialFocusDone && _searchField != null)
            {
                _initialFocusDone = true;
                _searchField.Focus();
            }
            else
            {
                root.Focus();
            }
        }).StartingIn(0);
    }

    private VisualElement BuildRow(DropdownNode node, int index)
    {
        var row = new VisualElement();
        row.AddToClassList("dropdown-row");

        var iconImg = new Image();
        iconImg.AddToClassList("dropdown-icon");
        iconImg.image         = node.Icon;
        iconImg.style.display = node.Icon != null ? DisplayStyle.Flex : DisplayStyle.None;

        var label = new Label(node.Label);
        label.AddToClassList("dropdown-label");

        var baseLabel = new Label();
        baseLabel.AddToClassList("dropdown-right");
        bool showBase = node.IsLeaf && !string.IsNullOrEmpty(node.RightText);
        baseLabel.text          = showBase ? node.RightText : string.Empty;
        baseLabel.style.display = showBase ? DisplayStyle.Flex : DisplayStyle.None;

        var arrow = new Label("›");
        arrow.AddToClassList("dropdown-arrow");
        arrow.style.display = node.IsFolder ? DisplayStyle.Flex : DisplayStyle.None;

        row.tooltip = node.Tooltip ?? string.Empty;

        row.RegisterCallback<MouseEnterEvent>(_ =>
        {
            SetSelected(index);
            if (node.IsFolder) ScheduleChildOpen(node, row);
            else               _chain.CloseDeeperThan(_level);
        });

        row.RegisterCallback<PointerDownEvent>(e =>
        {
            if (e.button == 1)
            {
                if (node.IsLeaf && _chain.Data.OnItemContext != null) { _chain.Data.OnItemContext(node.Index); e.StopPropagation(); }
                return;
            }

            if (e.button != 0) return;

            if (node.IsLeaf) _chain.SelectLeaf(node.Index);
            else             _chain.OpenChild(_level, node, RowScreenRect(row));
        });

        row.Add(iconImg);
        row.Add(label);
        row.Add(baseLabel);
        row.Add(arrow);
        return row;
    }

    private void SetSelected(int index)
    {
        _selected = Mathf.Clamp(index, 0, _nodes.Count - 1);
        for (int i = 0; i < _rowEls.Count; i++)
            _rowEls[i].EnableInClassList("dropdown-row--selected", i == _selected);
        if (_selected >= 0 && _selected < _rowEls.Count)
            _scroll.ScrollTo(_rowEls[_selected]);
    }

    private void OnKeyDown(KeyDownEvent e)
    {
        if (e.keyCode == KeyCode.Escape)
        {
            _chain.CloseAll();
            e.StopPropagation();
            return;
        }

        bool searching = IsRoot && !string.IsNullOrWhiteSpace(_search);

        if (IsRoot && _searchFocused)
        {
            switch (e.keyCode)
            {
                case KeyCode.DownArrow:
                    if (searching) EnterResults(); else EnterBrowse();
                    e.StopPropagation();
                    return;
                case KeyCode.UpArrow:
                    e.StopPropagation();
                    return;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    if (searching)
                    {
                        int i = _resultsList.selectedIndex;
                        if (i >= 0 && i < _searchResults.Count) ActivateResult(_searchResults[i]);
                    }
                    else
                    {
                        var node = SelectedNode();
                        if (node != null)
                        {
                            if (node.IsLeaf) _chain.SelectLeaf(node.Index);
                            else             _chain.OpenChild(_level, node, RowScreenRect(_rowEls[_selected]));
                        }
                    }
                    e.StopPropagation();
                    return;
            }
            return;
        }

        if (searching)
        {
            switch (e.keyCode)
            {
                case KeyCode.UpArrow:
                {
                    int next = Mathf.Max(0, _resultsList.selectedIndex - 1);
                    _resultsList.selectedIndex = next;
                    _resultsList.ScrollToItem(next);
                    e.StopPropagation();
                    return;
                }
                case KeyCode.DownArrow:
                {
                    int cur  = _resultsList.selectedIndex;
                    int next = cur < _searchResults.Count - 1 ? cur + 1 : cur;
                    _resultsList.selectedIndex = next;
                    _resultsList.ScrollToItem(next);
                    e.StopPropagation();
                    return;
                }
                case KeyCode.Home:
                    if (_searchResults.Count > 0) { _resultsList.selectedIndex = 0; _resultsList.ScrollToItem(0); }
                    e.StopPropagation();
                    return;
                case KeyCode.End:
                    if (_searchResults.Count > 0) { int last = _searchResults.Count - 1; _resultsList.selectedIndex = last; _resultsList.ScrollToItem(last); }
                    e.StopPropagation();
                    return;
                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                {
                    int i = _resultsList.selectedIndex;
                    if (i >= 0 && i < _searchResults.Count) ActivateResult(_searchResults[i]);
                    e.StopPropagation();
                    return;
                }
                default:
                    if (DropdownKeys.IsTypingChar(e)) FocusSearchWithChar(e);
                    return;
            }
        }

        switch (e.keyCode)
        {
            case KeyCode.DownArrow:
                if (_nodes.Count > 0) SetSelected(_selected < 0 ? 0 : _selected + 1);
                e.StopPropagation();
                return;

            case KeyCode.UpArrow:
                if (_nodes.Count > 0) SetSelected(_selected - 1);
                e.StopPropagation();
                return;

            case KeyCode.Home:
                if (_nodes.Count > 0) SetSelected(0);
                e.StopPropagation();
                return;

            case KeyCode.End:
                if (_nodes.Count > 0) SetSelected(_nodes.Count - 1);
                e.StopPropagation();
                return;

            case KeyCode.RightArrow:
            {
                var node = SelectedNode();
                if (node != null && node.IsFolder)
                    _chain.OpenChild(_level, node, RowScreenRect(_rowEls[_selected]));
                e.StopPropagation();
                return;
            }

            case KeyCode.Return:
            case KeyCode.KeypadEnter:
            {
                var node = SelectedNode();
                if (node == null) return;
                if (node.IsLeaf) _chain.SelectLeaf(node.Index);
                else             _chain.OpenChild(_level, node, RowScreenRect(_rowEls[_selected]));
                e.StopPropagation();
                return;
            }

            case KeyCode.LeftArrow:
            case KeyCode.Backspace:
                if (_level > 0) _chain.CloseDeeperThan(_level - 1);
                e.StopPropagation();
                return;

            default:
                if (IsRoot && DropdownKeys.IsTypingChar(e)) FocusSearchWithChar(e);
                return;
        }
    }

    private void EnterBrowse()
    {
        if (_nodes.Count == 0) return;
        SetSelected(_selected < 0 ? 0 : _selected + 1);
        rootVisualElement.Focus();
    }

    private void EnterResults()
    {
        if (_searchResults.Count == 0) return;
        int sel  = _resultsList.selectedIndex;
        int next = sel < 0 ? 0 : Mathf.Min(sel + 1, _searchResults.Count - 1);
        _resultsList.selectedIndex = next;
        _resultsList.ScrollToItem(next);
        _resultsList.Focus();
    }

    private void FocusSearchWithChar(KeyDownEvent e)
    {
        _searchField.value += e.character;
        _searchField.Focus();
        e.StopPropagation();
    }

    private DropdownNode SelectedNode() =>
        _selected >= 0 && _selected < _nodes.Count ? _nodes[_selected] : null;

    private void OnLostFocus()
    {
        var chain = _chain;
        if (chain == null) return;
        EditorApplication.delayCall += () =>
        {
            if (chain.IsDisposed) return;
            if (!chain.Contains(focusedWindow)) chain.CloseAll();
        };
    }

    private void ScheduleChildOpen(DropdownNode folder, VisualElement row)
    {
        CancelPendingOpen();
        _chain.CloseDeeperThan(_level);
        _pendingOpen = rootVisualElement.schedule
            .Execute(() => _chain.OpenChild(_level, folder, RowScreenRect(row)))
            .StartingIn(200);
    }

    private void CancelPendingOpen()
    {
        _pendingOpen?.Pause();
        _pendingOpen = null;
    }

    private Rect RowScreenRect(VisualElement row)
    {
        var wb = row.worldBound;
        return new Rect(position.x + wb.x, position.y + wb.y, wb.width, wb.height);
    }
}
