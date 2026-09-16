using System;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class AssetSelectorAttribute : PropertyAttribute
{
	public readonly GroupMode Group;
	public readonly DropdownRenderMode RenderMode;

	/// <summary>
	/// Optional folders to search in. Example: "Assets/Art","Assets/Configs". If empty, searches whole project.
	/// </summary>
	public string[] Folders;
	public AssetSelectorAttribute(GroupMode groupMode = GroupMode.None, params string[] folders)
	{
		Group = groupMode;
		RenderMode = DropdownRenderMode.SearchDrilldown;
		this.Folders = folders ?? Array.Empty<string>();
	}

	public AssetSelectorAttribute(DropdownRenderMode renderMode, GroupMode groupMode = GroupMode.None, params string[] folders)
	{
		Group = groupMode;
		RenderMode = renderMode;
		this.Folders = folders ?? Array.Empty<string>();
	}

	public enum GroupMode
	{
		None,
		ByPath,
		ByType
	}
	
}


