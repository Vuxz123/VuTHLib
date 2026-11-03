using UnityEngine;
using VContainer.Unity;

namespace _VuTH.Common.DI
{
    /// <summary>
    /// VContainer diagnostics (<c>VContainerSettings.EnableDiagnostics</c>) record every registration / resolve and
    /// keep stack traces: useful in the editor's Diagnostics window, pure overhead in a shipped build.
    /// This turns it off at startup in release players (not in the editor, not in Development builds), without
    /// touching the settings asset, so the editor keeps whatever the team chose.
    /// </summary>
    internal static class ReleaseDiagnosticsGuard
    {
#if !UNITY_EDITOR && !DEVELOPMENT_BUILD
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void DisableDiagnosticsInRelease()
        {
            var settings = VContainerSettings.Instance;
            if (settings != null && settings.EnableDiagnostics)
            {
                settings.EnableDiagnostics = false;
            }
        }
#endif
    }
}
