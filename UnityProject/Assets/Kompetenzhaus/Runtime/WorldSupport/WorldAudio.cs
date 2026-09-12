using System;
using System.Collections.Generic;
using Kompetenzhaus.Content;
using Kompetenzhaus.Quiz;
using Kompetenzhaus.State;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Kompetenzhaus.WorldSupport
{
    [DisallowMultipleComponent]
    public sealed class WorldAudio : MonoBehaviour
    {
        public AudioClip uiHover, uiSelect, uiUnavailable, buildPlace, questComplete, milestone;
        public AudioClip footstepA, footstepB, courtyardAmbience;
        public bool ambienceEnabled = true;
        public bool hoverEnabled;
        [Range(0f, 1f)] public float ambienceVolume = 0.20f;
        [Range(0f, 1f)] public float effectsVolume = 0.70f;
        [Range(0f, 1f)] public float footstepsVolume = 0.35f;
        public float stepDistance = 1.65f;
        public event Action<string> CuePlayed;
        public bool HasUserInteraction { get; private set; }

        private GameBootstrap game;
        private AudioSource ambience, effects, footsteps;
        private bool subscribed, hadPlayerPosition, alternateStep;
        private Vector3 lastPlayerPosition;
        private float walkedDistance, lastHoverTime = -1f;
        private int knownQuestCount;
        private QuizPhase lastQuizPhase;
        private readonly HashSet<string> knownBuiltModules = new HashSet<string>(StringComparer.Ordinal);

        public void Initialize(GameBootstrap application)
        {
            Unsubscribe();
            game = application;
            EnsureSources();
            knownBuiltModules.Clear();
            if (game?.Progression != null)
            {
                knownBuiltModules.UnionWith(game.Progression.Data.placedModuleIds);
                knownQuestCount = game.Progression.Data.completedQuestIds.Count;
                lastQuizPhase = game.Quiz.Phase;
                Subscribe();
            }
            hadPlayerPosition = false;
        }

        // The browser shell calls this from a genuine pointer/key gesture. It never runs on load.
        public void NotifyUserInteraction()
        {
            HasUserInteraction = true;
            EnsureSources();
        }

        public void PlayUiHover()
        {
            if (!hoverEnabled || Time.unscaledTime - lastHoverTime < 0.12f) return;
            lastHoverTime = Time.unscaledTime;
            Play(uiHover, "ui-hover", 0.55f);
        }
        public void PlayUiSelect() => Play(uiSelect, "ui-select");
        public void PlayUiUnavailable() => Play(uiUnavailable, "ui-unavailable", 0.70f);

        private void EnsureSources()
        {
            if (ambience == null) ambience = CreateSource("Courtyard ambience", true);
            if (effects == null) effects = CreateSource("Learning and interface cues", false);
            if (footsteps == null) footsteps = CreateSource("Player footsteps", false);
        }

        private AudioSource CreateSource(string objectName, bool loop)
        {
            var child = new GameObject(objectName);
            child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0f;
            source.volume = loop ? 0f : 1f;
            source.priority = loop ? 200 : 128;
            return source;
        }

        private void OnEnable() { if (game?.Progression != null) Subscribe(); }
        private void OnDisable()
        {
            Unsubscribe();
            if (ambience != null) ambience.Stop();
            if (effects != null) effects.Stop();
            if (footsteps != null) footsteps.Stop();
            hadPlayerPosition = false;
        }

        private void Subscribe()
        {
            if (subscribed || game?.Progression == null) return;
            game.Progression.ModulePlaced += OnModulePlaced;
            game.Progression.Changed += OnProgressChanged;
            game.Quiz.Changed += OnQuizChanged;
            subscribed = true;
        }
        private void Unsubscribe()
        {
            if (!subscribed || game == null) return;
            if (game.Progression != null)
            {
                game.Progression.ModulePlaced -= OnModulePlaced;
                game.Progression.Changed -= OnProgressChanged;
            }
            if (game.Quiz != null) game.Quiz.Changed -= OnQuizChanged;
            subscribed = false;
        }

        private void Update()
        {
            if (game?.Progression == null) return;
            if (!HasUserInteraction && ((Mouse.current?.leftButton.wasPressedThisFrame ?? false) ||
                (Keyboard.current?.anyKey.wasPressedThisFrame ?? false))) NotifyUserInteraction();
            var audible = HasUserInteraction && game.Progression.Data.accessibility.masterVolume > 0f;
            var target = audible && ambienceEnabled && courtyardAmbience != null ? ambienceVolume : 0f;
            if (ambience != null)
            {
                if (target > 0f && !ambience.isPlaying)
                {
                    ambience.clip = courtyardAmbience;
                    ambience.Play();
                }
                ambience.volume = Mathf.MoveTowards(ambience.volume, target, Time.unscaledDeltaTime * 0.5f);
                if (target <= 0f && ambience.volume <= 0.001f) ambience.Stop();
            }
        }

        private void LateUpdate()
        {
            var camera = game?.cameraController;
            var player = camera?.player;
            if (player == null) { hadPlayerPosition = false; return; }
            var current = player.transform.position;
            var distance = hadPlayerPosition ? Vector3.ProjectOnPlane(current - lastPlayerPosition, Vector3.up).magnitude : 0f;
            lastPlayerPosition = current;
            hadPlayerPosition = true;
            if (!HasUserInteraction || !player.enabled || camera.Mode != ViewMode.FirstPerson || camera.InputBlocked ||
                !player.isGrounded || distance < 0.002f || distance > 1f)
            {
                walkedDistance = 0f;
                return;
            }
            walkedDistance += distance;
            if (walkedDistance < Mathf.Max(0.5f, stepDistance)) return;
            walkedDistance %= Mathf.Max(0.5f, stepDistance);
            var clip = alternateStep ? footstepB : footstepA;
            alternateStep = !alternateStep;
            if (clip != null && game.Progression.Data.accessibility.masterVolume > 0f)
            {
                footsteps.PlayOneShot(clip, footstepsVolume);
                CuePlayed?.Invoke("footstep");
            }
        }

        private void OnModulePlaced(ModuleDefinition module)
        {
            var firstBuild = knownBuiltModules.Add(module.id);
            if (firstBuild && (module.id == "BA" || module.id == "MA")) Play(milestone, "milestone");
            else Play(buildPlace, "build-place");
        }
        private void OnProgressChanged()
        {
            var count = game.Progression.Data.completedQuestIds.Count;
            if (count > knownQuestCount) Play(questComplete, "quest-complete");
            knownQuestCount = count;
        }
        private void OnQuizChanged()
        {
            var phase = game.Quiz.Phase;
            if (phase == QuizPhase.Feedback && lastQuizPhase != QuizPhase.Feedback)
            {
                if (game.Quiz.LastAnswerCorrect) PlayUiSelect(); else PlayUiUnavailable();
            }
            else if (phase == QuizPhase.Question && lastQuizPhase == QuizPhase.Idle) PlayUiSelect();
            else if (phase == QuizPhase.Complete && !string.IsNullOrEmpty(game.ActiveModuleId)) Play(milestone, "quiz-complete", 0.70f);
            lastQuizPhase = phase;
        }
        private void Play(AudioClip clip, string cueId, float gain = 1f)
        {
            if (!isActiveAndEnabled || !HasUserInteraction || clip == null || game?.Progression == null ||
                game.Progression.Data.accessibility.masterVolume <= 0f) return;
            effects.PlayOneShot(clip, effectsVolume * gain);
            CuePlayed?.Invoke(cueId);
        }
    }
}
