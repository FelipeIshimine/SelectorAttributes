using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public enum DropdownRenderMode
{
    SearchDrilldown,
    MillerColumns,
    Accordion,
    CascadingFlyouts,
}

internal interface IDropdownRenderer
{
    void Show(Rect screenRect, BuiltDropdown data);
}

internal static class DropdownKeys
{
    public static bool IsTypingChar(KeyDownEvent e)
    {
        char c = e.character;
        if (c == '\0' || c < ' ' || c == (char)127) return false;
        if (e.ctrlKey || e.commandKey || e.altKey) return false;
        return true;
    }
}

internal static class DropdownRendererFactory
{
    public static IDropdownRenderer Create(DropdownRenderMode mode) => mode switch
    {
        DropdownRenderMode.SearchDrilldown => new SearchDrilldownRenderer(),
        DropdownRenderMode.MillerColumns   => new MillerColumnsRenderer(),
        DropdownRenderMode.Accordion       => new AccordionRenderer(),
        DropdownRenderMode.CascadingFlyouts => new CascadingFlyoutsRenderer(),
        _ => throw new NotImplementedException($"Dropdown render mode '{mode}' is not implemented yet."),
    };

    public static Rect ToScreenRect(Rect worldBound)
    {
        var screenRect = worldBound;
        if (EditorWindow.focusedWindow != null)
        {
            screenRect.x += EditorWindow.focusedWindow.position.x;
            screenRect.y += EditorWindow.focusedWindow.position.y;
        }
        return screenRect;
    }
}

internal sealed class SearchDrilldownRenderer : IDropdownRenderer
{
    public void Show(Rect screenRect, BuiltDropdown data) => DropdownWindow.Open(screenRect, data);
}
