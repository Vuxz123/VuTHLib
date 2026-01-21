using System;
using Common.SharedLib;
using Common.SharedLib.Log;
using Core.GameCycle.Screen;
using Core.GameCycle.ScreenFlow;
using Core.Generated;
using UnityEditor;
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