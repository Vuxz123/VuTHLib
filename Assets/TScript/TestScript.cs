using Common;
using Common.Log;
using Core.GameCycle.ScreenFlow;
using Core.Window;
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