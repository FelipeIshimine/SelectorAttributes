// Drop-in replacement for UnityEditor.IMGUI.Controls.AdvancedDropdown.
// Same AdvancedDropdownBuilder API — only Build() return type changes from
// AdvancedDropdown to BuiltDropdown, which has the same .Show(Rect) method.
// Delete the old AdvancedDropdownBuilder.cs and QuickAdvancedDropdown.cs.

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

// ── Public struct (unchanged) ─────────────────────────────────────────────────

public struct AdvancedDropdownPath
{
    public readonly string    Path;
    public readonly Texture2D Icon;
    public readonly string    Tooltip;
    public readonly string    RightText;

    public AdvancedDropdownPath(string path, Texture2D icon, string tooltip = null, string rightText = null)
    {
        Path      = path;
        Icon      = icon;
        Tooltip   = tooltip;
        RightText = rightText;
    }
}

// ── Builder (identical public API) ───────────────────────────────────────────

public sealed class AdvancedDropdownBuilder
{
    private string                              _title;
    private readonly List<AdvancedDropdownPath> _values = new();
    private Action<int>                         _callback;
    private char                                _splitChar = '/';
    private Action<string>                      _onCreate;
    private string                              _createLabelFormat = "＋ Create \"{0}\"";
    private Action<int>                         _onItemContext;
    private DropdownRenderMode                  _renderMode = DropdownRenderMode.SearchDrilldown;
    private bool                                _showTitle;

    public AdvancedDropdownBuilder WithTitle(string title)      { _title     = title; return this; }

    /// <summary>Shows the title bar at the top of the dropdown. Hidden by default.</summary>
    public AdvancedDropdownBuilder ShowTitle(bool show = true)  { _showTitle = show;  return this; }
    public AdvancedDropdownBuilder SetSplitCharacter(char c)    { _splitChar = c;     return this; }
    public AdvancedDropdownBuilder SetCallback(Action<int> cb)  { _callback  = cb;    return this; }

    /// <summary>Selects how the built dropdown renders its tree. Defaults to <see cref="DropdownRenderMode.SearchDrilldown"/>.</summary>
    public AdvancedDropdownBuilder WithRenderMode(DropdownRenderMode mode) { _renderMode = mode; return this; }

    /// <summary>
    /// Adds a synthetic "create" row shown while searching whenever no leaf exactly matches the typed
    /// text. Selecting it invokes <paramref name="onCreate"/> with the current (trimmed) search text.
    /// <paramref name="labelFormat"/> may contain <c>{0}</c> for that text.
    /// </summary>
    public AdvancedDropdownBuilder SetCreateOption(Action<string> onCreate, string labelFormat = null)
    {
        _onCreate = onCreate;
        if (labelFormat != null) _createLabelFormat = labelFormat;
        return this;
    }

    /// <summary>
    /// Enables a right-click context menu on leaf rows. The handler receives the leaf's element index
    /// (the same index passed to <see cref="SetCallback"/>) and is responsible for showing its own menu.
    /// </summary>
    public AdvancedDropdownBuilder SetItemContextHandler(Action<int> onContext)
    {
        _onItemContext = onContext;
        return this;
    }

    public AdvancedDropdownBuilder AddElements(IEnumerable<string> elements, out List<int> indices)
    {
        indices = new List<int>();
        foreach (var e in elements) { indices.Add(_values.Count); _values.Add(new(e, null)); }
        return this;
    }

    public AdvancedDropdownBuilder AddElements(IEnumerable<(string, Texture2D)> elements, out List<int> indices)
    {
        indices = new List<int>();
        foreach (var (p, i) in elements) { indices.Add(_values.Count); _values.Add(new(p, i)); }
        return this;
    }

