using System.Collections.Generic;
using System.Linq;
using _VuTH.Common.MessagePipe.Attributes;
using _VuTH.Common.MessagePipe.Core;

namespace _VuTH.Common.MessagePipe.Editor
{
    /// <summary>
    /// Splits baked <see cref="EventScopeEntry"/> items into global events and scene events grouped by scene name,
    /// as shown by the MessagePipe settings Events Browser.
    /// </summary>
    public sealed class EventScopeGrouping
    {
        /// <summary>Group name used for scene-scoped entries whose scene name is null or empty.</summary>
        public const string EmptySceneName = "(empty)";

        /// <summary>Total number of entries that were grouped.</summary>
        public int TotalCount { get; }

        /// <summary>Entries with <see cref="EventScope.Global"/> scope, in source order.</summary>
        public List<EventScopeEntry> GlobalEvents { get; }

        /// <summary>Non-global entries keyed by scene name (source order preserved within each scene).</summary>
        public Dictionary<string, List<EventScopeEntry>> SceneEventsByScene { get; }

        /// <summary>Scene names of <see cref="SceneEventsByScene"/>, sorted with the default string comparer.</summary>
        public List<string> SortedSceneNames { get; }

        private EventScopeGrouping(int totalCount,
            List<EventScopeEntry> globalEvents,
            Dictionary<string, List<EventScopeEntry>> sceneEventsByScene)
        {
            TotalCount = totalCount;
            GlobalEvents = globalEvents;
            SceneEventsByScene = sceneEventsByScene;
            SortedSceneNames = sceneEventsByScene.Keys.OrderBy(n => n).ToList();
        }

        /// <summary>Groups the given entries by scope and scene.</summary>
        public static EventScopeGrouping Group(IReadOnlyList<EventScopeEntry> entries)
        {
            var globalEvents = new List<EventScopeEntry>();
            var sceneEventsByScene = new Dictionary<string, List<EventScopeEntry>>();

            foreach (var entry in entries)
            {
                if (entry.scope == EventScope.Global)
                {
                    globalEvents.Add(entry);
                }
                else // Scene
                {
                    var sceneName = string.IsNullOrEmpty(entry.sceneName) ? EmptySceneName : entry.sceneName;
                    if (!sceneEventsByScene.TryGetValue(sceneName, out var list))
                    {
                        list = new List<EventScopeEntry>();
                        sceneEventsByScene[sceneName] = list;
                    }
                    list.Add(entry);
                }
            }

            return new EventScopeGrouping(entries.Count, globalEvents, sceneEventsByScene);
        }
    }
}
