// filepath: c:\Users\DPC00176\VuTH Lib\Assets\_VuTH\Common\SharedLib\SerializableDictionary.cs

using System;
using System.Collections.Generic;
using UnityEngine;

namespace _VuTH.Common
{
    /// <summary>
    /// A Unity-serializable dictionary. Unity serializes the parallel key/value lists; at runtime we rebuild the Dictionary.
    /// Keys and values must be Unity-serializable types (primitive, enum, UnityEngine.Object, or [Serializable] types).
    /// </summary>
    /// <typeparam name="TKey">Key type</typeparam>
    /// <typeparam name="TValue">Value type</typeparam>
    [Serializable]
    public class SerializableDictionary<TKey, TValue> : Dictionary<TKey, TValue>, ISerializationCallbackReceiver
    {
        [SerializeField] private List<TKey> keys = new();
        [SerializeField] private List<TValue> values = new();

        // Optional: expose read-only access to backing lists for editor code (by name via SerializedProperty).
        public const string KeysFieldName = nameof(keys);
        public const string ValuesFieldName = nameof(values);

        public void OnBeforeSerialize()
        {
            // Sync from Dictionary (runtime) to backing lists (serialized)
            keys.Clear();
            values.Clear();
            foreach (var kvp in this)
            {
                keys.Add(kvp.Key);
                values.Add(kvp.Value);
            }
        }

        public void OnAfterDeserialize()
        {
            // Rebuild the dictionary from the serialized lists.
            Clear();
            if (keys == null || values == null) return;
            var count = Math.Min(keys.Count, values.Count);
            for (var i = 0; i < count; i++)
            {
                var key = keys[i];
                this[key] = values[i];
            }
        }
    }
}
