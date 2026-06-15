using System;

/// <summary>
/// Excludes the decorated class from <c>[TypeSelector]</c> dropdowns.
/// The type is still a valid <c>[SerializeReference]</c> value (assigned in code,
/// migrated saves, etc.) — it simply is not offered as a choice in the picker.
/// </summary>
/// <remarks>
/// Useful for intermediate base classes, deprecated implementations you keep around
/// for save compatibility, or editor-only / test scaffolding types you don't want
/// designers to pick.
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Interface, Inherited = false)]
public class HideInSelectorAttribute : Attribute
{
	/// <summary>
	/// When <c>true</c>, every type deriving from the decorated type is hidden as well.
	/// When <c>false</c> (default) only the decorated type itself is hidden, and concrete
	/// subclasses still appear.
	/// </summary>
	public readonly bool HideDerived;

	public HideInSelectorAttribute(bool hideDerived = false)
	{
		HideDerived = hideDerived;
	}
}

public static class SelectorVisibility
{
	/// <summary>
	/// Returns <c>true</c> if <paramref name="type"/> should be omitted from selector dropdowns,
	/// either because it carries <see cref="HideInSelectorAttribute"/> directly, or because an
	/// ancestor declared <c>HideDerived = true</c>.
	/// </summary>
	public static bool IsHidden(Type type)
	{
		if (type == null) return false;

		// Explicit attribute on the type itself (inherit:false so each level is checked deliberately).
		if (Attribute.IsDefined(type, typeof(HideInSelectorAttribute), inherit: false))
			return true;

		// An ancestor may hide its whole subtree.
		for (var ancestor = type.BaseType; ancestor != null; ancestor = ancestor.BaseType)
		{
			if (Attribute.GetCustomAttribute(ancestor, typeof(HideInSelectorAttribute), inherit: false)
				is HideInSelectorAttribute attr && attr.HideDerived)
				return true;
		}

		return false;
	}
}
