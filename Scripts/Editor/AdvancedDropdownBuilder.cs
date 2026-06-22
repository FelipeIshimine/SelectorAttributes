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

    public AdvancedDropdownPath(string path, Texture2D icon, string tooltip = null)
    {
        Path    = path;
        Icon    = icon;
        Tooltip = tooltip;
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

    public AdvancedDropdownBuilder WithTitle(string title)      { _title     = title; return this; }
    public AdvancedDropdownBuilder SetSplitCharacter(char c)    { _splitChar = c;     return this; }
    public AdvancedDropdownBuilder SetCallback(Action<int> cb)  { _callback  = cb;    return this; }

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
                        Label    = seg,
                        FullPath = _values[i].Path,
                        Icon     = _values[i].Icon,
                        Tooltip  = _values[i].Tooltip,
                        Index    = i,
                        Parent   = parent,
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
        };
    }
}

// ── Returned by Build() ───────────────────────────────────────────────────────

public sealed class BuiltDropdown
{
    internal readonly DropdownNode Root;
    internal readonly string       Title;
    internal readonly Action<int>  Callback;

    internal Action<string> OnCreate;
    internal string         CreateLabelFormat = "＋ Create \"{0}\"";
    internal Action<int>    OnItemContext;

    internal BuiltDropdown(DropdownNode root, string title, Action<int> callback)
    {
        Root = root; Title = title; Callback = callback;
    }

    /// <summary>
    /// Open the dropdown anchored below <paramref name="anchor"/>.
    /// Pass <c>element.worldBound</c> directly from a UI Toolkit element.
    /// </summary>
    public void Show(Rect anchor) => DropdownWindow.Open(anchor, this);
}

// ── Internal tree ─────────────────────────────────────────────────────────────

internal sealed class DropdownNode
{
    public string             Label;
    public string             FullPath;   // full slash-separated path from root, used for search
    public Texture2D          Icon;
    public string             Tooltip;
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

