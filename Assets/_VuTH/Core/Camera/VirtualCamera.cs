using _VuTH.Common.Init;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace _VuTH.Core.Camera
{
    public enum VirtualCameraInitStrategy
    {
        OnStart,
        VInitializeCall,
    }
    
    public class VirtualCamera : MonoBehaviour, IVInitializable
    {
        [Header("Camera Profile")]
        [SerializeField] private CameraProfile profile;

        [Header("Optional Anchor")]
        [Tooltip("If set, camera position will be relative to this transform")]
        [SerializeField] private Transform anchor;
        
        [Header("Initialization")]
        [SerializeField] private VirtualCameraInitStrategy initStrategy = VirtualCameraInitStrategy.OnStart;

        private CameraProfile Profile => profile;
        
#if UNITY_EDITOR
        private void OnValidate()
        {
            if (profile == null) return;

            profile.worldPosition = transform.position;
            profile.worldEulerRotation = transform.eulerAngles;
        }
#endif

        private void OnEnable()
        {
            if (initStrategy == VirtualCameraInitStrategy.OnStart)
            {
                Init();
            }
        }

        public UniTask VInitialize()
        {
            if (initStrategy == VirtualCameraInitStrategy.VInitializeCall)
            {
                Init();
            }
            return UniTask.CompletedTask;
        }

        private void Init()
        {
            // Initialization logic here
            if (CameraManager.HasInstance)
            {
                CameraManager.Instance.ApplyProfile(Profile);
            }
        }
    }
}