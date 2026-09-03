using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

public abstract class StringDropdownDrawerBase : PropertyDrawer
{
	List<(string path, string value)> _cached;

	protected abstract string DropdownTitle { get; }
	protected abstract List<(string path, string value)> Gather();

	public override VisualElement CreatePropertyGUI(SerializedProperty property)
	{
		if (property.propertyType != SerializedPropertyType.String)
			return new Label($"[{GetType().Name}] only works on string fields.");

		var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

		var label = new Label(property.displayName)
		{
			style = { width = Length.Percent(42), flexShrink = 0, unityTextAlign = TextAnchor.MiddleLeft }
		};
		row.Add(label);

		var button = new Button { style = { flexGrow = 1, unityTextAlign = TextAnchor.MiddleLeft, marginLeft = 2 } };
		button.text = DisplayFor(property.stringValue);

		button.clicked += () =>
		{
			_cached ??= Gather();

			var pairs = new List<(string path, string value)> { ("<none>", string.Empty) };
			pairs.AddRange(_cached);

			new AdvancedDropdownBuilder()
				.WithTitle(DropdownTitle)
				.AddElements(pairs, out string[] values)
				.SetCallback(i =>
				{
					property.stringValue = values[i];
					property.serializedObject.ApplyModifiedProperties();
					button.text = DisplayFor(values[i]);
				})
				.Build()
				.Show(button.worldBound);
		};

		row.Add(button);
		return row;
	}

	static string DisplayFor(string value) => string.IsNullOrEmpty(value) ? "<none>" : value;

	protected static bool PassesSourceFilter(string path, bool includeEditor, bool includePackages)
	{
		if (!includePackages && path.StartsWith("Packages/")) return false;
		if (!includeEditor && (path.Contains("/Editor/") || path.StartsWith("Editor/"))) return false;
		return true;
	}
}
