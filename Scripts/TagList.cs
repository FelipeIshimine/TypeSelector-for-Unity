using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Base type for serializable lists that only accept ScriptableObject references.
/// Create a concrete subclass per ScriptableObject type to expose it in the inspector.
/// </summary>
[Serializable]
public class TagList<T> : IList<T>, IList, IReadOnlyList<T> where T : ScriptableObject
{
	[SerializeField] private List<T> _items = new List<T>();

	public TagList()
	{
	}

	public TagList(IEnumerable<T> collection)
	{
		_items = new List<T>(collection);
	}

	// IList<T>
	public T this[int index] { get => _items[index]; set => _items[index] = value; }
	public int Count => _items.Count;
	public bool IsReadOnly => false;
	public void Add(T item) => _items.Add(item);
	public void Clear() => _items.Clear();
	public bool Contains(T item) => _items.Contains(item);
	public void CopyTo(T[] array, int arrayIndex) => _items.CopyTo(array, arrayIndex);
	public IEnumerator<T> GetEnumerator() => _items.GetEnumerator();
	public int IndexOf(T item) => _items.IndexOf(item);
	public void Insert(int index, T item) => _items.Insert(index, item);
	public bool Remove(T item) => _items.Remove(item);
	public void RemoveAt(int index) => _items.RemoveAt(index);
	IEnumerator IEnumerable.GetEnumerator() => _items.GetEnumerator();

	// IList (non-generic, used by the drawer's runtime fallback)
	bool IList.IsFixedSize => false;
	bool IList.IsReadOnly => false;
	bool ICollection.IsSynchronized => false;
	object ICollection.SyncRoot => ((ICollection)_items).SyncRoot;
	object IList.this[int index] { get => _items[index]; set => _items[index] = (T)value; }
	int IList.Add(object value) { _items.Add((T)value); return _items.Count - 1; }
	bool IList.Contains(object value) => value is T t && _items.Contains(t);
	int IList.IndexOf(object value) => value is T t ? _items.IndexOf(t) : -1;
	void IList.Insert(int index, object value) => _items.Insert(index, (T)value);
	void IList.Remove(object value) { if (value is T t) _items.Remove(t); }
	void IList.RemoveAt(int index) => _items.RemoveAt(index);
	void ICollection.CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);
}