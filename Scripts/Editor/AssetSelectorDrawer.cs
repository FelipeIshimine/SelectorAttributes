using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

[CustomPropertyDrawer(typeof(AssetSelectorAttribute))]
public class AssetSelectorDrawer : PropertyDrawer
{
    // ── Cache ─────────────────────────────────────────────────────────────────

    // Stores the asset items; creation items are kept separately so they are
    // always accessible even on a cache hit.
    private static readonly Dictionary<string, CacheEntry> s_cache = new();
    private static readonly Dictionary<Texture, WeakReference<Texture2D>> s_icons = new();

    // Invalidate on any project change (asset import, delete, move, etc.)
    static AssetSelectorDrawer()
    {
        EditorApplication.projectChanged += () => s_cache.Clear();
    }

    // ── PropertyDrawer entry point ────────────────────────────────────────────

    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        var attr = attribute as AssetSelectorAttribute;
        return CreateProperty(property, GetFieldType(), attr.Group, attr.RenderMode, attr.Folders);
    }

    // ── Public factory (also used by ScriptableObjectFallbackDrawer) ──────────

    public VisualElement CreateProperty(
        SerializedProperty property,
        Type fieldType,
        AssetSelectorAttribute.GroupMode group,
        DropdownRenderMode renderMode = DropdownRenderMode.SearchDrilldown,
        params string[] folders)
    {
        var container = new VisualElement
        {
            style =
            {
                flexDirection = FlexDirection.Row,
                alignItems    = Align.Center,
                flexGrow      = 1,
                flexShrink    = 1,
            }
        };

        if (property.propertyType != SerializedPropertyType.ObjectReference)
        {
            container.Add(new Label("[AssetSelector] can only be used on UnityEngine.Object fields"));
            return container;
        }

        var objectField = new ObjectField(property.displayName)
        {
            value             = property.objectReferenceValue,
            objectType        = fieldType,
            allowSceneObjects = false,
            style             = { flexGrow = 1, flexShrink = 1 },
        };
        objectField.BindProperty(property);

        objectField.RegisterCallbackOnce<GeometryChangedEvent>(_ =>
        {
            var label = objectField.Q<Label>();
            if (label == null) return;
            label.style.width     = Length.Percent(45);
            label.style.minWidth  = 120;
            label.style.marginRight = 4;
        });

        var dropButton = new Button { text = "▾", tooltip = "Select asset...", style = { width = 24 } };

        // Capture path, not the property itself, to survive drawer pooling.
        var propertyPath           = property.propertyPath;
        var serializedObjectTarget = property.serializedObject.targetObject;

        dropButton.clicked += () =>
        {
            // Re-resolve the property at click time — safe against pooling.
            var so   = new SerializedObject(serializedObjectTarget);
            var prop = so.FindProperty(propertyPath);
            if (prop != null)
                ShowDropdown(prop, dropButton, fieldType, group, renderMode, folders);
        };

        container.Add(objectField);
        container.Add(dropButton);
        return container;
    }

    // ── Dropdown ──────────────────────────────────────────────────────────────

    private void ShowDropdown(
        SerializedProperty property,
        VisualElement anchor,
        Type fieldType,
        AssetSelectorAttribute.GroupMode group,
        DropdownRenderMode renderMode,
        string[] folders)
    {
        var key = BuildCacheKey(fieldType, folders);

        // Rebuild cache entry if missing.
        if (!s_cache.TryGetValue(key, out var entry))
            entry = BuildCacheEntry(fieldType, folders);

        // Build creation-type list fresh every time (cheap reflection, no I/O).
        var creationTypes = BuildCreationTypes(fieldType);

        // ── Dropdown paths ────────────────────────────────────────────────────

        var dropdownBuilder = new AdvancedDropdownBuilder().WithTitle(fieldType.Name).WithRenderMode(renderMode);
        List<int> indices;

        var displayPaths = BuildDisplayPaths(entry.Assets, creationTypes, group, entry.TypesFound).ToList();
        dropdownBuilder.AddElements(displayPaths, out indices);

        dropdownBuilder
            .SetCallback(OnSelection)
            .Build()
            .Show(anchor.worldBound);

        // ── Selection callback ────────────────────────────────────────────────

        void OnSelection(int dropdownIndex)
        {
            int cacheIndex = indices[dropdownIndex];

            if (cacheIndex < entry.Assets.Count)
            {
                // Existing asset selected.
                property.objectReferenceValue = entry.Assets[cacheIndex].Asset;
            }
            else
            {
                // Creation item selected.
                int creationIndex = cacheIndex - entry.Assets.Count;
                if (creationIndex < 0 || creationIndex >= creationTypes.Count) return;

                var type = creationTypes[creationIndex];
                var path = EditorUtility.SaveFilePanelInProject(
                    $"New {type.Name}", "New", "asset", "");
                if (string.IsNullOrEmpty(path)) return;

                var instance = ScriptableObject.CreateInstance(type);
                AssetDatabase.CreateAsset(instance, path);
                property.objectReferenceValue = instance;

                // Invalidate so the new asset appears next time.
                s_cache.Remove(key);
            }

            EditorUtility.SetDirty(property.serializedObject.targetObject);
            property.serializedObject.ApplyModifiedProperties();
        }
    }

    // ── Cache building ────────────────────────────────────────────────────────

    private CacheEntry BuildCacheEntry(Type fieldType, string[] folders)
    {
        var assets     = new List<Item>();
        var typesFound = new HashSet<Type>();

        var guids = AssetDatabase.FindAssets($"t:{fieldType.Name}", folders?.Length > 0 ? folders : null);
        foreach (var guid in guids)
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var main = AssetDatabase.LoadMainAssetAtPath(path);
            var subAssets = AssetDatabase.LoadAllAssetRepresentationsAtPath(path)
                .Where(fieldType.IsInstanceOfType)
                .OrderBy(sub => sub.name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (fieldType.IsInstanceOfType(main))
            {
                typesFound.Add(main.GetType());
                assets.Add(new Item(path, subAssets.Count > 0 ? ThisLabel : null, main, GetCachedIcon(path)));
            }

            foreach (var sub in subAssets)
            {
                typesFound.Add(sub.GetType());
                assets.Add(new Item(path, sub.name, sub, AssetPreview.GetMiniThumbnail(sub)));
            }
        }

        assets.Sort((x, y) =>
        {
            int byPath = string.Compare(x.Path, y.Path, StringComparison.Ordinal);
            if (byPath != 0) return byPath;
            if (x.SubLabel == ThisLabel) return -1;
            if (y.SubLabel == ThisLabel) return 1;
            return string.Compare(x.SubLabel, y.SubLabel, StringComparison.OrdinalIgnoreCase);
        });

        var entry = new CacheEntry(assets, typesFound);
        s_cache[BuildCacheKey(fieldType, folders)] = entry;
        return entry;
    }

    private static List<Type> BuildCreationTypes(Type fieldType)
    {
        var list = new List<Type>();

        if (typeof(ScriptableObject).IsAssignableFrom(fieldType))
            list.AddRange(TypeCache.GetTypesDerivedFrom(fieldType)
                .Where(t => !t.IsAbstract && !t.IsGenericType));

        if (!fieldType.IsAbstract && !fieldType.IsGenericType && !list.Contains(fieldType))
            list.Add(fieldType);

        list.Sort((x, y) => string.Compare(
            SelectorName.GetDisplayName(x),
            SelectorName.GetDisplayName(y),
            StringComparison.OrdinalIgnoreCase));

        return list;
    }

    private static IEnumerable<AdvancedDropdownPath> BuildDisplayPaths(
        List<Item> assets,
        List<Type> creationTypes,
        AssetSelectorAttribute.GroupMode group,
        HashSet<Type> typesFound)
    {
        // Asset entries
        foreach (var item in assets)
        {
            // For modes that hide the path in the label, surface it as a tooltip instead.
            string tooltip = group == AssetSelectorAttribute.GroupMode.ByPath ? null : item.Path;

            string suffix = item.SubLabel == null ? string.Empty : $"/{item.SubLabel}";

            yield return group switch
            {
                AssetSelectorAttribute.GroupMode.ByPath => new AdvancedDropdownPath(item.Path + suffix, item.Icon),
                AssetSelectorAttribute.GroupMode.ByType when typesFound.Count > 1 =>
                    new AdvancedDropdownPath(
                        $"{SelectorName.GetDisplayName(item.Asset.GetType())}/{Path.GetFileName(item.Path)}{suffix}", item.Icon, tooltip),
                _ => new AdvancedDropdownPath(Path.GetFileName(item.Path) + suffix, item.Icon, tooltip),
            };
        }

        // Creation entries — always shown flat under a "-New-" folder
        foreach (var type in creationTypes)
        {
            yield return new AdvancedDropdownPath(
                $"-New-/{SelectorName.GetDisplayName(type)}", null);
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private Type GetFieldType()
    {
        if (fieldInfo == null) return typeof(Object);

        var t = fieldInfo.FieldType;

        // Unwrap List<T> and T[]
        if (t.IsArray)
            t = t.GetElementType() ?? t;
        else if (t.IsGenericType)
            t = t.GetGenericArguments()[0];

        if (t == typeof(Object) || t == typeof(object)) return typeof(Object);
        return t.IsSubclassOf(typeof(Object)) ? t : typeof(Object);
    }

    private const string ThisLabel = "this";

    private static string BuildCacheKey(Type fieldType, string[] folders) =>
        $"{fieldType.FullName}|{(folders?.Length > 0 ? string.Join(";", folders) : "<all>")}";

    private Texture2D GetCachedIcon(string assetPath)
    {
        var source = AssetDatabase.GetCachedIcon(assetPath);
        if (source == null) return null;

        if (s_icons.TryGetValue(source, out var weakRef) &&
            weakRef.TryGetTarget(out var cached))
            return cached;

        // Convert to a readable Texture2D.
        if (source is Texture2D tx && tx.isReadable)
        {
            s_icons[source] = new WeakReference<Texture2D>(tx);
            return tx;
        }

        int w  = source.width  > 0 ? source.width  : 32;
        int h  = source.height > 0 ? source.height : 32;
        var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
        var prev = RenderTexture.active;
        try
        {
            Graphics.Blit(source, rt);
            RenderTexture.active = rt;

            var readable = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
            readable.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
            readable.Apply(false, false);

            s_icons[source] = new WeakReference<Texture2D>(readable);
            return readable;
        }
        finally
        {
            RenderTexture.active = prev;
            RenderTexture.ReleaseTemporary(rt);
        }
    }

    // ── Nested types ──────────────────────────────────────────────────────────

    private sealed class CacheEntry
    {
        public readonly List<Item>     Assets;
        public readonly HashSet<Type>  TypesFound;
        public CacheEntry(List<Item> assets, HashSet<Type> typesFound)
        {
            Assets     = assets;
            TypesFound = typesFound;
        }
    }
}

// ── Shared item struct ────────────────────────────────────────────────────────

internal readonly struct Item
{
    public readonly string    Path;
    public readonly string    SubLabel;
    public readonly Object    Asset;
    public readonly Texture2D Icon;

    public Item(string path, string subLabel, Object asset, Texture2D icon)
    {
        Path     = path;
        SubLabel = subLabel;
        Asset    = asset;
        Icon     = icon;
    }
}
