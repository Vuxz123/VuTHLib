using System;
using System.Collections.Generic;
using _VuTH.Common.Log;
using _VuTH.Core.GameCycle.Screen.Identifier;
using UnityEngine;
using UnityEngine.Serialization;

namespace _VuTH.Core.GameCycle.Screen.Core
{
    /// <summary>
    /// The screens a <see cref="ScreenManager"/> knows about, looked up by <see cref="ScreenIdentifier"/>.
    /// <para>
    /// The library requires no particular screen. The only special slot is the optional
    /// <see cref="baselineScreen"/>: the screen considered "current" before the first <c>Enter</c>, typically
    /// the bootstrap scene that is already loaded when the game starts, so the first <c>Enter</c> closes it.
    /// </para>
    /// </summary>
    [Serializable]
    public class ScreenModelContainer
    {
        public ScreenModel[] screens;

        [Tooltip("Optional. Screen treated as current before the first Enter (e.g. the already-loaded bootstrap " +
                 "scene), so the first Enter closes it. Leave empty to use the screen whose ID is the built-in " +
                 "'Bootstrap' identifier, if any; otherwise the first Enter has no previous screen.")]
        [FormerlySerializedAs("bootstrapScreen")]
        public ScreenModel baselineScreen;

        private Dictionary<ScreenIdentifier, ScreenModel> _screens;

        /// <summary>Baseline resolved by <see cref="SetupContainer"/> (explicit field, else the built-in Bootstrap ID).</summary>
        public ScreenModel BaselineScreen { get; private set; }

        /// <summary>Screens successfully registered by <see cref="SetupContainer"/>.</summary>
        public int Count => _screens?.Count ?? 0;

        public void SetupContainer(ScreenModel[] ss)
        {
            screens = ss ?? Array.Empty<ScreenModel>();
            _screens = new Dictionary<ScreenIdentifier, ScreenModel>();
            for (var i = 0; i < screens.Length; i++)
            {
                var screen = screens[i];

                // Misconfigured entries are reported and skipped instead of aborting the whole bootstrap.
                if (!screen)
                {
                    this.LogError($"Screen Manager: phần tử screens[{i}] đang trống (null) — bỏ qua.");
                    continue;
                }

                if (!screen.screenId)
                {
                    this.LogError($"Screen Manager: Screen '{screen.name}' chưa có Screen Identifier — bỏ qua.");
                    continue;
                }

                if (!_screens.TryAdd(screen.screenId, screen))
                {
                    this.LogError($"Screen Manager: Screen ID '{screen.screenId.name}' bị trùng giữa " +
                                  $"'{_screens[screen.screenId].name}' và '{screen.name}' — giữ screen đầu tiên.");
                }
            }

            BaselineScreen = ResolveBaseline();
        }

        public bool TryGetScreen(ScreenIdentifier id, out ScreenModel screen)
        {
            screen = null;
            return id && _screens != null && _screens.TryGetValue(id, out screen);
        }

        public ScreenModel GetScreenById(ScreenIdentifier id)
        {
            if (TryGetScreen(id, out var screen)) return screen;

            this.LogError($"Screen Manager không tìm thấy Screen với ID: {(id ? id.name : "null")}");
            return null;
        }

        private ScreenModel ResolveBaseline()
        {
            if (baselineScreen)
            {
                if (!baselineScreen.screenId || !TryGetScreen(baselineScreen.screenId, out var registered) ||
                    registered != baselineScreen)
                {
                    this.LogWarning($"Screen Manager: baseline screen '{baselineScreen.name}' không nằm trong danh sách " +
                                    "screens. Vẫn dùng làm baseline, nhưng Enter(ScreenIdentifier) sẽ không tìm thấy nó.");
                }

                return baselineScreen;
            }

            // Backward compatible default: the library ships a 'Bootstrap' identifier. No error when it is absent —
            // games without a bootstrap screen are valid.
            var bootstrapId = DefaultScreenIds.Bootstrap;
            return bootstrapId && TryGetScreen(bootstrapId, out var bootstrap) ? bootstrap : null;
        }
    }
}
