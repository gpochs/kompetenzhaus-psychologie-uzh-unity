using System;
using System.Collections.Generic;
using System.Linq;
using Kompetenzhaus.Content;
using Kompetenzhaus.Presentation;
using Kompetenzhaus.State;
using Kompetenzhaus.WorldSupport;
using Kompetenzhaus.Gameplay;
using UnityEngine;

namespace Kompetenzhaus.World
{
    // The scene contains a reviewed kit, never a fixed baked house. Every saved
    // tile, room, opening, module and decoration is reconstructed from state.
    public sealed class GameWorld : MonoBehaviour
    {
        public const float Bay = 4f, Storey = 3.2f;
        public GameBootstrap game;
        public WorldAssetLibrary assets;
        public Light sun;
        public int visibleFloor = 3;
        public bool showRoofs;
        private Transform buildings, campus;
        private readonly List<(GameObject gameObject, int floor, bool roof)> levels = new();
        private readonly Dictionary<string, Material> variants = new();
        private readonly List<GameObject> worldLabels = new();
        private string renderedArchitecture = "", renderedLanguage = "";
        private bool initialized;
        private ViewMode lastMode;
        private WorldFeedback feedback;

        public static Vector3 HouseOrigin(string id) => new Vector3(id == "msc" ? 22f : -22f, .03f, 0f);
        public static Vector3 TileCentre(GridTile tile) => new Vector3((tile.x + .5f) * Bay, tile.floor * Storey, (tile.z + .5f) * Bay);

        private void Start()
        {
            if (game == null || game.Catalog == null || assets == null) return;
            var audio = GetComponent<WorldAudio>(); if (audio != null) audio.Initialize(game);
            var interactor = GetComponent<WorldInteractor>(); if (interactor != null) interactor.Initialize(game, audio);
            feedback = GetComponent<WorldFeedback>() ?? gameObject.AddComponent<WorldFeedback>();
            feedback.Initialize(game, interactor, assets.labelFont, assets.ivory);
            game.Architecture.Changed += RebuildIfChanged;
            game.Progression.Changed += RefreshState;
            game.HouseFocusRequested += FocusHouse;
            BuildCampus();
            RebuildIfChanged();
            initialized = true;
            if (game.cameraController != null)
            {
                game.cameraController.worldReady = true;
                game.cameraController.SetMode(game.Progression.Data.viewMode);
                FocusHouse(game.Progression.Data.selectedHouseId);
            }
        }

        public void SetArchitectureView(int floor, bool roofs)
        {
            visibleFloor = Mathf.Clamp(floor, 0, 3); showRoofs = roofs; ApplyVisibility();
        }

        public void FocusHouse(string id)
        {
            if (game.cameraController == null) return;
            var all = id != "bsc" && id != "msc";
            game.cameraController.FitBirdView(new Bounds(all ? new Vector3(0, 2, 1) : HouseOrigin(id) + Vector3.up * 2,
                all ? new Vector3(80, 12, 40) : new Vector3(34, 12, 34)));
        }

        private void RefreshState()
        {
            if (game.Progression.Data.language != renderedLanguage)
            {
                if (campus != null) { campus.gameObject.SetActive(false); Destroy(campus.gameObject); }
                BuildCampus(); renderedArchitecture = "";
            }
            RebuildIfChanged();
        }

        private void RebuildIfChanged()
        {
            var json = JsonUtility.ToJson(game.Architecture.State);
            if (json == renderedArchitecture) return;
            renderedArchitecture = json;
            if (buildings != null) { buildings.gameObject.SetActive(false); Destroy(buildings.gameObject); }
            buildings = new GameObject("Player architecture").transform; buildings.SetParent(transform, false);
            levels.Clear();
            foreach (var house in game.Architecture.State.houses) BuildHouse(house);
            feedback?.NotifyArchitectureRebuilt();
            ApplyEnvironment(); ApplyVisibility();
        }

