using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

internal sealed class ScriptableObjectKeyDropdown : EditorWindow
{
    // null entry in _display = the "✦ Create" sentinel
    private Type _keyType;
    private SerializedProperty _property;
    private Action _onChanged;
    private string _search = "";
    private List<ScriptableObject> _allAssets = new();
    private readonly List<ScriptableObject> _display = new();

    private TextField _searchField;
    private ListView _listView;

    static readonly Color C_BG      = new(0.18f, 0.18f, 0.18f);
    static readonly Color C_HEADER  = new(0.13f, 0.13f, 0.13f);
    static readonly Color C_BORDER  = new(0.09f, 0.09f, 0.09f);
    static readonly Color C_ROW_ALT = new(0f, 0f, 0f, 0.06f);
    static readonly Color C_HOVER   = new(0.28f, 0.28f, 0.28f);
    static readonly Color C_TEXT    = new(0.85f, 0.85f, 0.85f);
    static readonly Color C_SUBTEXT = new(0.50f, 0.50f, 0.50f);
    static readonly Color C_CREATE  = new(0.35f, 0.85f, 0.45f);

    internal static void Show(Rect worldBound, Type keyType, SerializedProperty property, Action onChanged)
    {
        var screenRect = worldBound;
        if (focusedWindow != null)
        {
            screenRect.x += focusedWindow.position.x;
            screenRect.y += focusedWindow.position.y;
        }

        var win        = CreateInstance<ScriptableObjectKeyDropdown>();
        win.hideFlags  = HideFlags.DontSave;
        win._keyType   = keyType;
        win._property  = property.Copy();
        win._onChanged = onChanged;
        win.ShowAsDropDown(screenRect, new Vector2(Mathf.Max(screenRect.width, 260), 300));
    }

