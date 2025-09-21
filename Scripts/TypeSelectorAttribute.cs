using System;
using UnityEngine;

[AttributeUsage(AttributeTargets.Field)]
public class TypeSelectorAttribute : PropertyAttribute
{
	public readonly DrawMode Mode;
	public TypeSelectorAttribute(DrawMode mode = DrawMode.Default)
	{
		this.Mode = mode;
	}
}

public enum DrawMode
{
	Default = 0, NoFoldout = 1, Inline = 2
}
	