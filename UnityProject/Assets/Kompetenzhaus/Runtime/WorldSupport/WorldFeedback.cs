using System;
using System.Collections.Generic;
using System.Linq;
using Kompetenzhaus.Gameplay;
using Kompetenzhaus.State;
using Kompetenzhaus.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace Kompetenzhaus.WorldSupport
{
    // Lightweight, non-colliding feedback. Architecture and achievement state are
    // never animated or changed, so a visual confirmation cannot move the player.
    [DisallowMultipleComponent]
    public sealed class WorldFeedback : MonoBehaviour
    {
        public Font labelFont;
        public Material markerMaterial;
        private GameBootstrap game;
        private WorldInteractor interactor;
        private GameWorld world;
        private readonly List<TargetMarker> targets = new();
        private readonly List<PlacementPulse> pulses = new();
        private readonly Dictionary<string, Material> materials = new();
        private HashSet<string> knownTiles;
        private Dictionary<string, string> knownPlacements;
        private string recommendedQuestId;

        public void Initialize(GameBootstrap application, WorldInteractor worldInteractor, Font font = null, Material material = null)
        {
            Unsubscribe();
            game = application; interactor = worldInteractor;
            world = GetComponent<GameWorld>();
            if (font != null) labelFont = font;
            if (material != null) markerMaterial = material;
            if (game?.Progression == null) return;
            game.Progression.Changed += Refresh;
            if (interactor != null) interactor.FocusChanged += OnFocusChanged;
            Refresh();
        }

        public void RegisterTarget(WorldInteraction target)
        {
            if (target == null || game?.Progression == null || targets.Any(item => item.target == target)) return;
            targets.RemoveAll(item => item.target == null);
            var renderers = target.GetComponentsInChildren<Renderer>();
            var bounds = new Bounds(target.transform.position + Vector3.up * .5f, Vector3.one);
            if (renderers.Length > 0)
            {
                bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
            }
            var holder = new GameObject("Interaction status").transform;
            holder.SetParent(target.transform, true);
            holder.position = new Vector3(bounds.center.x, bounds.max.y + .27f, bounds.center.z);
            var text = holder.gameObject.AddComponent<TextMesh>();
            text.font = labelFont; text.fontSize = 48; text.characterSize = .047f;
            text.anchor = TextAnchor.LowerCenter; text.alignment = TextAlignment.Center;
            if (labelFont != null) holder.GetComponent<MeshRenderer>().sharedMaterial = labelFont.material;
            var ringObject = new GameObject("Interaction focus"); ringObject.transform.SetParent(target.transform, true);
            ringObject.transform.position = new Vector3(bounds.center.x, bounds.min.y + .035f, bounds.center.z);
            var ring = ringObject.AddComponent<LineRenderer>();
            ConfigureLine(ring, .04f);
            var marker = new TargetMarker { target = target, text = text, ring = ring,
                radius = Mathf.Clamp(Mathf.Max(bounds.extents.x, bounds.extents.z) + .14f, .48f, 1.6f) };
            targets.Add(marker);
            RefreshTarget(marker);
        }

        // Call after all architecture targets are registered. First load is quiet.
        public void NotifyArchitectureRebuilt()
        {
            if (game?.Architecture?.State == null) return;
            var tiles = new HashSet<string>();
            var placements = new Dictionary<string, string>();
            var confirmations = new List<Vector3>();
            foreach (var house in game.Architecture.State.houses)
            {
                var origin = GameWorld.HouseOrigin(house.houseId);
                foreach (var tile in house.footprintTiles)
                {
                    var key = house.houseId + ":" + tile.Key; tiles.Add(key);
                    if (knownTiles != null && !knownTiles.Contains(key)) confirmations.Add(origin + GameWorld.TileCentre(tile));
                }
                foreach (var item in house.modulePlacements)
                {
                    var position = house.houseId + ":" + item.gridX + ":" + item.gridZ + ":" + item.floor + ":" + item.quarterTurns + ":" + item.width + ":" + item.depth;
                    placements[item.moduleId] = position;
                    if (knownPlacements != null && (!knownPlacements.TryGetValue(item.moduleId, out var oldPosition) || oldPosition != position))
                        confirmations.Add(origin + new Vector3((item.gridX + .5f) * GameWorld.Bay, item.floor * GameWorld.Storey, (item.gridZ + .5f) * GameWorld.Bay));
                }
            }
            knownTiles = tiles; knownPlacements = placements;
            // Cap a large preset to twelve confirmations, and never stack unbounded effects.
            foreach (var point in confirmations.Distinct().Take(12)) ShowPlacementConfirmation(point);
            Refresh();
        }

        private void ShowPlacementConfirmation(Vector3 point)
        {
            while (pulses.Count >= 12)
            {
                if (pulses[0].line != null) Destroy(pulses[0].line.gameObject);
                pulses.RemoveAt(0);
            }
            var item = new GameObject("Placement confirmation"); item.transform.SetParent(transform, false);
            item.transform.position = point + Vector3.up * .08f;
            var line = item.AddComponent<LineRenderer>(); ConfigureLine(line, .07f);
            line.sharedMaterial = MaterialFor("confirmation", new Color(.22f, .48f, .36f));
            var pulse = new PlacementPulse { line = line, started = Time.unscaledTime, floor = Mathf.RoundToInt(point.y / GameWorld.Storey) };
            pulses.Add(pulse);
            SetRing(line, game.Progression.Data.accessibility.reducedMotion ? 1.3f : .7f);
        }

        private void Refresh()
        {
            if (game?.Progression == null) return;
            recommendedQuestId = JourneyDirector.Create(game.Catalog, game.Progression).questId;
            for (var i = targets.Count - 1; i >= 0; i--)
            {
                if (targets[i].target == null) targets.RemoveAt(i); else RefreshTarget(targets[i]);
            }
        }

        private void OnFocusChanged(WorldInteraction _) => Refresh();

        private void RefreshTarget(TargetMarker item)
        {
            var english = game.Progression.Data.language == "en";
            var status = item.target.GetStatus(game, recommendedQuestId ?? "");
            var focused = interactor != null && interactor.FocusedTarget == item.target;
            var text = status switch
            {
                WorldTargetStatus.Recommended => english ? ">> NEXT ASSIGNMENT" : ">> NÄCHSTER AUFTRAG",
                WorldTargetStatus.Completed => english ? "OK · COMPLETED" : "OK · ABGESCHLOSSEN",
                WorldTargetStatus.Unavailable => english ? "... PREREQUISITE" : "... VORAUSSETZUNG",
                _ => string.IsNullOrEmpty(item.target.questId) ? (english ? "+ PRACTISE" : "+ ÜBEN")
                    : (english ? "+ SIDE ASSIGNMENT" : "+ NEBENAUFTRAG")
            };
            // Main quests available out of order in sandbox remain main quests.
            if (status == WorldTargetStatus.Available && !string.IsNullOrEmpty(item.target.questId) &&
                Array.Find(game.Catalog.Document.quests, quest => quest.id == item.target.questId)?.type == "main")
                text = english ? "+ MAIN ASSIGNMENT" : "+ HAUPTAUFTRAG";
            item.text.text = text;
            item.text.color = game.Progression.Data.accessibility.highContrast ? Color.black : new Color(.08f, .16f, .22f);
            item.text.characterSize = .047f * game.Progression.Data.accessibility.textScale;
            var color = focused ? new Color(.78f, .4f, .12f) : status == WorldTargetStatus.Recommended ? new Color(.22f, .48f, .36f) : new Color(.18f, .28f, .36f);
            item.ring.sharedMaterial = MaterialFor(focused ? "focus" : status.ToString(), color);
            item.ring.enabled = focused || status == WorldTargetStatus.Recommended;
            SetRing(item.ring, item.radius);
        }

        private void LateUpdate()
        {
            var camera = game?.cameraController;
            if (camera == null) return;
            var view = camera.Mode == ViewMode.FirstPerson ? camera.firstPersonCamera : camera.birdCamera;
            targets.RemoveAll(item => item.target == null);
            foreach (var marker in targets)
            {
                if (marker.target == null || marker.text == null || view == null) continue;
                marker.text.transform.rotation = view.transform.rotation;
                // Avoid a wall of labels at first-person eye level; interaction hints
                // remain in the semantic HTML shell and are not color-dependent.
                var nearby = Vector3.Distance(view.transform.position, marker.target.transform.position) < 12f;
                marker.text.gameObject.SetActive(camera.Mode == ViewMode.BirdView || nearby);
            }
            for (var i = pulses.Count - 1; i >= 0; i--)
            {
                var pulse = pulses[i];
                if (pulse.line == null) { pulses.RemoveAt(i); continue; }
                var elapsed = Time.unscaledTime - pulse.started;
                if (elapsed >= .7f) { Destroy(pulse.line.gameObject); pulses.RemoveAt(i); continue; }
                pulse.line.enabled = camera.Mode == ViewMode.FirstPerson || world == null || pulse.floor <= world.visibleFloor;
                var reducedMotion = game.Progression.Data.accessibility.reducedMotion;
                // Reduced motion retains a static confirmation for the same duration.
                SetRing(pulse.line, reducedMotion ? 1.3f : Mathf.Lerp(.7f, 1.6f, Smooth(elapsed / .7f)));
                pulse.line.widthMultiplier = reducedMotion ? .07f : Mathf.Lerp(.09f, .025f, elapsed / .7f);
            }
        }

        private static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }
        private static void ConfigureLine(LineRenderer line, float width)
        {
            line.useWorldSpace = false; line.loop = true; line.positionCount = 33;
            line.widthMultiplier = width; line.numCornerVertices = 2;
            line.shadowCastingMode = ShadowCastingMode.Off; line.receiveShadows = false;
        }
        private static void SetRing(LineRenderer line, float radius)
        {
            for (var i = 0; i < line.positionCount; i++)
            {
                var angle = i * Mathf.PI * 2f / line.positionCount;
                line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
        }
        private Material MaterialFor(string key, Color color)
        {
            if (materials.TryGetValue(key, out var existing)) return existing;
            // The scene passes a serialized material, preventing shader stripping surprises.
            if (markerMaterial == null) return null;
            var material = new Material(markerMaterial) { name = "Feedback " + key, color = color };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            materials[key] = material;
            return material;
        }
        private void Unsubscribe()
        {
            if (game?.Progression != null) game.Progression.Changed -= Refresh;
            if (interactor != null) interactor.FocusChanged -= OnFocusChanged;
        }
        private void OnDestroy()
        {
            Unsubscribe();
            foreach (var material in materials.Values) if (material != null) Destroy(material);
        }
        private sealed class TargetMarker { public WorldInteraction target; public TextMesh text; public LineRenderer ring; public float radius; }
        private sealed class PlacementPulse { public LineRenderer line; public float started; public int floor; }
    }
}
