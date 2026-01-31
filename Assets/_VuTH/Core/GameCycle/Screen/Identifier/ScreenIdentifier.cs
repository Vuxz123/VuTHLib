using UnityEngine;

namespace _VuTH.Core.GameCycle.Screen.Identifier
{
    public class ScreenIdentifier : ScriptableObject
    {
        // Có thể thêm description nếu muốn note cho team
        [TextArea] public string description;
        
        // Helper: Convert về string để debug cho dễ
        public override string ToString() => name;
    }
}