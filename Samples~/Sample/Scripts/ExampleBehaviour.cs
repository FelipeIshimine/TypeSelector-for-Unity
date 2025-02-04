using Type_Selector.Sample;
using TypeSelector;
using UnityEngine;

public class ExampleBehaviour : MonoBehaviour
{
	[SerializeReference, TypeSelector, Tooltip("With the TypeSelectorAttribute, this field can be set to any class that inherits from ExampleAbstractClass")]
	public ExampleAbstractClass abstractField;
	
	[Space]
	[SerializeReference, TypeSelector, Tooltip("The names on this classes was set using the TypeSelectorName")]
	public CustomNameExampleClass fieldCustomNames;
	[Space]
	[SerializeReference, TypeSelector(DrawMode.NoFoldout), Tooltip("This field uses the 'NoFoldout' DrawMode")]
	public CustomNameExampleClass noFoldoutField = new CustomNameExampleClassWithBoolAndFloat();
	[Space]
	[SerializeReference, TypeSelector(DrawMode.Inline), Tooltip("This field uses the 'Inline' DrawMode ")]
	public CustomNameExampleClass inlineField = new CustomNameExampleClassWithBoolAndFloat();
}