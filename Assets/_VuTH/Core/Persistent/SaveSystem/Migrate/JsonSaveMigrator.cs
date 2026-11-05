#nullable enable
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace _VuTH.Core.Persistent.SaveSystem.Migrate
{
    /// <summary>
    /// Base migrator that edits the payload as a JSON object (rename, move, add or remove fields) instead of as text.
    /// Requires a JSON serializer (the default <see cref="Serialize.NewtonsoftJsonSerializer"/>).
    /// </summary>
    /// <example>
    /// <code>
    /// sealed class RenameGoldToCoins : JsonSaveMigrator
    /// {
    ///     public RenameGoldToCoins() : base(1, 2) { }
    ///     protected override void Migrate(JObject data)
    ///     {
    ///         data["coins"] = data["gold"];
    ///         data.Remove("gold");
    ///     }
    /// }
    /// </code>
    /// </example>
    public abstract class JsonSaveMigrator : ISaveMigrator
    {
        public int FromVersion { get; }
        public int ToVersion { get; }

        protected JsonSaveMigrator(int fromVersion, int toVersion)
        {
            FromVersion = fromVersion;
            ToVersion = toVersion;
        }

        public string Migrate(string rawPayload)
        {
            var data = JObject.Parse(rawPayload);
            Migrate(data);
            return data.ToString(Formatting.None);
        }

        /// <summary>Changes <paramref name="data"/> in place from <see cref="FromVersion"/> to <see cref="ToVersion"/>.</summary>
        protected abstract void Migrate(JObject data);
    }
}
