using System;

namespace _VuTH.Core.Persistent.DataPackage
{
    /// <summary>
    /// Generates <c>ExtractPayload</c> and <c>InjectPayload</c> of a <c>partial</c> <see cref="PersistencePackage{TData}"/>:
    /// each <see cref="PersistentField{T}"/> of the package is mapped to the DTO member of the same name (exact name
    /// first, then ignoring case, so <c>MasterVolume</c> maps to <c>masterVolume</c>).
    /// <code>
    /// [GeneratePayloadMapping]
    /// public partial class ProfilePackage : PersistencePackage&lt;ProfileDto&gt;
    /// {
    ///     public PersistentField&lt;long&gt; Gold { get; }   // ProfileDto.Gold
    /// }
    /// </code>
    /// A method you write yourself is kept: only the missing one is generated. Mapping problems are compile errors or
    /// warnings (VUTHP001 to VUTHP015, listed in the persistence documentation).
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class GeneratePayloadMappingAttribute : Attribute
    {
    }

    /// <summary>
    /// Like <see cref="GeneratePayloadMappingAttribute"/>, and also generates a field for every DTO member the package
    /// does not declare itself: <c>masterVolume</c> becomes a <c>MasterVolume</c> property. The field type follows the
    /// member's Unity attributes:
    /// <list type="bullet">
    /// <item><c>[Range(min, max)]</c> → <see cref="RangedPersistentField{T}"/> clamped to min..max;</item>
    /// <item><c>[Min(min)]</c> → <see cref="RangedPersistentField{T}"/> from min;</item>
    /// <item><c>[ColorUsage(showAlpha, hdr)]</c> on a Color → <see cref="ColorPersistentField"/>;</item>
    /// <item>an attribute implementing <see cref="IPersistentFieldFactory{T, TField}"/> → the field it creates;</item>
    /// <item>anything else → <see cref="PersistentField{T}"/>.</item>
    /// </list>
    /// <see cref="RejectOutOfRangeAttribute"/> refuses out-of-range values instead of clamping them, and attributes
    /// implementing <see cref="IPersistentFieldNormalizer{T}"/> add normalizations.
    /// Members marked <c>[NonSerialized]</c> are not saved, so they get no field. The defaults are those of a new DTO.
    /// Generated fields are created on first use, so the package needs no constructor code for them.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, Inherited = false)]
    public sealed class GeneratePersistentFieldsAttribute : Attribute
    {
    }

    /// <summary>Excludes a <see cref="PersistentField{T}"/> from the generated payload mapping.</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class PayloadIgnoreAttribute : Attribute
    {
    }

    /// <summary>Maps a <see cref="PersistentField{T}"/> to the DTO member <paramref name="name"/> instead of its own name.</summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class PayloadMemberAttribute : Attribute
    {
        public PayloadMemberAttribute(string name)
        {
            Name = name;
        }

        public string Name { get; }
    }
}