        private void BuildHouse(HouseDesign house)
        {
            var root = new GameObject(house.houseId == "msc" ? "Master house" : "Bachelor house").transform;
            root.SetParent(buildings, false); root.localPosition = HouseOrigin(house.houseId);
            var tiles = house.footprintTiles.ToDictionary(t => t.Key);
            var circulation = new ArchitectureCirculationPlan(house);
            var stairs = new Dictionary<string, RoomConnection>();
            foreach (var connection in house.connections)
            {
                if (connection.kind == "stair")
                {
                    var upper = connection.fromTile.floor > connection.toTile.floor ? connection.fromTile : connection.toTile;
                    stairs[upper.Key] = connection;
                }
            }
            foreach (var tile in house.footprintTiles)
            {
                var level = new GameObject("Bay " + tile.Key).transform; level.SetParent(root, false);
                levels.Add((level.gameObject, tile.floor, false));
                var centre = TileCentre(tile);
                if (stairs.ContainsKey(tile.Key))
                {
                    circulation.TryStairYaw(stairs[tile.Key], out var stairYaw);
                    var stairRotation = Quaternion.Euler(0, stairYaw, 0);
                    // Real open stairwell: two side strips leave 1.7 m clear.
                    foreach (var side in new[] { -1f, 1f })
                    {
                        var strip = Cube("Stairwell landing", level, centre + stairRotation * new Vector3(side * 1.425f, -.13f, 0),
                            new Vector3(1.15f, .26f, 4), assets.path, true);
                        strip.transform.localRotation = stairRotation;
                    }
                    var landing = Cube("Upper stair landing", level, centre + stairRotation * new Vector3(0, -.13f, 1.6f),
                        new Vector3(4, .26f, .8f), assets.path, true);
                    landing.transform.localRotation = stairRotation;
                }
                else Spawn("floor-tile", level, centre, 0, Vector3.one, house, true);
                foreach (var direction in Directions)
                {
                    var neighbour = new GridTile(tile.x + direction.x, tile.z + direction.y, tile.floor);
                    var key = ArchitectureCirculationPlan.EdgeKey(tile, neighbour);
                    var model = circulation.BoundaryModel(tile, neighbour);
                    if (model == null) continue;
                    var at = centre + new Vector3(direction.x * 2, 0, direction.y * 2);
                    var wall = Spawn(model, level, at, direction.x != 0 ? 90 : 0, Vector3.one, house, true);
                    wall.name = "Boundary " + key;
                    if (!circulation.HasRoom(neighbour) && model != "wall-door" && house.facadeStyle == "glass") MakeGlassFacade(wall, level, at, direction.x != 0 ? 90 : 0);
                }
                if (circulation.HasRoom(tile) && !tiles.ContainsKey(new GridTile(tile.x, tile.z, tile.floor + 1).Key))
                {
                    var roof = Spawn(house.roofStyle == "gabled" ? "roof-gabled" : "roof-flat", root,
                        centre + Vector3.up * Storey, 0, Vector3.one, house, true);
                    if (house.roofStyle == "terrace")
                    {
                        ReplaceNamedMaterial(roof, "Sage", "Terrace timber", new Color(.62f, .45f, .28f), false);
                        var front = new GridTile(tile.x, tile.z - 1, tile.floor);
                        if (!tiles.ContainsKey(front.Key))
                        {
                            var balcony = Spawn("balcony", root, centre + new Vector3(0, Storey + .3f, -2), 0, Vector3.one, house, true);
                            levels.Add((balcony, tile.floor, true));
                        }
                    }
                    levels.Add((roof, tile.floor, true));
                }
            }
            foreach (var connection in house.connections.Where(c => c.kind == "stair"))
            {
                var lower = connection.fromTile.floor < connection.toTile.floor ? connection.fromTile : connection.toTile;
                var upper = connection.fromTile.floor > connection.toTile.floor ? connection.fromTile : connection.toTile;
                var position = TileCentre(upper); position.y = lower.floor * Storey;
                circulation.TryStairYaw(connection, out var stairYaw);
                var stairsModel = Spawn("staircase", root, position, stairYaw, Vector3.one, house, true);
                levels.Add((stairsModel, lower.floor, false));
            }
            foreach (var room in house.rooms)
            {
                if (room.tiles.Count == 0) continue;
                var centre = room.tiles.Select(TileCentre).Aggregate(Vector3.zero, (a, b) => a + b) / room.tiles.Count;
                var label = Label(room.name, root, centre + new Vector3(0, .12f, -.7f), 1.05f);
                levels.Add((label, room.tiles[0].floor, false));
            }
            foreach (var placement in house.modulePlacements)
            {
                var module = game.Progression.GetContentForSlot(placement.moduleId);
                var at = new Vector3((placement.gridX + .5f) * Bay, placement.floor * Storey,
                    (placement.gridZ + .5f) * Bay);
                var model = ModuleModel(module);
                var station = Spawn(model, root, at, placement.quarterTurns * 90, Vector3.one * .85f, house, true);
                station.name = "Module " + placement.moduleId;
                var target = station.AddComponent<WorldInteraction>(); target.moduleId = placement.moduleId;
                feedback?.RegisterTarget(target);
                levels.Add((station, placement.floor, false));
                var label = Label(module.shortTitle.Get(game.Progression.Data.language), root, at + new Vector3(0, 1.2f, 0), .75f);
                levels.Add((label, placement.floor, false));
            }
            foreach (var decoration in house.decorations)
            {
                var at = new Vector3(decoration.x, decoration.floor * Storey, decoration.z);
                var model = CosmeticModel(decoration.cosmeticId);
                var item = Spawn(model, root, at, decoration.rotationDegrees, Vector3.one * decoration.scale, house, true);
                item.name = "Decoration " + decoration.instanceId;
                levels.Add((item, decoration.floor, false));
            }
        }

