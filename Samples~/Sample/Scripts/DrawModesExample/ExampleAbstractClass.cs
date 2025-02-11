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

}


