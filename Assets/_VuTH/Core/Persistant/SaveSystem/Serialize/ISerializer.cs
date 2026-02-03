#nullable enable

namespace _VuTH.Core.Persistant.SaveSystem.Serialize
{
    /// <summary>
    /// Serialization contract - pluggable module.
    /// </summary>
    public interface ISerializer
    {
        /// <summary>
        /// Serializes an object to a JSON string.
        /// </summary>
        string Serialize<T>(T data);

        /// <summary>
        /// Deserializes a JSON string to an object.
        /// </summary>
        T Deserialize<T>(string json);
    }
}
