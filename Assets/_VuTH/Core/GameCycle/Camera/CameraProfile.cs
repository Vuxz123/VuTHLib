using System;
using UnityEngine;

namespace Core.GameCycle.Camera
{
    [Serializable]
    public class CameraProfile
    {
        public bool useOrthographic;
        public float fieldOfView;
        public float orthographicSize;

        // ABSOLUTE world-space pose
        public Vector3 worldPosition;
        public Vector3 worldEulerRotation;

        public float transitionDuration;
    }
}