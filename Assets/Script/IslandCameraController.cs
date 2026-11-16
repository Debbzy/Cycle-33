using UnityEngine;
using UnityEngine.InputSystem;
using Cycle33.Data;

namespace Cycle33.Systems
{
    public class IslandCameraController : MonoBehaviour
    {
        [Header("Target Island Center")]
        public GlobalRulesSO globalRules;
        public Vector3 islandCenter = new Vector3(5f, 0f, 5f);

        [Header("Rotation Settings")]
        public float keyOrbitSpeed = 90f;
        public float mouseDragOrbitSpeed = 0.25f;
        public float pitchAngle = 55f;
        public float minPitch = 20f;
        public float maxPitch = 80f;

        [Header("Zoom Settings")]
        public float distance = 13f;
        public float minDistance = 4f;
        public float maxDistance = 24f;
        public float scrollZoomSpeed = 0.015f;
        public float keyZoomSpeed = 10f;
        public float smoothSpeed = 12f;

        private float _targetYaw = 0f;
        private float _targetPitch = 55f;
        private float _targetDistance = 13f;

        private float _currentYaw;
        private float _currentPitch;
        private float _currentDistance;

        private void Start()
        {
            if (globalRules != null)
            {
                islandCenter = new Vector3(globalRules.gridSize.x * 0.5f, 0f, globalRules.gridSize.y * 0.5f);
            }

            _targetPitch = pitchAngle;
            _targetDistance = distance;

            _currentYaw = _targetYaw;
            _currentPitch = _targetPitch;
            _currentDistance = _targetDistance;

            ApplyCameraTransform();
        }

        private void LateUpdate()
        {
            // Menggunakan unscaledDeltaTime agar kecepatan kamera tidak ikut ngebut saat Fast-Forward 6x
            float dt = Time.unscaledDeltaTime;

            HandleKeyboardInput(dt);
            HandleMouseInput();

            _targetPitch = Mathf.Clamp(_targetPitch, minPitch, maxPitch);
            _targetDistance = Mathf.Clamp(_targetDistance, minDistance, maxDistance);

            _currentYaw = Mathf.LerpAngle(_currentYaw, _targetYaw, dt * smoothSpeed);
            _currentPitch = Mathf.Lerp(_currentPitch, _targetPitch, dt * smoothSpeed);
            _currentDistance = Mathf.Lerp(_currentDistance, _targetDistance, dt * smoothSpeed);

            ApplyCameraTransform();
        }

        private void HandleKeyboardInput(float dt)
        {
            if (Keyboard.current == null) return;

            // Putar kiri/kanan dengan Q/E atau A/D
            if (Keyboard.current.qKey.isPressed || Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed)
                _targetYaw += keyOrbitSpeed * dt;
            if (Keyboard.current.eKey.isPressed || Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed)
                _targetYaw -= keyOrbitSpeed * dt;

            // Miringkan atas/bawah dengan W/S
            if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed)
                _targetPitch += (keyOrbitSpeed * 0.6f) * dt;
            if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed)
                _targetPitch -= (keyOrbitSpeed * 0.6f) * dt;

            // Alternatif Zoom dengan Z / X
            if (Keyboard.current.zKey.isPressed)
                _targetDistance -= keyZoomSpeed * dt;
            if (Keyboard.current.xKey.isPressed)
                _targetDistance += keyZoomSpeed * dt;
        }

        private void HandleMouseInput()
        {
            if (Mouse.current == null) return;

            // Tahan Middle Mouse Button (Klik Scroll) untuk drag rotasi pulau
            if (Mouse.current.middleButton.isPressed)
            {
                Vector2 delta = Mouse.current.delta.ReadValue();
                _targetYaw += delta.x * mouseDragOrbitSpeed;
                _targetPitch -= delta.y * mouseDragOrbitSpeed;
            }

            // Scroll Wheel untuk Zoom In / Zoom Out
            float scrollY = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scrollY) > 0.01f)
            {
                _targetDistance -= scrollY * scrollZoomSpeed;
            }
        }

        private void ApplyCameraTransform()
        {
            Quaternion rot = Quaternion.Euler(_currentPitch, _currentYaw, 0f);
            Vector3 targetPos = islandCenter - (rot * Vector3.forward * _currentDistance);

            transform.position = targetPos;
            transform.LookAt(islandCenter);
        }
    }
}