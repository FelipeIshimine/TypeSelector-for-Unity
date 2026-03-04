using System;
using UnityEngine;

[Serializable]
public class InterfaceReference<T> where T : class
{
    public bool useObject = true;
    public UnityEngine.Object objectValue;
    [SerializeReference, TypeSelector(DrawMode.Inline)]
    public T managedValue;

    public T Value => useObject
        ? objectValue as T
        : managedValue;
    
    public static implicit operator T(InterfaceReference<T> value) => value.Value;
}