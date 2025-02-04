using System;

namespace TypeSelector
{
	[AttributeUsage(AttributeTargets.Class)]
	public class TypeSelectorNameAttribute : Attribute
	{
		public readonly string Name;
		public TypeSelectorNameAttribute(string name)
		{
			Name = name;
		}
	}
}