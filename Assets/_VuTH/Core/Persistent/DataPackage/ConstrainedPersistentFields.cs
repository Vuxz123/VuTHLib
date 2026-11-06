using System;
using _VuTH.Common.Log;
using UnityEngine;

namespace _VuTH.Core.Persistent.DataPackage
{
    /// <summary>
    /// A <see cref="PersistentField{T}"/> kept within <see cref="Min"/>..<see cref="Max"/>. Created by the source generator
    /// for DTO members marked with Unity's <c>[Range]</c> or <c>[Min]</c>; <see cref="Min"/> / <see cref="Max"/> can drive a
    /// UI slider.
    /// <list type="bullet">
    /// <item>The default and loaded data are always clamped (a damaged save is repaired, not lost).</item>
    /// <item>A value the game sets is clamped (<see cref="RangeMode.Clamp"/>, settings) or refused with an error
    /// (<see cref="RangeMode.Reject"/>, currencies); use <see cref="PersistentField{T}.TrySetValue"/> to test it.</item>
    /// </list>
    /// </summary>
    public class RangedPersistentField<T> : PersistentField<T> where T : struct, IComparable<T>
    {
        /// <param name="normalize">
        /// Optional extra normalization (e.g. a step). It runs between two clamps, so its result is still within the
        /// range: Min..Max always holds, whatever the normalizer does.
        /// </param>
        public RangedPersistentField(IPersistencePackage owner, T defaultValue, T min, T max,
            RangeMode mode = RangeMode.Clamp, Func<T, T> normalize = null, bool notifyOnChange = true)
            : base(owner, defaultValue, notifyOnChange, Chain(min, max, normalize))
        {
            if (max.CompareTo(min) < 0) throw new ArgumentException($"max ({max}) is below min ({min}).", nameof(max));

            Min = min;
            Max = max;
            Mode = mode;
        }

        public T Min { get; }
        public T Max { get; }
        public RangeMode Mode { get; }

        /// <summary>Whether <paramref name="value"/> is within <see cref="Min"/>..<see cref="Max"/>.</summary>
        public bool IsInRange(T value) => value.CompareTo(Min) >= 0 && value.CompareTo(Max) <= 0;

        protected override bool AcceptsValue(T value)
        {
            if (Mode == RangeMode.Clamp || IsInRange(value)) return true;

            this.LogError($"Rejected {value}: outside {Min}..{Max}. The field keeps {Value}.");
            return false;
        }

        private static Func<T, T> Chain(T min, T max, Func<T, T> normalize)
        {
            if (normalize == null) return value => PersistentFieldConstraints.Clamp(value, min, max);

            // Clamp, normalize, clamp again: [Range(0, 0.9)] + a 0.25 step must not turn 0.9 into 1.
            return value => PersistentFieldConstraints.Clamp(
                normalize(PersistentFieldConstraints.Clamp(value, min, max)), min, max);
        }
    }

    /// <summary>
    /// A color <see cref="PersistentField{T}"/> following Unity's <c>[ColorUsage]</c>: without alpha the alpha is always 1,
    /// without HDR the channels stay within 0..1. Created by the source generator for DTO members marked
    /// <c>[ColorUsage]</c>.
    /// </summary>
    public class ColorPersistentField : PersistentField<Color>
    {
        /// <param name="normalize">Optional extra normalization, between two applications of the color rules.</param>
        public ColorPersistentField(IPersistencePackage owner, Color defaultValue, bool showAlpha = true, bool hdr = false,
            Func<Color, Color> normalize = null, bool notifyOnChange = true)
            : base(owner, defaultValue, notifyOnChange, normalize == null
                ? value => PersistentFieldConstraints.Color(value, showAlpha, hdr)
                : value => PersistentFieldConstraints.Color(
                    normalize(PersistentFieldConstraints.Color(value, showAlpha, hdr)), showAlpha, hdr))
        {
            ShowAlpha = showAlpha;
            Hdr = hdr;
        }

        public bool ShowAlpha { get; }
        public bool Hdr { get; }
    }

    /// <summary>The normalizations behind the constrained fields; also used by generated <c>InjectPayload</c> code.</summary>
    public static class PersistentFieldConstraints
    {
        /// <summary><paramref name="value"/> within <paramref name="min"/>..<paramref name="max"/> (NaN becomes min).</summary>
        public static T Clamp<T>(T value, T min, T max) where T : IComparable<T>
        {
            if (value.CompareTo(min) < 0) return min;
            if (value.CompareTo(max) > 0) return max;
            return value;
        }

        /// <summary>Unity's <c>[ColorUsage]</c>: alpha 1 without alpha, channels within 0..1 without HDR.</summary>
        public static Color Color(Color value, bool showAlpha, bool hdr)
        {
            if (!hdr)
            {
                value.r = Mathf.Clamp01(value.r);
                value.g = Mathf.Clamp01(value.g);
                value.b = Mathf.Clamp01(value.b);
            }

            value.a = showAlpha ? Mathf.Clamp01(value.a) : 1f;
            return value;
        }
    }
}
