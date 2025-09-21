using System;
using System.IO;
using System.Reflection;

[AttributeUsage(AttributeTargets.Class)]
public class SelectorNameAttribute : Attribute
{
	public readonly string Name;
	public SelectorNameAttribute(string name)
	{
		Name = name;
	}
}


public static class SelectorName
{
	public static string GetDisplayName(Type type)
	{
		if (type == null) return "NULL";
		var typeFullName = type.FullName;
		var typeNamespace = type.Namespace;
		string resultName;
		if (type.GetCustomAttribute(typeof(SelectorNameAttribute), false) is SelectorNameAttribute nameAttribute)
		{
			resultName = nameAttribute.Name;
		}
		else if (!string.IsNullOrEmpty(typeFullName) && !string.IsNullOrEmpty(typeNamespace))
		{
			resultName = typeFullName.Replace(typeNamespace, string.Empty).Replace(".", string.Empty);
		}
		else if (!string.IsNullOrEmpty(typeFullName))
		{
			resultName = type.FullName;
		}
		else
		{
			resultName = type.Name;
		}
		return resultName;
	}
}