    public AdvancedDropdownBuilder AddElements(IEnumerable<AdvancedDropdownPath> elements, out List<int> indices)
    {
        indices = new List<int>();
        foreach (var e in elements) { indices.Add(_values.Count); _values.Add(e); }
        return this;
    }
    /// <summary>
    /// Typed overload — stores values alongside paths.
    /// Use <paramref name="values"/> directly in <see cref="SetCallback"/> to avoid manual index mapping.
    /// </summary>
    public AdvancedDropdownBuilder AddElements<T>(IEnumerable<(string path, T value)> elements, out T[] values)
    {
	    var arr = elements.ToArray();
	    values = new T[arr.Length];
	    for (int i = 0; i < arr.Length; i++)
	    {
		    values[i] = arr[i].value;
		    _values.Add(new AdvancedDropdownPath(arr[i].path, null));
	    }
	    return this;
    }
    /// <summary>Typed overload that also stores a right-justified secondary label per leaf.</summary>
    public AdvancedDropdownBuilder AddElements<T>(IEnumerable<(string path, string rightText, T value)> elements, out T[] values)
    {
	    var arr = elements.ToArray();
	    values = new T[arr.Length];
	    for (int i = 0; i < arr.Length; i++)
	    {
		    values[i] = arr[i].value;
		    _values.Add(new AdvancedDropdownPath(arr[i].path, null, null, arr[i].rightText));
	    }
	    return this;
    }
    public AdvancedDropdownBuilder AddElement(string path, Texture2D icon, out int index)
    {
        index = _values.Count; _values.Add(new(path, icon)); return this;
    }

    public AdvancedDropdownBuilder AddElement(string path, out int index)
    {
        index = _values.Count; _values.Add(new(path, null)); return this;
    }

    /// <summary>
    /// Builds the dropdown tree. Call <c>.Show(element.worldBound)</c> on the result.
    /// </summary>
    public BuiltDropdown Build()
    {
        var root         = new DropdownNode { Label = _title ?? string.Empty };
        var folderLookup = new Dictionary<string, DropdownNode>();

        for (int i = 0; i < _values.Count; i++)
        {
            var segments    = _values[i].Path.Split(_splitChar);
            var parent      = root;
            var accumulated = string.Empty;

            for (int j = 0; j < segments.Length; j++)
            {
                var  seg    = segments[j];
                bool isLeaf = j == segments.Length - 1;

                accumulated = accumulated.Length == 0
                    ? seg
                    : accumulated + _splitChar + seg;

                if (isLeaf)
                {
                    parent.Children.Add(new DropdownNode
                    {
                        Label     = seg,
                        FullPath  = _values[i].Path,
                        Icon      = _values[i].Icon,
                        Tooltip   = _values[i].Tooltip,
                        RightText = _values[i].RightText,
                        Index     = i,
                        Parent    = parent,
                    });
                }
                else
                {
                    if (!folderLookup.TryGetValue(accumulated, out var folder))
                    {
                        folder = new DropdownNode { Label = seg, Parent = parent };
                        folderLookup[accumulated] = folder;
                        parent.Children.Add(folder);
                    }
                    parent = folder;
                }
            }
        }

        return new BuiltDropdown(root, _title ?? string.Empty, _callback)
        {
            OnCreate          = _onCreate,
            CreateLabelFormat = _createLabelFormat,
            OnItemContext     = _onItemContext,
            RenderMode        = _renderMode,
            ShowTitleBar      = _showTitle,
        };
    }
}

// ── Returned by Build() ───────────────────────────────────────────────────────

public sealed class BuiltDropdown
{
    internal readonly DropdownNode Root;
    internal readonly string       Title;
    internal readonly Action<int>  Callback;

    internal Action<string>      OnCreate;
    internal string              CreateLabelFormat = "＋ Create \"{0}\"";
    internal Action<int>         OnItemContext;
    internal DropdownRenderMode  RenderMode = DropdownRenderMode.SearchDrilldown;
    internal bool                ShowTitleBar;

    internal BuiltDropdown(DropdownNode root, string title, Action<int> callback)
    {
        Root = root; Title = title; Callback = callback;
    }

