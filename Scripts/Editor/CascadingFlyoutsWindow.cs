using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;
using static DropdownTheme;

internal sealed class CascadingFlyoutsRenderer : IDropdownRenderer
{
    public void Show(Rect screenRect, BuiltDropdown data) => FlyoutChain.Open(screenRect, data);
}

internal sealed class FlyoutChain
{
    private const float PanelWidth = 240f;
    private const float RowHeight  = 28f;
    private const float MaxHeight  = 340f;

    private readonly BuiltDropdown      _data;
    private readonly Rect               _rootAnchor;
    private readonly List<FlyoutWindow> _levels = new();

    private bool _disposed;

    internal BuiltDropdown Data       => _data;
    internal bool          IsDisposed => _disposed;

    internal static void Open(Rect screenRect, BuiltDropdown data)
    {
        var chain = new FlyoutChain(screenRect, data);
        var rect  = new Rect(screenRect.x, screenRect.yMax, Mathf.Max(screenRect.width, PanelWidth), 0);
        chain.OpenLevel(0, data.Root, rect, isRoot: true);
    }

    private FlyoutChain(Rect rootAnchor, BuiltDropdown data)
    {
        _rootAnchor = rootAnchor;
        _data       = data;
    }

    private void OpenLevel(int level, DropdownNode owner, Rect rect, bool isRoot)
    {
        var win = ScriptableObject.CreateInstance<FlyoutWindow>();
        win.hideFlags = HideFlags.DontSave;
        win.Init(this, level, owner, isRoot);

        int   count  = owner.Children.Count;
        float height = Mathf.Min(count * RowHeight + (isRoot ? 34f : 0f) + 8f, MaxHeight);
        rect.height  = Mathf.Max(height, RowHeight + 8f);

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

        OpenLevel(parentLevel + 1, folder, new Rect(x, y, PanelWidth, height), isRoot: false);
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

    internal void FallbackToSearch()
    {
        var data = _data;
        var rect = _rootAnchor;
        CloseAll();
        new SearchDrilldownRenderer().Show(rect, data);
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
    private bool         _isRoot;

    private IVisualElementScheduledItem _pendingOpen;

    private ScrollView          _scroll;
    private TextField           _searchField;
    private readonly List<VisualElement> _rowEls = new();
    private readonly List<DropdownNode>  _nodes  = new();
    private int  _selected = -1;
    private bool _searchFocused;
    private bool _initialFocusDone;

    internal void Init(FlyoutChain chain, int level, DropdownNode owner, bool isRoot)
    {
        _chain  = chain;
        _level  = level;
        _owner  = owner;
        _isRoot = isRoot;
    }

    private void CreateGUI()
    {
        var root = rootVisualElement;
        root.style.flexDirection   = FlexDirection.Column;
        root.style.flexGrow        = 1;
        root.style.backgroundColor = C_BG;
        root.style.borderTopWidth  = root.style.borderRightWidth =
            root.style.borderBottomWidth = root.style.borderLeftWidth = 1;
        root.style.borderTopColor  = root.style.borderRightColor =
            root.style.borderBottomColor = root.style.borderLeftColor = C_BORDER;

        root.focusable = true;
        root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

        if (_isRoot)
            root.Add(BuildSearchBar());

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

        if (_nodes.Count > 0) SetSelected(0);

        FocusInner();
    }

    private void OnFocus() => FocusInner();

    private void FocusInner()
    {
        var root = rootVisualElement;
        if (root == null) return;
        root.schedule.Execute(() =>
        {
            if (_isRoot && !_initialFocusDone && _searchField != null)
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
        _searchField.RegisterValueChangedCallback(e =>
        {
            if (!string.IsNullOrEmpty(e.newValue)) _chain.FallbackToSearch();
        });

        var placeholder = new Label("Type to search...");
        placeholder.style.position       = Position.Absolute;
        placeholder.style.left           = 10;
        placeholder.style.top            = 0;
        placeholder.style.bottom         = 0;
        placeholder.style.fontSize       = 12;
        placeholder.style.color          = C_SUBTEXT;
        placeholder.style.unityTextAlign = TextAnchor.MiddleLeft;
        placeholder.pickingMode          = PickingMode.Ignore;

        bar.Add(_searchField);
        bar.Add(placeholder);
        return bar;
    }

    private VisualElement BuildRow(DropdownNode node, int index)
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems    = Align.Center;
        row.style.minHeight     = 28;
        row.style.paddingLeft   = 10;
        row.style.paddingRight  = 8;

        var iconImg = new Image();
        iconImg.style.width       = 16;
        iconImg.style.height      = 16;
        iconImg.style.marginRight = 6;
        iconImg.style.flexShrink  = 0;
        iconImg.image             = node.Icon;
        iconImg.style.display     = node.Icon != null ? DisplayStyle.Flex : DisplayStyle.None;

        var label = new Label(node.Label);
        label.style.flexGrow       = 1;
        label.style.fontSize       = 12;
        label.style.color          = C_TEXT;
        label.style.unityTextAlign = TextAnchor.MiddleLeft;

        var baseLabel = new Label();
        baseLabel.style.fontSize       = 9;
        baseLabel.style.color          = C_RIGHT;
        baseLabel.style.unityTextAlign = TextAnchor.MiddleRight;
        baseLabel.style.marginLeft     = 8;
        baseLabel.style.flexShrink     = 0;
        bool showBase = node.IsLeaf && !string.IsNullOrEmpty(node.RightText);
        baseLabel.text          = showBase ? node.RightText : string.Empty;
        baseLabel.style.display = showBase ? DisplayStyle.Flex : DisplayStyle.None;

        var arrow = new Label("›");
        arrow.style.fontSize = 14;
        arrow.style.color    = C_SUBTEXT;
        arrow.style.display  = node.IsFolder ? DisplayStyle.Flex : DisplayStyle.None;

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
            _rowEls[i].style.backgroundColor = i == _selected ? C_HOVER : C_TRANSPARENT;
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

        if (_searchFocused)
        {
            switch (e.keyCode)
            {
                case KeyCode.DownArrow:
                    EnterListZone();
                    e.StopPropagation();
                    return;

                case KeyCode.UpArrow:
                    e.StopPropagation();
                    return;

                case KeyCode.Return:
                case KeyCode.KeypadEnter:
                    ActivateSelected();
                    e.StopPropagation();
                    return;
            }
            return;
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
                ActivateSelected();
                e.StopPropagation();
                return;

            case KeyCode.LeftArrow:
            case KeyCode.Backspace:
                if (_level > 0) _chain.CloseDeeperThan(_level - 1);
                e.StopPropagation();
                return;

            default:
                if (DropdownKeys.IsTypingChar(e))
                {
                    if (_isRoot && _searchField != null)
                    {
                        _searchField.value += e.character;
                        _searchField.Focus();
                    }
                    else
                    {
                        _chain.FallbackToSearch();
                    }
                    e.StopPropagation();
                }
                return;
        }
    }

    private void ActivateSelected()
    {
        var node = SelectedNode();
        if (node == null) return;
        if (node.IsLeaf) _chain.SelectLeaf(node.Index);
        else             _chain.OpenChild(_level, node, RowScreenRect(_rowEls[_selected]));
    }

    private void EnterListZone()
    {
        if (_nodes.Count == 0) return;
        SetSelected(_selected < 0 ? 0 : _selected + 1);
        rootVisualElement.Focus();
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
