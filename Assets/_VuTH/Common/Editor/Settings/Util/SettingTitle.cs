using UnityEngine;
using UnityEngine.UIElements;

namespace _VuTH.Common.Editor.Settings.Util
{
    public sealed class SettingTitle : Label
    {
        public SettingTitle(string title)
        {
            text = title;
            style.unityFontStyleAndWeight = FontStyle.Bold;
            style.fontSize = 20;
            style.marginBottom = 4;
            style.marginTop = 4;
            style.marginLeft = 8;
        }
    }
}