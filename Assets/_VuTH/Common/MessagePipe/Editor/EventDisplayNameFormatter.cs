using System.Text.RegularExpressions;

namespace _VuTH.Common.MessagePipe.Editor
{
    /// <summary>
    /// Formats baked event type names (usually assembly-qualified) into short, human-readable
    /// display names for the MessagePipe settings Events Browser.
    /// </summary>
    public static class EventDisplayNameFormatter
    {
        /// <summary>Display name used when the type name is null or empty.</summary>
        public const string UnknownName = "(unknown)";

        /// <summary>
        /// Converts an assembly-qualified type name (e.g. <c>Namespace.TypeName, Assembly, Version=...</c>)
        /// into <c>Namespace.TypeName</c>. A leading <c>Global</c> namespace is dropped.
        /// </summary>
        public static string Format(string typeFullName)
        {
            if (string.IsNullOrEmpty(typeFullName))
                return UnknownName;

            try
            {
                // Try to get the short type name from the assembly-qualified name
                // Assembly-qualified name format: Namespace.TypeName, Assembly, Version=..., Culture=..., PublicKeyToken=...
                var typeMatch = Regex.Match(typeFullName, @"^([^,\s]+)\.([^,\s]+)");
                if (typeMatch.Success)
                {
                    var ns = typeMatch.Groups[1].Value;
                    var typeName = typeMatch.Groups[2].Value;

                    // If namespace is empty or just a common pattern, just show type name
                    if (string.IsNullOrEmpty(ns) || ns == "Global")
                        return typeName;

                    return $"{ns}.{typeName}";
                }

                // Fallback: just return the first part before comma
                var commaIndex = typeFullName.IndexOf(',');
                if (commaIndex > 0)
                    return typeFullName.Substring(0, commaIndex);

                return typeFullName;
            }
            catch
            {
                return typeFullName;
            }
        }
    }
}
