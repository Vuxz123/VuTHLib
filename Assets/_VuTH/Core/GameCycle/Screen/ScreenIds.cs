using UnityEngine;

namespace _VuTH.Core.GameCycle.Screen
{
    public static class DefaultScreenIds
    {
        private static ScreenIdentifier _Boostrap;
        public static ScreenIdentifier Boostrap => _Boostrap ??= Resources.Load<ScreenIdentifier>("GameCycle/ScreenIdentifiers/Boostrap");

        private static ScreenIdentifier _GamePlay;
        public static ScreenIdentifier GamePlay => _GamePlay ??= Resources.Load<ScreenIdentifier>("GameCycle/ScreenIdentifiers/GamePlay");

        private static ScreenIdentifier _Home;
        public static ScreenIdentifier Home => _Home ??= Resources.Load<ScreenIdentifier>("GameCycle/ScreenIdentifiers/Home");

    }
}
