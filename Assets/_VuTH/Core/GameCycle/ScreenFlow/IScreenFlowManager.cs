using _VuTH.Common;
using _VuTH.Core.GameCycle.Screen;
using _VuTH.Core.GameCycle.Screen.Core;

namespace _VuTH.Core.GameCycle.ScreenFlow
{
    /// <summary>
    /// ScreenFlowManager quyết định WHEN và WHERE chuyển Screen,
    /// nhưng KHÔNG thực hiện load Screen.
    /// </summary>
    public interface IScreenFlowManager : ICommonManager
    {
        /// <summary>
        /// Screen khởi đầu của Flow.
        /// Thường được gọi khi bootstrap game.
        /// </summary>
        ScreenModel GetStartScreen();

        /// <summary>
        /// Trigger một intent (event) để Flow resolve Screen tiếp theo.
        /// Đây là CỔNG DUY NHẤT để Flow chuyển state.
        /// </summary>
        void Trigger(string eventName);

        /// <summary>
        /// Screen hiện tại theo Flow (logical state).
        /// KHÔNG nhất thiết trùng ScreenManager.Current trong mọi thời điểm.
        /// </summary>
        ScreenModel Current { get; }

        /// <summary>
        /// Event cuối cùng đã trigger (debug / telemetry).
        /// </summary>
        string LastEvent { get; }
    }
}