using System;
using UnityEngine;

/// <summary>
/// Displays a searchable dropdown of sub-assets of the field's type stored inside the same
/// container asset (ScriptableObject) or scene file (GameObject in a scene).
/// Supports inline creation of new sub-assets by typing a name and selecting "＋ Create".
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public class SubAssetSelectorAttribute : PropertyAttribute { }