using Common;
using Common.Log;
using Core.GameCycle.ScreenFlow;
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
        }
    }
}