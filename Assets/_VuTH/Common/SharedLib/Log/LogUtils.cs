using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Cysharp.Text;

namespace _VuTH.Common.Log
{
    public static class LogUtils
    {
        /// <summary>
        /// Log dictionary dưới dạng một chuỗi "key1:val1, key2:val2, …".
        /// </summary>
        public static void LogDictionaryInline<TKey, TValue>(Dictionary<TKey, TValue> dict, Color? color = null)
        {
            if (dict == null || dict.Count == 0)
            {
                Log("<Empty Dictionary>", color);
                return;
            }

            using var sb = ZString.CreateStringBuilder();
            sb.Append("[Dictionary Color Sum] → { ");
                
            bool first = true;
            foreach (var kv in dict)
            {
                if (!first) sb.Append(", ");
                sb.Append(kv.Key);
                sb.Append(':');
                sb.Append(kv.Value);
                first = false;
            }
                
            sb.Append(" }");
            Log(sb.ToString(), color);
        }

        /// <summary>
        /// Log bình thường với màu tùy chọn.
        /// </summary>
        public static void Log(string message, Color? color = null)
        {
            // Handle multi-line messages
            if (message.IndexOf('\n') >= 0)
            {
                var processed = ProcessMultiLine(message, color);
                LogInternal(processed, color);
            }
            else
            {
                LogInternal(message, color);
            }
        }

        private static void LogInternal(string message, Color? color = null)
        {
            string hex = ToHex(color);

            using var sb = ZString.CreateStringBuilder();
            sb.Append("<color=#");
            sb.Append(hex);
            sb.Append('>');
            sb.Append(message);
            sb.Append("</color>");
            Debug.Log(sb.ToString());
        }

        public static void Log(string prefix, string message, Color? color = null)
        {
            // Handle multi-line messages
            if (message.Contains('\n'))
            { 
                var processed = ProcessMultiLine(message, color);
                LogInternal(prefix, processed, color);
            }
            else
            {
                LogInternal(prefix, message, color);
            }
        }

        private static void LogInternal(string prefix, string message, Color? color = null)
        {
            string hex = ToHex(color);

            using var sb = ZString.CreateStringBuilder();
            sb.Append("<color=#");
            sb.Append(hex);
            sb.Append(">[");
            sb.Append(prefix);
            sb.Append(']');
            sb.Append(message);
            sb.Append("</color>");
            Debug.Log(sb.ToString());
        }
        
        private static string ProcessMultiLine(string message, Color? color = null)
        {
            var hex = ToHex(color);
            ReadOnlySpan<char> span = message.AsSpan();
    
            using (var sb = ZString.CreateStringBuilder())
            {
                int start = 0;
                int lineIndex = 0;
                int totalLines = CountLines(message);
        
                for (int i = 0; i < span.Length; i++)
                {
                    if (span[i] == '\n' || i == span.Length - 1)
                    {
                        var line = i == span.Length - 1 
                            ? span.Slice(start) 
                            : span.Slice(start, i - start);
                
                        if (lineIndex == 0)
                        {
                            sb.Append(line);
                            sb.Append("</color>\n");
                        }
                        else if (lineIndex == totalLines - 1)
                        {
                            sb.Append("<color=#");
                            sb.Append(hex);
                            sb.Append('>');
                            sb.Append(line);
                        }
                        else
                        {
                            sb.Append("<color=#");
                            sb.Append(hex);
                            sb.Append('>');
                            sb.Append(line);
                            sb.Append("</color>\n");
                        }
                
                        start = i + 1;
                        lineIndex++;
                    }
                }
        
                return sb.ToString();
            }
        }

        private static int CountLines(string message)
        {
            int count = 1;
            foreach (char c in message)
                if (c == '\n') count++;
            return count;
        }

        /// <summary>
        /// Log warning với màu tùy chọn.
        /// </summary>
        public static void LogWarning(string message, Color? color = null)
        {
            string hex = ToHex(color);

            using var sb = ZString.CreateStringBuilder();
            sb.Append("<color=#");
            sb.Append(hex);
            sb.Append('>');
            sb.Append(message);
            sb.Append("</color>");
            Debug.LogWarning(sb.ToString());
        }

        /// <summary>
        /// Log error với màu tùy chọn.
        /// </summary>
        public static void LogError(string message, Color? color = null)
        {
            string hex = ToHex(color);

            using var sb = ZString.CreateStringBuilder();
            sb.Append("<color=#");
            sb.Append(hex);
            sb.Append('>');
            sb.Append(message);
            sb.Append("</color>");
            Debug.LogError(sb.ToString());
        }

        /// <summary>
        /// Log có tag (ví dụ "AI", "NETWORK") để dễ filter, kèm màu tùy chọn.
        /// </summary>
        public static void LogTag(string tag, string message, Color? color = null)
        {
            string hex = ToHex(color);

            using var sb = ZString.CreateStringBuilder();
            sb.Append("<color=#");
            sb.Append(hex);
            sb.Append(">[");
            sb.Append(tag);
            sb.Append("] ");
            sb.Append(message);
            sb.Append("</color>");
            Debug.Log(sb.ToString());
        }
        
        
        /// <summary>
        /// In từng phần tử của list, mỗi phần tử 1 dòng.
        /// </summary>
        public static void LogList<T>(IEnumerable<T> list, Color? color = null)
        {
            if (list == null)
            {
                Log("<List is null>", color);
                return;
            }

            var items = list.ToList();
            if (items.Count == 0)
            {
                Log("<Empty List>", color);
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                using var sb = ZString.CreateStringBuilder();
                sb.Append('[');
                sb.Append(i);
                sb.Append("] ");
                sb.Append(items[i]);
                Log(sb.ToString(), color);
            }
        }

        /// <summary>
        /// In toàn bộ list trên 1 dòng: [item0, item1, …].
        /// </summary>
        public static void LogListInline<T>(IEnumerable<T> list, Color? color = null)
        {
            if (list == null)
            {
                Log("<List is null>", color);
                return;
            }

            var items = list.ToList();
            if (items.Count == 0)
            {
                Log("<Empty List>", color);
                return;
            }

            using var sb = ZString.CreateStringBuilder();
            sb.Append("List → [ ");
                
            for (int i = 0; i < items.Count; i++)
            {
                if (i > 0) sb.Append(", ");
                sb.Append(items[i]);
            }
                
            sb.Append(" ]");
            Log(sb.ToString(), color);
        }

        #region Hex

        private static readonly ConcurrentDictionary<Color, string> HexCache = new();
        
        /// <summary>
        /// Chuyển UnityEngine.Color thành mã HEX (RRGGBB).
        /// </summary>
        public static string ToHex(Color? color = null)
        {
            color ??= Color.white;
            var c = color.Value;
    
            if (HexCache.TryGetValue(c, out var cached))
                return cached;
    
            var hex = ColorUtility.ToHtmlStringRGB(c);
            HexCache[c] = hex;
            return hex;
        }

        #endregion
    }
}