using UnityEditor;
using UnityEngine.UIElements;

internal static class DropdownTheme
{
    private const string StyleSheetPath =
        "Packages/com.felipe-ishimine.selector-attributes/Scripts/Editor/DropdownStyles.uss";

    public static void ApplyPanel(VisualElement root)
    {
        var sheet = AssetDatabase.LoadAssetAtPath<StyleSheet>(StyleSheetPath);
        if (sheet == null)
            throw new System.InvalidOperationException($"Dropdown stylesheet not found at {StyleSheetPath}");
        root.styleSheets.Add(sheet);
        root.AddToClassList("dropdown-panel");
    }
}
