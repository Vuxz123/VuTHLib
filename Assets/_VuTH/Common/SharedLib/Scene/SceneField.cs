using System;
using UnityEngine;

namespace Common.SharedLib.Scene
{
    [Serializable]
    public struct SceneField
    {
        [SerializeField]
        [SceneSelector] public string sceneName;
        
        public static implicit operator string(SceneField field)
        {
            return field.sceneName;
        }
        
        public static implicit operator SceneField(string value)
        {
            return new SceneField { sceneName = value };
        }
        
        public override string ToString() => sceneName;
    }
}