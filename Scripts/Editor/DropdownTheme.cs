using UnityEngine;
using UnityEngine.UIElements;

internal static class DropdownTheme
{
    public static readonly Color C_BG      = new(0.18f, 0.18f, 0.18f);
    public static readonly Color C_HEADER  = new(0.13f, 0.13f, 0.13f);
    public static readonly Color C_BORDER  = new(0.09f, 0.09f, 0.09f);
    public static readonly Color C_ROW_ALT = new(0.00f, 0.00f, 0.00f, 0.06f);
    public static readonly Color C_HOVER   = new(0.28f, 0.28f, 0.28f);
    public static readonly Color C_TEXT    = new(0.85f, 0.85f, 0.85f);
    public static readonly Color C_SUBTEXT = new(0.50f, 0.50f, 0.50f);
    public static readonly Color C_ACCENT  = new(0.25f, 0.49f, 0.96f);
    public static readonly Color C_RIGHT   = new(0.498f, 0.839f, 0.910f);

    public static readonly Color C_TRANSPARENT = new(0, 0, 0, 0);

    public static void SetBorderRadius(IStyle s, float r)
    {
        s.borderTopLeftRadius = s.borderTopRightRadius =
            s.borderBottomLeftRadius = s.borderBottomRightRadius = r;
    }
}
