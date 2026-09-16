using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

internal sealed class DropdownRenderModeTester : EditorWindow
{
    [MenuItem("Tools/SelectorAttributes/Dropdown Render Mode Tester")]
    private static void ShowWindow()
    {
        var win = GetWindow<DropdownRenderModeTester>();
        win.titleContent = new GUIContent("Dropdown Modes");
        win.minSize = new Vector2(320, 220);
    }

    private static readonly (string path, string right)[] Sample =
    {
        ("Weapons/Melee/Sword",        "dmg 12"),
        ("Weapons/Melee/Axe",          "dmg 18"),
        ("Weapons/Melee/Spear",        "dmg 14"),
        ("Weapons/Ranged/Bow",         "dmg 9"),
        ("Weapons/Ranged/Crossbow",    "dmg 15"),
        ("Weapons/Ranged/Sling",       "dmg 5"),
        ("Consumables/Potions/Health", "+50 hp"),
        ("Consumables/Potions/Mana",   "+30 mp"),
        ("Consumables/Food/Bread",     "+8 hp"),
        ("Consumables/Food/Cheese",    "+12 hp"),
        ("Materials/Ore/Iron",         null),
        ("Materials/Ore/Gold",         null),
        ("Materials/Wood/Oak",         null),
        ("Materials/Wood/Pine",        null),
        ("Misc/Key",                   null),
        ("Misc/Map",                   null),
    };

    private Label  _result;
    private Toggle _showTitle;

    private void CreateGUI()
    {
        var root = rootVisualElement;
        root.style.paddingLeft = root.style.paddingRight =
            root.style.paddingTop = root.style.paddingBottom = 10;

        var title = new Label("Pick a render mode, then click Open");
        title.style.unityFontStyleAndWeight = FontStyle.Bold;
        title.style.marginBottom = 8;
        root.Add(title);

        _showTitle = new Toggle("Show title bar") { value = false };
        _showTitle.style.marginBottom = 8;
        root.Add(_showTitle);

        root.Add(ModeButton("Search Drilldown", DropdownRenderMode.SearchDrilldown));
        root.Add(ModeButton("Miller Columns",   DropdownRenderMode.MillerColumns));
        root.Add(ModeButton("Accordion",        DropdownRenderMode.Accordion));
        root.Add(ModeButton("Cascading Flyouts", DropdownRenderMode.CascadingFlyouts));

        _result = new Label("Last pick: (none)");
        _result.style.marginTop = 12;
        _result.style.color     = new Color(0.50f, 0.50f, 0.50f);
        root.Add(_result);
    }

    private Button ModeButton(string label, DropdownRenderMode mode)
    {
        var button = new Button { text = label };
        button.style.height     = 26;
        button.style.marginBottom = 4;
        button.clicked += () =>
        {
            var built = new AdvancedDropdownBuilder()
                .WithTitle($"Items — {mode}")
                .ShowTitle(_showTitle.value)
                .WithRenderMode(mode)
                .AddElements(BuildElements(), out var values)
                .SetCallback(i => _result.text = $"Last pick: {values[i]}  (via {mode})")
                .SetCreateOption(text => _result.text = $"Create requested: \"{text}\"  (via {mode})")
                .SetItemContextHandler(i => _result.text = $"Context on: {values[i]}  (via {mode})")
                .Build();

            built.Show(button.worldBound);
        };
        return button;
    }

    private static System.Collections.Generic.IEnumerable<(string path, string rightText, string value)> BuildElements()
    {
        foreach (var (path, right) in Sample)
            yield return (path, right, path);
    }
}
