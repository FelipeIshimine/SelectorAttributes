# Selector Attributes — Documentation

Inspector attributes for flexible **type**, **asset**, **component**, **sub-asset** and **reference**
selection in Unity. Drop an attribute on a serialized field and get a searchable dropdown picker in
the Inspector — no custom editors required.

- **Package:** `com.felipe-ishimine.selector-attributes`
- **Minimum Unity:** 2022.1 (uses UI Toolkit property drawers)
- **Assemblies:** `SelectorAttributes` (runtime attributes) + `SelectorAttributes.Editor` (drawers)

> The attributes themselves live in the runtime assembly so your gameplay code can reference them;
> all the picker UI lives in the Editor assembly and is stripped from builds.

---

## Table of contents

- [`[TypeSelector]` — polymorphic type selection](#typeselector--polymorphic-type-selection)
- [`[SelectorName]` — custom dropdown names](#selectorname--custom-dropdown-names)
- [`[HideInSelector]` — hide a class from the dropdown](#hideinselector--hide-a-class-from-the-dropdown)
- [`[AssetSelector]` — project asset search](#assetselector--project-asset-search)
- [`[ComponentSelector]` — component picker](#componentselector--component-picker)
- [`[SubAssetSelector]` — sub-asset selection](#subassetselector--sub-asset-selection)
- [`[ScriptableObjectKey]` — identity tokens](#scriptableobjectkey--identity-tokens)
- [`[ShowEditor]` — inline editor UI](#showeditor--inline-editor-ui)
- [`InterfaceReference<T>` — polymorphic interface container](#interfacereferencet--polymorphic-interface-container)
- [`TagList<T>` — typed ScriptableObject lists](#taglistt--typed-scriptableobject-lists)
- [Common patterns](#common-patterns)
- [Notes & gotchas](#notes--gotchas)

---

## `[TypeSelector]` — polymorphic type selection

Selects a concrete implementation of a base class or interface and stores it as a
`[SerializeReference]` managed reference. The Inspector shows a **searchable dropdown** of every
non-abstract, non-Unity-Object type assignable to the field.

```csharp
[SerializeReference, TypeSelector]
public ModifierBase modifier;
```

**Draw modes** (`DrawMode`):

| Mode | Behaviour |
|---|---|
| `DrawMode.Default` | Standard collapsible foldout. |
| `DrawMode.NoFoldout` | Children shown without the foldout arrow. |
| `DrawMode.Inline` | Compact, single-row label + content layout. |

```csharp
[SerializeReference, TypeSelector(DrawMode.Inline)]
public EffectBase effect;

// Works on arrays and lists too:
[SerializeReference, TypeSelector(DrawMode.Default)]
public AbilityBase[] abilities;
```

The dropdown automatically excludes:
- abstract types and open generic definitions,
- types deriving from `UnityEngine.Object` (use `[AssetSelector]` / `[ComponentSelector]` for those),
- types hidden with [`[HideInSelector]`](#hideinselector--hide-a-class-from-the-dropdown).

Generic base types are supported: concrete generic implementations are constructed against the
field's type arguments and offered as candidates.

> **Requires `[SerializeReference]`.** Without it the drawer shows a warning box, because plain
> serialization cannot store polymorphic values.

---

## `[SelectorName]` — custom dropdown names

Overrides the label a type shows in a `[TypeSelector]` dropdown. Use `/` to nest entries into
sub-menus.

```csharp
[SelectorName("Modifiers/Multiply (2x)")]
public class MultiplyModifier : ModifierBase { }
```

Without it, the class name is used with its namespace stripped automatically
(e.g. `Game.Combat.MultiplyModifier` → `MultiplyModifier`).

---

## `[HideInSelector]` — hide a class from the dropdown

Excludes a class from `[TypeSelector]` dropdowns **without** making it invalid as a
`[SerializeReference]` value. The type can still be assigned from code or loaded from an existing
save — it is simply not *offered* as a choice in the picker.

```csharp
// Hidden from the picker; still a legal serialized value.
[HideInSelector]
public class LegacyModifier : ModifierBase { }
```

Typical uses:
- intermediate base classes you split out for code reuse but never want instantiated directly,
- deprecated implementations kept only so old saves keep deserializing,
- editor-only or test scaffolding types you don't want designers to pick.

By default only the decorated type itself is hidden; concrete subclasses still appear. Pass
`hideDerived: true` to hide the whole subtree:

```csharp
// Hides EditorOnlyEffectBase AND everything that derives from it.
[HideInSelector(hideDerived: true)]
public abstract class EditorOnlyEffectBase : EffectBase { }
```

> Abstract classes are already excluded from the dropdown automatically — reach for
> `[HideInSelector]` when you want to hide a *concrete* type, or use `hideDerived: true` to hide a
> whole branch of concrete subclasses in one place.

You can check the same rule from your own tooling via `SelectorVisibility.IsHidden(type)`.

---

## `[AssetSelector]` — project asset search

Browse and pick a project asset (typically a `ScriptableObject`) with an optional path filter and
grouping.

```csharp
[AssetSelector]
public ScriptableObject asset;

[AssetSelector(GroupMode.ByType, "Assets/Configs", "Assets/Data")]
public MyConfig config;
```

**`GroupMode`:** `None` (flat list), `ByPath` (grouped by folder), `ByType` (grouped by asset type).

---

## `[ComponentSelector]` — component picker

Select an existing component on the same GameObject, or create one (optionally on a new child).

```csharp
[ComponentSelector]
public MyComponent component;

[ComponentSelector(AddMode.CreateChildGameObject, "Controller")]
public Controller controller;
```

---

## `[SubAssetSelector]` — sub-asset selection

Pick a sub-asset (e.g. a sprite inside an atlas) embedded in another asset.

```csharp
[SubAssetSelector]
public Object subAsset;

[SubAssetSelector(ListMode.GroupedByType)]
public Sprite sprite;
```

---

## `[ScriptableObjectKey]` — identity tokens

Manage a `ScriptableObject` reference as an identity key, with a label + delete + dropdown UI. Handy
for event systems, registries, or named configuration tokens.

```csharp
[ScriptableObjectKey]
public ScriptableObject key;
```

---

## `[ShowEditor]` — inline editor UI

Draws an inline editor for a complex field so you can edit it in place.

```csharp
[ShowEditor]
public MyData data;
```

---

## `InterfaceReference<T>` — polymorphic interface container

A serialized wrapper that lets a field hold **either** a Unity Object (Component / ScriptableObject)
**or** a managed instance, as long as it implements interface `T`.

```csharp
[SerializeField]
public InterfaceReference<IController> controller;

// Use via implicit conversion — returns the Unity Object or the managed instance.
IController ctrl = controller;
```

---

## `TagList<T>` — typed ScriptableObject lists

Base class for a serialized, strongly-typed list of a specific `ScriptableObject` type. Implements
`IList<T>` for runtime mutation.

```csharp
[Serializable]
public class ConfigTags : TagList<ConfigAsset> { }

[SerializeField]
public ConfigTags configs;
```

---

## Common patterns

```csharp
// Polymorphic gameplay data
[SerializeReference, TypeSelector(DrawMode.Default)]
public AbilityBase[] abilities;

// Asset registry grouped by type
[AssetSelector(GroupMode.ByType)]
public ScriptableObject[] effects;

// Named configuration token
[ScriptableObjectKey]
public ScriptableObject difficultyLevel;

// Interface with Unity-Object-or-managed fallback
public InterfaceReference<IInputHandler> input;
```

---

## Notes & gotchas

- `[TypeSelector]` **requires `[SerializeReference]`**; abstracts, open generics and Unity Objects
  are filtered out automatically, and `[HideInSelector]` types are excluded.
- `[HideInSelector]` only affects the *picker*. Already-assigned values of a hidden type keep
  working and keep displaying.
- `[AssetSelector]` searches by type; folder arguments constrain the search.
- All attributes work on arrays and lists.
- Missing managed references are auto-cleaned on save (`HasManagedReferencesWithMissingTypes`).
