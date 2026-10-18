// ComponentSelectorDrawer.cs
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

[CustomPropertyDrawer(typeof(ComponentSelectorAttribute), true)]
public class ComponentSelectorDrawer : PropertyDrawer
{
	public override VisualElement CreatePropertyGUI(SerializedProperty property)
	{
		var root = new VisualElement();

		// Quick guards
		if (property.serializedObject.isEditingMultipleObjects)
		{
			root.Add(new Label("Multi-object editing not supported"));
			return root;
		}

		if (property.propertyType != SerializedPropertyType.ObjectReference)
		{
			root.Add(new Label("ComponentSelector must be applied to Component reference fields."));
			return root;
		}

		// Read attribute parameters
		var attr = attribute as ComponentSelectorAttribute ?? new ComponentSelectorAttribute();

		// Resolve the required Type (handles arrays/lists/elements)
		Type requiredType = ResolveRequiredType(property);
		if (requiredType == null || !typeof(Component).IsAssignableFrom(requiredType))
		{
			root.Add(new Label("Field type is not a Component-derived type."));
			return root;
		}

		// Build compatible concrete types
		var compatible = TypeCache.GetTypesDerivedFrom(requiredType)
		                          .Where(t => typeof(Component).IsAssignableFrom(t) && !t.IsAbstract)
		                          .OrderBy(t => t.Name)
		                          .ToList();

		if (!requiredType.IsAbstract && requiredType != typeof(Component) && !compatible.Contains(requiredType))
			compatible.Insert(0, requiredType);

		if (compatible.Count == 0)
		{
			root.Add(new Label($"No concrete Component types found for {requiredType.Name}"));
			return root;
		}

		// Stable names list with "(None)" first
		var names = new List<string> { "(None)" };
		names.AddRange(compatible.Select(t => t.Name));

		// Row layout: label | object field | popup | clear button
		var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

		var label = new Label(property.displayName)
		{
			style = { minWidth = 120, unityTextAlign = TextAnchor.MiddleLeft }
		};

		// ObjectField: shows current assigned component and supports drag & drop / picker
		var objField = new ObjectField
		{
			objectType = requiredType,
			allowSceneObjects = true,
			style = { flexGrow = 1, marginRight = 6, flexShrink = 1 }
		};

		// Initialize ObjectField value from property
		property.serializedObject.Update();
		objField.SetValueWithoutNotify(property.objectReferenceValue);

		// Popup (type chooser)
		property.serializedObject.Update();
		var currentComp = property.objectReferenceValue as Component;
		string curName = currentComp != null ? currentComp.GetType().Name : "(None)";
		int initialIndex = Math.Max(0, names.IndexOf(curName));
		var popup = new PopupField<string>(names, initialIndex) { style = { width = 160, marginRight = 6 } };

		// Clear button
		var destroyButton = new Button(() =>
			{
				property.serializedObject.Update();
				if (property.objectReferenceValue != null)
				{
					var toRemove = property.objectReferenceValue as Component;
					if (toRemove)
					{
						var target = toRemove.gameObject;
						Undo.DestroyObjectImmediate(toRemove);

						if (target.transform.childCount == 0 &&
						    !target.GetComponents<Component>().Any(x => x is not Transform))
						{
							Undo.DestroyObjectImmediate(target);
						}

						property.objectReferenceValue = null;
						property.serializedObject.ApplyModifiedProperties();
						objField.SetValueWithoutNotify(null);
						popup.SetValueWithoutNotify("(None)");
					}
				}
			})
			{ text = "✖", style = { width = 24 } };

		var clearButton = new Button(() =>
			{
				property.serializedObject.Update();
				if (property.objectReferenceValue != null)
				{
					var toRemove = property.objectReferenceValue as Component;
					if (toRemove)
					{
						var target = toRemove.gameObject;
						property.objectReferenceValue = null;
						property.serializedObject.ApplyModifiedProperties();
						objField.SetValueWithoutNotify(null);
						popup.SetValueWithoutNotify("(None)");
					}
				}
			})
			{ text = "○", style = { width = 24 } };

		// ObjectField change: user dragged/dropped or picked a component manually.
		objField.RegisterValueChangedCallback(evt =>
		{
			// If user assigned a Component (could be from scene or prefab instance)
			var assigned = evt.newValue as Component;
			property.serializedObject.Update();

			// If user assigned something not a Component or incompatible type, reject.
			if (assigned != null && !requiredType.IsAssignableFrom(assigned.GetType()))
			{
				// revert the field visually and do nothing.
				objField.SetValueWithoutNotify(property.objectReferenceValue);
				Debug.LogWarning($"Assigned component is not assignable to {requiredType.Name}");
				return;
			}

			// Assign the component reference directly (no component creation)
			property.objectReferenceValue = assigned;
			property.serializedObject.ApplyModifiedProperties();

			// Update popup to match the exact type name or "(None)"
			var newName = assigned != null ? assigned.GetType().Name : "(None)";
			if (names.Contains(newName))
				popup.SetValueWithoutNotify(newName);
			else
				popup.SetValueWithoutNotify("(None)");
		});

		// Popup change: create selected component (or None -> destroy)
		popup.RegisterValueChangedCallback(evt =>
		{
			if (evt.newValue == evt.previousValue) return;
			property.serializedObject.Update();

			// If user chose None -> destroy assigned component
			if (evt.newValue == "(None)")
			{
				if (property.objectReferenceValue != null)
				{
					var existing = property.objectReferenceValue as Component;
					if (existing)
					{
						Undo.DestroyObjectImmediate(existing);
						property.objectReferenceValue = null;
						property.serializedObject.ApplyModifiedProperties();
						objField.SetValueWithoutNotify(null);
					}
				}

				return;
			}

			// Map name -> type (names[0] == (None) so offset)
			int idx = names.IndexOf(evt.newValue);
			if (idx <= 0 || idx - 1 >= compatible.Count)
			{
				property.serializedObject.ApplyModifiedProperties();
				return;
			}

			Type selectedType = compatible[idx - 1];

			// Remove existing assigned component (undoable) to avoid duplicates
			if (property.objectReferenceValue != null)
			{
				var existing = property.objectReferenceValue as Component;
				if (existing)
				{
					Undo.DestroyObjectImmediate(existing);
					property.objectReferenceValue = null;
					objField.SetValueWithoutNotify(null);
				}
			}

			// Find host GameObject depending on AddMode
			var target = property.serializedObject.targetObject;
			GameObject host = null;
			if (target is Component compTarget) host = compTarget.gameObject;
			else if (target is GameObject goTarget) host = goTarget;

			if (host == null)
			{
				Debug.LogWarning("ComponentSelector: couldn't find a GameObject to add the component to.");
				property.serializedObject.ApplyModifiedProperties();
				return;
			}

			Component added = null;

			if (attr.addMode == AddMode.AddToSameGameObject)
			{
				added = Undo.AddComponent(host, selectedType);
			}
			else // CreateChildGameObject
			{
				var childName = $"{attr.childNamePrefix}{selectedType.Name}";
				var child = new GameObject(childName);
				Undo.RegisterCreatedObjectUndo(child, "Create child GameObject");
				child.transform.SetParent(host.transform, worldPositionStays: false);
				added = Undo.AddComponent(child, selectedType);
			}

			if (added != null)
			{
				property.objectReferenceValue = added;
				objField.SetValueWithoutNotify(added);
			}

			property.serializedObject.ApplyModifiedProperties();
		});

		// Assemble row
		row.Add(label);
		row.Add(objField);
		row.Add(popup);
		row.Add(clearButton);
		row.Add(destroyButton);
		root.Add(row);

		return root;
	}

	// Try to resolve the element type (handles arrays and List<T>)
	static Type ResolveRequiredType(SerializedProperty property)
	{
		var fi = property.serializedObject.targetObject.GetType().GetField(property.propertyPath.Split('[', '.')[0],
			System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic |
			System.Reflection.BindingFlags.Public);

		// Fallback to the drawer's FieldInfo if available (when called from PropertyDrawer fieldInfo)
		// But because this static helper doesn't have fieldInfo, we use reflection on targetObject above.
		if (fi != null)
		{
			var fType = fi.FieldType;
			if (fType.IsArray) return fType.GetElementType();
			if (fType.IsGenericType && fType.GetGenericTypeDefinition() == typeof(List<>))
				return fType.GetGenericArguments()[0];
			return fType;
		}

		// Last resort: attempt to use the current assigned object's type
		if (property.objectReferenceValue != null)
			return property.objectReferenceValue.GetType();

		return null;
	}
}
