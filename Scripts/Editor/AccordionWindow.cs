using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

internal sealed class AccordionRenderer : IDropdownRenderer
{
    public void Show(Rect screenRect, BuiltDropdown data) => AccordionWindow.Open(screenRect, data);
}

internal sealed class AccordionWindow : EditorWindow
{
    private const float IndentPerDepth = 14f;

    internal static void Open(Rect screenRect, BuiltDropdown data)
    {
        var win       = CreateInstance<AccordionWindow>();
        win.hideFlags = HideFlags.DontSave;
        win._root              = data.Root;
        win._title             = data.Title;
        win._callback          = data.Callback;
        win._onCreate          = data.OnCreate;
        win._createLabelFormat = data.CreateLabelFormat;
        win._onItemContext     = data.OnItemContext;
        win._showTitle         = data.ShowTitleBar;
        win.ShowAsDropDown(screenRect, new Vector2(Mathf.Max(screenRect.width, 260), 340));
    }

    private DropdownNode   _root;
    private string         _title;
    private Action<int>    _callback;
    private Action<string> _onCreate;
    private string         _createLabelFormat = "＋ Create \"{0}\"";
    private Action<int>    _onItemContext;

    private string _search = "";
    private bool   _searchFocused = true;
    private bool   _refreshingSelection;
    private bool   _showTitle;

    private readonly HashSet<DropdownNode> _expanded       = new();
    private readonly HashSet<DropdownNode> _matchLeaves    = new();
    private readonly HashSet<DropdownNode> _matchAncestors = new();

    private sealed class Row
    {
        public DropdownNode Node;
        public int          Depth;
        public bool         Dim;
    }

    private sealed class RowRef
    {
        public DropdownNode Node;
    }

    private readonly List<Row> _visible = new();

    private Label     _titleLabel;
    private TextField _searchField;
    private ListView  _listView;

    private void CreateGUI()
    {
        var root = rootVisualElement;
        DropdownTheme.ApplyPanel(root);

        if (_showTitle) root.Add(BuildHeader());
        root.Add(BuildSearchBar());
        root.Add(BuildList());

        RebuildVisible();

        root.schedule.Execute(() => _searchField.Focus()).StartingIn(50);
        root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
        root.RegisterCallback<NavigationMoveEvent>(ev => ev.StopPropagation(), TrickleDown.TrickleDown);
        root.RegisterCallback<NavigationSubmitEvent>(ev => ev.StopPropagation(), TrickleDown.TrickleDown);
    }

