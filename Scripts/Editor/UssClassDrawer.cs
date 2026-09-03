using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;

[CustomPropertyDrawer(typeof(UssClassAttribute))]
public class UssClassDrawer : StringDropdownDrawerBase
{
	static readonly Regex ClassSelector = new Regex(@"\.(-?[_a-zA-Z][_a-zA-Z0-9-]*)", RegexOptions.Compiled);

	protected override string DropdownTitle => "USS Classes";

	protected override List<(string path, string value)> Gather()
	{
		var attr = (UssClassAttribute)attribute;
		var seen = new HashSet<string>();
		var result = new List<(string path, string value)>();
		foreach (var guid in AssetDatabase.FindAssets("t:StyleSheet"))
		{
			var path = AssetDatabase.GUIDToAssetPath(guid);
			if (!path.EndsWith(".uss")) continue;
			if (!PassesSourceFilter(path, attr.IncludeEditor, attr.IncludePackages)) continue;
			if (!string.IsNullOrEmpty(attr.PathFilter) && path.IndexOf(attr.PathFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;

			var file = Path.GetFileNameWithoutExtension(path);
			var text = File.ReadAllText(path);
			foreach (Match m in ClassSelector.Matches(text))
			{
				var name = m.Groups[1].Value;
				if (seen.Add($"{file}/{name}"))
					result.Add(($"{file}/{name}", name));
			}
		}
		return result.OrderBy(p => p.path).ToList();
	}
}
