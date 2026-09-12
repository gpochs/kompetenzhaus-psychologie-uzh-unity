using Kompetenzhaus.State;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Kompetenzhaus.Presentation
{
    public sealed class CameraModeController : MonoBehaviour
    {
        public Camera birdCamera;
        public Camera firstPersonCamera;
        public CharacterController player;
        public float walkingSpeed = 4.2f;
        public float birdPanSpeed = 12f;
        public Vector3 entryPoint = new Vector3(0f, 0.05f, -8f);
        public bool worldReady;
        public ViewMode Mode { get; private set; }
        public bool InputBlocked { get; private set; }
        private ProgressionService progression;
        private float pitch;
        private float verticalSpeed;
        private Bounds fittedBounds;
        private bool hasFittedBounds;
        private float fittedAspect, maximumBirdSize = 45f;

        public void Initialize(ProgressionService service)
        {
            progression = service;
            if (player != null)
            {
                player.enabled = false;
                player.transform.position = entryPoint;
                player.enabled = true;
            }
            ApplyMode(worldReady ? progression.Data.viewMode : ViewMode.BirdView);
        }

        public bool SetMode(ViewMode mode)
        {
            if (mode == ViewMode.FirstPerson && !worldReady) return false;
            ApplyMode(mode);
            if (progression != null)
            {
                progression.Data.viewMode = mode;
                progression.SaveAndNotify();
            }
            return true;
        }

        public void SetInputBlocked(bool blocked)
        {
            InputBlocked = blocked;
            if (blocked) ReleasePointer();
        }

        public void FitBirdView(Bounds bounds)
        {
            if (birdCamera == null) return;
            var target = bounds.center;
            var viewOffset = new Vector3(20f, 26f, -24f).normalized;
            birdCamera.transform.position = target + viewOffset * 40f;
            birdCamera.transform.LookAt(target);
            var cameraRotation = Quaternion.Inverse(birdCamera.transform.rotation);
            var extent = Vector3.zero;
            for (var x = -1; x <= 1; x += 2) for (var y = -1; y <= 1; y += 2) for (var z = -1; z <= 1; z += 2)
            {
                var projected = cameraRotation * Vector3.Scale(bounds.extents, new Vector3(x, y, z));
                extent = Vector3.Max(extent, new Vector3(Mathf.Abs(projected.x), Mathf.Abs(projected.y), Mathf.Abs(projected.z)));
            }
            var aspect = Mathf.Max(0.1f, birdCamera.aspect);
            var size = Mathf.Max(8f, Mathf.Max(extent.y, extent.x / aspect) * 1.08f);
            var distance = Mathf.Max(40f, extent.z + birdCamera.nearClipPlane + 1f);
            birdCamera.transform.position = target + viewOffset * distance;
            birdCamera.orthographic = true;
            birdCamera.orthographicSize = size;
            birdCamera.farClipPlane = Mathf.Max(birdCamera.farClipPlane, distance + extent.z + 1f);
            fittedBounds = bounds;
            hasFittedBounds = true;
            fittedAspect = birdCamera.aspect;
            maximumBirdSize = Mathf.Max(45f, size * 1.5f);
        }

        private void ApplyMode(ViewMode mode)
        {
            Mode = mode;
            if (birdCamera != null) birdCamera.gameObject.SetActive(mode == ViewMode.BirdView);
            if (firstPersonCamera != null) firstPersonCamera.gameObject.SetActive(mode == ViewMode.FirstPerson);
            if (player != null) player.enabled = mode == ViewMode.FirstPerson;
            ReleasePointer();
        }

        private void Update()
        {
            var keyboard = Keyboard.current;
            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame) ReleasePointer();
            if (InputBlocked || progression == null) return;
            if (Mode == ViewMode.BirdView) UpdateBirdView(keyboard);
            else if (worldReady) UpdateFirstPerson(keyboard);
        }

        private void UpdateBirdView(Keyboard keyboard)
        {
            if (birdCamera == null) return;
            if (hasFittedBounds && Mathf.Abs(birdCamera.aspect - fittedAspect) > 0.01f) FitBirdView(fittedBounds);
            var mouse = Mouse.current;
            var overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (mouse != null && !overUi && Mathf.Abs(mouse.scroll.ReadValue().y) > 0.01f)
                birdCamera.orthographicSize = Mathf.Clamp(birdCamera.orthographicSize - mouse.scroll.ReadValue().y * 0.015f, 6f, maximumBirdSize);
            var motion = ReadMovement(keyboard);
            var forward = Vector3.ProjectOnPlane(birdCamera.transform.forward, Vector3.up).normalized;
            var right = Vector3.ProjectOnPlane(birdCamera.transform.right, Vector3.up).normalized;
            birdCamera.transform.position += (forward * motion.y + right * motion.x) * (birdPanSpeed * Time.deltaTime);
        }

        private void UpdateFirstPerson(Keyboard keyboard)
        {
            if (player == null || firstPersonCamera == null) return;
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame &&
                (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
            if (Cursor.lockState != CursorLockMode.Locked) return;
            if (mouse != null)
            {
                var look = mouse.delta.ReadValue() * (0.08f * progression.Data.accessibility.lookSensitivity);
                player.transform.Rotate(0f, look.x, 0f);
                pitch = Mathf.Clamp(pitch - look.y, -78f, 78f);
                firstPersonCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
            }
            var motion = ReadMovement(keyboard);
            var velocity = player.transform.forward * motion.y + player.transform.right * motion.x;
            if (player.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
            verticalSpeed += Physics.gravity.y * Time.deltaTime;
            player.Move((velocity * walkingSpeed + Vector3.up * verticalSpeed) * Time.deltaTime);
        }

        private static Vector2 ReadMovement(Keyboard keyboard)
        {
            if (keyboard == null) return Vector2.zero;
            var x = (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed ? 1f : 0f) -
                    (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed ? 1f : 0f);
            var y = (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed ? 1f : 0f) -
                    (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed ? 1f : 0f);
            return Vector2.ClampMagnitude(new Vector2(x, y), 1f);
        }

        private static void ReleasePointer()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void OnDisable() => ReleasePointer();
    }
}
