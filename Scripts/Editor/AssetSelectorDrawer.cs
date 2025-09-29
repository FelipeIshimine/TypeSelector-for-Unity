// ---------- Property drawer using UI Toolkit ----------

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using Object = UnityEngine.Object;
using ObjectField = UnityEditor.UIElements.ObjectField;

[CustomPropertyDrawer(typeof(AssetSelectorAttribute))]
public class AssetSelectorDrawer : PropertyDrawer
{
// Cache per-property asset lists so we don't query AssetDatabase on every UI rebuild
	private static readonly Dictionary<string, List<Item>> cache = new();
	private static readonly Dictionary<Texture, Texture2D> icons = new();

	public override VisualElement CreatePropertyGUI(SerializedProperty property)
	{
		var assetSelectorAttribute = attribute as AssetSelectorAttribute;

		var group = assetSelectorAttribute.Group;
		var folders = assetSelectorAttribute.Folders;
		
		return CreateProperty(property, GetFieldType(), group, folders);
	}

	public VisualElement CreateProperty(SerializedProperty property,Type fieldType, AssetSelectorAttribute.GroupMode group, params string[] folders)
	{
		var container = new VisualElement() { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, flexGrow = 1,flexShrink = 1} };
		
		if (property.propertyType != SerializedPropertyType.ObjectReference)
		{
			container.Add(new Label("[QuickAssetDropdown] can only be used on UnityEngine.Object fields"));
			return container;
		}
			
		ObjectField objectField = new ObjectField(property.displayName)
		{
			value = property.objectReferenceValue,
			objectType = fieldType,
			allowSceneObjects = false,
			style = { flexGrow = 1, flexShrink = 1}
		};
		
		objectField.BindProperty(property);
	
		var dropButton = new Button() { text = "▾", tooltip = "Select asset...", style = { width = 24 } };

		//var fieldType = GetFieldType();

		dropButton.clicked += () => ShowDropdown(property, dropButton,fieldType, group,folders);

		objectField.RegisterCallbackOnce<GeometryChangedEvent>(x =>
		{
			var label =objectField.Q<Label>();
			if (label != null)
			{
				label.style.width = Length.Percent(45);
				label.style.minWidth = 120;
				label.style.marginRight = 4;
			}
		});
		
