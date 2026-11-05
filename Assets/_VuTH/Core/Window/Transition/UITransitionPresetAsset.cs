using UnityEngine;

namespace _VuTH.Core.Window.Transition
{
    /// <summary>
    /// A named transition preset made in the Editor (e.g. "PopupBounce": Scale with a custom curve + Fade). Add it to
    /// the <see cref="UITransitionFactory"/>'s presets; windows then use it by name (<c>TransitionPreset</c>). It takes
    /// precedence over the built-in presets of the same name.
    /// </summary>
    [CreateAssetMenu(fileName = "TransitionPreset", menuName = "VuTH/Window/Transition Preset")]
    public sealed class UITransitionPresetAsset : ScriptableObject
    {
        [Tooltip("Name used in TransitionPreset (case-insensitive). Empty: the asset name.")]
        [SerializeField] private string presetName;

        [SerializeReference]
        [TransitionSettingsReference]
        private UITransitionSettings transition;

        public string PresetName => string.IsNullOrWhiteSpace(presetName) ? name : presetName.Trim();

        public UITransitionSettings Transition => transition;

        public IUITransition Create() => transition?.Create();
    }
}
