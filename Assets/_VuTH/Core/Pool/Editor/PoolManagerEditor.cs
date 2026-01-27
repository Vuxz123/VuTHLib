using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;
using ZLinq;

namespace _VuTH.Core.Pool.Editor
{
    [CustomEditor(typeof(PoolManager))]
    public class PoolManagerEditor : UnityEditor.Editor
    {
        private PoolManager _manager;
        
        // UI Elements
        private VisualElement _statsContainer;
        private VisualElement _analyticsContainer;
        private VisualElement _debugContainer;
        private Label _noPoolsLabel;
        private Foldout _statsFoldout;
        private Foldout _analyticsFoldout;
        private Foldout _debugFoldout;
        
        // Update interval
        private const float UpdateInterval = 0.2f;

        public override VisualElement CreateInspectorGUI()
        {
            _manager = (PoolManager)target;
            
            var root = new VisualElement();
            
            // Default inspector fields
            InspectorElement.FillDefaultInspector(root, serializedObject, this);
            
            // Spacer
            root.Add(new VisualElement { style = { height = 10 } });
            
            // Info box for edit mode
            var editModeInfo = new HelpBox("Pool statistics are only available in Play Mode.", HelpBoxMessageType.Info)
                {
                    name = "edit-mode-info"
                };
            root.Add(editModeInfo);
            
            // Build sections
            BuildStatsSection(root);
            BuildAnalyticsSection(root);
            BuildDebugSection(root);
            BuildActionButtons(root);
            
            // Schedule updates in play mode
            root.schedule.Execute(UpdateAll).Every((long)(UpdateInterval * 1000));
            
            // Initial visibility
            UpdateVisibility(root);
            
            return root;
        }
        
        #region BUILD SECTIONS
        
        private void BuildStatsSection(VisualElement root)
        {
            _statsFoldout = new Foldout
            {
                text = "📊 Pool Statistics",
                value = true,
                name = "stats-foldout"
            };
            ApplyFoldoutStyle(_statsFoldout);
            root.Add(_statsFoldout);
            
            _statsContainer = new VisualElement
            {
                name = "stats-container",
                style =
                {
                    marginLeft = 4
                }
            };
            _statsFoldout.Add(_statsContainer);
            
            _noPoolsLabel = new Label("No pools active.")
            {
                style =
                {
                    unityTextAlign = TextAnchor.MiddleCenter,
                    color = new Color(0.5f, 0.5f, 0.5f),
                    marginTop = 8,
                    marginBottom = 8
                }
            };
            _statsContainer.Add(_noPoolsLabel);
        }
        
        private void BuildAnalyticsSection(VisualElement root)
        {
            _analyticsFoldout = new Foldout
            {
                text = "📈 Analytics",
                value = false,
                name = "analytics-foldout"
            };
            ApplyFoldoutStyle(_analyticsFoldout);
            root.Add(_analyticsFoldout);
            
            _analyticsContainer = new VisualElement
            {
                name = "analytics-container",
                style =
                {
                    marginLeft = 4
                }
            };
            _analyticsFoldout.Add(_analyticsContainer);
        }
        
        private void BuildDebugSection(VisualElement root)
        {
            _debugFoldout = new Foldout
            {
                text = "🔧 Debug Info",
                value = false,
                name = "debug-foldout"
            };
            ApplyFoldoutStyle(_debugFoldout);
            root.Add(_debugFoldout);
            
            _debugContainer = new VisualElement
            {
                name = "debug-container",
                style =
                {
                    marginLeft = 4
                }
            };
            _debugFoldout.Add(_debugContainer);
        }
        
        private void BuildActionButtons(VisualElement root)
        {
            var buttonContainer = new VisualElement
            {
                name = "action-buttons",
                style =
                {
                    flexDirection = FlexDirection.Row,
                    marginTop = 10,
                    justifyContent = Justify.SpaceAround
                }
            };
            
            var trimButton = new Button(() => _manager?.TrimExcess())
            {
                text = "✂️ Trim Excess",
                style = { flexGrow = 1, marginRight = 4 }
            };
            
            var cleanupButton = new Button(() => _manager?.CleanupUnused(30f))
            {
                text = "🧹 Cleanup Unused",
                style = { flexGrow = 1, marginRight = 4 }
            };
            
            var clearButton = new Button(() => _manager?.ClearAllPools())
            {
                text = "🗑️ Clear All",
                style = { flexGrow = 1, backgroundColor = new Color(0.8f, 0.3f, 0.3f, 0.3f) }
            };
            
            buttonContainer.Add(trimButton);
            buttonContainer.Add(cleanupButton);
            buttonContainer.Add(clearButton);
            
            root.Add(buttonContainer);
        }
        
        #endregion
        
        #region UPDATE METHODS
        
