using System;
using Common.SharedLib;
using Cysharp.Threading.Tasks;

namespace Core.GameCycle.Camera
{
    public interface ICameraManager : ICommonManager
    {
        // ===== State =====

        UnityEngine.Camera MainCamera { get; }

        bool IsTransitioning { get; }

        // ===== Apply camera for Screen =====

        /// <summary>
        /// Apply camera configuration for a Screen.
        /// Called by ScreenManager when Enter(Screen).
        /// </summary>
        UniTask ApplyProfile(CameraProfile profile);

        /// <summary>
        /// Reset camera to default (optional).
        /// </summary>
        UniTask ResetProfile();

        // ===== Override (system-level) =====

        /// <summary>
        /// Push a high-priority camera override (force update, cutscene, etc.)
        /// </summary>
        UniTask PushOverride(CameraProfile overrideProfile);

        /// <summary>
        /// Pop override and restore previous camera state.
        /// </summary>
        UniTask PopOverride();

        bool IsOverriding { get; }

        // ===== Hooks / Events =====

        event Action<CameraProfile> OnProfileApplied;
        event Action<CameraProfile> OnOverridePushed;
        event Action OnOverridePopped;
    }
}