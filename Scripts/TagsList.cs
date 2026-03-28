using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PillsList
{
    /// <summary>
    /// Base type for serializable lists that only accept ScriptableObject references.
    /// Create a concrete subclass per ScriptableObject type to expose it in the inspector.
    /// </summary>
    [Serializable]
    public class TagsList<T> : List<T>, IList where T : ScriptableObject
    {
        public TagsList()
        {
        }

        public TagsList(IEnumerable<T> collection) : base(collection)
        {
        }
    }
}
