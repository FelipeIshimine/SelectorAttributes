using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace TypeSelector
{
    public static class TypeSelectorUI
    {
        // ── Cache ─────────────────────────────────────────────────────────────────

        private static readonly Dictionary<string, Type[]> GenericCandidatesCache = new();

        static TypeSelectorUI()
        {
            AssemblyReloadEvents.beforeAssemblyReload += () => GenericCandidatesCache.Clear();
        }

        // ── Palette ───────────────────────────────────────────────────────────────

        static readonly Color PanelBg        = new(0.14f, 0.14f, 0.14f, 1f);
        static readonly Color PanelBorder    = new(0.25f, 0.25f, 0.25f, 1f);
        static readonly Color RowBgEven      = new(0.17f, 0.17f, 0.17f, 1f);
        static readonly Color RowBgOdd       = new(0.20f, 0.20f, 0.20f, 1f);
        static readonly Color AccentBlue     = new(0.22f, 0.50f, 0.90f, 1f);
        static readonly Color AccentBlueDark = new(0.16f, 0.38f, 0.70f, 1f);
        static readonly Color AccentGreen    = new(0.22f, 0.70f, 0.44f, 1f);
        static readonly Color AccentGreenDark = new(0.16f, 0.52f, 0.32f, 1f);
        static readonly Color AccentRed      = new(0.80f, 0.26f, 0.26f, 1f);
        static readonly Color AccentRedDark  = new(0.60f, 0.18f, 0.18f, 1f);
        static readonly Color HeaderBg       = new(0.19f, 0.22f, 0.28f, 1f);
        static readonly Color LabelMuted     = new(0.65f, 0.65f, 0.65f, 1f);
        static readonly Color LabelBright    = new(0.90f, 0.90f, 0.90f, 1f);
        static readonly Color IndexLabel     = new(0.45f, 0.72f, 1.00f, 1f);

        // ── Entry point ───────────────────────────────────────────────────────────

        public static VisualElement Build(SerializedProperty property,
                                          FieldInfo fieldInfo,
                                          TypeSelectorAttribute attribute)
        {
	        /*if (IsCollectionField(fieldInfo))
	        {
		        return BuildCollection(property, fieldInfo);
	        }*/
	        return Build(property, fieldInfo.FieldType,attribute.Mode,attribute.Label);
        }


        public static VisualElement BuildCollection(SerializedProperty property, FieldInfo fieldInfo)
        {
	        if (SerializationUtility.HasManagedReferencesWithMissingTypes(property.serializedObject.targetObject))
		        SerializationUtility.ClearAllManagedReferencesWithMissingTypes(property.serializedObject.targetObject);

	        var root = new VisualElement();

		    root.Add(BuildCollectionGUI(property, fieldInfo));
		    return root;
        }
        
        public static VisualElement Build(SerializedProperty property, Type declaredType, DrawMode drawMode, string label)
        {
            if (SerializationUtility.HasManagedReferencesWithMissingTypes(property.serializedObject.targetObject))
                SerializationUtility.ClearAllManagedReferencesWithMissingTypes(property.serializedObject.targetObject);

            var root = new VisualElement();

            if (property.propertyType != SerializedPropertyType.ManagedReference)
            {
	            root.Add(BuildWarningBox(property));
            }

            // ── Single field ──────────────────────────────────────────────────────
            var treeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(
                "Packages/com.felipe-ishimine.selector-attributes/Assets/UI_TypeSelector.uxml");
            var container = treeAsset?.CloneTree().Q<VisualElement>("Container") ?? new VisualElement();

            var ss = AssetDatabase.LoadAssetAtPath<StyleSheet>(
                "Packages/com.felipe-ishimine.selector-attributes/Assets/ST_TypeSelector.uss");
            if (ss != null) container.styleSheets.Add(ss);

            label ??= property.displayName;
            
            switch (drawMode)
            {
                case DrawMode.Default:
                    container.Add(new PropertyField(property,label) { style = { flexGrow = 1 } });
                    break;

                case DrawMode.Inline:
                {
                    property.isExpanded = true;
                    var row = new VisualElement
                    {
                        style = { flexDirection = FlexDirection.Row, flexGrow = 1, marginRight = 16 }
                    };
                    row.Add(new Label(label)
                    {
                        style = { width = Length.Percent(30), maxWidth = 220, flexGrow = 0, flexShrink = 0 }
                    });

                    var contentBox = new VisualElement
                    {
                        style = { flexGrow = 1, flexShrink = 0, width = Length.Percent(70) }
                    };
                    contentBox.AddToClassList("no-foldout-container");

                    var pf = new PropertyField(property) { style = { flexGrow = 1, flexShrink = 1 } };
                    pf.AddToClassList("inline");
                    contentBox.Add(pf);
                    row.Add(contentBox);
                    container.Add(row);
                    break;
                }

                case DrawMode.NoFoldout:
                {
                    property.isExpanded = true;

                    var sub = new VisualElement { style = { flexGrow = 1 } };
                    sub.Add(new Label(label));
                    
					var contentBox = new VisualElement();
                    contentBox.AddToClassList("no-foldout-container");

                    var pf = new PropertyField(property) { style = { flexGrow = 1 } };
                    pf.AddToClassList("no-foldout");
                    contentBox.Add(pf);
                    sub.Add(contentBox);
                    container.Add(sub);
                    contentBox.style.display = property.hasVisibleChildren ? DisplayStyle.Flex : DisplayStyle.None;
                    break;
                }
            }

            root.Add(container);

            var typeSelectorBtn = container.Q<Button>("TypeSelector") ?? StyledButton("Select Type", AccentBlueDark, AccentBlue);
            var activeTypeName  = container.Q<Label>("TypeName") ?? new Label();

            activeTypeName.text = GetButtonLabel(property);
            activeTypeName.RemoveFromClassList("none");
            activeTypeName.RemoveFromClassList("show");

            if (property.managedReferenceValue == null)
                activeTypeName.AddToClassList("none");
            else if (drawMode != DrawMode.Inline)
                activeTypeName.AddToClassList("show");

            typeSelectorBtn.clicked += () => SelectorButtonClicked_ForProperty(typeSelectorBtn, property,declaredType);
            typeSelectorBtn.RemoveFromHierarchy();
            container.Add(typeSelectorBtn);

            return root;
        }
        
        // ── Warning box ───────────────────────────────────────────────────────────

        private static VisualElement BuildWarningBox(SerializedProperty property)
        {
            var box = new VisualElement { style = { flexDirection = FlexDirection.Column, marginBottom = 4 } };
            box.style.backgroundColor = new Color(0.45f, 0.18f, 0.10f, 0.6f);
            SetRadius(box.style, 4f);
            SetPadding(box.style, 4f, 6f);
            SetBorderColor(box.style, AccentRed);
            SetBorderWidth(box.style, 1f);

            box.Add(new PropertyField(property));

            var warn = new Label($"⚠  Invalid use of {nameof(TypeSelectorAttribute)} — requires [SerializeReference]");
            warn.style.color    = new Color(1f, 0.55f, 0.45f);
            warn.style.fontSize = 10;
            warn.style.unityFontStyleAndWeight = FontStyle.Italic;
            warn.style.marginTop = 2;
            box.Add(warn);

            return box;
        }

        // ── Collection UI ─────────────────────────────────────────────────────────

        private static VisualElement BuildCollectionGUI(SerializedProperty collectionProperty, FieldInfo fieldInfo)
        {
            var panel = new VisualElement();
            panel.style.marginTop       = 4;
            panel.style.marginBottom    = 4;
            panel.style.backgroundColor = PanelBg;
            panel.style.overflow        = Overflow.Hidden;
            SetRadius(panel.style, 6f);
            SetBorderColor(panel.style, PanelBorder);
            SetBorderWidth(panel.style, 1f);

            var header = new Foldout { text = collectionProperty.displayName, value = true };
            header.style.backgroundColor = HeaderBg;
            SetPadding(header.style, 4f, 8f);
            panel.Add(header);

            var content = new VisualElement { style = { flexDirection = FlexDirection.Column, paddingBottom = 6 } };
            header.Add(content);

            RebuildContent(collectionProperty, content, fieldInfo);
            return panel;
        }

        private static void RebuildContent(SerializedProperty collectionProperty, VisualElement content, FieldInfo fieldInfo)
        {
            content.Clear();
            content.Add(BuildSizeRow(collectionProperty, content, fieldInfo));
            BuildElementRows(collectionProperty, content, fieldInfo);
        }

        private static VisualElement BuildSizeRow(SerializedProperty collectionProperty, VisualElement content, FieldInfo fieldInfo)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems    = Align.Center;
            row.style.marginTop     = 6;
            row.style.marginBottom  = 4;
            row.style.paddingLeft   = 8;
            row.style.paddingRight  = 8;

            var sizeLabel = new Label("Size");
            sizeLabel.style.color    = LabelMuted;
            sizeLabel.style.fontSize = 11;
            sizeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            sizeLabel.style.width    = 36;
            row.Add(sizeLabel);

            var sizeField = new IntegerField { value = collectionProperty.arraySize };
            sizeField.style.width           = 52;
            sizeField.style.marginLeft      = 4;
            sizeField.style.backgroundColor = PanelBorder;
            SetRadius(sizeField.style, 3f);
            sizeField.RegisterValueChangedCallback(evt =>
            {
                int newSize = Mathf.Max(0, evt.newValue);
                if (newSize == collectionProperty.arraySize) return;
                collectionProperty.arraySize = newSize;
                collectionProperty.serializedObject.ApplyModifiedProperties();
                RebuildContent(collectionProperty, content, fieldInfo);
            });
            row.Add(sizeField);

            var addBtn = StyledButton("＋ Add", AccentGreenDark, AccentGreen);
            addBtn.style.marginLeft = 6;
            addBtn.clicked += () =>
            {
                collectionProperty.arraySize++;
                collectionProperty.serializedObject.ApplyModifiedProperties();
                RebuildContent(collectionProperty, content, fieldInfo);
                sizeField.SetValueWithoutNotify(collectionProperty.arraySize);
            };
            row.Add(addBtn);

            return row;
        }

        private static void BuildElementRows(SerializedProperty collectionProperty, VisualElement parent, FieldInfo fieldInfo)
        {
            int count = collectionProperty.arraySize;

            for (int i = 0; i < count; i++)
            {
                var elementProp = collectionProperty.GetArrayElementAtIndex(i);

                var row = new VisualElement();
                row.style.flexDirection   = FlexDirection.Row;
                row.style.alignItems      = Align.Center;
                row.style.marginTop       = 1;
                row.style.paddingTop      = 3;
                row.style.paddingBottom   = 3;
                row.style.paddingLeft     = 8;
                row.style.paddingRight    = 8;
                row.style.backgroundColor = i % 2 == 0 ? RowBgEven : RowBgOdd;

                var indexLabel = new Label($"[{i}]");
                indexLabel.style.color       = IndexLabel;
                indexLabel.style.fontSize    = 11;
                indexLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
                indexLabel.style.width       = 32;
                indexLabel.style.flexShrink  = 0;
                row.Add(indexLabel);

                var elementField = new PropertyField(elementProp) { style = { flexGrow = 1 } };
                row.Add(elementField);

                var typeBtn = StyledButton(GetButtonLabel(elementProp), AccentBlueDark, AccentBlue, 130f);
                typeBtn.style.marginLeft = 6;
                typeBtn.clicked += () => SelectorButtonClicked_ForElement(typeBtn, elementProp, collectionProperty, fieldInfo);
                row.Add(typeBtn);

                var sep = new VisualElement();
                sep.style.width           = 1;
                sep.style.height          = Length.Percent(70);
                sep.style.backgroundColor = PanelBorder;
                sep.style.marginLeft      = 6;
                sep.style.marginRight     = 2;
                sep.style.alignSelf       = Align.Center;
                row.Add(sep);

                var removeBtn = StyledButton("✕", AccentRedDark, AccentRed, 28f);
                removeBtn.style.marginLeft = 2;
                int capturedIndex = i;
                removeBtn.clicked += () =>
                {
                    collectionProperty.DeleteArrayElementAtIndex(capturedIndex);
                    collectionProperty.serializedObject.ApplyModifiedProperties();
                    RebuildContent(collectionProperty, parent, fieldInfo);
                };
                row.Add(removeBtn);

                parent.Add(row);
            }

            if (count == 0)
            {
                var empty = new Label("— empty —");
                empty.style.color       = LabelMuted;
                empty.style.fontSize    = 11;
                empty.style.unityFontStyleAndWeight = FontStyle.Italic;
                empty.style.alignSelf   = Align.Center;
                empty.style.marginTop   = 6;
                empty.style.marginBottom = 6;
                parent.Add(empty);
            }
        }

        // ── Click handlers ────────────────────────────────────────────────────────

        private static void SelectorButtonClicked_ForProperty(Button typeBtn, SerializedProperty property, Type declaredType)
        {
	        ShowTypeDropdown(typeBtn.worldBound, declaredType, chosenType =>
            {
                property.managedReferenceValue = chosenType != null ? Activator.CreateInstance(chosenType) : null;
                property.serializedObject.ApplyModifiedProperties();
                typeBtn.parent?.Bind(property.serializedObject);
            });
        }

        private static void SelectorButtonClicked_ForElement(Button typeBtn, SerializedProperty elementProp, SerializedProperty collectionProp, FieldInfo fieldInfo)
        {
            var declaredType = GetElementTargetType(fieldInfo);

            if (declaredType == null)
            {
                var full = elementProp.managedReferenceFullTypename;
                if (!string.IsNullOrEmpty(full))
                {
                    var parts = full.Split(' ');
                    var name  = parts.Length > 1 ? string.Join(" ", parts.Skip(1)) : parts[0];
                    declaredType = AppDomain.CurrentDomain.GetAssemblies()
                        .Select(a => a.GetType(name)).FirstOrDefault(t => t != null);
                }
            }

            if (declaredType == null)
            {
                Debug.LogWarning("[TypeSelector] Could not resolve element declared type; falling back to object.");
                declaredType = typeof(object);
            }

            ShowTypeDropdown(typeBtn.worldBound, declaredType, chosenType =>
            {
                elementProp.managedReferenceValue = chosenType != null ? Activator.CreateInstance(chosenType) : null;
                collectionProp.serializedObject.ApplyModifiedProperties();
                typeBtn.parent?.Bind(collectionProp.serializedObject);
                typeBtn.text = GetButtonLabel(elementProp);
            });
        }

        // ── Dropdown ──────────────────────────────────────────────────────────────

        
        private static void ShowTypeDropdown(Rect worldRect, Type targetType, Action<Type> onSelect)
        {
            if (targetType == null) { onSelect?.Invoke(null); return; }

            if (targetType.IsArray)
                targetType = targetType.GetElementType();
            else if (targetType.IsGenericType && targetType.GetGenericTypeDefinition() == typeof(List<>))
                targetType = targetType.GetGenericArguments()[0];

            var unityObjectType = typeof(UnityEngine.Object);
            var candidates      = new List<Type>();

            if (targetType.IsGenericType && !targetType.IsGenericTypeDefinition)
            {
                var genericDef  = targetType.GetGenericTypeDefinition();
                var targetArgs  = targetType.GetGenericArguments();
                string cacheKey = (genericDef.FullName ?? genericDef.Name) + ":" +
                                  string.Join(",", targetArgs.Select(a => a.FullName ?? a.Name));

                if (!GenericCandidatesCache.TryGetValue(cacheKey, out var cached))
                {
                    var list = new List<Type>();
                    foreach (var candidate in TypeCache.GetTypesDerivedFrom(genericDef))
                    {
                        try
                        {
                            var constructed = candidate.IsGenericTypeDefinition
                                ? candidate.MakeGenericType(targetArgs)
                                : candidate;

                            if (!constructed.IsAbstract && !constructed.IsGenericTypeDefinition &&
                                !unityObjectType.IsAssignableFrom(constructed) &&
                                !SelectorVisibility.IsHidden(candidate) &&
                                !SelectorVisibility.IsHidden(constructed) &&
                                targetType.IsAssignableFrom(constructed))
                                list.Add(constructed);
                        }
                        catch { /* ignore construction failures */ }
                    }
                    cached = list.ToArray();
                    GenericCandidatesCache[cacheKey] = cached;
                }
                candidates.AddRange(cached);

                if (!targetType.IsAbstract && !targetType.IsGenericTypeDefinition &&
                    !unityObjectType.IsAssignableFrom(targetType) &&
                    !SelectorVisibility.IsHidden(targetType))
                    candidates.Add(targetType);
            }
            else
            {
                foreach (var t in TypeCache.GetTypesDerivedFrom(targetType))
                {
                    if (!t.IsAbstract && !t.IsGenericTypeDefinition && !unityObjectType.IsAssignableFrom(t) &&
                        !SelectorVisibility.IsHidden(t))
                        candidates.Add(t);
                }
                if (!targetType.IsAbstract && !targetType.IsGenericTypeDefinition &&
                    !SelectorVisibility.IsHidden(targetType))
                    candidates.Add(targetType);
            }

            // Build (path, Type) pairs — null entry for "none"
            (string path, Type type)[] pairs = candidates
                                     .Select(t =>
                                     {
	                                     var path = t.GetCustomAttributes(typeof(SelectorNameAttribute), false)
	                                                 .OfType<SelectorNameAttribute>().FirstOrDefault()?.Name;
	                                     return (path: string.IsNullOrEmpty(path) ? SelectorName.GetDisplayName(t) : path, type: t);
                                     })
                                     .Append(("-null-", (Type)null))
                                     .OrderBy(p => p.Item1, StringComparer.Ordinal)
                                     .ToArray();

            new AdvancedDropdownBuilder()
                .WithTitle($"{targetType.Name} Types")
                .AddElements(pairs.Select(p => (p.path, p.type)), out var resolvedTypes)
                .SetCallback(i => onSelect?.Invoke(resolvedTypes[i]))
                .Build()
                .Show(worldRect);
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static Type GetElementTargetType(FieldInfo fi)
        {
            var ft = fi.FieldType;
            if (ft.IsArray) return ft.GetElementType();
            if (ft.IsGenericType)
            {
                var args = ft.GetGenericArguments();
                if (args.Length == 1) return args[0];

                return ft.GetInterfaces()
                    .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>))
                    ?.GetGenericArguments()[0];
            }
            return null;
        }

        private static bool IsCollectionField(FieldInfo fi)
        {
            if (fi == null) return false;
            var ft = fi.FieldType;
            if (ft.IsArray) return true;
            if (!ft.IsGenericType) return false;

            return ft.GetGenericArguments().Length == 1 ||
                   ft.GetInterfaces().Any(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        }

        private static string GetButtonLabel(SerializedProperty p)
        {
            var val = p.managedReferenceValue;
            return val != null ? SelectorName.GetDisplayName(val.GetType()) : "-null-";
        }

        // ── Style helpers ─────────────────────────────────────────────────────────

        private static void SetRadius(IStyle s, float r)
        {
            s.borderTopLeftRadius = s.borderTopRightRadius =
                s.borderBottomLeftRadius = s.borderBottomRightRadius = r;
        }

        private static void SetBorderColor(IStyle s, Color c)
        {
            s.borderTopColor = s.borderRightColor = s.borderBottomColor = s.borderLeftColor = c;
        }

        private static void SetBorderWidth(IStyle s, float w)
        {
            s.borderTopWidth = s.borderRightWidth = s.borderBottomWidth = s.borderLeftWidth = w;
        }

        private static void SetPadding(IStyle s, float v, float h)
        {
            s.paddingTop = s.paddingBottom = v;
            s.paddingLeft = s.paddingRight  = h;
        }

        private static Button StyledButton(string text, Color bg, Color border, float width = -1)
        {
            var btn = new Button { text = text };
            btn.style.backgroundColor         = bg;
            btn.style.color                   = LabelBright;
            btn.style.fontSize                = 11;
            btn.style.unityFontStyleAndWeight  = FontStyle.Bold;
            SetBorderColor(btn.style, border);
            SetBorderWidth(btn.style, 1f);
            SetRadius(btn.style, 4f);
            SetPadding(btn.style, 3f, 8f);
            if (width > 0) btn.style.width = width;
            return btn;
        }
    }
}