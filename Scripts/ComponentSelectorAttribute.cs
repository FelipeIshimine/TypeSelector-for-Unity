using System;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field, AllowMultiple = false)]
public sealed class ComponentSelectorAttribute : PropertyAttribute
{
	public readonly AddMode addMode;
	public readonly string childNamePrefix;

	/// <summary>
	/// addMode: where to add the created component (same GameObject or a new child).
	/// childNamePrefix: prefix for the child GameObject name (if addMode == CreateChildGameObject).
	/// </summary>
	public ComponentSelectorAttribute(AddMode addMode = AddMode.AddToSameGameObject, string childNamePrefix = "")
	{
		this.addMode = addMode;
		this.childNamePrefix = childNamePrefix;
	}
}

public enum AddMode
{
	AddToSameGameObject = 0, CreateChildGameObject = 1,
}