    internal static void Open(Rect worldBound, BuiltDropdown data)
    {
        // worldBound is in EditorWindow-local space; convert to screen space.
        var screenRect = worldBound;
        if (focusedWindow != null)
        {
            screenRect.x += focusedWindow.position.x;
            screenRect.y += focusedWindow.position.y;
        }

        var win       = CreateInstance<DropdownWindow>();
        win.hideFlags = HideFlags.DontSave;
        win._root     = data.Root;
        win._current  = data.Root;
        win._title    = data.Title;
        win._callback = data.Callback;
        win._onCreate          = data.OnCreate;
        win._createLabelFormat = data.CreateLabelFormat;
        win._onItemContext     = data.OnItemContext;
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

    // ── UI refs ───────────────────────────────────────────────────────────────

    private Label     _titleLabel;
    private TextField _searchField;
    private ListView  _listView;
    private Button    _backButton;

    // ── Theme ─────────────────────────────────────────────────────────────────

    static readonly Color C_BG      = new(0.18f, 0.18f, 0.18f);
    static readonly Color C_HEADER  = new(0.13f, 0.13f, 0.13f);
    static readonly Color C_BORDER  = new(0.09f, 0.09f, 0.09f);
    static readonly Color C_ROW_ALT = new(0.00f, 0.00f, 0.00f, 0.06f);
    static readonly Color C_HOVER   = new(0.28f, 0.28f, 0.28f);
    static readonly Color C_TEXT    = new(0.85f, 0.85f, 0.85f);
    static readonly Color C_SUBTEXT = new(0.50f, 0.50f, 0.50f);
    static readonly Color C_ACCENT  = new(0.25f, 0.49f, 0.96f);

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    private void CreateGUI()
    {
        var root = rootVisualElement;
        root.style.flexDirection   = FlexDirection.Column;
        root.style.flexGrow        = 1;
        root.style.backgroundColor = C_BG;

        root.Add(BuildHeader());
        root.Add(BuildSearchBar());
        root.Add(BuildList());

        RefreshDisplay();

        // Focus search after first layout pass
        root.schedule.Execute(() => _searchField.Focus()).StartingIn(50);

        root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
    }

    // ── Header ────────────────────────────────────────────────────────────────

    private VisualElement BuildHeader()
    {
        var header = new VisualElement();
        header.style.flexDirection     = FlexDirection.Row;
        header.style.alignItems        = Align.Center;
        header.style.minHeight         = 30;
        header.style.paddingLeft       = 4;
        header.style.paddingRight      = 8;
        header.style.paddingTop        = 4;
        header.style.paddingBottom     = 4;
        header.style.backgroundColor   = C_HEADER;
        header.style.borderBottomWidth = 1;
        header.style.borderBottomColor = C_BORDER;

        _backButton = new Button(GoBack) { text = "‹" };
        _backButton.style.display         = DisplayStyle.None;
        _backButton.style.width           = 24;
        _backButton.style.height          = 24;
        _backButton.style.fontSize        = 18;
        _backButton.style.paddingLeft     = 0; _backButton.style.paddingRight  = 0;
        _backButton.style.paddingTop      = 0; _backButton.style.paddingBottom = 0;
        _backButton.style.marginRight     = 2;
        _backButton.style.backgroundColor = new Color(0, 0, 0, 0);
        _backButton.style.color           = C_ACCENT;
        _backButton.style.borderTopWidth  = _backButton.style.borderRightWidth =
            _backButton.style.borderBottomWidth = _backButton.style.borderLeftWidth = 0;
        SetBorderRadius(_backButton.style, 4);
        _backButton.style.unityTextAlign  = TextAnchor.MiddleCenter;

        _backButton.RegisterCallback<PointerEnterEvent>(_ =>
            _backButton.style.backgroundColor = new Color(C_ACCENT.r, C_ACCENT.g, C_ACCENT.b, 0.15f));
        _backButton.RegisterCallback<PointerLeaveEvent>(_ =>
            _backButton.style.backgroundColor = new Color(0, 0, 0, 0));

        _titleLabel = new Label(_title ?? string.Empty);
        _titleLabel.style.flexGrow                = 1;
        _titleLabel.style.fontSize                = 12;
        _titleLabel.style.color                   = C_TEXT;
        _titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        _titleLabel.style.unityTextAlign          = TextAnchor.MiddleCenter;

        header.Add(_backButton);
        header.Add(_titleLabel);
        return header;
    }

    // ── Search bar ────────────────────────────────────────────────────────────

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

        _searchField.RegisterCallbackOnce<AttachToPanelEvent>(_ =>
        {
            var input = _searchField.Q(className: "unity-base-field__input");
            if (input == null) return;
            input.style.backgroundColor   = new Color(0.13f, 0.13f, 0.13f);
            input.style.color             = C_TEXT;
            input.style.borderTopWidth    = input.style.borderRightWidth =
                input.style.borderBottomWidth = input.style.borderLeftWidth = 1;
            input.style.borderTopColor    = input.style.borderRightColor =
                input.style.borderBottomColor = input.style.borderLeftColor = C_BORDER;
            SetBorderRadius(input.style, 4);
        });

        _searchField.RegisterValueChangedCallback(e =>
        {
            _search = e.newValue;
            RefreshDisplay();
        });

        // Placeholder
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
            placeholder.style.display = string.IsNullOrEmpty(e.newValue)
                ? DisplayStyle.Flex : DisplayStyle.None);

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
        _listView.selectionChanged += _ =>_listView.RefreshItems();
        _listView.style.flexGrow = 1;
        return _listView;
    }

    private VisualElement MakeRow()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems    = Align.Center;
        row.style.paddingLeft   = 10;
        row.style.paddingRight  = 8;

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

        var arrow = new Label("›") { name = "arrow" };
        arrow.style.fontSize = 14;
        arrow.style.color    = C_SUBTEXT;
        arrow.style.display  = DisplayStyle.None;

        row.RegisterCallback<PointerEnterEvent>(_ =>
            row.style.backgroundColor = C_HOVER);
        row.RegisterCallback<PointerLeaveEvent>(_ =>
        {
            // Restore alternating tint on hover-out
            if (row.userData is int idx)
                row.style.backgroundColor = idx % 2 == 0
                    ? new Color(0, 0, 0, 0) : C_ROW_ALT;
        });

        row.RegisterCallback<PointerDownEvent>(e =>
        {
            if (!(row.userData is int idx) || idx < 0 || idx >= _display.Count) return;
            var node = _display[idx];

            if (e.button == 1)   // right-click → leaf context menu (if enabled)
            {
                if (node.IsLeaf && _onItemContext != null) { _onItemContext(node.Index); e.StopPropagation(); }
                return;
            }

            if (e.button == 0) OnItemClicked(node);
        });

        row.Add(iconImg);
        row.Add(label);
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

	    // In search mode show the full path so the user knows where the node lives.
	    label.text  = !string.IsNullOrWhiteSpace(_search) && node.FullPath != null
	        ? node.FullPath
	        : node.Label;
	    row.tooltip = node.Tooltip ?? string.Empty;

	    if (iconImg != null)
	    {
		    iconImg.image         = node.Icon;
		    iconImg.style.display = node.Icon != null ? DisplayStyle.Flex : DisplayStyle.None;
	    }

	    if (arrow != null)
		    arrow.style.display = node.IsFolder ? DisplayStyle.Flex : DisplayStyle.None;

	    bool isSelected = index == _listView.selectedIndex;

	    row.style.backgroundColor = isSelected
		    ? C_HOVER
		    : (index % 2 == 0 ? new Color(0, 0, 0, 0) : C_ROW_ALT);
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
        _current = _current.Parent;
        _search  = "";
        _searchField.SetValueWithoutNotify("");
        RefreshDisplay();
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
            CollectScoredLeaves(_root, query, scored);
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

    private static void CollectScoredLeaves(DropdownNode node, string query, List<(DropdownNode, int)> results)
    {
        foreach (var child in node.Children)
        {
            if (child.IsLeaf)
            {
                var score = FuzzyScore(child.FullPath ?? child.Label, query);
                if (score > 0) results.Add((child, score));
            }
            else
            {
                CollectScoredLeaves(child, query, results);
            }
        }
    }

    // Scores a full path against a query string.
    // Returns 0 for no match. Higher = better match.
    // Bonuses: exact substring > consecutive run > word-start > in-order subsequence.
    private static int FuzzyScore(string text, string query)
    {
        var lText  = text.ToLowerInvariant();

        // Exact substring anywhere in the full path: strong base score, shorter path wins ties.
        if (lText.Contains(query))
            return 10000 - text.Length;

        // Subsequence match with progressive bonuses.
        int score       = 0;
        int textIdx     = 0;
        int consecutive = 0;

        foreach (var qc in query)
        {
            bool matched = false;
            for (var i = textIdx; i < lText.Length; i++)
            {
                if (lText[i] != qc) { consecutive = 0; continue; }

                bool wordStart = i == 0 || lText[i - 1] == '/' || lText[i - 1] == ' ' || lText[i - 1] == '_';
                score += 1 + consecutive + (wordStart ? 8 : 0);
                consecutive++;
                textIdx = i + 1;
                matched = true;
                break;
            }

            if (!matched) return 0;
        }

        return score;
    }

    private void UpdateHeader()
    {
        bool atRoot               = _current.Parent == null;
        _backButton.style.display = atRoot ? DisplayStyle.None : DisplayStyle.Flex;
        _titleLabel.text          = atRoot ? (_title ?? string.Empty) : _current.Label;
    }

    // ── Keyboard ──────────────────────────────────────────────────────────────

    private void OnKeyDown(KeyDownEvent e)
    {
        switch (e.keyCode)
        {
            case KeyCode.Escape:
                Close();
                e.StopPropagation();
                return;

            // Backspace with empty search → go up one folder
            case KeyCode.Backspace when string.IsNullOrEmpty(_search) && _current.Parent != null:
                GoBack();
                e.StopPropagation();
                return;

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
        }
    }

    // ── Style helpers ─────────────────────────────────────────────────────────

    private static void SetBorderRadius(IStyle s, float r)
    {
        s.borderTopLeftRadius = s.borderTopRightRadius =
            s.borderBottomLeftRadius = s.borderBottomRightRadius = r;
    }
}