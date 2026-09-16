using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

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

        int   count  = owner.Children.Count;
        float height = Mathf.Min(count * RowHeight + 8f, MaxHeight);
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
        root.schedule.Execute(() => root.Focus()).StartingIn(0);
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
        }
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