        private void BuildCampus()
        {
            renderedLanguage = game.Progression.Data.language;
            campus = new GameObject("Shared campus").transform; campus.SetParent(transform, false);
            Cube("Landscape", campus, new Vector3(0, -.42f, 0), new Vector3(96, .7f, 70), assets.ground, true);
            Cube("Campus promenade", campus, new Vector3(0, -.04f, -18), new Vector3(82, .1f, 4.2f), assets.path, true);
            Cube("Connecting courtyard", campus, new Vector3(0, -.03f, 0), new Vector3(11, .12f, 36), assets.path, true);
            foreach (var id in new[] { "bsc", "msc" })
            {
                var origin = HouseOrigin(id);
                Cube(id + " building lot", campus, origin + new Vector3(0, -.12f, 0), new Vector3(32.3f, .16f, 32.3f), assets.ivory, true);
                // Thin inlaid lines show scale while the initial lot is empty.
                for (var n = -4; n <= 4; n++)
                {
                    Cube("Lot inlay", campus, origin + new Vector3(n * 4, -.025f, 0), new Vector3(.018f, .015f, 32), assets.path, false);
                    Cube("Lot inlay", campus, origin + new Vector3(0, -.025f, n * 4), new Vector3(32, .015f, .018f), assets.path, false);
                }
                Label(id == "bsc" ? "BACHELOR" : "MASTER", campus, origin + new Vector3(0, .12f, -16.9f), 2.1f);
            }
            for (var i = 0; i < 14; i++)
            {
                var x = -43 + i * 6.6f;
                Spawn("courtyard-tree", campus, new Vector3(x, 0, 22), i * 31, Vector3.one * (1.25f + (i % 3) * .15f), null, true);
                if (i % 2 == 0) Spawn("courtyard-tree", campus, new Vector3(x, 0, -25), i * 17, Vector3.one, null, true);
            }
            Spawn("evidence-table", campus, new Vector3(0, .04f, 7), 0, Vector3.one * 1.1f, null, true);
            Spawn("discussion-sofa", campus, new Vector3(0, .04f, 10), 180, Vector3.one * 1.1f, null, true);
            Spawn("wall-door", campus, new Vector3(0, .04f, 14), 0, Vector3.one, null, true);
            Label(renderedLanguage == "en" ? "EVIDENCE WORKSHOP" : "EVIDENZWERKSTATT", campus, new Vector3(0, 3.5f, 14), 1.4f);
            var guide = Spawn("student-guide", campus, new Vector3(2.8f, .06f, 7), 0, Vector3.one, null, false);
            guide.name = "Student guide";
            var guideRoute = new GameObject("Guide route").transform; guideRoute.SetParent(campus, false);
            guideRoute.localPosition = guide.transform.localPosition; guide.transform.SetParent(guideRoute, true);
            guide.transform.localPosition = Vector3.zero;
            var motion = guideRoute.gameObject.AddComponent<GuideMotion>(); motion.visualRoot = guide.transform;
            motion.localWaypoints = new[] { new Vector3(2.8f, .06f, 7), new Vector3(2.8f, .06f, 11), new Vector3(-2.8f, .06f, 11), new Vector3(-2.8f, .06f, 7) };
            motion.Initialize(game);
            foreach (var pair in game.Catalog.Document.quests.Select((q, index) => (q, index)))
            {
                var side = pair.index % 2 == 0 ? -1 : 1;
                var at = new Vector3(side * 3.25f, .06f, -13 + (pair.index / 2) * 4.2f);
                var station = Spawn(JourneyDirector.StationAssetFor(pair.q), campus, at, side < 0 ? 90 : -90, Vector3.one * .75f, null, true);
                station.name = "Quest " + pair.q.id;
                var target = station.AddComponent<WorldInteraction>(); target.questId = pair.q.id;
                feedback?.RegisterTarget(target);
                Label((pair.index + 1).ToString("00") + "  " + pair.q.title.Get(renderedLanguage), campus,
                    at + Vector3.up * 1.3f, .53f);
            }
        }

