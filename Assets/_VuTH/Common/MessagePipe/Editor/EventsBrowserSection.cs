using System;
using _VuTH.Common.MessagePipe.Configuration;
using _VuTH.Common.MessagePipe.Core;
using UnityEngine;
using UnityEngine.UIElements;
using ZLinq;

namespace _VuTH.Common.MessagePipe.Editor
{
    /// <summary>
    /// UI Toolkit view for the "Events Browser" section of <see cref="MessagePipeSettingTab"/>.
    /// Loads the baked <see cref="EventScopeLookup"/> and renders its entries grouped by scope and scene.
    /// </summary>
    internal sealed class EventsBrowserSection
    {
        /// <summary>Container that receives the rendered content on each <see cref="Refresh"/>.</summary>
        public VisualElement Root { get; } = new();

        /// <summary>Clears and rebuilds the events browser content.</summary>
        public void Refresh()
        {
            Root.Clear();

            var lookup = LoadEventScopeLookup();
            if (lookup == null)
            {
                var noteLabel = new Label("No baked data found. Click \"Bake / Generate\" to create lookup data.")
                {
                    style =
                    {
                        color = new Color(1f, 0.6f, 0f, 1f),
                        fontSize = 12,
                        marginTop = 8,
                        marginBottom = 8,
                        whiteSpace = WhiteSpace.Normal
                    }
                };
                Root.Add(noteLabel);
                return;
            }

            var entries = lookup.Entries;
            if (entries == null || entries.Count == 0)
            {
                var noteLabel = new Label("No events found in baked data.")
                {
                    style =
                    {
                        color = Color.gray,
                        fontSize = 12,
                        marginTop = 8,
                        marginBottom = 8
                    }
                };
                Root.Add(noteLabel);
                return;
            }

            // Separate global and scene events
            var grouping = EventScopeGrouping.Group(entries);
            var globalEvents = grouping.GlobalEvents;
            var sceneEventsByScene = grouping.SceneEventsByScene;

            var card = new VisualElement
            {
                style =
                {
                    backgroundColor = new Color(0.16f, 0.16f, 0.16f, 1f),
                    borderLeftColor = new Color(0.24f, 0.24f, 0.24f, 1f),
                    borderRightColor = new Color(0.24f, 0.24f, 0.24f, 1f),
                    borderTopColor = new Color(0.24f, 0.24f, 0.24f, 1f),
                    borderBottomColor = new Color(0.24f, 0.24f, 0.24f, 1f),
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    paddingLeft = 10,
                    paddingRight = 10,
                    paddingTop = 8,
                    paddingBottom = 10,
                    marginBottom = 10
                }
            };

            var header = new VisualElement
            {
                style =
                {
                    flexDirection = FlexDirection.Row,
                    alignItems = Align.Center,
                    marginBottom = 6
                }
            };

            var titleLabel = new Label("Baked Events")
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 12,
                    flexGrow = 1
                }
            };
            header.Add(titleLabel);

            var totalBadge = new Label(grouping.TotalCount.ToString())
            {
                style =
                {
                    backgroundColor = new Color(0.24f, 0.42f, 0.64f, 1f),
                    color = Color.white,
                    paddingLeft = 6,
                    paddingRight = 6,
                    paddingTop = 2,
                    paddingBottom = 2,
                    fontSize = 10,
                    unityTextAlign = TextAnchor.MiddleCenter,
                    marginLeft = 4,
                    minWidth = 26
                }
            };
            header.Add(totalBadge);

            card.Add(header);

            var hint = new Label("Shows baked events grouped by scope and scene.")
            {
                style =
                {
                    color = Color.gray,
                    fontSize = 11,
                    marginBottom = 6
                }
            };
            card.Add(hint);

            var scrollView = new ScrollView(ScrollViewMode.Vertical)
            {
                style =
                {
                    maxHeight = 280,
                    borderLeftWidth = 1,
                    borderRightWidth = 1,
                    borderTopWidth = 1,
                    borderBottomWidth = 1,
                    borderLeftColor = new Color(0.22f, 0.22f, 0.22f, 1f),
                    borderRightColor = new Color(0.22f, 0.22f, 0.22f, 1f),
                    borderTopColor = new Color(0.22f, 0.22f, 0.22f, 1f),
                    borderBottomColor = new Color(0.22f, 0.22f, 0.22f, 1f),
                    paddingLeft = 6,
                    paddingRight = 6,
                    paddingTop = 4,
                    paddingBottom = 4
                }
            };

            // Global Events foldout (expanded)
            var globalFoldout = new Foldout
            {
                text = $"Global Events ({globalEvents.Count})",
                value = true,
                style =
                {
                    marginTop = 4,
                    marginLeft = 4,
                    marginBottom = 6
                }
            };

            foreach (var eventLabel in globalEvents.AsValueEnumerable()
                         .Select(CreateEventLabel))
            {
                globalFoldout.Add(eventLabel);
            }

            if (globalEvents.Count == 0)
            {
                var emptyLabel = new Label("(none)") { style = { color = Color.gray, fontSize = 11, marginLeft = 2 } };
                globalFoldout.Add(emptyLabel);
            }

            scrollView.Add(globalFoldout);

            // Scene Events foldout (collapsed)
            var sceneFoldout = new Foldout
            {
                text = $"Scene Events ({sceneEventsByScene.Count} scenes)",
                value = false,
                style =
                {
                    marginLeft = 4,
                    marginBottom = 2
                }
            };

            foreach (var sceneName in grouping.SortedSceneNames)
            {
                var sceneList = sceneEventsByScene[sceneName];
                var sceneChildFoldout = new Foldout
                {
                    text = $"{sceneName} ({sceneList.Count})",
                    value = false,
                    style =
                    {
                        marginLeft = 10,
                        marginTop = 2
                    }
                };

                foreach (var eventLabel in sceneList
                             .AsValueEnumerable()
                             .Select(CreateEventLabel))
                {
                    sceneChildFoldout.Add(eventLabel);
                }

                sceneFoldout.Add(sceneChildFoldout);
            }

            if (sceneEventsByScene.Count == 0)
            {
                var emptyLabel = new Label("(none)") { style = { color = Color.gray, fontSize = 11, marginLeft = 2 } };
                sceneFoldout.Add(emptyLabel);
            }

            scrollView.Add(sceneFoldout);

            card.Add(scrollView);
            Root.Add(card);
        }

        private static Label CreateEventLabel(EventScopeEntry entry)
        {
            var displayName = EventDisplayNameFormatter.Format(entry.typeFullName);
            var label = new Label($"• {displayName}")
            {
                style =
                {
                    fontSize = 12,
                    marginLeft = 16,
                    marginTop = 2,
                    marginBottom = 2
                }
            };
            return label;
        }

        private static EventScopeLookup LoadEventScopeLookup()
        {
            try
            {
                return Resources.Load<EventScopeLookup>(MessagePipeConstants.EventScopeLookupPath);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[MessagePipe Settings] Failed to load EventScopeLookup: {ex.Message}");
                return null;
            }
        }
    }
}
