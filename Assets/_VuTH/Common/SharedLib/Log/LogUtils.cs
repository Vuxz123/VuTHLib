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
        #region Log Level Filtering
        
        public enum LogLevel { Verbose, Debug, Info, Warning, Error }
        
        private static LogLevel _minLogLevel = LogLevel.Debug;
        
        /// <summary>
        /// Set minimum log level. Logs below this level will be ignored.
        /// </summary>
        public static void SetMinLogLevel(LogLevel level)
        {
            _minLogLevel = level;
        }
        
        private static bool ShouldLog(LogLevel level)
        {
            return level >= _minLogLevel;
        }
        
        #endregion

        #region Thread Safety
        
        private static readonly object LogLock = new();
        
        /// <summary>
        /// Thread-safe logging for multithreading scenarios.
        /// </summary>
        public static void LogThreadSafe(string message, Color? color = null)
        {
            lock (LogLock)
            {
                Log(message, color);
            }
        }
        
        #endregion

        #region Dictionary & Collection Logging
        
        /// <summary>
        /// Log dictionary dưới dạng một chuỗi "key1:val1, key2:val2, …".
        /// </summary>
        public static void LogDictionaryInline<TKey, TValue>(Dictionary<TKey, TValue> dict, Color? color = null)
        {
            if (!ShouldLog(LogLevel.Debug)) return;
            
            if (dict == null || dict.Count == 0)
            {
                Log("<Empty Dictionary>", color);
                return;
            }

            var sb = ZString.CreateStringBuilder();
            try
            {
                sb.Append("[Dictionary] → { ");
                
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
            finally
            {
                sb.Dispose();
            }
        }
        
        /// <summary>
        /// In từng phần tử của list, mỗi phần tử 1 dòng.
        /// </summary>
        public static void LogList<T>(IEnumerable<T> list, Color? color = null)
        {
            if (!ShouldLog(LogLevel.Debug)) return;
            
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
                var sb = ZString.CreateStringBuilder();
                try
                {
                    sb.Append('[');
                    sb.Append(i);
                    sb.Append("] ");
                    sb.Append(items[i]);
                    Log(sb.ToString(), color);
                }
                finally
                {
                    sb.Dispose();
                }
            }
        }

        /// <summary>
        /// In toàn bộ list trên 1 dòng: [item0, item1, …].
        /// </summary>
        public static void LogListInline<T>(IEnumerable<T> list, Color? color = null)
        {
            if (!ShouldLog(LogLevel.Debug)) return;
            
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

            var sb = ZString.CreateStringBuilder();
            try
            {
                sb.Append("List → [ ");
                
                for (int i = 0; i < items.Count; i++)
                {
                    if (i > 0) sb.Append(", ");
                    sb.Append(items[i]);
                }
                
                sb.Append(" ]");
                Log(sb.ToString(), color);
            }
            finally
            {
                sb.Dispose();
            }
        }
        
        #endregion

        #region Core Logging Methods
        
        /// <summary>
        /// Log bình thường với màu tùy chọn.
        /// </summary>
        public static void Log(string message, Color? color = null)
        {
            if (!ShouldLog(LogLevel.Info)) return;
            
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

            var sb = ZString.CreateStringBuilder();
            try
            {
                sb.Append("<color=#");
                sb.Append(hex);
                sb.Append('>');
                sb.Append(message);
                sb.Append("</color>");
                Debug.Log(sb.ToString());
            }
            finally
            {
                sb.Dispose();
            }
        }

        public static void Log(string prefix, string message, Color? color = null)
        {
            if (!ShouldLog(LogLevel.Info)) return;
            
            // Handle multi-line messages
            if (message.IndexOf('\n') >= 0)
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

            var sb = ZString.CreateStringBuilder();
            try
            {
                sb.Append("<color=#");
                sb.Append(hex);
                sb.Append(">[");
                sb.Append(prefix);
                sb.Append(']');
                sb.Append(message);
                sb.Append("</color>");
                Debug.Log(sb.ToString());
            }
            finally
            {
                sb.Dispose();
            }
        }

        /// <summary>
        /// Log warning với màu tùy chọn.
        /// </summary>
        public static void LogWarning(string message, Color? color = null)
        {
            if (!ShouldLog(LogLevel.Warning)) return;
            
            string hex = ToHex(color);

            var sb = ZString.CreateStringBuilder();
            try
            {
                sb.Append("<color=#");
                sb.Append(hex);
                sb.Append('>');
                sb.Append(message);
                sb.Append("</color>");
                Debug.LogWarning(sb.ToString());
            }
            finally
            {
                sb.Dispose();
            }
        }

        /// <summary>
        /// Log error với màu tùy chọn.
        /// </summary>
        public static void LogError(string message, Color? color = null)
        {
            // Errors always log regardless of level
            string hex = ToHex(color);

            var sb = ZString.CreateStringBuilder();
            try
            {
                sb.Append("<color=#");
                sb.Append(hex);
                sb.Append('>');
                sb.Append(message);
                sb.Append("</color>");
                Debug.LogError(sb.ToString());
            }
            finally
            {
                sb.Dispose();
            }
        }

        /// <summary>
        /// Log có tag (ví dụ "AI", "NETWORK") để dễ filter, kèm màu tùy chọn.
        /// </summary>
        public static void LogTag(string tag, string message, Color? color = null)
        {
            if (!ShouldLog(LogLevel.Info)) return;
            
            string hex = ToHex(color);

            var sb = ZString.CreateStringBuilder();
            try
            {
                sb.Append("<color=#");
                sb.Append(hex);
                sb.Append(">[");
                sb.Append(tag);
                sb.Append("] ");
                sb.Append(message);
                sb.Append("</color>");
                Debug.Log(sb.ToString());
            }
            finally
            {
                sb.Dispose();
            }
        }
        
        #endregion

        #region Multi-line Processing
        
        private static string ProcessMultiLine(string message, Color? color = null)
        {
            var hex = ToHex(color);
            ReadOnlySpan<char> span = message.AsSpan();
    
            var sb = ZString.CreateStringBuilder();
            try
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
            finally
            {
                sb.Dispose();
            }
        }

        private static int CountLines(string message)
        {
            int count = 1;
            foreach (char c in message)
                if (c == '\n') count++;
            return count;
        }
        
        #endregion

        #region Hex Color Caching

        private static readonly ConcurrentDictionary<Color, string> HexCache = new();
        
        /// <summary>
        /// Chuyển UnityEngine.Color thành mã HEX (RRGGBB) với caching.
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
        
        #region Rich Text Helpers
        
        /// <summary>
        /// Helper methods for rich text formatting.
        /// </summary>
        public static class RichText
        {
            public static string Bold(string text)
            {
                return ZString.Concat("<b>", text, "</b>");
            }
            
            public static string Italic(string text)
            {
                return ZString.Concat("<i>", text, "</i>");
            }
            
            public static string Size(string text, int size)
            {
                return ZString.Format("<size={0}>{1}</size>", size, text);
            }
            
            public static string Color(string text, Color color)
            {
                var hex = ToHex(color);
                return ZString.Format("<color=#{0}>{1}</color>", hex, text);
            }
        }
        
        #endregion
    }
}