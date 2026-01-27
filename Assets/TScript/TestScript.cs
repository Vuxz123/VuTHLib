using _VuTH.Common;
using _VuTH.Common.Log;
using _VuTH.Core.GameCycle.ScreenFlow;
using _VuTH.Core.Window;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TScript
{
    public class TestScript : MonoBehaviour
    {
        [SerializeField] private SerializableDictionary<string, string> serializedObject;

        private void Update()
        {
            if (Keyboard.current[Key.I].wasPressedThisFrame)
            {
                this.Log("Press I key detected");
                ScreenFlowManager.Instance.Trigger("BoostrapCompleted");
            }
            if (Keyboard.current[Key.O].wasPressedThisFrame)
            {
                this.Log("Press O key detected");
                WindowManager.Instance.Open<TestPopup>();
            }
        }
    }
}