using System;
using System.Collections.Generic;
using _VuTH.Common;
using _VuTH.Common.Log;
using _VuTH.Core.GameCycle.Screen.Identifier;

namespace _VuTH.Core.GameCycle.Screen.Core
{
    [Serializable]
    public class ScreenModelContainer
    {
        public ScreenModel[] screens;
        [ReadOnlyField] public ScreenModel bootstrapScreen;
        [ReadOnlyField] public ScreenModel homeScreen;
        [ReadOnlyField] public ScreenModel gameplayScreen;
        
        private Dictionary<ScreenIdentifier, ScreenModel> _screens;
        
        public void SetupContainer(ScreenModel[] ss)
        {
            screens = ss;
            _screens = new Dictionary<ScreenIdentifier, ScreenModel>();
            foreach (var screen in ss)
            {
                if (screen.screenId == DefaultScreenIds.Boostrap)
                    bootstrapScreen = screen;
                else if (screen.screenId == DefaultScreenIds.Home)
                    homeScreen = screen;
                else if (screen.screenId == DefaultScreenIds.GamePlay) 
                    gameplayScreen = screen;
                
                _screens.Add(screen.screenId, screen);
            }
            
            // Validate
            if (!bootstrapScreen)
            {
                this.LogError("Screen Manager không có Bootstrap Screen hợp lệ! Vui lòng kiểm tra lại cấu hình.");
            }
            if (!homeScreen)
            {
                this.LogError("Screen Manager không có Home Screen hợp lệ! Vui lòng kiểm tra lại cấu hình.");
            }
            if (!gameplayScreen)
            {
                this.LogError("Screen Manager không có Gameplay Screen hợp lệ! Vui lòng kiểm tra lại cấu hình.");
            }
            if (_screens.Count != screens.Length)
            {
                this.LogError("Screen Manager có các Screen ID bị trùng lặp! Vui lòng kiểm tra lại cấu hình.");
            }
        }

        public ScreenModel GetScreenById(ScreenIdentifier id)
        {
            if (_screens != null && _screens.TryGetValue(id, out var screen))
            {
                return screen;
            }
            this.LogError($"Screen Manager không tìm thấy Screen với ID: {id}");
            return null;
        }
    }
}