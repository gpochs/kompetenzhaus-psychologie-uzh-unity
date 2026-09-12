using System;
using System.Collections.Generic;
using System.Linq;
using Kompetenzhaus.Quiz;
using Kompetenzhaus.State;
using UnityEngine;

namespace Kompetenzhaus.WorldSupport
{
    // Rigid authored meshes on inexpensive pivots. This is deliberately not a skinned humanoid rig.
    [DisallowMultipleComponent]
    public sealed class GuideMotion : MonoBehaviour
    {
        public Transform visualRoot;
        public Vector3[] localWaypoints = Array.Empty<Vector3>();
        public float walkSpeed = 1.05f;
        public float idlePauseSeconds = 3f;
        public float forwardYawOffset;
        public float turnSpeed = 180f;
        [Min(1f)] public float greetingDistance = 5f;
        private GameBootstrap game;
        private readonly List<PivotPose> pivots = new List<PivotPose>();
        private bool prepared;
        private Vector3 visualRestPosition;
        private int nextWaypoint;
        private float pauseRemaining, motionClock, actualSpeed, greetingTime, greetingCooldown;
        private bool attendingVisitor;
        private int arrivalCount;
        private static readonly float[] PauseFactors = { 1f, 1.45f, .8f, 1.2f };

        public bool IsAttendingVisitor => attendingVisitor;

        public void Initialize(GameBootstrap application)
        {
            game = application;
            PreparePivots();
        }

        private void PreparePivots()
        {
            if (prepared || visualRoot == null) return;
            prepared = true;
            visualRestPosition = visualRoot.localPosition;
            AddPivot("Left shoulder", new Vector3(-0.27f, 1.37f, 0f), 1f, "LeftUpperArm", "LeftForearm", "LeftHand");
            AddPivot("Right shoulder", new Vector3(0.27f, 1.37f, 0f), -1f, "RightUpperArm", "RightForearm", "RightHand");
            AddPivot("Left hip", new Vector3(-0.13f, 0.87f, 0f), -1f, "LeftLeg", "LeftFoot");
            AddPivot("Right hip", new Vector3(0.13f, 0.87f, 0f), 1f, "RightLeg", "RightFoot");
            pauseRemaining = Mathf.Max(0f, idlePauseSeconds);
        }

        private void AddPivot(string name, Vector3 localPosition, float phase, params string[] pieceNames)
        {
            var pieces = visualRoot.GetComponentsInChildren<Transform>(true)
                .Where(child => pieceNames.Contains(child.name)).ToArray();
            if (pieces.Length == 0) return;
            var pivot = new GameObject(name).transform;
            pivot.SetParent(visualRoot, false);
            pivot.localPosition = localPosition;
            foreach (var piece in pieces) piece.SetParent(pivot, true);
            pivots.Add(new PivotPose { pivot = pivot, restRotation = pivot.localRotation, phase = phase, greetingArm = name == "Right shoulder" });
        }

        private void Update()
        {
            if (!prepared) PreparePivots();
            if (!prepared || game?.Progression == null) return;
            if (game.Progression.Data.accessibility.reducedMotion)
            {
                actualSpeed = 0f;
                attendingVisitor = false;
                RestorePose();
                return;
            }
            // Keep the world calm while a question or another panel has focus.
            if (game.UiFocus || game.Quiz?.Phase != QuizPhase.Idle)
            {
                actualSpeed = 0f;
                RestorePose();
                return;
            }
            greetingCooldown = Mathf.Max(0f, greetingCooldown - Time.deltaTime);
            if (AttendNearbyVisitor()) { Animate(false); return; }
            var moving = false;
            if (pauseRemaining > 0f) pauseRemaining -= Time.deltaTime;
            else if (localWaypoints != null && localWaypoints.Length > 1)
            {
                nextWaypoint %= localWaypoints.Length;
                var target = localWaypoints[nextWaypoint];
                var direction = target - transform.localPosition;
                direction.y = 0f;
                if (direction.sqrMagnitude < 0.015f)
                {
                    nextWaypoint = (nextWaypoint + 1) % localWaypoints.Length;
                    // Four distinct pauses make a short authored circuit feel less mechanical.
                    var pauseFactor = PauseFactors[arrivalCount++ % PauseFactors.Length];
                    pauseRemaining = Mathf.Max(0f, idlePauseSeconds) * pauseFactor;
                }
                else
                {
                    var rotation = Quaternion.LookRotation(direction.normalized, Vector3.up) * Quaternion.Euler(0f, forwardYawOffset, 0f);
                    transform.localRotation = Quaternion.RotateTowards(transform.localRotation, rotation, Mathf.Max(0f, turnSpeed) * Time.deltaTime);
                    var facing = Mathf.Clamp01(1f - Quaternion.Angle(transform.localRotation, rotation) / 80f);
                    var speed = Mathf.Clamp(walkSpeed, 0f, 2f) * facing * Mathf.Clamp01(direction.magnitude / .45f);
                    actualSpeed = Mathf.MoveTowards(actualSpeed, speed, Time.deltaTime * 2.5f);
                    if (VisitorBlocksPath(direction)) actualSpeed = 0f;
                    moving = actualSpeed > .025f;
                    var next = Vector3.MoveTowards(transform.localPosition, target, actualSpeed * Time.deltaTime);
                    next.y = transform.localPosition.y;
                    transform.localPosition = next;
                }
            }
            if (!moving) actualSpeed = 0f;
            Animate(moving);
        }

