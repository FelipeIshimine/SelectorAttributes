using System;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field)]
public class TypeSelectorAttribute : PropertyAttribute
{
	public string Label { get; }
	public readonly DrawMode Mode;
	public readonly bool ShowBaseType;
	public readonly DropdownRenderMode RenderMode;
	public TypeSelectorAttribute(DrawMode mode = DrawMode.Default, string label = null, bool showBaseType = false, DropdownRenderMode renderMode = DropdownRenderMode.SearchDrilldown)
	{
		Label = label;
		this.Mode = mode;
		ShowBaseType = showBaseType;
		RenderMode = renderMode;
	}
}

public enum DrawMode
{
	Default = 0, NoFoldout = 1, Inline = 2
}
