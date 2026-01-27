using System.Collections.Generic;
using System.Linq;
using UnityEngine;

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

            string body = string.Join(", ",
                dict.Select(kv => $"{kv.Key}:{kv.Value}")
            );
            Log($"[Dictionary Color Sum] → {{ {body} }}", color);
        }
        
        /// <summary>
        /// Chuyển UnityEngine.Color thành mã HEX (RRGGBB).
        /// </summary>
        public static string ToHex(Color? color = null)
        {
            color ??= Color.white;
            return ColorUtility.ToHtmlStringRGB(color.Value);
        }

        /// <summary>
        /// Log bình thường với màu tuỳ chọn.
        /// </summary>
        public static void Log(string message, Color? color = null)
        {
            // Handle multi-line messages
            if (message.Contains('\n'))
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
            Debug.Log($"<color=#{hex}>{message}</color>");
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
            Debug.Log($"<color=#{hex}>[{prefix}]{message}</color>");
        }
        
        private static string ProcessMultiLine(string message, Color? color = null)
        {
            var hex = ToHex(color);
            var lines = message.Split('\n');
            var linesLength = lines.Length;
            var processed = "";
            for (var index = 0; index < linesLength; index++)
            {
                var line = lines[index];
                if (index == 0)
                {
                    processed = line + "</color>\n";
                }
                else if (index == linesLength - 1)
                {
                    processed += $"<color=#{hex}>" + line;
                }
                else
                {
                    processed += $"<color=#{hex}>" + line + "</color>\n";
                }
            }

            return processed;
        }

        /// <summary>
        /// Log warning với màu tuỳ chọn.
        /// </summary>
        public static void LogWarning(string message, Color? color = null)
        {
            string hex = ToHex(color);
            Debug.LogWarning($"<color=#{hex}>{message}</color>");
        }

        /// <summary>
        /// Log error với màu tuỳ chọn.
        /// </summary>
        public static void LogError(string message, Color? color = null)
        {
            string hex = ToHex(color);
            Debug.LogError($"<color=#{hex}>{message}</color>");
        }

        /// <summary>
        /// Log có tag (ví dụ "AI", "NETWORK") để dễ filter, kèm màu tuỳ chọn.
        /// </summary>
        public static void LogTag(string tag, string message, Color? color = null)
        {
            string hex = ToHex(color);
            Debug.Log($"<color=#{hex}>[{tag}] {message}</color>");
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
                Log($"[{i}] {items[i]}", color);
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

            string body = string.Join(", ", items);
            Log($"List → [ {body} ]", color);
        }

    }
}