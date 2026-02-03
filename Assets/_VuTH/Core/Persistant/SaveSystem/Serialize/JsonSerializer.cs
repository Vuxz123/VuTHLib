using System.Text.Json;

namespace _VuTH.Core.Persistant.SaveSystem.Serialize
{
    /// <summary>
    /// JSON-based serializer using System.Text.Json.
    /// Pluggable implementation of ISerializer.
    /// </summary>
    public class JsonSerializer : ISerializer
    {
        private readonly JsonSerializerOptions _options;

        public JsonSerializer()
        {
            _options = new JsonSerializerOptions
            {
                WriteIndented = false,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                PropertyNameCaseInsensitive = true,
                IgnoreReadOnlyProperties = false,
                IncludeFields = true
            };
        }

        public JsonSerializer(JsonSerializerOptions options)
        {
            _options = options;
        }

        public string Serialize<T>(T data)
        {
            return System.Text.Json.JsonSerializer.Serialize(data, _options);
        }

        public T Deserialize<T>(string rawData)
        {
            return System.Text.Json.JsonSerializer.Deserialize<T>(rawData, _options)!;
        }
    }
}
