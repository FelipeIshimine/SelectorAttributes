# Type Selector for Unity

![Unity_Sm42mt0HKq](https://github.com/user-attachments/assets/653679e3-e69b-4dd0-a892-5635d0306329)

## Overview

Type Selector is a custom Property Attribute and Property Drawer that enables intuitive selection and instantiation of polymorphic types in the Unity Inspector. It works seamlessly with `SerializedReference` fields, making it ideal for creating flexible, data-driven systems with multiple implementations of the same interface or base class.

## Features

- **Custom PropertyDrawer** — Intuitive dropdown UI for type selection directly in the Inspector
- **SerializedReference Support** — Full integration with Unity's polymorphic serialization system
- **Multiple Draw Modes** — Flexible display options (Default, NoFoldout, Inline)
- **Type Filtering** — Automatically filters invalid types (abstract classes, Unity objects)
- **Custom Display Names** — Use `[TypeSelectorName]` to customize type names in dropdowns
- **Serialization Safety** — Automatic cleanup of missing references

## Installation

1. Add the package to your project:
   - **Via Package Manager:** Add `file:../relative/path/to/TypeSelector-for-Unity` in Package Manager → Add Package from disk
   - **Via Submodule:** Clone/link into your `Packages/` folder
   - **Manual:** Copy the package folder to `Assets/Plugins/` or `Packages/`

2. Ensure the package includes the following structure:
   ```
   TypeSelector-for-Unity/
   ├── Runtime/
   │   └── TypeSelector.cs (attribute + filtering logic)
   ├── Editor/
   │   └── TypeSelectorPropertyDrawer.cs (Inspector UI)
   └── package.json
   ```

## Quick Start

### 1. Basic Usage with SerializedReference

```csharp
using TypeSelector;
using UnityEngine;

public class GameConfig : MonoBehaviour
{
    [SerializeReference, TypeSelector]
    public ModifierBase modifier;
}
```

### 2. Create a Base Class

```csharp
public abstract class ModifierBase
{
    public abstract void Apply(ref float value);
}
```

### 3. Implement Subclasses

```csharp
public class MultiplyModifier : ModifierBase
{
    [SerializeField] public float factor = 2f;
    
    public override void Apply(ref float value) => value *= factor;
}

public class AddModifier : ModifierBase
{
    [SerializeField] public float amount = 10f;
    
    public override void Apply(ref float value) => value += amount;
}
```

### 4. Inspect and Configure

In the Inspector, you can now:
- Click the dropdown on the `modifier` field
- Select `MultiplyModifier` or `AddModifier`
- Configure their public fields directly in the Inspector

## API Reference

### TypeSelector Attribute

```csharp
[TypeSelector(DrawMode mode = DrawMode.Default)]
```

Apply to `SerializedReference` fields to enable type selection.

**Parameters:**
- `mode` (optional) — How to display the selected type's properties:
  - `DrawMode.Default` — Standard foldout-based UI (default)
  - `DrawMode.NoFoldout` — Properties displayed inline without foldout
  - `DrawMode.Inline` — Minimal layout with compact spacing

**Example:**
```csharp
[SerializeReference, TypeSelector(DrawMode.Inline)]
public EffectBase effect;
```

### TypeSelectorName Attribute

```csharp
[TypeSelectorName("Custom Display Name")]
```

Apply to subclasses to customize their display name in the type dropdown.

**Example:**
```csharp
[TypeSelectorName("Multiply by 2x")]
public class MultiplyModifier : ModifierBase
{
    public override void Apply(ref float value) => value *= 2f;
}
```

## Draw Modes Explained

### Default (Foldout)
Shows a foldout header with the type name. Properties are nested under the header and can be collapsed.

```csharp
[SerializeReference, TypeSelector(DrawMode.Default)]
public ModifierBase modifier;
```

**Best for:** Complex types with many properties; when space is not a constraint.

### NoFoldout
Properties are displayed inline without a foldout header. The type name is shown but not collapsible.

```csharp
[SerializeReference, TypeSelector(DrawMode.NoFoldout)]
public ModifierBase modifier;
```

**Best for:** Simple types with few properties; when you want all data visible at a glance.

### Inline
Compact layout with minimal spacing. Useful for dense inspector layouts.

```csharp
[SerializeReference, TypeSelector(DrawMode.Inline)]
public ModifierBase modifier;
```

**Best for:** Inspector space constraints; arrays of polymorphic objects.

## Advanced Usage

### Multiple Polymorphic Fields

```csharp
public class EffectChain : MonoBehaviour
{
    [SerializeReference, TypeSelector(DrawMode.Inline)]
    public EffectBase[] effects = new EffectBase[3];
}
```

The drawer handles arrays seamlessly. Each element can be a different type.

### Custom Naming for Better UX

```csharp
[TypeSelectorName("Projectile (Fast)")]
public class FastProjectile : ProjectileBase { }

[TypeSelectorName("Projectile (Heavy)")]
public class HeavyProjectile : ProjectileBase { }

[TypeSelectorName("Projectile (Homing)")]
public class HomingProjectile : ProjectileBase { }
```

### Type Constraints

Only direct subclasses and implementing classes are shown in the dropdown. The package automatically filters out:
- Abstract classes (unless explicitly used as a base)
- Unity `Object` types (MonoBehaviour, ScriptableObject, etc.)
- Non-serializable types

### Combining with Other Attributes

```csharp
[SerializeReference, TypeSelector(DrawMode.Default)]
[Tooltip("Choose a modifier to apply to player stats")]
public ModifierBase statModifier;
```

## Important Notes

### SerializedReference is Required
Always pair `[TypeSelector]` with `[SerializeReference]`. Without it, polymorphic serialization won't work:

```csharp
// ✓ Correct
[SerializeReference, TypeSelector]
public BaseClass field;

// ✗ Wrong - won't serialize polymorphic data
[SerializeField, TypeSelector]
public BaseClass field;
```

### Type Filtering
The package automatically excludes:
- Abstract classes (as concrete types)
- Unity `Object` subclasses (MonoBehaviour, Component, etc.)
- Non-serializable types (internal classes, generic types with constraints)

If a type doesn't appear in the dropdown, check that it's:
1. A concrete (non-abstract) class
2. A direct subclass of the field type
3. Serializable (public, or marked `[SerializeField]`)

### Serialization Safety
Missing or broken references are automatically cleaned up during serialization. No manual intervention needed.

## Troubleshooting

### Dropdown shows no types
**Cause:** No concrete subclasses exist, or they're in the wrong namespace.
**Solution:** Ensure you have at least one concrete subclass of the base class, and it's in a valid C# namespace.

### Selected type doesn't appear in Inspector
**Cause:** The type may be abstract or not properly marked `public`.
**Solution:** Verify the class is `public class ClassName : BaseClass` (not `abstract`).

### Changes not persisting
**Cause:** Field is missing `[SerializeReference]` attribute.
**Solution:** Add `[SerializeReference]` to the field: `[SerializeReference, TypeSelector]`.

### Custom display name not showing
**Cause:** `[TypeSelectorName]` attribute is misspelled or not in the `TypeSelector` namespace.
**Solution:** Verify the attribute is spelled exactly: `[TypeSelectorName("Name")]` and the class has `using TypeSelector;`.

### Large Inspector slowdown with many types
**Cause:** Too many subclasses or expensive reflection during type discovery.
**Solution:** Consider grouping types by namespace or using nested classes to reduce the displayed type list.

## Common Use Cases

### Game Configuration
```csharp
public class GameSettings : MonoBehaviour
{
    [SerializeReference, TypeSelector]
    public DifficultyModifier difficulty;
}
```

### Gameplay Mechanics
```csharp
public class AbilitySystem : MonoBehaviour
{
    [SerializeReference, TypeSelector(DrawMode.Inline)]
    public AbilityBase[] abilities;
}
```

### Asset-Driven Behavior
```csharp
[CreateAssetMenu(fileName = "Effect")]
public class EffectAsset : ScriptableObject
{
    [SerializeReference, TypeSelector(DrawMode.Default)]
    public EffectBase effect;
}
```

## License

This package is part of the InfiniDrift project. See LICENSE for details.


