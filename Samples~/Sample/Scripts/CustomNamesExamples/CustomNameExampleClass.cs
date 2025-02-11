using TypeSelector;

namespace Type_Selector.Sample
{
	[System.Serializable, TypeSelectorName("BaseClass")]
	public class CustomNameExampleClass
	{
	}
	
	[System.Serializable, TypeSelectorName("Int")]
	public class CustomNameExampleClassWithInt : CustomNameExampleClass
	{
		public int intField;
	}
	
	[System.Serializable, TypeSelectorName("Bool")]
	public class CustomNameExampleClassWithBool : CustomNameExampleClass
	{
		public int boolField;
	}
	
	[System.Serializable, TypeSelectorName("SubMenu/IntWithFloat")]
	public class CustomNameExampleClassWithBoolAndFloat : CustomNameExampleClassWithBool
	{
		public float floatField;
	}
}