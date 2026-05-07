using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public class ScriptableObjectKeySettings : ScriptableSingleton<ScriptableObjectKeySettings>
{
    [SerializeField] private string keyRootFolder = "Assets/ScriptableObjects";
    [SerializeField] private List<TypeFolderOverride> typeOverrides = new();

    [Serializable]
    public class TypeFolderOverride
    {
        public string typeName;
        public string folder;
    }

    public string GetFolderForType(Type type)
    {
        foreach (var entry in typeOverrides)
            if (entry.typeName == type.Name) return entry.folder;
        return $"{keyRootFolder}/{type.Name}";
    }

    [MenuItem("Tools/Selector Attributes/Key Settings")]
    public static void ShowSettings() => Selection.activeObject = instance;
}
