using System;
using UnityEditor;
using UnityEngine;

internal static class SubAssetDefaults
{
	public const string DefaultName = "[DEFAULT]";

	public static ScriptableObject GetOrCreateDefault(string containerPath, System.Type fieldType,Type explicitDefaultType = null)
	{
		var targetType = explicitDefaultType ?? fieldType;

		if (targetType.IsAbstract)
		{
			//Debug.LogError($"[SubAssetSelector] Cannot create Default for abstract type '{fieldType.Name}' without an explicit default type.");
			return null;
		}
		
		var all = AssetDatabase.LoadAllAssetsAtPath(containerPath);

		// Try find existing
		foreach (var obj in all)
		{
			if (obj is ScriptableObject so &&
			    so.name == DefaultName     &&
			    fieldType.IsAssignableFrom(so.GetType()))
			{
				return so;
			}
		}

		// Create if missing
		var instance = ScriptableObject.CreateInstance(fieldType);
		instance.name = DefaultName;

		AssetDatabase.AddObjectToAsset(instance, containerPath);
		AssetDatabase.SaveAssets();

		return instance;
	}
}