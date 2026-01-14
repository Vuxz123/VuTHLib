using Common.SharedLib;
using System;
using System.Collections.Generic;
using Common.SharedLib.Log;
using Cysharp.Threading.Tasks;
using PrimeTween;
using UnityEngine;

namespace Core.GameCycle.Camera
{
    public class CameraManager : VBoostrapManager<CameraManager, ICameraManager>, ICameraManager
    {
        [Header("Camera Settings")]
        [Tooltip("Optional. If not assigned, the manager will use Camera.main.")]
        [SerializeField] private UnityEngine.Camera mainCamera;

        private readonly Stack<CameraProfile> _overrideStack = new();

        private CameraProfile _defaultProfile;
        private CameraProfile _currentBaseProfile;
        private CameraProfile _pendingBaseProfile;

        // PrimeTween handles for cancellation.
        private Tween _posTween;
        private Tween _rotTween;
        private Tween _lensTween;

        public UnityEngine.Camera MainCamera { get; private set; }
        public bool IsTransitioning { get; private set; }
        public bool IsOverriding => _overrideStack.Count > 0;

        public event Action<CameraProfile> OnProfileApplied;
        public event Action<CameraProfile> OnOverridePushed;
        public event Action OnOverridePopped;

        protected override void InitializeBootstrap()
        {
            MainCamera = mainCamera ? mainCamera : UnityEngine.Camera.main;
            if (!MainCamera)
            {
                this.LogError("CameraManager: Cannot bootstrap because MainCamera is null (assign reference or tag a camera as MainCamera).");
                return;
            }

            StopActiveTweens();

            _overrideStack.Clear();
            IsTransitioning = false;

            // Capture default profile from camera state at bootstrap (requested).
            _defaultProfile = CaptureProfileFromCamera(MainCamera);
            _currentBaseProfile = _defaultProfile;
            _pendingBaseProfile = null;

            // Ensure camera is in a consistent baseline state (no offsets).
            ApplyProfileImmediate(MainCamera, _defaultProfile, useOffsets: false);
        }

        protected override void DeinitializeBootstrap()
        {
            StopActiveTweens();

            _overrideStack.Clear();
            _pendingBaseProfile = null;
            _currentBaseProfile = null;
            _defaultProfile = null;

            IsTransitioning = false;
            MainCamera = null;
        }

        public UniTask ApplyProfile(CameraProfile profile)
        {
            if (profile == null)
                return ResetProfile();

            // If overriding, queue the latest base profile (requested: Option A).
            if (IsOverriding)
            {
                _pendingBaseProfile = profile;
                return UniTask.CompletedTask;
            }

            _currentBaseProfile = profile;
            return ApplyEffectiveProfileAsync(profile, fireAppliedEvent: true);
        }

        public UniTask ResetProfile()
        {
            if (_defaultProfile == null)
            {
                // Bootstrap might not be ready yet.
                if (!MainCamera)
                    MainCamera = mainCamera ? mainCamera : UnityEngine.Camera.main;

                if (!MainCamera)
                {
                    this.LogError("CameraManager: ResetProfile failed because MainCamera is null.");
                    return UniTask.CompletedTask;
                }

                _defaultProfile = CaptureProfileFromCamera(MainCamera);
            }

            if (IsOverriding)
            {
                _pendingBaseProfile = _defaultProfile;
                return UniTask.CompletedTask;
            }

            _currentBaseProfile = _defaultProfile;
            return ApplyEffectiveProfileAsync(_currentBaseProfile, fireAppliedEvent: true);
        }

        public UniTask PushOverride(CameraProfile overrideProfile)
        {
            if (overrideProfile == null)
            {
                this.LogError("CameraManager: PushOverride called with null profile.");
                return UniTask.CompletedTask;
            }

            _overrideStack.Push(overrideProfile);
            OnOverridePushed?.Invoke(overrideProfile);

            return ApplyEffectiveProfileAsync(overrideProfile, fireAppliedEvent: false);
        }

        public UniTask PopOverride()
        {
            if (_overrideStack.Count == 0)
            {
                // No-op.
                return UniTask.CompletedTask;
            }

            _overrideStack.Pop();
            OnOverridePopped?.Invoke();

            // After pop, either apply next override, or restore base (including pending latest base).
            if (_overrideStack.Count > 0)
            {
                return ApplyEffectiveProfileAsync(_overrideStack.Peek(), fireAppliedEvent: false);
            }

            if (_pendingBaseProfile != null)
            {
                _currentBaseProfile = _pendingBaseProfile;
                _pendingBaseProfile = null;
            }

            var baseProfile = _currentBaseProfile ?? _defaultProfile;
            return ApplyEffectiveProfileAsync(baseProfile, fireAppliedEvent: true);
        }

