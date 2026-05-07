using UnityEngine;

/// <summary>
/// Marks a ScriptableObject field as a keyed identity token.
/// Renders a label + delete + dropdown drawer via ScriptableObjectKeyDrawer.
/// </summary>
[System.AttributeUsage(System.AttributeTargets.Field)]
public class ScriptableObjectKeyAttribute : PropertyAttribute { }