    private VisualElement BuildHeader()
    {
        var header = new VisualElement();
        header.AddToClassList("dropdown-header");

        _titleLabel = new Label(_title ?? string.Empty);
        _titleLabel.AddToClassList("dropdown-title");

        header.Add(_titleLabel);
        return header;
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
            RebuildVisible();
        });

        bar.Add(_searchField);
        bar.Add(placeholder);
        return bar;
    }

    private VisualElement BuildList()
    {
        _listView = new ListView
        {
            fixedItemHeight = 28,
            selectionType   = SelectionType.Single,
            makeItem        = MakeRow,
            bindItem        = BindRow,
        };
        _listView.AddToClassList("dropdown-list");
        _listView.selectionChanged += _ =>
        {
            if (_refreshingSelection) return;
            _refreshingSelection = true;
            _listView.RefreshItems();
            _refreshingSelection = false;
        };
        return _listView;
    }

    private VisualElement MakeRow()
    {
        var row = new VisualElement();
        row.AddToClassList("dropdown-row");
        row.userData = new RowRef();

        var twist = new Label { name = "twist" };
        twist.AddToClassList("dropdown-twist");

        var iconImg = new Image { name = "icon" };
        iconImg.AddToClassList("dropdown-icon");

        var label = new Label { name = "label" };
        label.AddToClassList("dropdown-label");

        var baseLabel = new Label { name = "base" };
        baseLabel.AddToClassList("dropdown-right");
        baseLabel.style.display = DisplayStyle.None;

        row.RegisterCallback<PointerDownEvent>(e =>
        {
            if (!(row.userData is RowRef r) || r.Node == null) return;

            if (e.button == 1)
            {
                if (r.Node.IsLeaf && _onItemContext != null) { _onItemContext(r.Node.Index); e.StopPropagation(); }
                return;
            }

            if (e.button == 0) OnItemClicked(r.Node);
        });

        row.Add(twist);
        row.Add(iconImg);
        row.Add(label);
        row.Add(baseLabel);
        return row;
    }

    private void BindRow(VisualElement row, int index)
    {
        var entry = _visible[index];
        var node  = entry.Node;
        ((RowRef)row.userData).Node = node;

        var twist   = row.Q<Label>("twist");
        var iconImg = row.Q<Image>("icon");
        var label   = row.Q<Label>("label");
        var baseLbl = row.Q<Label>("base");

        row.style.paddingLeft = 8 + entry.Depth * IndentPerDepth;

        twist.text = node.IsFolder ? (IsExpanded(node) ? "▾" : "▸") : string.Empty;

        label.text  = node.Label;
        row.tooltip = node.Tooltip ?? string.Empty;

        iconImg.image         = node.Icon;
        iconImg.style.display = node.Icon != null ? DisplayStyle.Flex : DisplayStyle.None;

        bool showBase = node.IsLeaf && !string.IsNullOrEmpty(node.RightText);
        baseLbl.text          = showBase ? node.RightText : string.Empty;
        baseLbl.style.display = showBase ? DisplayStyle.Flex : DisplayStyle.None;

        row.EnableInClassList("dropdown-row--dim", entry.Dim);
        row.EnableInClassList("dropdown-row--selected", index == _listView.selectedIndex);
    }

    private bool IsExpanded(DropdownNode folder) =>
        string.IsNullOrWhiteSpace(_search) ? _expanded.Contains(folder) : _matchAncestors.Contains(folder);

    private void OnItemClicked(DropdownNode node)
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

        ToggleFolder(node);
    }

    private void ToggleFolder(DropdownNode folder)
    {
        if (!string.IsNullOrWhiteSpace(_search)) return;

        if (!_expanded.Remove(folder))
            _expanded.Add(folder);

        RebuildVisible();

        int idx = _visible.FindIndex(v => v.Node == folder);
        if (idx >= 0) _listView.selectedIndex = idx;
    }

    private void RebuildVisible()
    {
        _visible.Clear();
        bool searching = !string.IsNullOrWhiteSpace(_search);

        if (searching)
        {
            ComputeMatches();
            WalkSearch(_root, 0);

            if (_onCreate != null)
            {
                var typed = _search.Trim();
                bool hasExact = _matchLeaves.Any(n => string.Equals(n.Label, typed, StringComparison.OrdinalIgnoreCase));
                if (typed.Length > 0 && !hasExact)
                    _visible.Add(new Row { Node = new DropdownNode { Label = string.Format(_createLabelFormat, typed), IsCreate = true }, Depth = 0, Dim = false });
            }
        }
        else
        {
            Walk(_root, 0);
        }

        _listView.itemsSource = _visible;
        _listView.Rebuild();
        _listView.ClearSelection();
        if (_visible.Count > 0) _listView.selectedIndex = 0;
    }

    private void Walk(DropdownNode node, int depth)
    {
        foreach (var child in node.Children)
        {
            _visible.Add(new Row { Node = child, Depth = depth, Dim = false });
            if (child.IsFolder && _expanded.Contains(child))
                Walk(child, depth + 1);
        }
    }

    private void WalkSearch(DropdownNode node, int depth)
    {
        foreach (var child in node.Children)
        {
            bool bright = _matchLeaves.Contains(child) || _matchAncestors.Contains(child);
            _visible.Add(new Row { Node = child, Depth = depth, Dim = !bright });
            if (child.IsFolder && _matchAncestors.Contains(child))
                WalkSearch(child, depth + 1);
        }
    }

    private void ComputeMatches()
    {
        _matchLeaves.Clear();
        _matchAncestors.Clear();
        var query = _search.ToLowerInvariant();
        MarkMatches(_root, query);
    }

    private bool MarkMatches(DropdownNode node, string query)
    {
        bool any = false;
        foreach (var child in node.Children)
        {
            if (child.IsLeaf)
            {
                if (DropdownSearch.FuzzyScore(child.FullPath ?? child.Label, query) > 0)
                {
                    _matchLeaves.Add(child);
                    any = true;
                }
            }
            else
            {
                if (MarkMatches(child, query))
                {
                    _matchAncestors.Add(child);
                    any = true;
                }
            }
        }
        return any;
    }

    private void OnKeyDown(KeyDownEvent e)
    {
        if (e.keyCode == KeyCode.Escape)
        {
            Close();
            e.StopPropagation();
            return;
        }

        if (_searchFocused)
        {
            switch (e.keyCode)
            {
                case KeyCode.DownArrow:
                    EnterList();
                    e.StopPropagation();
                    return;

                case KeyCode.UpArrow:
                    e.StopPropagation();
                    return;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                {
                    var node = SelectedNode();
                    if (node != null) OnItemClicked(node);
                    e.StopPropagation();
                    return;
                }
            }
            return;
        }

        switch (e.keyCode)
        {
            case KeyCode.UpArrow:
            {
                int next = Mathf.Max(0, _listView.selectedIndex - 1);
                if (_listView.selectedIndex < 0 && _visible.Count > 0) next = 0;
                _listView.selectedIndex = next;
                _listView.ScrollToItem(next);
                e.StopPropagation();
                return;
            }

            case KeyCode.DownArrow:
            {
                int cur  = _listView.selectedIndex;
                int next = cur < _visible.Count - 1 ? cur + 1 : cur;
                if (cur < 0 && _visible.Count > 0) next = 0;
                _listView.selectedIndex = next;
                _listView.ScrollToItem(next);
                e.StopPropagation();
                return;
            }

            case KeyCode.Home:
                if (_visible.Count > 0) { _listView.selectedIndex = 0; _listView.ScrollToItem(0); }
                e.StopPropagation();
                return;

            case KeyCode.End:
                if (_visible.Count > 0) { int last = _visible.Count - 1; _listView.selectedIndex = last; _listView.ScrollToItem(last); }
                e.StopPropagation();
                return;

            case KeyCode.RightArrow:
            {
                var node = SelectedNode();
                if (node != null && node.IsFolder)
                {
                    if (!IsExpanded(node)) ToggleFolder(node);
                    MoveToFirstChild(node);
                }
                e.StopPropagation();
                return;
            }

            case KeyCode.LeftArrow:
            case KeyCode.Backspace:
            {
                GoBack();
                e.StopPropagation();
                return;
            }

            case KeyCode.Return:
            case KeyCode.KeypadEnter:
            {
                var node = SelectedNode();
                if (node != null) OnItemClicked(node);
                e.StopPropagation();
                return;
            }

            default:
                if (DropdownKeys.IsTypingChar(e))
                {
                    _searchField.value += e.character;
                    _searchField.Focus();
                    e.StopPropagation();
                }
                return;
        }
    }

    private void EnterList()
    {
        if (_visible.Count == 0) return;
        int sel  = _listView.selectedIndex;
        int next = sel < 0 ? 0 : Mathf.Min(sel + 1, _visible.Count - 1);
        _listView.selectedIndex = next;
        _listView.ScrollToItem(next);
        _listView.Focus();
    }

    private DropdownNode SelectedNode()
    {
        int i = _listView.selectedIndex;
        return i >= 0 && i < _visible.Count ? _visible[i].Node : null;
    }

    private void MoveToFirstChild(DropdownNode folder)
    {
        int idx = _visible.FindIndex(v => v.Node == folder);
        if (idx < 0 || idx + 1 >= _visible.Count) return;
        if (_visible[idx + 1].Depth <= _visible[idx].Depth) return;
        _listView.selectedIndex = idx + 1;
        _listView.ScrollToItem(idx + 1);
    }

    private void GoBack()
    {
        var node = SelectedNode();
        if (node == null) return;

        if (node.IsFolder && IsExpanded(node) && string.IsNullOrWhiteSpace(_search))
        {
            ToggleFolder(node);
            return;
        }

        var parent = node.Parent;
        if (parent == null || parent == _root) return;

        int idx = _visible.FindIndex(v => v.Node == parent);
        if (idx < 0) return;
        _listView.selectedIndex = idx;
        _listView.ScrollToItem(idx);
    }
}
