using UnityEngine;

namespace _VuTH.Core.GameCycle.Screen.Identifier
{
    public static class DefaultScreenIds
    {
        private static ScreenIdentifier _Bootstrap;
        public static ScreenIdentifier Bootstrap => _Bootstrap != null ? _Bootstrap : (_Bootstrap = Resources.Load<ScreenIdentifier>("GameCycle/ScreenIdentifiers/Bootstrap"));

        private static ScreenIdentifier _GamePlay;
        public static ScreenIdentifier GamePlay => _GamePlay != null ? _GamePlay : (_GamePlay = Resources.Load<ScreenIdentifier>("GameCycle/ScreenIdentifiers/GamePlay"));

        private static ScreenIdentifier _Home;
        public static ScreenIdentifier Home => _Home != null ? _Home : (_Home = Resources.Load<ScreenIdentifier>("GameCycle/ScreenIdentifiers/Home"));

        /// <summary>ScreenIdentifier names as constants (usable in attributes).</summary>
        public static class Names
        {
            public const string Bootstrap = "Bootstrap";
            public const string GamePlay = "GamePlay";
            public const string Home = "Home";
        }
    }
}
