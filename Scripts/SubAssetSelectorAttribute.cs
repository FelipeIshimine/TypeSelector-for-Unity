using UnityEngine;

[System.AttributeUsage(System.AttributeTargets.Field, AllowMultiple = false)]
public sealed class SubAssetSelectorAttribute : PropertyAttribute
{
	public readonly ListMode Mode;
	public readonly System.Type DefaultType;

	public SubAssetSelectorAttribute(
		ListMode mode = ListMode.Flat,
		System.Type defaultType = null)
	{
		Mode = mode;
		DefaultType = defaultType;
	}

	public enum ListMode
	{
		Flat,
		GroupedByType,
	}
}