    /// <summary>
    /// Open the dropdown anchored below <paramref name="anchor"/>, using the render mode set on the builder.
    /// Pass <c>element.worldBound</c> directly from a UI Toolkit element.
    /// </summary>
    public void Show(Rect anchor) => Show(anchor, RenderMode);

    /// <summary>
    /// Open the dropdown anchored below <paramref name="anchor"/>, overriding the render mode for this call.
    /// </summary>
    public void Show(Rect anchor, DropdownRenderMode mode)
    {
        var screenRect = DropdownRendererFactory.ToScreenRect(anchor);
        DropdownRendererFactory.Create(mode).Show(screenRect, this);
    }
}

// ── Internal tree ─────────────────────────────────────────────────────────────

internal sealed class DropdownNode
{
    public string             Label;
    public string             FullPath;   // full slash-separated path from root, used for search
    public Texture2D          Icon;
    public string             Tooltip;
    public string             RightText;  // optional right-justified secondary label
    public int                Index    = -1;   // -1 = folder
    public List<DropdownNode> Children = new();
    public DropdownNode       Parent;
    public bool               IsCreate;        // synthetic "create new" row (not a real entry)

    public bool IsLeaf   => Index >= 0;
    public bool IsFolder => !IsLeaf && !IsCreate;
}

// ── Popup window ──────────────────────────────────────────────────────────────

internal sealed class DropdownWindow : EditorWindow
{
    // ── Factory ───────────────────────────────────────────────────────────────

    internal static void Open(Rect screenRect, BuiltDropdown data)
    {
        var win       = CreateInstance<DropdownWindow>();
        win.hideFlags = HideFlags.DontSave;
        win._root     = data.Root;
        win._current  = data.Root;
        win._title    = data.Title;
        win._callback = data.Callback;
        win._onCreate          = data.OnCreate;
        win._createLabelFormat = data.CreateLabelFormat;
        win._onItemContext     = data.OnItemContext;
        win._showTitle         = data.ShowTitleBar;
        win.ShowAsDropDown(screenRect, new Vector2(Mathf.Max(screenRect.width, 260), 340));
    }

    // ── State ─────────────────────────────────────────────────────────────────

    private DropdownNode       _root;
    private DropdownNode       _current;
    private string             _title;
    private Action<int>        _callback;
    private string             _search  = "";
    private List<DropdownNode> _display = new();
    private Action<string>     _onCreate;
    private string             _createLabelFormat = "＋ Create \"{0}\"";
    private Action<int>        _onItemContext;
    private bool               _searchFocused = true;
    private bool               _refreshingSelection;
    private bool               _showTitle;

    // ── UI refs ───────────────────────────────────────────────────────────────

    private VisualElement _header;
    private Label         _titleLabel;
    private TextField     _searchField;
    private ListView      _listView;
    private Button        _backButton;

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void CreateGUI()
    {
        var root = rootVisualElement;
        DropdownTheme.ApplyPanel(root);

        root.Add(BuildHeader());
        root.Add(BuildSearchBar());
        root.Add(BuildList());

        RefreshDisplay();
        UpdateHeader();

        // Focus search after first layout pass
        root.schedule.Execute(() => _searchField.Focus()).StartingIn(50);

        root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
        root.RegisterCallback<NavigationMoveEvent>(ev => ev.StopPropagation(), TrickleDown.TrickleDown);
        root.RegisterCallback<NavigationSubmitEvent>(ev => ev.StopPropagation(), TrickleDown.TrickleDown);
    }

    // ── Header ────────────────────────────────────────────────────────────────

    private VisualElement BuildHeader()
    {
        var header = new VisualElement();
        header.AddToClassList("dropdown-header");
        _header = header;

        _backButton = new Button(GoBack) { text = "‹" };
        _backButton.AddToClassList("dropdown-back");
        _backButton.style.display = DisplayStyle.None;

        _titleLabel = new Label(_title ?? string.Empty);
        _titleLabel.AddToClassList("dropdown-title");

        header.Add(_backButton);
        header.Add(_titleLabel);
        return header;
    }

