#nullable enable
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using UnityEngine.Serialization;

namespace _VuTH.Core.Persistent.SaveSystem.Serialize
{
    /// <summary>
    /// Contract resolver following Unity's serialization attributes, so a DTO behaves the same in a save as in the
    /// Inspector:
    /// <list type="bullet">
    /// <item><c>[FormerlySerializedAs("old")]</c>: a save written before the member was renamed is still read (the old
    /// name is accepted when reading, the new one is written).</item>
    /// <item><c>[SerializeField]</c> private fields and <c>[NonSerialized]</c>: already honored for <c>[Serializable]</c>
    /// types (<see cref="DefaultContractResolver.IgnoreSerializableAttribute"/> is false).</item>
    /// </list>
    /// </summary>
    public sealed class UnityContractResolver : DefaultContractResolver
    {
        protected override IList<JsonProperty> CreateProperties(System.Type type, MemberSerialization memberSerialization)
        {
            var properties = base.CreateProperties(type, memberSerialization);
            var names = new HashSet<string>(properties.Select(p => p.PropertyName ?? string.Empty));
            var formerNames = new List<JsonProperty>();

            foreach (var property in properties)
            {
                var member = property.DeclaringType?.GetMember(property.UnderlyingName ?? string.Empty,
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).FirstOrDefault();
                if (member == null || !property.Writable) continue;

                foreach (var former in member.GetCustomAttributes<FormerlySerializedAsAttribute>(true))
                {
                    var oldName = ResolvePropertyName(former.oldName);
                    if (string.IsNullOrEmpty(oldName) || !names.Add(oldName)) continue;

                    // Read-only alias: fills the renamed member, never written back.
                    formerNames.Add(new JsonProperty
                    {
                        PropertyName = oldName,
                        UnderlyingName = property.UnderlyingName,
                        PropertyType = property.PropertyType,
                        DeclaringType = property.DeclaringType,
                        ValueProvider = property.ValueProvider,
                        Converter = property.Converter,
                        Readable = false,
                        Writable = true,
                        ShouldSerialize = _ => false,
                    });
                }
            }

            foreach (var alias in formerNames) properties.Add(alias);
            return properties;
        }
    }
}
