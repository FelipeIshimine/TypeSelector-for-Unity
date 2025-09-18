using System;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class AssetSelectorAttribute : PropertyAttribute
{
	public readonly GroupMode Group;

	/// <summary>
	/// Optional folders to search in. Example: "Assets/Art","Assets/Configs". If empty, searches whole project.
	/// </summary>
	public string[] Folders;
	public AssetSelectorAttribute(GroupMode groupMode = GroupMode.None, params string[] folders)
	{
		Group = groupMode;
		this.Folders = folders ?? Array.Empty<string>();
	}

	public enum GroupMode
	{
		None,
		ByPath,
		ByType
	}
	
}