        private void UpdateVisibility(VisualElement root)
        {
            var editModeInfo = root.Q<HelpBox>("edit-mode-info");
            var actionButtons = root.Q<VisualElement>("action-buttons");
            
            bool isPlaying = Application.isPlaying;
            
            if (editModeInfo != null)
                editModeInfo.style.display = isPlaying ? DisplayStyle.None : DisplayStyle.Flex;
            
            _statsFoldout.style.display = isPlaying ? DisplayStyle.Flex : DisplayStyle.None;
            _analyticsFoldout.style.display = isPlaying ? DisplayStyle.Flex : DisplayStyle.None;
            _debugFoldout.style.display = isPlaying ? DisplayStyle.Flex : DisplayStyle.None;
            
            if (actionButtons != null)
                actionButtons.style.display = isPlaying ? DisplayStyle.Flex : DisplayStyle.None;
        }
        
        private void UpdateAll()
        {
            if (_statsFoldout?.parent != null)
                UpdateVisibility(_statsFoldout.parent);
            
            if (!Application.isPlaying || !_manager) return;
            
            RefreshStats();
            RefreshAnalytics();
            RefreshDebugInfo();
        }
        
        private void RefreshStats()
        {
            if (_statsContainer == null || !_manager) return;
            
            var allStats = _manager.GetAllStats();
            
            // Clear old stats
            var toRemove = _statsContainer.Query<VisualElement>(className: "pool-stat-card").ToList();
            foreach (var element in toRemove)
                _statsContainer.Remove(element);
            
            if (allStats == null || allStats.Count == 0)
            {
                _noPoolsLabel.style.display = DisplayStyle.Flex;
                return;
            }
            
            _noPoolsLabel.style.display = DisplayStyle.None;
            
            foreach (var card in allStats.AsValueEnumerable()
                         .Select(kvp => CreatePoolStatCard(kvp.Key, kvp.Value)))
            {
                _statsContainer.Add(card);
            }
        }
        
        private void RefreshAnalytics()
        {
            if (_analyticsContainer == null || !_manager) return;
            
            _analyticsContainer.Clear();
            
            var analytics = _manager.GetAnalytics();
            if (analytics == null) return;
            
            var summary = analytics.GetSummary();
            
            // Summary card
            var summaryCard = new VisualElement();
            ApplyCardStyle(summaryCard);
            
            summaryCard.Add(CreateHeaderLabel("Overall Performance"));
            
            var grid = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween } };
            
            var col1 = new VisualElement();
            col1.Add(CreateStatLabel("Total Spawns", summary.TotalSpawns, Color.white));
            col1.Add(CreateStatLabel("Total Despawns", summary.TotalDespawns, Color.white));
            grid.Add(col1);
            
            var col2 = new VisualElement();
            col2.Add(CreateStatLabel("Pool Hits", summary.TotalPoolHits, new Color(0.4f, 1f, 0.4f)));
            col2.Add(CreateStatLabel("Pool Misses", summary.TotalPoolMisses, new Color(1f, 0.6f, 0.4f)));
            grid.Add(col2);
            
            var col3 = new VisualElement();
            col3.Add(CreateStatLabel("Hit Rate", $"{summary.OverallHitRate:P1}", GetHitRateColor(summary.OverallHitRate)));
            col3.Add(CreateUsageBar(summary.OverallHitRate, "Efficiency"));
            grid.Add(col3);
            
