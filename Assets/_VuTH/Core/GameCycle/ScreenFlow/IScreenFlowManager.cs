using _VuTH.Common;
using _VuTH.Core.GameCycle.Screen;
using _VuTH.Core.GameCycle.Screen.Core;
using _VuTH.Core.GameCycle.Screen.Core.A;

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

        /// <summary>
        /// Truy cập history của flow.
        /// </summary>
        /// <param name="stepsBack">Số bước lùi (1 = previous, 2 = 2 steps back, etc.)</param>
        /// <param name="node">Node tương ứng hoặc null nếu không đủ history</param>
        /// <returns>True nếu có đủ history, false nếu stepsBack vượt quá độ dài history</returns>
        bool TryGetPrevious(int stepsBack, out ScreenFlowNode node);
    }
}