#nullable enable

using System;
using _VuTH.Common.Log;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using VContainer;

namespace _VuTH.Core.Persistant.SaveSystem.Serialize
{
    /// <summary>
    /// JSON serialization implementation using Newtonsoft.Json (Json.NET).
    /// Configured for backward compatibility with IncludeFields=true behavior.
    /// </summary>
    [Serializable]
    public class NewtonsoftJsonSerializer : ISerializer
    {
        private readonly JsonSerializerSettings _settings;

        public NewtonsoftJsonSerializer()
        {
            // Configure CamelCaseNamingStrategy for case-insensitive property matching
            // and to process dictionary keys while preserving specified names
            var namingStrategy = new CamelCaseNamingStrategy
            {
                ProcessDictionaryKeys = true,
                OverrideSpecifiedNames = false
            };

            _settings = new JsonSerializerSettings
            {
                // Formatting: No indentation for compact storage
                Formatting = Formatting.None,

                // Case-insensitive property matching via CamelCaseNamingStrategy
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = namingStrategy,
                    IgnoreSerializableAttribute = false
                },

                // Ignore missing members for forward compatibility
                MissingMemberHandling = MissingMemberHandling.Ignore,

                // Include null values in serialization
                NullValueHandling = NullValueHandling.Include,

                // Include default values
                DefaultValueHandling = DefaultValueHandling.Include,

                // ISO 8601 date format
                DateFormatString = "yyyy-MM-ddTHH:mm:ss.fffZ",

                // Allow non-public default constructors
                ConstructorHandling = ConstructorHandling.AllowNonPublicDefaultConstructor,

                // Ignore metadata properties
                MetadataPropertyHandling = MetadataPropertyHandling.Ignore,

                // No type name embedding
                TypeNameHandling = TypeNameHandling.None,

                // No reference tracking
                PreserveReferencesHandling = PreserveReferencesHandling.None,

                // Ignore reference loops
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };
        }

        /// <summary>
        /// Serializes an object to a JSON string.
        /// </summary>
        public string Serialize<T>(T data)
        {
            try
            {
                return JsonConvert.SerializeObject(data, _settings);
            }
            catch (Exception ex)
            {
                this.LogError($"Serialize failed: {ex.Message}");
                throw new InvalidOperationException($"Failed to serialize {typeof(T).Name}: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Deserializes a JSON string to an object.
        /// </summary>
        public T Deserialize<T>(string json)
        {
            try
            {
                return JsonConvert.DeserializeObject<T>(json, _settings)
                    ?? throw new InvalidOperationException("Deserialization returned null");
            }
            catch (Exception ex)
            {
                this.LogError($"Deserialize failed: {ex.Message}");
                throw new InvalidOperationException($"Failed to deserialize {typeof(T).Name}: {ex.Message}", ex);
            }
        }
    }
}
