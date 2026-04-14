using System;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field)]
public class TypeSelectorAttribute : PropertyAttribute
{
	public string Label { get; }
	public readonly DrawMode Mode;
	public TypeSelectorAttribute(DrawMode mode = DrawMode.Default, string label = null)
	{
		Label = label;
		this.Mode = mode;
	}
}

public enum DrawMode
{
	Default = 0, NoFoldout = 1, Inline = 2
}
