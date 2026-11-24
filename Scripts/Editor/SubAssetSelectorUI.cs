using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;

public static class SubAssetSelectorUI
{
	public static VisualElement Build(
		SerializedProperty property,
		FieldInfo fieldInfo)
	{
		if (property.propertyType != SerializedPropertyType.ObjectReference)
		{
			var errorLabel = new Label($"[SubAssetSelector] requires an Object reference field, got {property.propertyType}.");
			errorLabel.style.color    = new Color(0.9f, 0.3f, 0.3f);
			errorLabel.style.fontSize = 11;
			return errorLabel;
		}

		var container = new VisualElement();
		container.style.flexDirection = FlexDirection.Row;
		container.style.alignItems    = Align.Center;
		container.style.minHeight     = 20f;

		var fieldLabel = new Label($"[{fieldInfo.FieldType.Name}] {property.displayName}");
		fieldLabel.style.width          = 160;
		fieldLabel.style.flexGrow          = 1;
		fieldLabel.style.minWidth       = 30f;
		fieldLabel.style.flexShrink     = 0f;
		fieldLabel.style.overflow       = Overflow.Hidden;
		fieldLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

		var selectorButton = BuildSelectorButton(property, fieldInfo);
		selectorButton.style.flexGrow = 1;

		container.Add(fieldLabel);
		container.Add(selectorButton);
		container.Add(BuildClearButton(property));

		return container;
	}
	
	   private static VisualElement BuildClearButton(SerializedProperty property)
    {
        var button = new Button()
        {
	        text = "X",
	        style =
	        {
		        height = 18,
		        width = 18,
		        color = Color.gray
	        }
        };
        button.clicked += () =>
        {
	        SelectorAssignment.AssignToEveryTarget(property.serializedObject, property.propertyPath, null);
        };
      
        return button;
    }

    private static Button BuildSelectorButton(SerializedProperty property, FieldInfo fieldInfo)
    {
        var button = new Button();
        button.style.height        = 18f;
        button.style.flexDirection = FlexDirection.Row;
        button.style.alignItems    = Align.Center;
        button.style.paddingLeft   = 6f;
        button.style.paddingRight  = 4f;
        button.style.paddingTop    = 0f;
        button.style.paddingBottom = 0f;

        var valueLabel = new Label();
        valueLabel.style.flexGrow       = 1;
        valueLabel.style.overflow       = Overflow.Hidden;
        valueLabel.style.textOverflow   = TextOverflow.Ellipsis;
        valueLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

        var chevron = new Label("▾");
        chevron.style.fontSize   = 10f;
        chevron.style.marginLeft = 4f;
        chevron.style.flexShrink = 0f;
        chevron.style.color      = new Color(0.55f, 0.55f, 0.55f);

        button.Add(valueLabel);
        button.Add(chevron);

        SyncValueLabel(valueLabel, property.objectReferenceValue, fieldInfo.FieldType);

        button.TrackPropertyValue(property,
            p => SyncValueLabel(valueLabel, p.objectReferenceValue, fieldInfo.FieldType));

        button.clicked += () =>
        {
            var containerPath = ResolveContainerAssetPath(property);
            if (containerPath == null)
            {
                Debug.LogWarning("[SubAssetSelector] Cannot resolve a container asset path. " +
                                 "The object must be a saved ScriptableObject or a GameObject inside a saved scene.");
                return;
            }

            if (containerPath.EndsWith(".unity", StringComparison.OrdinalIgnoreCase))
            {
                Debug.LogWarning("[SubAssetSelector] Scene objects are not supported. " +
                    "Move the ScriptableObject field to a ScriptableObject asset instead.");
                return;
            }

          

        
            
            
            // Capture path, type, and mode now — SerializedProperty may be invalid in the async callback.
            var serializedObject = property.serializedObject;
            var propertyPath     = property.propertyPath;
            var fieldType        = fieldInfo.FieldType;
            
            var attr = fieldInfo
                       .GetCustomAttributes(typeof(SubAssetSelectorAttribute), false)
                       .FirstOrDefault() as SubAssetSelectorAttribute;
            var listMode = attr?.Mode ?? SubAssetSelectorAttribute.ListMode.Flat;
            var defaultType = attr?.DefaultType;
            
            
            SubAssetSelectorDropdown.Open(
                button.worldBound,
                containerPath,
                fieldType,
                listMode,
                selectedAsset =>
                {
                    SelectorAssignment.AssignToEveryTarget(serializedObject, propertyPath, selectedAsset);
                },
                defaultType
            );
        };

        return button;
    }

    private static void SyncValueLabel(Label valueLabel, Object currentValue, System.Type fieldType)
    {
        if (currentValue != null)
        {
            valueLabel.text        = $"{currentValue.name}";
            valueLabel.style.color = StyleKeyword.Null;
        }
        else
        {
            valueLabel.text        = $"None ({ObjectNames.NicifyVariableName(fieldType.Name)})";
            valueLabel.style.color = new Color(0.45f, 0.45f, 0.45f);
        }
    }

    private static string ResolveContainerAssetPath(SerializedProperty property)
    {
        var targetObject = property.serializedObject.targetObject;

        if (targetObject is ScriptableObject)
        {
            var path = AssetDatabase.GetAssetPath(targetObject);
            return string.IsNullOrEmpty(path) ? null : path;
        }

        if (targetObject is Component component && component.gameObject.scene.IsValid())
        {
            var scenePath = component.gameObject.scene.path;
            return string.IsNullOrEmpty(scenePath) ? null : scenePath;
        }

        if (targetObject is GameObject go && go.scene.IsValid())
        {
            var scenePath = go.scene.path;
            return string.IsNullOrEmpty(scenePath) ? null : scenePath;
        }

        return null;
    }
}