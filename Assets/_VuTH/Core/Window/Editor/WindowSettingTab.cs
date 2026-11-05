using System.Linq;
using System.Text;
using _VuTH.Common.Editor.Settings;
using _VuTH.Common.Editor.Settings.Util;
using _VuTH.Core.Window.Blocker;
using _VuTH.Core.Window.Profile;
using Cysharp.Threading.Tasks;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace _VuTH.Core.Window.Editor
{
    [SettingsTab]
    public class WindowSettingTab : ISettingsTab
    {
        private const long RefreshIntervalMs = 300;

        public string Id => "Window";
        public string Title => "Window";
        public int Order => 20;

        private SerializedObject _serializedProfile;

        public VisualElement CreateView()
        {
            var container = new VisualElement();

            if (!WindowProfileUtilities.TryGetProfile(out var profile))
            {
                container.Add(new Label("Error: WindowProfile not found."));
                return container;
            }

            _serializedProfile = new SerializedObject(profile);
            container.Add(new SettingTitle("Window System Settings"));

            var sorting = new SettingSection("Canvas Sorting");
            sorting.Add(new PropertyField(_serializedProfile.FindProperty(nameof(WindowProfile.popupBaseSortingOrder))));
            sorting.Add(new PropertyField(_serializedProfile.FindProperty(nameof(WindowProfile.systemBaseSortingOrder))));
            sorting.Add(new PropertyField(_serializedProfile.FindProperty(nameof(WindowProfile.sortingOrderStep))));
            container.Add(sorting);

            var input = new SettingSection("Input Blocking");
            input.Add(new PropertyField(_serializedProfile.FindProperty(nameof(WindowProfile.blockInputDuringTransitions))));
            container.Add(input);

            container.Bind(_serializedProfile);

            container.Add(CreateRuntimeSection());
            return container;
        }

        #region Runtime (Play Mode)

        /// <summary>Live view of the window stack, the queue and the input blocker, with close buttons.</summary>
        private static VisualElement CreateRuntimeSection()
        {
            var section = new SettingSection("Runtime");
            section.style.marginTop = 12;
            section.Add(Header("Runtime (Play Mode)"));

            var content = new VisualElement();
            section.Add(content);

            string lastSignature = null;
            void Refresh()
            {
                var signature = BuildSignature();
                if (signature == lastSignature) return; // Rebuilt only on change, so buttons stay clickable.
                lastSignature = signature;
                Rebuild(content);
            }

            Refresh();
            section.schedule.Execute(Refresh).Every(RefreshIntervalMs);
            return section;
        }

        private static Label Header(string text)
        {
            var label = new Label(text);
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.marginBottom = 4;
            return label;
        }

        private static bool TryGetManager(out WindowManager manager)
        {
            manager = Application.isPlaying && WindowManager.HasInstance ? WindowManager.Instance as WindowManager : null;
            return manager;
        }

        private static string BuildSignature()
        {
            if (!TryGetManager(out var manager)) return "none";

            var builder = new StringBuilder();
            builder.Append(manager.QueuedCount).Append('|').Append(manager.IsTransitioning).Append('|');
            AppendBlocker(builder, manager.InputBlocker);
            foreach (var info in manager.GetOpenWindows())
            {
                builder.Append('|').Append(info.Window ? info.Window.GetHashCode() : 0)
                    .Append(':').Append(info.State).Append(':').Append(info.SortingOrder);
            }

            return builder.ToString();
        }

        private static void AppendBlocker(StringBuilder builder, IUIInputBlocker blocker)
        {
            if (blocker == null)
            {
                builder.Append("disabled");
                return;
            }

            builder.Append(blocker.IsBlocked ? "blocked" : "free");
            if (blocker is UIInputBlocker concrete && concrete.Reasons.Count > 0)
                builder.Append(" (").Append(string.Join(", ", concrete.Reasons)).Append(')');
        }

        private static void Rebuild(VisualElement content)
        {
            content.Clear();

            if (!TryGetManager(out var manager))
            {
                content.Add(new HelpBox("Enter Play Mode to inspect the window stack.", HelpBoxMessageType.Info));
                return;
            }

            var blocker = new StringBuilder();
            AppendBlocker(blocker, manager.InputBlocker);
            content.Add(new Label($"Windows: {manager.WindowCount}    Queued: {manager.QueuedCount}    " +
                                  $"Transitioning: {manager.IsTransitioning}    Input: {blocker}"));

            var windows = manager.GetOpenWindows();
            if (windows.Count == 0)
            {
                content.Add(new Label("No open window."));
            }

            // Top first, like the screen.
            foreach (var info in windows.Reverse())
            {
                content.Add(CreateWindowRow(manager, info));
            }

            var actions = new VisualElement { style = { flexDirection = FlexDirection.Row, marginTop = 6 } };
            actions.Add(new Button(() => manager.CloseAll().Forget()) { text = "Close All" });
            actions.Add(new Button(() => manager.CloseAll(forceCleanup: true).Forget()) { text = "Force Close All" });
            actions.Add(new Button(manager.ClearQueue) { text = "Clear Queue" });
            content.Add(actions);
        }

        private static VisualElement CreateWindowRow(WindowManager manager, WindowInfo info)
        {
            var row = new VisualElement { style = { flexDirection = FlexDirection.Row, alignItems = Align.Center } };

            var name = info.Window ? info.Window.name : "(destroyed)";
            var back = info.CloseOnBackPress ? "" : ", no back";
            var label = new Label($"{name}  [{info.WindowType}, order {info.SortingOrder}, {info.State}{back}]")
            {
                style = { flexGrow = 1 }
            };
            row.Add(label);

            if (info.Window)
            {
                row.Add(new Button(() => Selection.activeGameObject = info.Window.gameObject) { text = "Select" });
                row.Add(new Button(() => manager.Close(info.Window).Forget()) { text = "Close" });
                row.Add(new Button(info.Window.ForceClose) { text = "Force" });
            }

            return row;
        }

        #endregion
    }
}
