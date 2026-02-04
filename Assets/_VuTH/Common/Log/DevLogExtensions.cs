using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using UnityEngine;
using Cysharp.Text;

namespace _VuTH.Common.Log
{
    /// <summary>
    /// Provides extension methods for logging that include an origin-based prefix and color support.
    /// </summary>
    public static class DevLogExtensions
    {
        private enum PrefixKind { Log, Warning, Error }
        
        private static readonly ConcurrentDictionary<(Type type, PrefixKind kind), string> PrefixCache = new();
        
        private const int MaxLOGLength = 15000; // Unity Console safe limit
        
        private static string GetPrefix(Type type, PrefixKind kind)
        {
            var key = (type, kind);
            if (PrefixCache.TryGetValue(key, out var cached))
                return cached;

            var originName = type != null ? type.Name : "null";

            var baseColor = kind switch
            {
                PrefixKind.Log => DefaultColor,
                PrefixKind.Warning => DefaultWarningColor,
                _ => DefaultErrorColor
            };

            var hex = LogUtils.ToHex(baseColor);
            
            var sb = ZString.CreateStringBuilder();
            try
            {
                sb.Append("<color=#");
                sb.Append(hex);
                sb.Append(">[");
                sb.Append(originName);
                sb.Append("]</color> ");
                cached = sb.ToString();
            }
            finally
            {
                sb.Dispose();
            }
            
            PrefixCache[key] = cached;
            return cached;
        }
        
        private static string WithOrigin(object origin, string msg, PrefixKind kind)
        {
            msg ??= string.Empty;

            Type t = null;
            if (origin is UnityEngine.Object uo)
            {
                if (uo) t = uo.GetType();
            }

            if (origin is Type type)
            {
                t = type;
            }
            else
            {
                t = origin?.GetType();
            }

            var prefix = GetPrefix(t, kind);
            
            var sb = ZString.CreateStringBuilder();
            try
            {
                sb.Append(prefix);
                sb.Append(msg);
                return sb.ToString();
            }
            finally
            {
                sb.Dispose();
            }
        }
        
        private static string TruncateIfNeeded(string message)
        {
            if (message.Length <= MaxLOGLength)
                return message;
            
            var sb = ZString.CreateStringBuilder();
            try
            {
                sb.Append(message.AsSpan(0, MaxLOGLength - 50));
                sb.Append("\n... [TRUNCATED ");
                sb.Append(message.Length - MaxLOGLength);
                sb.Append(" chars]");
                return sb.ToString();
            }
            finally
            {
                sb.Dispose();
            }
        }

        /// <summary>
        /// Logs a message with the object's type prefixed. Compiled only in the Unity Editor.
        /// </summary>
        /// <param name="origin">The origin object for the log.</param>
        /// <param name="msg">The message to log.</param>
        /// <param name="color">Optional text color.</param>
        [Conditional("UNITY_EDITOR")]
        public static void Log(this object origin, string msg, Color? color = null)
        {
            if (origin == null)
            {
                LogUtils.LogWarning("Attempted to log from null origin", Color.yellow);
                return;
            }
            
            LogInternal(origin, msg, color, PrefixKind.Log);
        }

        /// <summary>
        /// Logs a warning message with the object's type prefixed. Compiled only in the Unity Editor.
        /// </summary>
        /// <param name="origin">The origin object for the warning.</param>
        /// <param name="msg">The warning message.</param>
        /// <param name="color">Optional text color.</param>
        [Conditional("UNITY_EDITOR")]
        public static void LogWarning(this object origin, string msg, Color? color = null)
        {
            if (origin == null)
            {
                LogUtils.LogWarning("Attempted to log warning from null origin", Color.yellow);
                return;
            }
            
            LogInternal(origin, msg, color, PrefixKind.Warning);
        }

        /// <summary>
        /// Logs an error message with the object's type prefixed.
        /// </summary>
        /// <param name="origin">The origin object for the error.</param>
        /// <param name="msg">The error message.</param>
        /// <param name="color">Optional text color.</param>
        public static void LogError(this object origin, string msg, Color? color = null)
        {
            if (origin == null)
            {
                LogUtils.LogError("Attempted to log error from null origin", Color.red);
                return;
            }
            
            LogInternal(origin, msg, color, PrefixKind.Error);
        }
        
        /// <summary>
        /// Logs a verbose debug message. Only compiled in UNITY_EDITOR or DEVELOPMENT_BUILD.
        /// </summary>
        [Conditional("UNITY_EDITOR"), Conditional("DEVELOPMENT_BUILD")]
        public static void LogVerbose(this object origin, string msg, Color? color = null)
        {
            if (origin == null) return;
            
            var c = color ?? new Color(0.7f, 0.7f, 0.7f); // Gray for verbose
            LogInternal(origin, $"[VERBOSE] {msg}", c, PrefixKind.Log);
        }
        
        private static void LogInternal(object origin, string msg, Color? color, PrefixKind kind)
        {
            try
            {
                var c = color ?? Color.white;
                var finalMsg = WithOrigin(origin, msg, kind);
                finalMsg = TruncateIfNeeded(finalMsg);

                switch (kind)
                {
                    case PrefixKind.Warning:
                        LogUtils.LogWarning(finalMsg, c);
                        break;
                    case PrefixKind.Log:
                        LogUtils.Log(finalMsg, c);
                        break;
                    case PrefixKind.Error:
                    default:
                        LogUtils.LogError(finalMsg, c);
                        break;
                }
            }
            catch (Exception ex)
            {
                // Fallback logging if something goes wrong
                LogUtils.LogError($"[DevLog Error] Failed to log message: {ex.Message}");
            }
        }
        
        private static readonly Color DefaultColor = new(0.6f, 0.9f, 1f);
        private static readonly Color DefaultWarningColor = new(1f, 0.85f, 0.2f);
        private static readonly Color DefaultErrorColor = new(1f, 0.35f, 0.35f);
    }
}