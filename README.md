# Type Selector for Unity

Inspector attributes for flexible asset, type, component, and reference selection in Unity.

## Installation

Add via Package Manager → Add Package from disk, or copy to `Packages/TypeSelector-for-Unity/`.

## Features & API

### `[TypeSelector]` — Polymorphic Type Selection

Selects concrete implementations of a base class/interface with dropdown UI. Pair with `[SerializeReference]`.

```csharp
[SerializeReference, TypeSelector]
public ModifierBase modifier;
```

**Draw Modes:**
- `DrawMode.Default` — Foldout (collapsible)
- `DrawMode.NoFoldout` — Inline without foldout
- `DrawMode.Inline` — Compact

```csharp
[SerializeReference, TypeSelector(DrawMode.Inline)]
public EffectBase effect;
```

### `[SelectorName]` — Custom Dropdown Names

Rename types in selectors:

```csharp
[SelectorName("Modifier (2x Multiply)")]
public class MultiplyModifier : ModifierBase { }
```

Without this, class name is shown (namespace stripped automatically).

### `[HideInSelector]` — Hide a Class from the Dropdown

Excludes a class from `[TypeSelector]` dropdowns without making it an invalid `[SerializeReference]`
value. The type can still be assigned from code or loaded from an existing save — it is just not
offered as a choice in the picker.

```csharp
[HideInSelector]
public class LegacyModifier : ModifierBase { }
```

Pass `hideDerived: true` to also hide everything that derives from the type:

```csharp
[HideInSelector(hideDerived: true)]
public abstract class EditorOnlyEffectBase : EffectBase { }
```

Use it for intermediate base classes, deprecated implementations kept for save compatibility, or
editor-only / test scaffolding you don't want designers to pick. Your own tooling can query the same
rule via `SelectorVisibility.IsHidden(type)`.

### `[AssetSelector]` — Project Asset Search

Browse and select project assets with optional path filtering and grouping.

```csharp
[AssetSelector]
public ScriptableObject asset;

[AssetSelector(GroupMode.ByType, "Assets/Configs", "Assets/Data")]
public MyConfig config;
```

**GroupMode:**
- `None` — Flat list
- `ByPath` — Grouped by folder
- `ByType` — Grouped by asset type

### `[ComponentSelector]` — Component Picker

Select or create components on the same GameObject or a new child.

```csharp
[ComponentSelector]
public MyComponent component;

[ComponentSelector(AddMode.CreateChildGameObject, "Controller")]
public Controller controller;
```

### `[SubAssetSelector]` — Sub-Asset Selection

Pick sub-assets (e.g., sprites in atlases) from assets.

```csharp
[SubAssetSelector]
public Object subAsset;

[SubAssetSelector(ListMode.GroupedByType)]
public Sprite sprite;
```

### `[ScriptableObjectKey]` — Identity Tokens

Manage ScriptableObject references as identity keys with label + delete + dropdown UI.

```csharp
[ScriptableObjectKey]
public ScriptableObject key;
```

Useful for event systems, registries, or named configuration tokens.

### `[ShowEditor]` — Inline Editor UI

Display an inline property editor for complex fields.

```csharp
[ShowEditor]
public MyData data;
```

### `InterfaceReference<T>` — Polymorphic Interface Container

Generic wrapper allowing choice between Unity Objects (Component, ScriptableObject) or managed instances for interface types.

```csharp
[SerializeField]
public InterfaceReference<IController> controller;

// Use via implicit conversion
IController ctrl = controller; // Returns UnityEngine.Object OR managed instance
```

### `TagList<T>` — Typed ScriptableObject Lists

Base class for serialized lists of a specific ScriptableObject type.

```csharp
[Serializable]
public class ConfigTags : TagList<ConfigAsset> { }

[SerializeField]
public ConfigTags configs;
```

Implements `IList<T>` for runtime mutation.

## Common Patterns

**Polymorphic Gameplay:**
```csharp
[SerializeReference, TypeSelector(DrawMode.Default)]
public AbilityBase[] abilities;
```

**Asset Registry:**
```csharp
[AssetSelector(GroupMode.ByType)]
public ScriptableObject[] effects;
```

**Named Configuration Tokens:**
```csharp
[ScriptableObjectKey]
public ScriptableObject difficultyLevel;
```

**Interface with Fallback:**
```csharp
public InterfaceReference<IInputHandler> input;
```

## Custom Dropdown (`AdvancedDropdownBuilder`)

A reusable, styled dropdown used by the selector drawers and available on its own. You feed it
slash-separated paths (which become a folder tree) and a callback, then `Show(...)` it anchored to a
UI Toolkit element.

```csharp
new AdvancedDropdownBuilder()
    .AddElements(new[]
    {
        ("Weapons/Melee/Sword", swordId),
        ("Weapons/Ranged/Bow",  bowId),
        ("Consumables/Potion",  potionId),
    }, out var values)
    .SetCallback(i => Pick(values[i]))
    .WithRenderMode(DropdownRenderMode.MillerColumns)   // optional; default is SearchDrilldown
    .ShowTitle()                                        // optional; title bar is hidden by default
    .Build()
    .Show(button.worldBound);
```

Optional: `SetCreateOption(...)` adds a "＋ Create" row while searching, `SetItemContextHandler(...)`
enables right-click on leaves, `WithTitle(...)` sets the title text (shown only with `ShowTitle()`).

### Render modes

The same tree can be shown four ways via `WithRenderMode(DropdownRenderMode.*)`:

| Mode | Best for |
|---|---|
| **SearchDrilldown** (default) | Large sets where you know the name — search box + drill in/out |
| **MillerColumns** | Browsing a hierarchy, comparing siblings across columns |
| **Accordion** | Scanning several branches at once, expanded in place |
| **CascadingFlyouts** | Fast mouse traversal of a known path (browse-only, no search) |

![SearchDrilldown](Documentation~/images/search-drilldown.png)
![MillerColumns](Documentation~/images/miller-columns.png)
![Accordion](Documentation~/images/accordion.png)
![CascadingFlyouts](Documentation~/images/cascading-flyouts.png)

### Keyboard

Consistent across all modes, with two focus zones:

- **In the search field:** `↓` moves into the list (past the preselected first row); `↑` is ignored;
  `←`/`→` edit the search text; `Enter` activates the selection; typing filters.
- **In the list:** `↑`/`↓` move; `→`/`Enter` descend into a folder (`Enter` also selects a leaf);
  `←`/`Backspace` go back; any printable key jumps back to the search field and filters.
- `Esc` closes. CascadingFlyouts is browse-only (no search field).

Try all four live via **Tools → SelectorAttributes → Dropdown Render Mode Tester**.

## Notes

- `[TypeSelector]` requires `[SerializeReference]`; automatic filtering excludes abstracts, Unity Objects, and `[HideInSelector]` types
- `[AssetSelector]` searches by type; optional folder constraints
- All attributes work in arrays and lists
- Missing references are auto-cleaned on save