    // ── Search bar ────────────────────────────────────────────────────────────

    private VisualElement BuildSearchBar()
    {
        var bar = new VisualElement();
        bar.AddToClassList("dropdown-searchbar");

        _searchField = new TextField();
        _searchField.style.flexGrow = 1;
        _searchField.RegisterCallback<FocusInEvent>(_  => _searchFocused = true);
        _searchField.RegisterCallback<FocusOutEvent>(_ => _searchFocused = false);
        _searchField.RegisterValueChangedCallback(e =>
        {
            _search = e.newValue;
            RefreshDisplay();
        });

        var placeholder = new Label("Search...");
        placeholder.AddToClassList("dropdown-placeholder");
        placeholder.pickingMode = PickingMode.Ignore;
        _searchField.RegisterValueChangedCallback(e =>
            placeholder.style.display = string.IsNullOrEmpty(e.newValue) ? DisplayStyle.Flex : DisplayStyle.None);

        bar.Add(_searchField);
        bar.Add(placeholder);
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

        var iconImg = new Image { name = "icon" };
        iconImg.AddToClassList("dropdown-icon");

        var label = new Label { name = "label" };
        label.AddToClassList("dropdown-label");

        var baseLabel = new Label { name = "base" };
        baseLabel.AddToClassList("dropdown-right");
        baseLabel.style.display = DisplayStyle.None;

        var arrow = new Label("›") { name = "arrow" };
        arrow.AddToClassList("dropdown-arrow");
        arrow.style.display = DisplayStyle.None;

        row.RegisterCallback<PointerDownEvent>(e =>
        {
            if (!(row.userData is int idx) || idx < 0 || idx >= _display.Count) return;
            var node = _display[idx];

            if (e.button == 1)
            {
                if (node.IsLeaf && _onItemContext != null) { _onItemContext(node.Index); e.StopPropagation(); }
                return;
            }

            if (e.button == 0) OnItemClicked(node);
        });

        row.Add(iconImg);
        row.Add(label);
        row.Add(baseLabel);
        row.Add(arrow);
        return row;
    }

    private void BindRow(VisualElement row, int index)
    {
        row.userData = index;

        var node    = _display[index];
        var iconImg = row.Q<Image>("icon");
        var label   = row.Q<Label>("label");
        var arrow   = row.Q<Label>("arrow");
        var baseLbl = row.Q<Label>("base");

        label.text  = !string.IsNullOrWhiteSpace(_search) && node.FullPath != null
            ? node.FullPath
            : node.Label;
        row.tooltip = node.Tooltip ?? string.Empty;

        iconImg.image         = node.Icon;
        iconImg.style.display = node.Icon != null ? DisplayStyle.Flex : DisplayStyle.None;

        arrow.style.display = node.IsFolder ? DisplayStyle.Flex : DisplayStyle.None;

        bool showBase = node.IsLeaf && !string.IsNullOrEmpty(node.RightText);
        baseLbl.text          = showBase ? node.RightText : string.Empty;
        baseLbl.style.display = showBase ? DisplayStyle.Flex : DisplayStyle.None;

        row.EnableInClassList("dropdown-row--selected", index == _listView.selectedIndex);
    }

    // ── Navigation ────────────────────────────────────────────────────────────

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