        private bool AttendNearbyVisitor()
        {
            var controls = game.cameraController;
            var player = controls?.player;
            if (controls == null || controls.Mode != ViewMode.FirstPerson || player == null || !player.enabled)
            { attendingVisitor = false; greetingTime = 0f; return false; }
            var delta = player.transform.position - transform.position;
            delta.y = 0f;
            var reach = Mathf.Clamp(greetingDistance, 1f, 7f) + (attendingVisitor ? 1.5f : 0f);
            if (delta.sqrMagnitude > reach * reach || Mathf.Abs(player.transform.position.y - transform.position.y) > 1.5f)
            { attendingVisitor = false; greetingTime = 0f; return false; }
            // Do not greet through house walls. Ignore the player's own collider at the ray end.
            var direction = (player.transform.position + Vector3.up * 1.3f) - (transform.position + Vector3.up * 1.3f);
            if (Physics.Raycast(transform.position + Vector3.up * 1.3f, direction.normalized, out var hit,
                Mathf.Max(0f, direction.magnitude - .45f), ~0, QueryTriggerInteraction.Ignore) &&
                hit.collider.GetComponentInParent<CharacterController>() != player)
            { attendingVisitor = false; greetingTime = 0f; return false; }
            if (!attendingVisitor && greetingCooldown <= 0f) { greetingTime = 1.5f; greetingCooldown = 12f; }
            attendingVisitor = true;
            actualSpeed = 0f;
            if (delta.sqrMagnitude > .01f)
            {
                var look = Quaternion.LookRotation(delta.normalized, Vector3.up) * Quaternion.Euler(0f, forwardYawOffset, 0f);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, look, Mathf.Max(0f, turnSpeed) * .65f * Time.deltaTime);
            }
            pauseRemaining = Mathf.Max(pauseRemaining, .8f);
            return true;
        }

        private bool VisitorBlocksPath(Vector3 localDirection)
        {
            var controls = game.cameraController;
            if (controls?.Mode != ViewMode.FirstPerson || controls.player == null || !controls.player.enabled) return false;
            var delta = controls.player.transform.position - transform.position;
            if (Mathf.Abs(delta.y) > 1.5f) return false;
            delta.y = 0f;
            var worldDirection = transform.parent != null ? transform.parent.TransformDirection(localDirection) : localDirection;
            return delta.sqrMagnitude < 1.8f * 1.8f && Vector3.Dot(delta.normalized, worldDirection.normalized) > .25f;
        }

        private void Animate(bool moving)
        {
            motionClock += Time.deltaTime * (moving ? 6.8f * Mathf.Clamp(actualSpeed / 1.05f, .3f, 1.6f) : 1.4f);
            greetingTime = Mathf.Max(0f, greetingTime - Time.deltaTime);
            foreach (var pose in pivots)
            {
                if (pose.pivot == null) continue;
                var angle = Mathf.Sin(motionClock) * (moving ? 22f : 1.1f) * pose.phase;
                // One short, gentle rigid-arm greeting; no repeating wave or claimed humanoid rig.
                var greeting = pose.greetingArm ? Mathf.Sin(Mathf.Clamp01(greetingTime / 1.5f) * Mathf.PI) : 0f;
                pose.pivot.localRotation = pose.restRotation * Quaternion.Euler(angle - greeting * 24f, 0f, greeting * -28f);
            }
            if (visualRoot != transform)
                visualRoot.localPosition = visualRestPosition + Vector3.up * (moving ? Mathf.Abs(Mathf.Sin(motionClock)) * 0.018f : Mathf.Sin(motionClock) * 0.008f);
        }

        private void RestorePose()
        {
            foreach (var pose in pivots) if (pose.pivot != null) pose.pivot.localRotation = pose.restRotation;
            if (visualRoot != null && visualRoot != transform) visualRoot.localPosition = visualRestPosition;
        }
        private void OnDisable() => RestorePose();
        private sealed class PivotPose { public Transform pivot; public Quaternion restRotation; public float phase; public bool greetingArm; }
    }
}
