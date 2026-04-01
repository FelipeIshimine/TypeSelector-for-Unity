using UnityEngine;

[System.AttributeUsage(System.AttributeTargets.Field, AllowMultiple = false)]
public sealed class SubAssetSelectorAttribute : PropertyAttribute
{
	public readonly ListMode Mode;

	public SubAssetSelectorAttribute(ListMode mode = ListMode.Flat)
	{
		Mode = mode;
	}

	public enum ListMode
	{
		/// <summary>
		/// All sub-assets in a single flat list. Shows a subtle type label on the right of each row.
		/// </summary>
		Flat,

		/// <summary>
		/// Sub-assets grouped under their concrete type name. No type label on individual rows.
		/// Groups collapse into a flat list when the user is searching.
		/// </summary>
		GroupedByType,
	}
}