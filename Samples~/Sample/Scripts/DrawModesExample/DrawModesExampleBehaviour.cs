using Type_Selector.Sample;
using TypeSelector;
using UnityEngine;

public class DrawModesExampleBehaviour : MonoBehaviour
{
	[Header("DrawMode.Default")]
	[SerializeReference, TypeSelector(DrawMode.Default)]
	public CustomNameExampleClass defaultField = new CustomNameExampleClassWithBoolAndFloat();
	
	[Header("DrawMode.NoFoldout")]
	[SerializeReference, TypeSelector(DrawMode.NoFoldout)]
	public CustomNameExampleClass noFoldoutField = new CustomNameExampleClassWithBoolAndFloat();

	[Header("DrawMode.Inline")]
	[SerializeReference, TypeSelector(DrawMode.Inline)]
	public CustomNameExampleClass inlineField = new CustomNameExampleClassWithBoolAndFloat();
	
}