using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using _VuTH.Common.MessagePipe.Attributes;
using _VuTH.Common.MessagePipe.Core;
using UnityEditor;

namespace _VuTH.Common.MessagePipe.Editor
{
    /// <summary>One problem found by <see cref="MessagePipeTargetValidator"/>.</summary>
    public readonly struct MessagePipeTargetIssue
    {
        public MessagePipeTargetIssue(bool isError, Type eventType, string message)
        {
            IsError = isError;
            EventType = eventType;
            Message = message;
        }

        public bool IsError { get; }
        public Type EventType { get; }
        public string Message { get; }
    }

    /// <summary>
    /// Checks the second argument of <c>[MessagePipeEvent(Scene/Screen, "...")]</c>: a scene name must match a scene
    /// asset in the project (Build Settings or Addressables — any scene asset), a screen id must match a
    /// ScreenIdentifier asset. A typo there silently disables the isolation, so it is surfaced in the settings tab.
    /// </summary>
    public static class MessagePipeTargetValidator
    {
        /// <summary>Pure validation against the given known names (tests use this overload).</summary>
        public static List<MessagePipeTargetIssue> Validate(
            IEnumerable<MessagePipeEventInfo> events,
            ICollection<string> knownSceneNames,
            ICollection<string> knownScreenIds)
        {
            var issues = new List<MessagePipeTargetIssue>();
            foreach (var e in events)
            {
                switch (e.Scope)
                {
                    case EventScope.Scene when e.SceneName.Length > 0 && !knownSceneNames.Contains(e.SceneName):
                        issues.Add(new MessagePipeTargetIssue(false, e.Type,
                            $"{e.Type.Name}: scene \"{e.SceneName}\" does not exist in the project — its scene broker will never be registered."));
                        break;

                    case EventScope.Screen when e.ScreenId.Length == 0:
                        issues.Add(new MessagePipeTargetIssue(true, e.Type,
                            $"{e.Type.Name}: EventScope.Screen needs a ScreenIdentifier name, e.g. ScreenIds.Names.Home."));
                        break;

                    case EventScope.Screen when !knownScreenIds.Contains(e.ScreenId):
                        issues.Add(new MessagePipeTargetIssue(false, e.Type,
                            $"{e.Type.Name}: no ScreenIdentifier named \"{e.ScreenId}\" — its screen broker will never be registered."));
                        break;
                }
            }

            return issues;
        }

        /// <summary>Validates the discovered events against the scenes / ScreenIdentifiers of this project.</summary>
        public static List<MessagePipeTargetIssue> ValidateProject()
        {
            return Validate(MessagePipeEventCatalog.All, FindAssetNames("t:Scene"), FindAssetNames("t:ScreenIdentifier"));
        }

        private static HashSet<string> FindAssetNames(string filter)
        {
            return new HashSet<string>(
                AssetDatabase.FindAssets(filter)
                    .Select(AssetDatabase.GUIDToAssetPath)
                    .Select(Path.GetFileNameWithoutExtension),
                StringComparer.Ordinal);
        }
    }
}
