using System;
using Kompetenzhaus.Quiz;
using Kompetenzhaus.State;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Kompetenzhaus.WorldSupport
{
    [DisallowMultipleComponent]
    public sealed class WorldInteractor : MonoBehaviour
    {
        public LayerMask interactionLayers = ~0;
        public float firstPersonReach = 4f;
        public float birdReach = 250f;
        public WorldInteraction FocusedTarget { get; private set; }
        public string CurrentHint => FocusedTarget == null ? "" : FocusedTarget.GetHint(game, game?.cameraController?.Mode == ViewMode.FirstPerson);
        public event Action<WorldInteraction> FocusChanged;
        public event Action<WorldInteraction> InteractionTriggered;
        private GameBootstrap game;
        private WorldAudio audioFeedback;

        public void Initialize(GameBootstrap application, WorldAudio audio = null)
        {
            game = application;
            audioFeedback = audio;
            SetFocus(null);
        }

        private void Update()
        {
            var controls = game?.cameraController;
            if (controls == null || !controls.worldReady || controls.InputBlocked || game.UiFocus || game.Quiz?.Phase != QuizPhase.Idle)
            { SetFocus(null); return; }
            var overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (overUi) { SetFocus(null); return; }
            var bird = controls.Mode == ViewMode.BirdView;
            var camera = bird ? controls.birdCamera : controls.firstPersonCamera;
            if (camera == null) { SetFocus(null); return; }
            Ray ray;
            if (bird)
            {
                if (Mouse.current == null || !camera.pixelRect.Contains(Mouse.current.position.ReadValue())) { SetFocus(null); return; }
                ray = camera.ScreenPointToRay(Mouse.current.position.ReadValue());
            }
            else ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            var hitTarget = Physics.Raycast(ray, out var hit, bird ? Mathf.Clamp(birdReach, 1f, 500f) : Mathf.Clamp(firstPersonReach, 0.5f, 4f),
                interactionLayers, QueryTriggerInteraction.Collide) ? hit.collider.GetComponentInParent<WorldInteraction>() : null;
            SetFocus(hitTarget != null && hitTarget.IsValid(game) ? hitTarget : null);
            var activate = bird ? (Mouse.current?.leftButton.wasPressedThisFrame ?? false) : (Keyboard.current?.eKey.wasPressedThisFrame ?? false);
            if (!activate || FocusedTarget == null) return;
            audioFeedback?.NotifyUserInteraction();
            var target = FocusedTarget;
            if (target.Activate(game)) InteractionTriggered?.Invoke(target);
            else audioFeedback?.PlayUiUnavailable();
        }

        private void SetFocus(WorldInteraction target)
        {
            if (FocusedTarget == target) return;
            FocusedTarget = target;
            FocusChanged?.Invoke(target);
            if (target != null) audioFeedback?.PlayUiHover();
        }
        private void OnDisable() => SetFocus(null);
    }
}
