using System.Collections.Generic;
using System.IO;
using Coldfall.Player;
using Coldfall.Sim;
using Coldfall.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Coldfall.Editor.Setup
{
    /// <summary>
    /// Builds the M0 greybox scene from primitives.
    /// <para>
    /// M0 exists to answer one question: does moving and hitting things feel good. That needs a
    /// space with something to walk on, something to climb, something to vault, and an interior
    /// with the two entries and the chokepoint that docs/05-world-and-map.md, 5.4 requires of
    /// every building. It does not need art, so the whole scene is boxes.
    /// </para>
    /// <para>
    /// Generated rather than hand-authored so it can be rebuilt after a controller change without
    /// re-dragging anything, and so the scene file stays reviewable in a diff.
    /// </para>
    /// </summary>
    public static class GreyboxSceneSetup
    {
        private const string ScenesFolder = "Assets/Scenes";
        private const string ScenePath = ScenesFolder + "/M0_Greybox.unity";
        private const string MaterialsFolder = "Assets/Settings/GreyboxMaterials";

        // Values inside the 20-70% luminance band of docs/07-art-direction.md, 7.1, so the
        // greybox already obeys the first visual law and a greyscale screenshot reads correctly.
        private static readonly Color GroundColour = new Color(0.34f, 0.34f, 0.32f);
        private static readonly Color StructureColour = new Color(0.52f, 0.52f, 0.49f);
        private static readonly Color PropColour = new Color(0.62f, 0.56f, 0.42f);

        [MenuItem("Coldfall/Setup/Build M0 greybox scene")]
        public static void Build()
        {
            Directory.CreateDirectory(ScenesFolder);
            Directory.CreateDirectory(MaterialsFolder);

            var ground = GetOrCreateMaterial("Greybox_Ground", GroundColour);
            var structure = GetOrCreateMaterial("Greybox_Structure", StructureColour);
            var prop = GetOrCreateMaterial("Greybox_Prop", PropColour);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            CreateSun();
            CreateGround(ground);
            CreateStaircase(structure);
            CreateVaultFence(prop);
            CreateHouse(structure);
            CreateBasement(structure);
            CreateMeleeTargets(prop);
            CreateSimClockHost();
            CreateThermalVolumes();
            CreatePlayer(prop);

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AddSceneToBuildSettings();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"Coldfall setup: built the M0 greybox scene at {ScenePath}");
        }

        private static void CreateSun()
        {
            var sun = new GameObject("Sun");
            var light = sun.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            // A cool overcast key, matching the autumn "fog and sky" role in docs/07, 7.2.
            light.color = new Color(0.86f, 0.88f, 0.92f);
            light.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(38f, 150f, 0f);
        }

        private static void CreateGround(Material material)
        {
            var ground = Box("Ground", new Vector3(0f, -0.5f, 0f), new Vector3(80f, 1f, 80f), material);
            ground.isStatic = true;
        }

        /// <summary>
        /// Eight steps with a 0.25 m rise, which is under the controller's 0.35 m step offset.
        /// Any staircase in the real game must stay under it too; this is the test case.
        /// </summary>
        private static void CreateStaircase(Material material)
        {
            var root = new GameObject("Staircase");
            root.transform.position = new Vector3(-10f, 0f, 6f);

            const int steps = 8;
            const float rise = 0.25f;
            const float run = 0.32f;

            for (var i = 0; i < steps; i++)
            {
                var height = rise * (i + 1);
                var step = Box(
                    $"Step_{i:00}",
                    root.transform.position + new Vector3(0f, height * 0.5f, i * run),
                    new Vector3(2.4f, height, run),
                    material);
                step.transform.SetParent(root.transform, worldPositionStays: true);
                step.isStatic = true;
            }

            var landing = Box(
                "Landing",
                root.transform.position + new Vector3(0f, rise * steps + 0.1f, steps * run + 1.5f),
                new Vector3(2.4f, 0.2f, 3f),
                material);
            landing.transform.SetParent(root.transform, worldPositionStays: true);
            landing.isStatic = true;
        }

        /// <summary>
        /// A 1.0 m fence: inside the controller's 0.5-1.35 m vault band, with clear space on both
        /// sides so a successful vault is the expected outcome rather than a lucky one.
        /// </summary>
        private static void CreateVaultFence(Material material)
        {
            var fence = Box("VaultFence", new Vector3(6f, 0.5f, 4f), new Vector3(6f, 1f, 0.25f), material);
            fence.isStatic = true;

            var tooHigh = Box("WallTooHighToVault", new Vector3(6f, 0.9f, 9f), new Vector3(6f, 1.8f, 0.25f), material);
            tooHigh.isStatic = true;
        }

        /// <summary>
        /// A two-room shell with the three things docs/05, 5.4 demands of every building: two
        /// entries of different cost, an interior chokepoint, and a dead end.
        /// </summary>
        private static void CreateHouse(Material material)
        {
            var root = new GameObject("GreyboxHouse");
            var origin = new Vector3(0f, 0f, -12f);
            root.transform.position = origin;

            const float wallHeight = 2.8f;
            const float thickness = 0.25f;
            const float width = 10f;
            const float depth = 8f;

            var parts = new List<GameObject>();

            // Front wall, split around a 1.1 m door opening: the loud, obvious entry.
            parts.Add(Box("Wall_Front_Left", origin + new Vector3(-3.2f, wallHeight * 0.5f, -depth * 0.5f),
                new Vector3(3.6f, wallHeight, thickness), material));
            parts.Add(Box("Wall_Front_Right", origin + new Vector3(2.75f, wallHeight * 0.5f, -depth * 0.5f),
                new Vector3(4.5f, wallHeight, thickness), material));
            parts.Add(Box("Wall_Front_Header", origin + new Vector3(-0.9f, 2.5f, -depth * 0.5f),
                new Vector3(2.3f, 0.6f, thickness), material));

            // Back wall with a 1.0 m window sill at 1.05 m: the quiet entry, and a vault.
            parts.Add(Box("Wall_Back_Left", origin + new Vector3(-3.5f, wallHeight * 0.5f, depth * 0.5f),
                new Vector3(3f, wallHeight, thickness), material));
            parts.Add(Box("Wall_Back_Right", origin + new Vector3(3.5f, wallHeight * 0.5f, depth * 0.5f),
                new Vector3(3f, wallHeight, thickness), material));
            parts.Add(Box("Wall_Back_Sill", origin + new Vector3(0f, 0.52f, depth * 0.5f),
                new Vector3(4f, 1.05f, thickness), material));
            parts.Add(Box("Wall_Back_Header", origin + new Vector3(0f, 2.4f, depth * 0.5f),
                new Vector3(4f, 0.8f, thickness), material));

            parts.Add(Box("Wall_Left", origin + new Vector3(-width * 0.5f, wallHeight * 0.5f, 0f),
                new Vector3(thickness, wallHeight, depth), material));
            parts.Add(Box("Wall_Right", origin + new Vector3(width * 0.5f, wallHeight * 0.5f, 0f),
                new Vector3(thickness, wallHeight, depth), material));

            // Interior wall with a single 1.0 m doorway: the chokepoint where one zombie at a
            // time can be fought, and the reason the far room is a dead end.
            parts.Add(Box("Wall_Interior_Left", origin + new Vector3(-3.25f, wallHeight * 0.5f, 1.5f),
                new Vector3(3.5f, wallHeight, thickness), material));
            parts.Add(Box("Wall_Interior_Right", origin + new Vector3(2.25f, wallHeight * 0.5f, 1.5f),
                new Vector3(5.5f, wallHeight, thickness), material));

            parts.Add(Box("Ceiling", origin + new Vector3(0f, wallHeight + 0.1f, 0f),
                new Vector3(width, 0.2f, depth), material));

            foreach (var part in parts)
            {
                part.transform.SetParent(root.transform, worldPositionStays: true);
                part.isStatic = true;
            }
        }

        /// <summary>A pit reached by a second staircase, to prove vertical space works.</summary>
        private static void CreateBasement(Material material)
        {
            var root = new GameObject("Basement");
            var origin = new Vector3(14f, 0f, -12f);
            root.transform.position = origin;

            const float depth = 2.6f;

            var parts = new List<GameObject>
            {
                Box("Floor", origin + new Vector3(0f, -depth - 0.1f, 0f), new Vector3(8f, 0.2f, 8f), material),
                Box("Wall_North", origin + new Vector3(0f, -depth * 0.5f, 4f), new Vector3(8f, depth, 0.25f), material),
                Box("Wall_South", origin + new Vector3(0f, -depth * 0.5f, -4f), new Vector3(8f, depth, 0.25f), material),
                Box("Wall_East", origin + new Vector3(4f, -depth * 0.5f, 0f), new Vector3(0.25f, depth, 8f), material),
                Box("Wall_West", origin + new Vector3(-4f, -depth * 0.5f, 0f), new Vector3(0.25f, depth, 8f), material),
            };

            // Descending steps along the east side, same 0.25 m rise as the outdoor staircase.
            const int steps = 11;
            for (var i = 0; i < steps; i++)
            {
                var y = -0.25f * (i + 1);
                var slab = Box(
                    $"DownStep_{i:00}",
                    origin + new Vector3(3f, y + 0.125f, -3.5f + i * 0.32f),
                    new Vector3(1.6f, 0.25f, 0.32f),
                    material);
                parts.Add(slab);
            }

            foreach (var part in parts)
            {
                part.transform.SetParent(root.transform, worldPositionStays: true);
                part.isStatic = true;
            }
        }

        private static void CreateSimClockHost()
        {
            var host = new GameObject("SimClock");
            host.AddComponent<SimClockHost>();
            host.AddComponent<WeatherHost>();
            host.AddComponent<ThermalWorld>();
        }

        /// <summary>
        /// Boxes the greybox interiors so the player can feel the difference between inside and
        /// out. The front room has a lit stove, the back window starts broken, and the interior
        /// doorway stands open: between them the three portal states that matter are all
        /// reachable without an interaction system.
        /// </summary>
        private static void CreateThermalVolumes()
        {
            var root = new GameObject("ThermalVolumes");

            // Heat comes from the stove below, not from the room itself.
            var front = Volume(
                root, "Room_House_Front", new Vector3(0f, 1.4f, -13.25f),
                new Vector3(9.5f, 2.8f, 5.3f), insulation: 0.80f, wind: 0f, heat: 0f, start: 9f);

            var back = Volume(
                root, "Room_House_Back", new Vector3(0f, 1.4f, -9.25f),
                new Vector3(9.5f, 2.8f, 2.3f), insulation: 0.75f, wind: 0.2f, heat: 0f, start: 9f);

            var basement = Volume(
                root, "Room_Basement", new Vector3(14f, -1.3f, -12f),
                new Vector3(7.5f, 2.6f, 7.5f), insulation: 0.88f, wind: 0f, heat: 0f, start: 9f);

            // Front door: the loud entry, and the fastest way to lose the room's heat.
            Portal(root, "Portal_FrontDoor", new Vector3(-0.45f, 1.25f, -16f),
                front, null, area: 1.9f, state: PortalState.Closed);

            // Back window: starts broken on purpose, so boarding it is the first time the
            // player sees a defensive verb change the temperature.
            Portal(root, "Portal_BackWindow", new Vector3(0f, 1.5f, -8f),
                back, null, area: 1.0f, state: PortalState.Broken);

            Portal(root, "Portal_InteriorDoor", new Vector3(-1f, 1.4f, -10.5f),
                front, back, area: 1.8f, state: PortalState.Open);

            Portal(root, "Portal_BasementHatch", new Vector3(14f, 0f, -15.5f),
                basement, null, area: 1.2f, state: PortalState.Open);

            // A stove against the back wall of the front room, lit, with a day and a half of
            // dry wood. Walking up to it and away from it is the point: radiant heat falls off
            // with distance, so the warm corner is a place rather than a status.
            Fire(root, "Stove", new Vector3(3.6f, 0.5f, -15.2f),
                HeatSourceKind.WoodStove, fuel: 30f, lit: true, dryness: 1f, room: front);

            // An outdoor campfire near the spawn, burning damp autumn wood. It heats no room at
            // all; everything it gives comes from standing close to it.
            Fire(root, "Campfire", new Vector3(4f, 0.3f, 2f),
                HeatSourceKind.Campfire, fuel: 8f, lit: true, dryness: 0.45f, room: null);
        }

        private static void Fire(
            GameObject root, string name, Vector3 position, HeatSourceKind kind,
            float fuel, bool lit, float dryness, ThermalVolumeComponent room)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, worldPositionStays: true);
            go.transform.position = position;

            go.AddComponent<FireComponent>().Configure(kind, fuel, lit, dryness, room);
        }

        private static ThermalVolumeComponent Volume(
            GameObject root, string name, Vector3 centre, Vector3 size,
            float insulation, float wind, float heat, float start)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, worldPositionStays: true);
            go.transform.position = centre;

            var volume = go.AddComponent<ThermalVolumeComponent>();
            volume.Configure(size, insulation, wind, heat, start);
            return volume;
        }

        private static void Portal(
            GameObject root, string name, Vector3 position,
            ThermalVolumeComponent a, ThermalVolumeComponent b, float area, PortalState state)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root.transform, worldPositionStays: true);
            go.transform.position = position;

            go.AddComponent<ThermalPortalComponent>().Configure(a, b, area, state);
        }

        /// <summary>
        /// Posts that count as legitimate melee targets. Three stand in the open, and one stands
        /// just inside the front doorway on purpose: swinging at that one from outside is the
        /// test for M0 exit criterion 2, because a full-charge swing there should clip the frame.
        /// </summary>
        private static void CreateMeleeTargets(Material material)
        {
            var root = new GameObject("MeleeTargets");

            var placements = new[]
            {
                new Vector3(-3f, 0f, 7f),
                new Vector3(0f, 0f, 8.5f),
                new Vector3(2.5f, 0f, 7f),
                // Inside the front doorway of the house: the clipping test.
                new Vector3(-0.45f, 0f, -15.2f),
            };

            for (var i = 0; i < placements.Length; i++)
            {
                var post = Box(
                    $"Target_{i:00}",
                    placements[i] + new Vector3(0f, 0.9f, 0f),
                    new Vector3(0.4f, 1.8f, 0.4f),
                    material);
                post.AddComponent<MeleeTarget>();
                post.transform.SetParent(root.transform, worldPositionStays: true);
            }
        }

        /// <summary>
        /// Creates the body and, as a sibling rather than a child, the camera rig. The separation
        /// is the whole reason the camera does not stutter; see FirstPersonController.
        /// </summary>
        private static void CreatePlayer(Material weaponMaterial)
        {
            var body = new GameObject("Player");
            body.transform.position = new Vector3(0f, 0.1f, 0f);

            var controller = body.AddComponent<CharacterController>();
            controller.radius = 0.3f;
            controller.height = 1.8f;
            controller.center = Vector3.up * 0.9f;

            var rig = new GameObject("PlayerCameraRig");
            rig.transform.position = new Vector3(0f, 1.66f, 0f);

            var camera = new GameObject("PlayerCamera");
            camera.transform.SetParent(rig.transform, worldPositionStays: false);
            var cam = camera.AddComponent<Camera>();
            cam.fieldOfView = 70f;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 600f;
            camera.tag = "MainCamera";

            var fps = body.AddComponent<FirstPersonController>();

            // The field is private and serialized, so it is assigned the way the editor would.
            var serialized = new SerializedObject(fps);
            serialized.FindProperty("cameraRig").objectReferenceValue = rig.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // The weapon hangs off the camera rig, so it follows the view rather than the body.
            var weapon = Box("Weapon", Vector3.zero, new Vector3(0.07f, 0.07f, 0.8f), weaponMaterial);
            weapon.transform.SetParent(rig.transform, worldPositionStays: false);
            weapon.transform.localPosition = new Vector3(0.28f, -0.26f, 0.42f);
            weapon.transform.localRotation = Quaternion.Euler(12f, -16f, 6f);

            // Without this the blade's own collider is the first thing the trace finds, and
            // every swing reads as having clipped the world.
            Object.DestroyImmediate(weapon.GetComponent<Collider>());

            body.AddComponent<SurvivalCharacter>();
            body.AddComponent<ConditionCardView>();
            body.AddComponent<SurvivalDebugReadout>();
            rig.AddComponent<Coldfall.Presentation.ConditionScreenEffects>();

            var melee = body.AddComponent<MeleeSwing>();
            var meleeSerialized = new SerializedObject(melee);
            meleeSerialized.FindProperty("weapon").objectReferenceValue = weapon.transform;
            meleeSerialized.FindProperty("controller").objectReferenceValue = fps;
            meleeSerialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static GameObject Box(string name, Vector3 centre, Vector3 size, Material material)
        {
            var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.position = centre;
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
            return box;
        }

        private static Material GetOrCreateMaterial(string name, Color colour)
        {
            var path = $"{MaterialsFolder}/{name}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                return existing;
            }

            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null)
            {
                Debug.LogError("Coldfall setup: URP Lit shader not found. Is URP installed and active?");
                shader = Shader.Find("Standard");
            }

            var material = new Material(shader) { name = name };
            material.SetColor("_BaseColor", colour);
            material.SetFloat("_Smoothness", 0.15f);
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static void AddSceneToBuildSettings()
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            if (scenes.Exists(s => s.path == ScenePath))
            {
                return;
            }

            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, enabled: true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