            summaryCard.Add(grid);
            _analyticsContainer.Add(summaryCard);
        }
        
        private void RefreshDebugInfo()
        {
            if (_debugContainer == null || !_manager) return;
            
            _debugContainer.Clear();
            
            var debugInfo = _manager.GetDebugInfo();
            if (debugInfo == null) return;
            
            var card = new VisualElement();
            ApplyCardStyle(card);
            
            foreach (var kvp in debugInfo)
            {
                var row = new VisualElement { style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween } };
                
                row.Add(new Label(kvp.Key) { style = { color = new Color(0.7f, 0.7f, 0.7f) } });
                
                var valueLabel = new Label(FormatValue(kvp.Value))
                {
                    style = { unityFontStyleAndWeight = FontStyle.Bold }
                };
                row.Add(valueLabel);
                
                card.Add(row);
            }
            
            _debugContainer.Add(card);
        }
        
        #endregion
        
        #region UI HELPERS
        
        private VisualElement CreatePoolStatCard(string poolName, PoolStats stats)
        {
            if (stats == null) return new VisualElement();
            
            var card = new VisualElement();
            card.AddToClassList("pool-stat-card");
            ApplyCardStyle(card);
            
            // Pool name header
            card.Add(CreateHeaderLabel(poolName));
            
            // Stats grid (3 columns)
            var grid = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, justifyContent = Justify.SpaceBetween }
            };
            
            // Column 1: Active & Pooled
            var col1 = new VisualElement();
            col1.Add(CreateStatLabel("Active", stats.activeCount, new Color(0.4f, 1f, 0.4f)));
            col1.Add(CreateStatLabel("Pooled", stats.pooledCount, new Color(0.4f, 0.8f, 1f)));
            grid.Add(col1);
            
            // Column 2: Hits & Misses
            var col2 = new VisualElement();
            col2.Add(CreateStatLabel("Hits", stats.poolHits, new Color(0.4f, 1f, 0.4f)));
            col2.Add(CreateStatLabel("Misses", stats.poolMisses, new Color(1f, 0.6f, 0.4f)));
            grid.Add(col2);
            
            // Column 3: Peak & Hit Rate
            var col3 = new VisualElement();
            col3.Add(CreateStatLabel("Peak", stats.peakActive, new Color(1f, 0.9f, 0.4f)));
            col3.Add(CreateStatLabel("Hit Rate", $"{stats.HitRate:P0}", GetHitRateColor(stats.HitRate)));
            grid.Add(col3);
            
            card.Add(grid);
            
            // Usage bar
            if (stats.peakActive > 0)
            {
                float usage = (float)stats.activeCount / stats.peakActive;
                card.Add(CreateUsageBar(usage, "Usage"));
            }
            
            return card;
        }
        
        private static Label CreateHeaderLabel(string text)
        {
            return new Label(text)
            {
                style =
                {
                    unityFontStyleAndWeight = FontStyle.Bold,
                    fontSize = 12,
                    marginBottom = 4
                }
            };
        }
        
        private static VisualElement CreateStatLabel(string label, object value, Color valueColor)
        {
            var container = new VisualElement { style = { flexDirection = FlexDirection.Row } };
            
            container.Add(new Label($"{label}: ") { style = { color = new Color(0.7f, 0.7f, 0.7f) } });
            container.Add(new Label(value.ToString())
            {
                style = { color = valueColor, unityFontStyleAndWeight = FontStyle.Bold }
            });
            
            return container;
        }
        
        private VisualElement CreateUsageBar(float usage, string label)
        {
            var container = new VisualElement
            {
                style = { flexDirection = FlexDirection.Row, alignItems = Align.Center, marginTop = 4 }
            };
            
            container.Add(new Label($"{label}: ") { style = { color = new Color(0.6f, 0.6f, 0.6f), fontSize = 10 } });
            
            var barBg = new VisualElement
            {
                style =
                {
                    width = 80, height = 8,
                    backgroundColor = new Color(0.15f, 0.15f, 0.15f),
                    borderTopLeftRadius = 2, borderTopRightRadius = 2,
                    borderBottomLeftRadius = 2, borderBottomRightRadius = 2
                }
            };
            
            var barFill = new VisualElement
            {
                style =
                {
                    width = Length.Percent(Mathf.Clamp01(usage) * 100),
                    height = Length.Percent(100),
                    backgroundColor = GetUsageColor(usage),
                    borderTopLeftRadius = 2, borderTopRightRadius = 2,
                    borderBottomLeftRadius = 2, borderBottomRightRadius = 2
                }
            };
            barBg.Add(barFill);
            container.Add(barBg);
            
            container.Add(new Label($" {usage:P0}") { style = { fontSize = 10, color = new Color(0.6f, 0.6f, 0.6f) } });
            
            return container;
        }
        
        private static void ApplyFoldoutStyle(Foldout foldout)
        {
            foldout.style.marginTop = 8;
            foldout.style.backgroundColor = new Color(0.2f, 0.2f, 0.2f, 0.3f);
            foldout.style.borderTopLeftRadius = 4;
            foldout.style.borderTopRightRadius = 4;
            foldout.style.borderBottomLeftRadius = 4;
            foldout.style.borderBottomRightRadius = 4;
            foldout.style.paddingBottom = 8;
            foldout.style.paddingLeft = 4;
            foldout.style.paddingRight = 4;
        }
        
        private static void ApplyCardStyle(VisualElement card)
        {
            card.style.backgroundColor = new Color(0.25f, 0.25f, 0.25f, 0.5f);
            card.style.borderTopLeftRadius = 4;
            card.style.borderTopRightRadius = 4;
            card.style.borderBottomLeftRadius = 4;
            card.style.borderBottomRightRadius = 4;
            card.style.marginBottom = 6;
            card.style.paddingTop = 6;
            card.style.paddingBottom = 6;
            card.style.paddingLeft = 8;
            card.style.paddingRight = 8;
        }
        
        private static Color GetUsageColor(float usage)
        {
            return usage switch
            {
                < 0.5f => new Color(0.3f, 0.8f, 0.3f),
                < 0.8f => new Color(1f, 0.8f, 0.2f),
                _ => new Color(1f, 0.3f, 0.3f)
            };
        }
        
        private static Color GetHitRateColor(float hitRate)
        {
            return hitRate switch
            {
                >= 0.8f => new Color(0.3f, 0.9f, 0.3f),
                >= 0.5f => new Color(1f, 0.8f, 0.2f),
                _ => new Color(1f, 0.4f, 0.4f)
            };
        }
        
        private static string FormatValue(object value)
        {
            return value switch
            {
                long bytes => FormatBytes(bytes),
                _ => value?.ToString() ?? "N/A"
            };
        }
        
        private static string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            int order = 0;
            double size = bytes;
            while (size >= 1024 && order < sizes.Length - 1)
            {
                order++;
                size /= 1024;
            }
            return $"{size:0.##} {sizes[order]}";
        }
        
        #endregion
    }
}

