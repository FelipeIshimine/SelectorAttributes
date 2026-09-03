using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using UnityEditor;

[CustomPropertyDrawer(typeof(UxmlNameAttribute))]
public class UxmlNameDrawer : StringDropdownDrawerBase
{
	protected override string DropdownTitle => "UXML Element Names";

	protected override List<(string path, string value)> Gather()
	{
		var attr = (UxmlNameAttribute)attribute;
		var seen = new HashSet<string>();
		var result = new List<(string path, string value)>();
		foreach (var guid in AssetDatabase.FindAssets("t:VisualTreeAsset"))
		{
			var path = AssetDatabase.GUIDToAssetPath(guid);
			if (!path.EndsWith(".uxml")) continue;
			if (!PassesSourceFilter(path, attr.IncludeEditor, attr.IncludePackages)) continue;
			if (!string.IsNullOrEmpty(attr.PathFilter) && path.IndexOf(attr.PathFilter, StringComparison.OrdinalIgnoreCase) < 0) continue;

			var file = Path.GetFileNameWithoutExtension(path);
			var doc = new XmlDocument();
			doc.Load(path);
			CollectNames(doc.DocumentElement, file, seen, result);
		}
		return result.OrderBy(p => p.path).ToList();
	}

	static void CollectNames(XmlNode node, string file, HashSet<string> seen, List<(string path, string value)> result)
	{
		if (node == null) return;
		if (node.Attributes != null)
		{
			var nameAttr = node.Attributes["name"];
			if (nameAttr != null && !string.IsNullOrEmpty(nameAttr.Value) && seen.Add($"{file}/{nameAttr.Value}"))
				result.Add(($"{file}/{nameAttr.Value}", nameAttr.Value));
		}
		foreach (XmlNode child in node.ChildNodes)
			CollectNames(child, file, seen, result);
	}
}
