using System;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class UssClassAttribute : PropertyAttribute
{
	public readonly string PathFilter;
	public readonly bool IncludeEditor;
	public readonly bool IncludePackages;

	public UssClassAttribute(string pathFilter = null, bool includeEditor = false, bool includePackages = false)
	{
		PathFilter = pathFilter;
		IncludeEditor = includeEditor;
		IncludePackages = includePackages;
	}
}
