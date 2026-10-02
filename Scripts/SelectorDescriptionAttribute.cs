using System;
using System.Reflection;

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct)]
public sealed class SelectorDescriptionAttribute : Attribute
{
	public readonly string Description;

	public SelectorDescriptionAttribute(string description) => Description = description;
}

public static class SelectorDescription
{
	public static string Of(Type type)
	{
		if (type == null)
			return null;

		return type.GetCustomAttribute(typeof(SelectorDescriptionAttribute), false) is SelectorDescriptionAttribute attribute
			? attribute.Description
			: null;
	}
}
