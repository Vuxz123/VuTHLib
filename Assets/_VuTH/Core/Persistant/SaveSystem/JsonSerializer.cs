#nullable enable
using Newtonsoft.Json;
using _VuTH.Core.Persistant.SaveSystem.Serialize;

namespace _VuTH.Core.Persistant.SaveSystem
{
    /// <summary>
    /// JSON serialization implementation using Newtonsoft.Json (Json.NET).
    /// </summary>
    public class JsonSerializer : ISerializer
    {
        private readonly JsonSerializerSettings _settings;

        public JsonSerializer()
        {
            _settings = new JsonSerializerSettings
            {
                Formatting = Formatting.None,
                DateFormatString = "yyyy-MM-ddTHH:mm:ss.fffZ"
            };
        }

        public string Serialize<T>(T data)
        {
            try
            {
                return JsonConvert.SerializeObject(data, _settings);
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[JsonSerializer] Serialize failed: {ex.Message}");
                throw;
            }
        }

        public T Deserialize<T>(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(json, _settings)
                    ?? throw new System.InvalidOperationException("Deserialization returned null");
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"[JsonSerializer] Deserialize failed: {ex.Message}");
                throw;
            }
        }
    }
}
