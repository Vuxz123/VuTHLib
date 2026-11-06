using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

// Records on netstandard2.0.
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit
    {
    }
}

namespace VuTH.Persistent.Generators
{
    /// <summary>
    /// Immutable array with value equality, so the incremental pipeline can tell that a step's output did not change
    /// and skip the next steps (ImmutableArray compares by reference).
    /// </summary>
    internal readonly struct EquatableArray<T> : IEquatable<EquatableArray<T>>, IEnumerable<T> where T : IEquatable<T>
    {
        private readonly T[]? _items;

        public EquatableArray(IEnumerable<T> items) => _items = items.ToArray();

        public int Count => _items?.Length ?? 0;

        public bool Equals(EquatableArray<T> other) =>
            (_items ?? Array.Empty<T>()).AsSpanEquals(other._items ?? Array.Empty<T>());

        public override bool Equals(object? obj) => obj is EquatableArray<T> other && Equals(other);

        public override int GetHashCode()
        {
            var hash = 17;
            foreach (var item in _items ?? Array.Empty<T>()) hash = hash * 31 + (item?.GetHashCode() ?? 0);
            return hash;
        }

        public IEnumerator<T> GetEnumerator() => ((IEnumerable<T>)(_items ?? Array.Empty<T>())).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    internal static class ArrayEquality
    {
        public static bool AsSpanEquals<T>(this T[] left, T[] right) where T : IEquatable<T>
        {
            if (left.Length != right.Length) return false;
            for (var i = 0; i < left.Length; i++)
            {
                if (!EqualityComparer<T>.Default.Equals(left[i], right[i])) return false;
            }

            return true;
        }
    }

    /// <summary>A location kept as plain values (a <see cref="Location"/> holds the whole syntax tree).</summary>
    internal sealed record LocationInfo(string FilePath, TextSpan Span, LinePositionSpan LineSpan)
    {
        public Location ToLocation() => Location.Create(FilePath, Span, LineSpan);

        public static LocationInfo? From(Location? location)
        {
            if (location?.SourceTree == null) return null;
            return new LocationInfo(location.SourceTree.FilePath, location.SourceSpan, location.GetLineSpan().Span);
        }
    }

    internal sealed record DiagnosticInfo(string Id, LocationInfo? Location, EquatableArray<string> Args)
    {
        public Diagnostic ToDiagnostic() =>
            Diagnostic.Create(Diagnostics.ById[Id], Location?.ToLocation() ?? Microsoft.CodeAnalysis.Location.None,
                Args.Cast<object>().ToArray());
    }

    /// <summary>Everything one package produces: plain values only, cached between compilations.</summary>
    internal sealed record GenerationResult(string HintName, string? Source, EquatableArray<DiagnosticInfo> Diagnostics);
}
