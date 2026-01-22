// filepath: c:\Users\DPC00176\VuTH Lib\Assets\_VuTH\Common\SharedLib\DictionaryAttribute.cs

using UnityEngine;

namespace Common
{
    /// <summary>
    /// Optional attribute: put [Dictionary] on a SerializableDictionary field to force the custom drawer
    /// in case your Unity version doesn't apply the open-generic type drawer automatically.
    /// </summary>
    public class DictionaryAttribute : PropertyAttribute { }
}