        private void ApplyEnvironment()
        {
            var state = game.Architecture.State;
            if (sun != null)
            {
                var height = Mathf.Sin((state.timeOfDay - 6) / 12f * Mathf.PI);
                sun.transform.rotation = Quaternion.Euler(Mathf.Clamp(height * 65, 8, 70), state.timeOfDay * 12, 0);
                sun.intensity = state.weather == "rain" ? .65f : Mathf.Lerp(.45f, 1.45f, Mathf.Max(0, height));
                sun.color = Color.Lerp(new Color(1, .73f, .47f), Color.white, Mathf.Clamp01(height * 2));
            }
            RenderSettings.ambientLight = state.weather == "rain" ? new Color(.55f, .64f, .7f) : new Color(.75f, .79f, .81f);
        }

        private void Update()
        {
            if (!initialized) return;
            var camera = game.cameraController.Mode == ViewMode.FirstPerson ? game.cameraController.firstPersonCamera : game.cameraController.birdCamera;
            if (lastMode != game.cameraController.Mode) { lastMode = game.cameraController.Mode; ApplyVisibility(); }
            var player = game.cameraController.player;
            if (player != null && player.enabled && (player.transform.position.y < -3 ||
                Mathf.Abs(player.transform.position.x) > 47 || Mathf.Abs(player.transform.position.z) > 34))
            {
                player.enabled = false; player.transform.position = game.cameraController.entryPoint; player.enabled = true;
            }
            // Labels rotate around their own anchor so their letters never mirror.
            for (var i = worldLabels.Count - 1; i >= 0; i--)
            {
                var label = worldLabels[i]; if (label == null) { worldLabels.RemoveAt(i); continue; }
                if (camera != null && label.activeInHierarchy) label.transform.rotation = camera.transform.rotation;
            }
        }

        private void ApplyVisibility()
        {
            var firstPerson = game.cameraController != null && game.cameraController.Mode == ViewMode.FirstPerson;
            foreach (var item in levels) if (item.gameObject != null)
                item.gameObject.SetActive(firstPerson || (item.floor <= visibleFloor && (!item.roof || showRoofs)));
        }

        private GameObject Spawn(string id, Transform parent, Vector3 position, float yaw, Vector3 scale, HouseDesign house, bool collider)
        {
            var instance = Instantiate(assets.Get(id), parent); instance.name = id;
            instance.transform.localPosition = position; instance.transform.localRotation = Quaternion.Euler(0, yaw, 0); instance.transform.localScale = scale;
            foreach (var renderer in instance.GetComponentsInChildren<MeshRenderer>())
            {
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++)
                {
                    var original = materials[i]; if (original == null || house == null) continue;
                    var color = original.name.Contains("Stone") ? house.wallColor : original.name.Contains("Navy") ? house.accentColor : null;
                    if (original.name.Contains("Oak")) color = house.theme == "light-mineral" ? "#AAAFA5" : house.theme == "terracotta" ? "#9E5C40" : "#B88B55";
                    if (color == null || !ColorUtility.TryParseHtmlString(color, out var tint)) continue;
                    var key = original.name + color;
                    if (!variants.TryGetValue(key, out var variant)) { variant = new Material(original); variant.color = tint; variants[key] = variant; }
                    materials[i] = variant;
                }
                renderer.sharedMaterials = materials;
            }
            if (collider) foreach (var filter in instance.GetComponentsInChildren<MeshFilter>())
            {
                if (filter.sharedMesh == null) continue;
                var meshCollider = filter.gameObject.AddComponent<MeshCollider>(); meshCollider.sharedMesh = filter.sharedMesh;
            }
            return instance;
        }

