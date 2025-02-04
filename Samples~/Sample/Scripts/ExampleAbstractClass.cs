using TypeSelector;

namespace Type_Selector.Sample
{
	//Classes should always have the [System.Serializable] attribute
	[System.Serializable]
	public class ExampleAbstractClass
	{
		public string textField;
	}

	[System.Serializable]
	public class ExampleClassWithInt : ExampleAbstractClass
	{
		public int intField;
	}
	
	[System.Serializable]
	public class ExampleClassWithBool : ExampleAbstractClass
	{
		public int boolField;
	}
	
	[System.Serializable]
	public class ExampleClassWithBoolAndFloat : ExampleClassWithBool
	{
		public float floatField;
	}


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


