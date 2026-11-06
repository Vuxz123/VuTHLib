using System;

namespace _VuTH.Core.Persistent.DataPackage
{
    /// <summary>
    /// Implemented by an attribute placed on a DTO member to constrain the value of the field generated for it
    /// ([GeneratePersistentFields]) and the value loaded into a declared field. Several normalizers apply in declaration
    /// order, after Unity's <c>[Range]</c> / <c>[Min]</c> / <c>[ColorUsage]</c>.
    /// <para>
    /// The generator writes <c>new YourAttribute(args)</c> into the package (no reflection at runtime, safe with IL2CPP
    /// and code stripping). <see cref="Normalize"/> must be pure: same input, same output, no side effect.
    /// </para>
    /// <code>
    /// public sealed class StepAttribute : Attribute, IPersistentFieldNormalizer&lt;float&gt;
    /// {
    ///     private readonly float _step;
    ///     public StepAttribute(float step) => _step = step;
    ///     public float Normalize(float value) => Mathf.Round(value / _step) * _step;
    /// }
    /// </code>
    /// </summary>
    /// <typeparam name="T">Type of the DTO member (checked at compile time, VUTHP011).</typeparam>
    public interface IPersistentFieldNormalizer<T>
    {
        T Normalize(T value);
    }

    /// <summary>
    /// Implemented by an attribute placed on a DTO member to choose the field type generated for it
    /// ([GeneratePersistentFields]), e.g. a currency field with its own API. The generated property has the type
    /// <typeparamref name="TField"/>. At most one factory per member (VUTHP012); Unity constraints and normalizers on the
    /// same member are then ignored (VUTHP013): the factory builds the whole field.
    /// <para>
    /// <see cref="Create"/> must only construct the field: no subscription, no service lookup, no other side effect.
    /// </para>
    /// <code>
    /// public sealed class CurrencyAttribute : Attribute, IPersistentFieldFactory&lt;long, CurrencyField&gt;
    /// {
    ///     public CurrencyField Create(IPersistencePackage owner, long defaultValue) => new CurrencyField(owner, defaultValue);
    /// }
    /// </code>
    /// </summary>
    /// <typeparam name="T">Type of the DTO member (checked at compile time, VUTHP011).</typeparam>
    /// <typeparam name="TField">The field type created.</typeparam>
    public interface IPersistentFieldFactory<T, out TField> where TField : PersistentField<T>
    {
        TField Create(IPersistencePackage owner, T defaultValue);
    }

    /// <summary>What a <see cref="RangedPersistentField{T}"/> does with a value the game sets outside its range.</summary>
    public enum RangeMode
    {
        /// <summary>Clamp it into the range. Right for settings (a volume of 1.2 becomes 1).</summary>
        Clamp,

        /// <summary>
        /// Refuse it: the field keeps its value and an error is logged. Right for currencies and counters, where a
        /// clamp would hide a logic error (spending 100 gold with 50 would silently leave 0).
        /// Loaded data is still clamped, so a damaged save is repaired rather than lost.
        /// </summary>
        Reject,
    }

    /// <summary>
    /// On a DTO member with <c>[Range]</c> or <c>[Min]</c>: the generated field uses <see cref="RangeMode.Reject"/>
    /// instead of clamping the values the game sets.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public sealed class RejectOutOfRangeAttribute : Attribute
    {
    }
}
