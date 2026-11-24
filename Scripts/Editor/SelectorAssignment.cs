using System;
using UnityEditor;
using Object = UnityEngine.Object;

internal static class SelectorAssignment
{
    public static void AssignToEveryTarget(SerializedObject shared, string propertyPath, Object value)
    {
        shared.ApplyModifiedProperties();
        foreach (var target in shared.targetObjects)
        {
            using var single = new SerializedObject(target);
            var property = single.FindProperty(propertyPath);
            if (property == null)
                throw new InvalidOperationException($"[Selector] '{target.name}' has no property '{propertyPath}'.");
            property.objectReferenceValue = value;
            single.ApplyModifiedProperties();
        }
        shared.Update();
    }
}