        private void MakeGlassFacade(GameObject wall, Transform parent, Vector3 at, float yaw)
        {
            ReplaceNamedMaterial(wall, "Stone", "Invisible facade infill", new Color(.8f, .9f, .93f, 0), true);
            ReplaceNamedMaterial(wall, "Glass", "Invisible old panes", new Color(.8f, .9f, .93f, 0), true);
            var panel = Cube("Full-height glazing", parent, at + Vector3.up * 1.6f, new Vector3(3.94f, 3.16f, .025f), assets.ivory, true);
            panel.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            panel.GetComponent<Renderer>().sharedMaterial = RuntimeMaterial("Clear facade glass", assets.ivory, new Color(.51f, .73f, .78f, .25f), true);
        }

        private void ReplaceNamedMaterial(GameObject item, string match, string key, Color tint, bool transparent)
        {
            foreach (var renderer in item.GetComponentsInChildren<MeshRenderer>())
            {
                var materials = renderer.sharedMaterials;
                for (var i = 0; i < materials.Length; i++) if (materials[i] != null && materials[i].name.Contains(match))
                    materials[i] = RuntimeMaterial(key, materials[i], tint, transparent);
                renderer.sharedMaterials = materials;
            }
        }

        private Material RuntimeMaterial(string key, Material source, Color tint, bool transparent)
        {
            if (variants.TryGetValue(key, out var material)) return material;
            material = new Material(source); material.name = key; material.SetColor("_BaseColor", tint);
            if (transparent)
            {
                material.SetFloat("_Surface", 1); material.SetFloat("_ZWrite", 0);
                material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                material.SetFloat("_Cull", 0); material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                material.SetShaderPassEnabled("ShadowCaster", false);
                material.SetOverrideTag("RenderType", "Transparent"); material.renderQueue = 3000;
            }
            variants[key] = material; return material;
        }

        private GameObject Cube(string name, Transform parent, Vector3 at, Vector3 scale, Material material, bool collider)
        {
            var item = GameObject.CreatePrimitive(PrimitiveType.Cube); item.name = name; item.transform.SetParent(parent, false);
            item.transform.localPosition = at; item.transform.localScale = scale; item.GetComponent<Renderer>().sharedMaterial = material;
            if (!collider) Destroy(item.GetComponent<Collider>());
            return item;
        }

        private GameObject Label(string text, Transform parent, Vector3 at, float size)
        {
            var item = new GameObject("Label " + text); item.transform.SetParent(parent, false); item.transform.localPosition = at;
            var label = item.AddComponent<TextMesh>(); label.text = Wrap(text, 27); label.font = assets.labelFont;
            label.fontSize = 48; label.characterSize = .09f * size; label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center; label.color = new Color(.08f, .16f, .22f);
            if (assets.labelFont != null) item.GetComponent<MeshRenderer>().sharedMaterial = assets.labelFont.material;
            worldLabels.Add(item); return item;
        }

        private static string Wrap(string text, int width)
        {
            var lines = new List<string>(); var line = "";
            foreach (var word in (text ?? "").Split(' '))
            {
                if (line.Length + word.Length > width) { lines.Add(line); line = ""; }
                line += (line.Length > 0 ? " " : "") + word;
            }
            lines.Add(line); return string.Join("\n", lines);
        }
        private static readonly Vector2Int[] Directions = { new(0, -1), new(1, 0), new(0, 1), new(-1, 0) };
        private static string ModuleModel(ModuleDefinition module) => module.competencyIds.Any(c => c == "Fa2" || c == "Fa3" || c == "Fa4") ? "research-desk" : module.stage >= 3 ? "evidence-table" : "discussion-sofa";
        private static string CosmeticModel(string id)
        {
            if (new[] { "bookshelf", "lamp", "noticeboard", "chair", "bench" }.Contains(id)) return id;
            if (id == "desk") return "research-desk";
            if (id.Contains("tree") || id.Contains("plant") || id.Contains("garden")) return "courtyard-tree";
            if (id.Contains("table") || id.Contains("evidence")) return "evidence-table";
            if (id.Contains("desk") || id.Contains("lab")) return "research-desk";
            if (id.Contains("acoustic") || id.Contains("attention")) return "acoustic-divider";
            if (id.Contains("poster") || id.Contains("gallery")) return "research-poster";
            return "achievement-display";
        }

        private void OnDestroy()
        {
            if (game?.Architecture != null) game.Architecture.Changed -= RebuildIfChanged;
            if (game?.Progression != null) game.Progression.Changed -= RefreshState;
            if (game != null) game.HouseFocusRequested -= FocusHouse;
            foreach (var material in variants.Values) if (material != null) Destroy(material);
        }
    }

}
