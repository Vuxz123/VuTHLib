using System;
using Common.SharedLib;
using Common.SharedLib.Log;
using Core.GameCycle.Screen;
using Core.Generated;
using UnityEditor;
using UnityEngine;

namespace TScript
{
    public class TestScript : MonoBehaviour
    {
        [SerializeField] private SerializableDictionary<string, string> serializedObject;

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                this.Log("Press Space Key");
                ScreenManager.Instance.Enter(ScreenIds.Home);
            }
        }
    }
}