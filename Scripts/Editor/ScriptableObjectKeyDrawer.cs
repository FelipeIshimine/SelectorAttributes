using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

/// <summary>
/// Abstract base for ScriptableObject key field drawers.
/// Subclass and apply [CustomPropertyDrawer(typeof(MyKey))] to register.
/// </summary>
public abstract class ScriptableObjectKeyDrawer<T> : PropertyDrawer where T : ScriptableObject
{
    public override VisualElement CreatePropertyGUI(SerializedProperty property)
    {
        var propertyCopy = property.Copy();

        var container = new VisualElement();
        container.style.flexDirection = FlexDirection.Row;
        container.style.alignItems    = Align.Center;

        var fieldLabel = new Label(property.displayName);
        fieldLabel.style.minWidth       = 120;
        fieldLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
        container.Add(fieldLabel);

        var keyNameLabel = new Label();
        keyNameLabel.style.flexGrow                = 1;
        keyNameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        keyNameLabel.style.unityTextAlign          = TextAnchor.MiddleLeft;
        keyNameLabel.style.marginLeft              = 4;
        container.Add(keyNameLabel);

        var deleteButton = new Button { text = "✕" };
        deleteButton.style.width        = 22;
        deleteButton.style.height       = 20;
        deleteButton.style.paddingLeft  = 0;
        deleteButton.style.paddingRight = 0;
        deleteButton.style.marginRight  = 2;
        deleteButton.style.color        = new Color(0.9f, 0.4f, 0.4f);
        deleteButton.clicked += () =>
        {
            var asset = propertyCopy.objectReferenceValue as T;
            if (asset == null) return;

            if (!EditorUtility.DisplayDialog(
                $"Delete {typeof(T).Name}",
                $"Delete \"{asset.name}\"?\n\nThis will remove the asset from the project. Any references to it will become missing.",
                "Delete", "Cancel"))
                return;

            propertyCopy.objectReferenceValue = null;
            propertyCopy.serializedObject.ApplyModifiedProperties();
            AssetDatabase.DeleteAsset(AssetDatabase.GetAssetPath(asset));
            Refresh(propertyCopy, keyNameLabel, deleteButton);
        };
        container.Add(deleteButton);

        var dropdownButton = new Button { text = "▾" };
        dropdownButton.style.width        = 22;
        dropdownButton.style.height       = 20;
        dropdownButton.style.paddingLeft  = 0;
        dropdownButton.style.paddingRight = 0;
        dropdownButton.clicked += () => ScriptableObjectKeyDropdown.Show(
            dropdownButton.worldBound,
            typeof(T),
            propertyCopy,
            () =>
            {
                propertyCopy.serializedObject.Update();
                Refresh(propertyCopy, keyNameLabel, deleteButton);
            });
        container.Add(dropdownButton);

        Refresh(propertyCopy, keyNameLabel, deleteButton);
        return container;
    }

    private static void Refresh(SerializedProperty property, Label nameLabel, Button deleteButton)
    {
        var asset            = property.objectReferenceValue as T;
        nameLabel.text        = asset != null ? asset.name : "(none)";
        nameLabel.style.color = asset != null
            ? new Color(0.7f, 0.9f, 0.7f)
            : new Color(0.9f, 0.4f, 0.4f);
        deleteButton.SetEnabled(asset != null);
    }
}
