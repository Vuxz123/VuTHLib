using System.Collections.Generic;

namespace _VuTH.Common.Editor.Field
{
    /// <summary>
    /// Pure helpers used by <see cref="DictionaryField"/> for unique key generation and search matching.
    /// </summary>
    public static class DictionaryKeyUtility
    {
        /// <summary>Base text used by "Add Template" for new string keys.</summary>
        public const string TemplateKeyBase = "NewKey";

        /// <summary>
        /// Returns <paramref name="baseStr"/> if unused, otherwise <c>"base (1)"</c>, <c>"base (2)"</c>, ...
        /// (or <c>"1"</c>, <c>"2"</c>, ... when the base is null/empty) — the first value not in <paramref name="used"/>.
        /// </summary>
        public static string MakeUniqueString(string baseStr, ICollection<string> used)
        {
            baseStr ??= string.Empty;
            string attempt = baseStr;
            int n = 1;
            while (used.Contains(attempt))
            {
                attempt = string.IsNullOrEmpty(baseStr) ? n.ToString() : ($"{baseStr} ({n})");
                n++;
            }

            return attempt;
        }

        /// <summary>
        /// Returns <see cref="TemplateKeyBase"/> if unused, otherwise <c>NewKey1</c>, <c>NewKey2</c>, ... — the first value not in <paramref name="existing"/>.
        /// </summary>
        public static string MakeTemplateKey(ICollection<string> existing)
        {
            string baseStr = TemplateKeyBase;
            string attempt = baseStr;
            int n = 1;
            while (existing.Contains(attempt))
            {
                attempt = baseStr + n;
                n++;
            }

            return attempt;
        }

        /// <summary>
        /// Case-insensitive (invariant) substring match of an already lower-cased search text against a row's key or value text.
        /// Null texts are treated as empty.
        /// </summary>
        public static bool MatchesSearch(string keyText, string valueText, string searchLower)
        {
            var keyStr = (keyText ?? string.Empty).ToLowerInvariant();
            var valStr = (valueText ?? string.Empty).ToLowerInvariant();
            return keyStr.Contains(searchLower) || valStr.Contains(searchLower);
        }
    }
}
