using Type_Selector.Sample;
using TypeSelector;
using UnityEngine;

public class CustomClassNameExamples : MonoBehaviour
{
	[Header("Classes with custom names Example")]
	[Space]
	[SerializeReference, TypeSelector, Tooltip("The names on this classes was set using the TypeSelectorName")]
	public CustomNameExampleClass fieldCustomNames;
}