        if (node.IsFolder)
        {
            _current = node;
            _search  = "";
            _searchField.SetValueWithoutNotify("");
            RefreshDisplay();
            UpdateHeader();
        }
        else
        {
            _callback?.Invoke(node.Index);
            Close();
        }
    }

    private void GoBack()
    {
        if (_current.Parent == null) return;
        var from  = _current;
        _current  = _current.Parent;
        _search   = "";
        _searchField.SetValueWithoutNotify("");
        RefreshDisplay();
        int idx = _display.IndexOf(from);
        if (idx >= 0) { _listView.selectedIndex = idx; _listView.ScrollToItem(idx); }
        UpdateHeader();
    }

    private void RefreshDisplay()
    {
        _display.Clear();

        if (string.IsNullOrWhiteSpace(_search))
        {
            _display.AddRange(_current.Children);
        }
        else
        {
            var query  = _search.ToLowerInvariant();
            var scored = new List<(DropdownNode node, int score)>();
            DropdownSearch.CollectScoredLeaves(_root, query, scored);
            scored.Sort((a, b) => b.score.CompareTo(a.score));
            foreach (var (node, _) in scored)
                _display.Add(node);

            // Offer "create new" unless a leaf already matches the typed name exactly.
            if (_onCreate != null)
            {
                var typed = _search.Trim();
                bool hasExact = _display.Any(n => n.IsLeaf && string.Equals(n.Label, typed, StringComparison.OrdinalIgnoreCase));
                if (typed.Length > 0 && !hasExact)
                    _display.Add(new DropdownNode { Label = string.Format(_createLabelFormat, typed), IsCreate = true });
            }
        }

        _listView.itemsSource = _display;
        _listView.Rebuild();
        _listView.ClearSelection();

        if (_display.Count > 0)
            _listView.selectedIndex = 0;
    }

    private void UpdateHeader()
    {
        bool atRoot   = _current.Parent == null;
        bool needBack = !atRoot;

        _header.style.display     = _showTitle || needBack ? DisplayStyle.Flex : DisplayStyle.None;
        _backButton.style.display = needBack ? DisplayStyle.Flex : DisplayStyle.None;
        _titleLabel.style.display = _showTitle ? DisplayStyle.Flex : DisplayStyle.None;
        _titleLabel.text          = atRoot ? (_title ?? string.Empty) : _current.Label;
    }

    // ── Keyboard ──────────────────────────────────────────────────────────────

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
                    int i = _listView.selectedIndex;
                    if (i >= 0 && i < _display.Count) OnItemClicked(_display[i]);
                    e.StopPropagation();
                    return;
                }
            }
            return;
        }

        switch (e.keyCode)
        {
            case KeyCode.Backspace when string.IsNullOrEmpty(_search) && _current.Parent != null:
            case KeyCode.LeftArrow when string.IsNullOrEmpty(_search) && _current.Parent != null:
                GoBack();
                e.StopPropagation();
                return;

            case KeyCode.RightArrow when string.IsNullOrEmpty(_search):
            {
                int i = _listView.selectedIndex;
                if (i >= 0 && i < _display.Count && _display[i].IsFolder) OnItemClicked(_display[i]);
                e.StopPropagation();
                return;
            }

            case KeyCode.UpArrow:
            {
                int next = Mathf.Max(0, _listView.selectedIndex - 1);
                if (_listView.selectedIndex < 0 && _display.Count > 0) next = 0;
                _listView.selectedIndex = next;
                _listView.ScrollToItem(next);
                e.StopPropagation();
                return;
            }

            case KeyCode.DownArrow:
            {
                int cur  = _listView.selectedIndex;
                int next = cur < _display.Count - 1 ? cur + 1 : cur;
                if (cur < 0 && _display.Count > 0) next = 0;
                _listView.selectedIndex = next;
                _listView.ScrollToItem(next);
                e.StopPropagation();
                return;
            }

            case KeyCode.Return:
            case KeyCode.KeypadEnter:
            {
                int i = _listView.selectedIndex;
                if (i >= 0 && i < _display.Count) OnItemClicked(_display[i]);
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
        if (_display.Count == 0) return;
        int sel  = _listView.selectedIndex;
        int next = sel < 0 ? 0 : Mathf.Min(sel + 1, _display.Count - 1);
        _listView.selectedIndex = next;
        _listView.ScrollToItem(next);
        _listView.Focus();
    }
}