    private void CreateGUI()
    {
        _allAssets = AssetDatabase
            .FindAssets($"t:{_keyType.Name}")
            .Select(g => AssetDatabase.LoadAssetAtPath(AssetDatabase.GUIDToAssetPath(g), _keyType) as ScriptableObject)
            .Where(a => a != null)
            .OrderBy(a => a.name)
            .ToList();

        var root = rootVisualElement;
        root.style.flexDirection   = FlexDirection.Column;
        root.style.flexGrow        = 1;
        root.style.backgroundColor = C_BG;

        root.Add(BuildHeader());
        root.Add(BuildSearchBar());
        root.Add(BuildList());

        Refresh();

        root.schedule.Execute(() => _searchField.Focus()).StartingIn(50);
        root.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);
    }

    private VisualElement BuildHeader()
    {
        var header = new VisualElement();
        header.style.flexDirection     = FlexDirection.Row;
        header.style.alignItems        = Align.Center;
        header.style.minHeight         = 28;
        header.style.paddingLeft       = 8;
        header.style.paddingRight      = 8;
        header.style.paddingTop        = 4;
        header.style.paddingBottom     = 4;
        header.style.backgroundColor   = C_HEADER;
        header.style.borderBottomWidth = 1;
        header.style.borderBottomColor = C_BORDER;

        var title = new Label($"Select {_keyType?.Name ?? "Key"}");
        title.style.flexGrow                = 1;
        title.style.fontSize                = 11;
        title.style.color                   = C_TEXT;
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.unityTextAlign          = TextAnchor.MiddleLeft;
        header.Add(title);
        return header;
    }

    private VisualElement BuildSearchBar()
    {
        var bar = new VisualElement();
        bar.style.paddingLeft       = 6;
        bar.style.paddingRight      = 6;
        bar.style.paddingTop        = 5;
        bar.style.paddingBottom     = 5;
        bar.style.backgroundColor   = C_BG;
        bar.style.borderBottomWidth = 1;
        bar.style.borderBottomColor = C_BORDER;

        _searchField = new TextField();
        _searchField.style.flexGrow = 1;

        _searchField.RegisterCallbackOnce<AttachToPanelEvent>(_ =>
        {
            var input = _searchField.Q(className: "unity-base-field__input");
            if (input == null) return;
            input.style.backgroundColor = new Color(0.13f, 0.13f, 0.13f);
            input.style.color           = C_TEXT;
            input.style.borderTopWidth  = input.style.borderRightWidth  =
                input.style.borderBottomWidth = input.style.borderLeftWidth = 1;
            input.style.borderTopColor  = input.style.borderRightColor  =
                input.style.borderBottomColor = input.style.borderLeftColor = C_BORDER;
            input.style.borderTopLeftRadius     = input.style.borderTopRightRadius   =
                input.style.borderBottomLeftRadius = input.style.borderBottomRightRadius = 4;
        });

        _searchField.RegisterValueChangedCallback(e =>
        {
            _search = e.newValue;
            Refresh();
        });

        var placeholder = new Label("Search or type name to create...");
        placeholder.style.position       = Position.Absolute;
        placeholder.style.left           = 10;
        placeholder.style.top            = 0;
        placeholder.style.bottom         = 0;
        placeholder.style.fontSize       = 11;
        placeholder.style.color          = C_SUBTEXT;
        placeholder.style.unityTextAlign = TextAnchor.MiddleLeft;
        placeholder.pickingMode          = PickingMode.Ignore;

        _searchField.RegisterValueChangedCallback(e =>
            placeholder.style.display = string.IsNullOrEmpty(e.newValue)
                ? DisplayStyle.Flex
                : DisplayStyle.None);

        bar.Add(_searchField);
        bar.Add(placeholder);
        return bar;
    }

    private VisualElement BuildList()
    {
        _listView = new ListView
        {
            fixedItemHeight = 26,
            selectionType   = SelectionType.Single,
            makeItem        = MakeRow,
            bindItem        = BindRow,
        };
        _listView.selectionChanged += _ => _listView.RefreshItems();
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

        var label = new Label { name = "label" };
        label.style.flexGrow       = 1;
        label.style.fontSize       = 11;
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        row.Add(label);

        row.RegisterCallback<PointerEnterEvent>(_ => row.style.backgroundColor = C_HOVER);
        row.RegisterCallback<PointerLeaveEvent>(_ =>
        {
            if (row.userData is int idx)
                row.style.backgroundColor = idx % 2 == 0 ? new Color(0, 0, 0, 0) : C_ROW_ALT;
        });
        row.RegisterCallback<PointerDownEvent>(e =>
        {
            if (e.button == 0 && row.userData is int idx && idx >= 0 && idx < _display.Count)
                OnItemClicked(idx);
        });

        return row;
    }

    private void BindRow(VisualElement row, int index)
    {
        row.userData = index;
        var label    = row.Q<Label>("label");
        var item     = _display[index];

        bool isSelected = index == _listView.selectedIndex;
        row.style.backgroundColor = isSelected
            ? C_HOVER
            : (index % 2 == 0 ? new Color(0, 0, 0, 0) : C_ROW_ALT);

        if (item == null)
        {
            label.text                          = $"✦  Create  \"{_search}\"";
            label.style.color                   = C_CREATE;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
        }
        else
        {
            label.text                          = item.name;
            label.style.color                   = C_TEXT;
            label.style.unityFontStyleAndWeight = FontStyle.Normal;
        }
    }

    private void Refresh()
    {
        _display.Clear();

        var filtered = string.IsNullOrWhiteSpace(_search)
            ? _allAssets
            : _allAssets
                .Where(a => a.name.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0)
                .ToList();

        if (!string.IsNullOrWhiteSpace(_search))
            _display.Add(null); // create sentinel

        _display.AddRange(filtered);

        _listView.itemsSource = _display;
        _listView.Rebuild();

        if (_display.Count > 0)
            _listView.selectedIndex = 0;
    }

    private void OnItemClicked(int index)
    {
        if (_display[index] == null)
            CreateAndAssign(_search);
        else
            Assign(_display[index]);
    }

    private void Assign(ScriptableObject asset)
    {
        _property.objectReferenceValue = asset;
        _property.serializedObject.ApplyModifiedProperties();
        _onChanged?.Invoke();
        Close();
    }

    private void CreateAndAssign(string assetName)
    {
        var folder = ScriptableObjectKeySettings.instance.GetFolderForType(_keyType);
        EnsureFolder(folder);

        var path  = AssetDatabase.GenerateUniqueAssetPath($"{folder}/{assetName}.asset");
        var asset = CreateInstance(_keyType);
        AssetDatabase.CreateAsset(asset, path);
        AssetDatabase.SaveAssets();

        _property.objectReferenceValue = asset;
        _property.serializedObject.ApplyModifiedProperties();
        _onChanged?.Invoke();
        Close();
    }

    private static void EnsureFolder(string folderPath)
    {
        var parts   = folderPath.Split('/');
        var current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
                AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    private void OnKeyDown(KeyDownEvent e)
    {
        switch (e.keyCode)
        {
            case KeyCode.Escape:
                Close();
                e.StopPropagation();
                return;

            case KeyCode.UpArrow:
            {
                var next = Mathf.Max(0, _listView.selectedIndex - 1);
                _listView.selectedIndex = next;
                _listView.ScrollToItem(next);
                e.StopPropagation();
                return;
            }

            case KeyCode.DownArrow:
            {
                var cur  = _listView.selectedIndex;
                var next = cur < _display.Count - 1 ? cur + 1 : cur;
                if (cur < 0 && _display.Count > 0) next = 0;
                _listView.selectedIndex = next;
                _listView.ScrollToItem(next);
                e.StopPropagation();
                return;
            }

            case KeyCode.Return:
            case KeyCode.KeypadEnter:
            {
                var i = _listView.selectedIndex;
                if (i >= 0 && i < _display.Count)
                    OnItemClicked(i);
                else if (!string.IsNullOrWhiteSpace(_search))
                    CreateAndAssign(_search);
                e.StopPropagation();
                return;
            }
        }
    }
}