        private UniTask ApplyEffectiveProfileAsync(CameraProfile profile, bool fireAppliedEvent)
        {
            if (!MainCamera)
            {
                MainCamera = mainCamera ? mainCamera : UnityEngine.Camera.main;
                if (!MainCamera)
                {
                    this.LogError("CameraManager: Cannot apply profile because MainCamera is null.");
                    return UniTask.CompletedTask;
                }
            }

            return TransitionToProfileInternal(profile, fireAppliedEvent);
        }

        private UniTask TransitionToProfileInternal(CameraProfile profile, bool fireAppliedEvent)
        {
            if (profile == null || !MainCamera)
                return UniTask.CompletedTask;

            var cam = MainCamera;
            var duration = Mathf.Max(0f, profile.transitionDuration);

            StopActiveTweens();

            // Projection
            cam.orthographic = profile.useOrthographic;

            if (duration <= 0f)
            {
                ApplyLens(cam, profile);
                cam.transform.SetPositionAndRotation(
                    profile.worldPosition,
                    Quaternion.Euler(profile.worldEulerRotation)
                );

                if (fireAppliedEvent)
                    OnProfileApplied?.Invoke(profile);

                return UniTask.CompletedTask;
            }

            IsTransitioning = true;

            _posTween = Tween.Position(
                cam.transform, 
                profile.worldPosition, 
                duration, 
                ease: Ease.InOutCubic);
            _rotTween = Tween.Rotation(
                cam.transform, 
                Quaternion.Euler(profile.worldEulerRotation), 
                duration, 
                ease: Ease.InOutCubic);

            if (profile.useOrthographic)
            {
                _lensTween = Tween.CameraOrthographicSize(
                    cam, profile.orthographicSize, duration, ease: Ease.InOutCubic);
            }
            else
            {
                _lensTween = Tween.CameraFieldOfView(
                    cam, profile.fieldOfView, duration, ease: Ease.InOutCubic);
            }

            return AwaitTransition(profile, fireAppliedEvent);
        }

        private async UniTask AwaitTransition(CameraProfile appliedProfile, bool fireAppliedEvent)
        {
            try
            {
                // PrimeTween await pattern (same as used in DefaultSliderLoadingController).
                await _posTween;

                if (fireAppliedEvent)
                    OnProfileApplied?.Invoke(appliedProfile);
            }
            catch (Exception e)
            {
                // If tween got stopped/cancelled, awaiting may throw depending on PrimeTween version.
                this.Log($"Camera transition interrupted: {e.Message}");
            }
            finally
            {
                IsTransitioning = false;
            }
        }

        private static void ApplyLens(UnityEngine.Camera cam, CameraProfile profile)
        {
            if (!cam || profile == null)
                return;

            cam.orthographic = profile.useOrthographic;
            if (profile.useOrthographic)
                cam.orthographicSize = profile.orthographicSize;
            else
                cam.fieldOfView = profile.fieldOfView;
        }

        private static CameraProfile CaptureProfileFromCamera(UnityEngine.Camera cam)
        {
            return new CameraProfile
            {
                useOrthographic = cam.orthographic,
                fieldOfView = cam.fieldOfView,
                orthographicSize = cam.orthographicSize,

                worldPosition = cam.transform.position,
                worldEulerRotation = cam.transform.rotation.eulerAngles,

                transitionDuration = 0f
            };
        }

        private static void ApplyProfileImmediate(UnityEngine.Camera cam, CameraProfile profile, bool useOffsets)
        {
            if (!cam || profile == null)
                return;

            ApplyLens(cam, profile);

            if (!useOffsets)
                return;

            cam.transform.SetPositionAndRotation(
                profile.worldPosition,
                Quaternion.Euler(profile.worldEulerRotation)
            );
        }

        private void StopActiveTweens()
        {
            // Stop in reverse order - doesn't matter, but keeps it tidy.
            if (_lensTween.isAlive) _lensTween.Stop();
            if (_rotTween.isAlive) _rotTween.Stop();
            if (_posTween.isAlive) _posTween.Stop();

            _lensTween = default;
            _rotTween = default;
            _posTween = default;
        }
    }
}

