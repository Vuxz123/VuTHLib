using UnityEngine.SceneManagement;

namespace _VuTH.Core.GameCycle.Screen.Loading
{
    public readonly struct LoadingContext
    {
        public readonly IScreenDefinition ScreenDefinition;
        public readonly Scene MainScene;
        public readonly Scene[] AdditiveScenes;

        public LoadingContext(IScreenDefinition screenDefinition, Scene mainScene, Scene[] additiveScenes)
        {
            ScreenDefinition = screenDefinition;
            MainScene = mainScene;
            AdditiveScenes = additiveScenes;
        }
    }
}