		container.Add(objectField);
		container.Add(dropButton);
		return container;
	}

	private Type GetFieldType()
	{
// propertyInfo is available on PropertyDrawer
		if (fieldInfo != null)
		{
// If it's a field of type T, but might be UnityEngine.Object or subclass
			var t = fieldInfo.FieldType;
// If it's serialized as object reference wrapper like Object, try to find generic type arguments (rare)
			if (t == typeof(UnityEngine.Object) || t == typeof(object)) return typeof(UnityEngine.Object);
			return t.IsSubclassOf(typeof(UnityEngine.Object)) ? t : typeof(UnityEngine.Object);
		}
		return typeof(UnityEngine.Object);
	}


	private void ShowDropdown(SerializedProperty property, VisualElement button,Type fieldType, AssetSelectorAttribute.GroupMode group, string[] folders)
	{
		/*AssetSelectorAttribute asseSelectorAttribute = fieldInfo.GetCustomAttributes(typeof(AssetSelectorAttribute),false)[0] as AssetSelectorAttribute;
		string[] folders = asseSelectorAttribute.Folders?.Length > 0 ? asseSelectorAttribute.Folders : null;*/

		string filter = fieldType == typeof(UnityEngine.Object) ? "" : $"t:{fieldType.Name}";

		var key = (folders == null ? "<all>" : string.Join(";", folders)) + "|" +
			(string.IsNullOrEmpty(filter) ? "<alltypes>" : filter);

		HashSet<Type> typesFound = new HashSet<Type>();
		List<Type> typesForCreation = new List<Type>();

		
		if (!cache.TryGetValue(key, out List<Item> cached))
		{
			cached = new();
			
			var guids = AssetDatabase.FindAssets($"t:{fieldType.Name}",folders);

			foreach (string guid in guids)
			{
				var path = AssetDatabase.GUIDToAssetPath(guid);
				var asset = AssetDatabase.LoadAssetAtPath(path, fieldType);
				if (asset)
				{
					var foundType = asset.GetType();
					cached.Add(new Item(path, TextureToTexture2DCache(AssetDatabase.GetCachedIcon(path)),foundType));
					typesFound.Add(foundType);
				}
			}
			cached.Sort((x,y) => String.Compare(x.Path, y.Path, StringComparison.Ordinal));
			
			var scriptableObjectType = typeof(ScriptableObject);
			if (scriptableObjectType.IsAssignableFrom(fieldType))
			{
				typesForCreation.AddRange(TypeCache.GetTypesDerivedFrom(fieldType).Where(x=> !x.IsAbstract && !x.IsGenericType));
			}
			
			if (!fieldType.IsAbstract && !fieldType.IsGenericType && !typesForCreation.Contains(fieldType))
			{
				typesForCreation.Add(fieldType);
			}
			
			if (typesForCreation.Count == 1)
			{
				cached.Add(new Item($"-New-/{SelectorName.GetDisplayName(typesForCreation[0])}", null, typesForCreation[0]));
			}
			else
			{
				List<Item> creationItems = new();

				for (var index = 0; index < typesForCreation.Count; index++)
				{
					var type = typesForCreation[index];
					creationItems.Add(new Item($"-New-/{SelectorName.GetDisplayName(type)}", null,
						type));
				}

				creationItems.Sort((x,y) => String.Compare(x.Path, y.Path, StringComparison.Ordinal));
				cached.AddRange(creationItems);
			}
			
		}

		var dropdownBuilder = new AdvancedDropdownBuilder().WithTitle($"{fieldType.Name}");
		List<int> indices;

		switch (group)
		{
			case AssetSelectorAttribute.GroupMode.None:
				dropdownBuilder.AddElements(
					cached.Select(x =>
					{
						if (x.Path.Contains("-New-"))
						{
							return new AdvancedDropdownPath(x.Path, x.Icon);
						}
						return new AdvancedDropdownPath(Path.GetFileName(x.Path), x.Icon);
					}),
					out indices);
				break;
			case AssetSelectorAttribute.GroupMode.ByPath:
				dropdownBuilder.AddElements(cached.ConvertAll(x=> new AdvancedDropdownPath(x.Path, x.Icon)), out indices);
				break;
			case AssetSelectorAttribute.GroupMode.ByType:
				if (typesFound.Count > 1)
				{
					dropdownBuilder.AddElements(
						cached.Select(x =>
						{
							if (x.Path.Contains("-New-"))
							{
								var split = x.Path.Split("/");
								return new AdvancedDropdownPath($"{split[1]}/{split[0]}", x.Icon);
							}
							return new AdvancedDropdownPath($"{SelectorName.GetDisplayName(x.Type)}/{Path.GetFileName(x.Path)}", x.Icon);
						}),
						out indices);
				}
				else
				{
					dropdownBuilder.AddElements(
						cached.Select(x => new AdvancedDropdownPath(Path.GetFileName(x.Path), x.Icon)),
						out indices);
				}
				break;
			default:
				throw new ArgumentOutOfRangeException();
		}
	
			
		dropdownBuilder.SetCallback(OnSelection)
		               .Build()
		               .Show(button.worldBound);

		void OnSelection(int index)
		{
			var assetsRange = cached.Count - typesForCreation.Count;
			if (index < assetsRange)
			{
				var result = cached[indices[index]];
				
				var assetFound=AssetDatabase.LoadAssetAtPath<Object>(result.Path);
				property.objectReferenceValue = assetFound; 
				EditorUtility.SetDirty(property.serializedObject.targetObject);
				property.serializedObject.ApplyModifiedProperties();
			}
			else
			{
				var item = cached[index];
				var path = EditorUtility.SaveFilePanelInProject($"New {item.Type.Name}", $"New ", "asset", "");
				if(!string.IsNullOrEmpty(path))
				{
					var instance = ScriptableObject.CreateInstance(item.Type);
					AssetDatabase.CreateAsset(instance, path);
					property.objectReferenceValue = instance;
					EditorUtility.SetDirty(property.serializedObject.targetObject);
					property.serializedObject.ApplyModifiedProperties();
				}
			}
		}
	}
	
	public Texture2D TextureToTexture2DCache(Texture sourceRenderTexture)
	{
		if (!icons.TryGetValue(sourceRenderTexture, out var icon))
		{
			if (sourceRenderTexture == null)
			{
				Debug.LogError("Source RenderTexture is not assigned!");
				return null;
			}

			if (sourceRenderTexture == null) return null;

			// If it's already a readable Texture2D, return it (no copy).
			if (sourceRenderTexture is Texture2D tx2 && tx2.isReadable)
				return tx2;

			// Determine dimensions (fallback to 32 if unknown)
			int w = sourceRenderTexture.width  > 0 ? sourceRenderTexture.width : 32;
			int h = sourceRenderTexture.height > 0 ? sourceRenderTexture.height : 32;

			// Create temporary RenderTexture and blit the sourceRenderTexture into it
			var rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.Default, RenderTextureReadWrite.Linear);
			var prev = RenderTexture.active;
			try
			{
				Graphics.Blit(sourceRenderTexture, rt);
				RenderTexture.active = rt;

				// Read back pixels into a new Texture2D (RGBA32 is safe)
				var readable = new Texture2D(w, h, TextureFormat.RGBA32, false, true);
				readable.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
				readable.Apply(false, false);

				icons[sourceRenderTexture] = icon = readable;
			}
			finally
			{
				RenderTexture.active = prev;
				RenderTexture.ReleaseTemporary(rt);
			}

		}
		return icon;
	}
}

internal struct Item
{
	public readonly Texture2D Icon;
	public readonly Type Type;
	public readonly string Path;
	public Item(string path, Texture2D icon, Type type)
	{
		Icon = icon;
		Type = type;
		Path = path;
	}
}