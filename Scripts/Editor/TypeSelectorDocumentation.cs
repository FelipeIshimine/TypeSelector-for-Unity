using UnityEditor;
using UnityEngine;

namespace Packages.ishimine.type_selector.Editor
{
	public static class TypeSelectorDocumentation
	{
		[MenuItem("Tools/Type Selector Documentation")]
		public static void OpenDocumentation()
		{
			Application.OpenURL(@"https://docs.google.com/document/d/18jEXBl_khysBDwu8PRY-Mx5zzYJJvavr3shg7RPdEDc/edit?usp=sharing");
		}
	}
}