using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace TypeSelector
{
	public class QuickAdvancedDropdown : AdvancedDropdown
	{
		private readonly AdvancedDropdownItem root;
		private readonly Action<int> callback;

		public QuickAdvancedDropdown(AdvancedDropdownItem root, Action<int> action) : base(new AdvancedDropdownState())
		{
			this.root = root;
			callback = action;
		}

		protected override AdvancedDropdownItem BuildRoot()
		{
			return root;
		}

		protected override void ItemSelected(AdvancedDropdownItem item) => callback.Invoke(item.id);
	}


	public class AdvancedDropdownBuilder
	{
		private string title;
		private List<AdvancedDropdownPath> values = new();
		private int startingID;
		private Action<int> callback;
		private char splitCharacter = '/';

		public AdvancedDropdownBuilder WithTitle(string title)
		{
			this.title = title;
			return this;
		}

		public AdvancedDropdownBuilder AddElements(IEnumerable<string> elements, out List<int> indices)
		{
			indices = new List<int>();
			foreach (string element in elements)
			{
				values.Add(new AdvancedDropdownPath(element, null));
				indices.Add(values.Count - 1);
			}

			return this;
		}

		public AdvancedDropdownBuilder AddElements(IEnumerable<(string, Texture2D)> elements, out List<int> indices)
		{
			indices = new List<int>();
			foreach (var element in elements)
			{
				values.Add(new AdvancedDropdownPath(element.Item1,element.Item2));
				indices.Add(values.Count - 1);
			}

			return this;
		}
		public AdvancedDropdownBuilder AddElements(IEnumerable<AdvancedDropdownPath> elements, out List<int> indices)
		{
			indices = new List<int>();
			foreach (var element in elements)
			{
				values.Add(element);
				indices.Add(values.Count - 1);
			}
			return this;
		}

		public AdvancedDropdownBuilder AddElement(string element, Texture2D icon, out int index)
		{
			values.Add(new (element, icon));
			index = values.Count - 1;
			return this;
		}

		public AdvancedDropdownBuilder AddElement(string element, out int index)
		{
			values.Add(new(element, null));
			index = values.Count - 1;
			return this;
		}

		public AdvancedDropdownBuilder SetSplitCharacter(char splitChar)
		{
			this.splitCharacter = splitChar;
			return this;
		}

		public AdvancedDropdownBuilder SetCallback(Action<int> callback)
		{
			this.callback = callback;
			return this;
		}

		public AdvancedDropdown Build()
		{
			Dictionary<string, AdvancedDropdownItem> pathToDropdownItems =
				new Dictionary<string, AdvancedDropdownItem>();
			AdvancedDropdownItem root = new AdvancedDropdownItem(title);
			AdvancedDropdownItem startingItem = null;
			for (int i = 0; i < values.Count; i++)
			{
				var split = values[i].Path.Split(splitCharacter);
				var previous = root;
				for (var j = 0; j < split.Length; j++)
				{
					var s = split[j];

					bool isLast = j == split.Length - 1;
					
					if (!pathToDropdownItems.TryGetValue(s, out var item) || isLast)
					{
						item = pathToDropdownItems[s] = new AdvancedDropdownItem(s)
						{
							id = i,
							icon = isLast?values[i].Icon:null
						};

						if (i == startingID)
						{
							startingItem = previous;
						}

						previous.AddChild(item);
					}

					previous = item;
				}
			}

			return new QuickAdvancedDropdown(root, callback);
		}

		public AdvancedDropdownBuilder SetStartingId(int i)
		{
			startingID = i;
			return this;
		}

		// Reflection method to set the initial selection
		private void SetInitialSelection(AdvancedDropdown dropdown, AdvancedDropdownItem startingItem)
		{
			if (startingItem == null) return;

			// Use reflection to access the internal `_selectedItem` field of `AdvancedDropdown`
			var dropdownType = typeof(AdvancedDropdown);
			var selectedItemField =
				dropdownType.GetField("itemSelected", BindingFlags.NonPublic | BindingFlags.Instance);
			if (selectedItemField != null)
			{
				// Set the initial selection to the starting item
				selectedItemField.SetValue(dropdown, startingItem);
				Debug.Log($"Initial item set via reflection: {startingItem.name}");
			}
			else
			{
				Debug.LogError("Could not find the internal 'itemSelected' field via reflection.");
			}
		}
	}
}

public struct AdvancedDropdownPath
{
	public readonly string Path;
	public readonly Texture2D Icon;

	public AdvancedDropdownPath(string path, Texture2D icon)
	{
		Path = path;
		Icon = icon;
	}
}