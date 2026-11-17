using Coldfall.Sim;
using Coldfall.World;
using UnityEditor;
using UnityEngine;

namespace Coldfall.Editor.Tools
{
    /// <summary>
    /// Draws every thermal volume and portal in the scene view, coloured by temperature and
    /// labelled in place.
    /// <para>
    /// Docs/09-technical-architecture.md, 9.11 calls this the tool that pays for itself inside
    /// a week, and the reason is that the thermal model is invisible by construction. A room is
    /// four degrees colder than the one next to it and nothing on screen says so. Without this
    /// the only way to find a portal wired to the wrong room, or a volume whose bounds miss half
    /// the floor, is to stand in it and wonder.
    /// </para>
    /// <para>
    /// Toggled from the Coldfall menu and remembered between sessions.
    /// </para>
    /// </summary>
    [InitializeOnLoad]
    public static class ThermalVisualizer
    {
        private const string EnabledKey = "Coldfall.ThermalVisualizer.Enabled";
        private const string MenuPath = "Coldfall/Tools/Thermal visualiser";

        // The cold and warm ends of the scale, in Celsius. Beyond them the colour clamps.
        private const float ColdCelsius = -20f;
        private const float WarmCelsius = 25f;

        private static readonly Color Cold = new Color(0.24f, 0.29f, 0.35f);
        private static readonly Color Warm = new Color(0.88f, 0.54f, 0.18f);

        static ThermalVisualizer()
        {
            EditorApplication.delayCall += () =>
            {
                Menu.SetChecked(MenuPath, IsEnabled);
                Apply();
            };
        }

        /// <summary>True while the visualiser is drawing.</summary>
        public static bool IsEnabled
        {
            get => EditorPrefs.GetBool(EnabledKey, true);
            private set => EditorPrefs.SetBool(EnabledKey, value);
        }

        [MenuItem(MenuPath)]
        private static void Toggle()
        {
            IsEnabled = !IsEnabled;
            Menu.SetChecked(MenuPath, IsEnabled);
            Apply();
            SceneView.RepaintAll();
        }

        private static void Apply()
        {
            SceneView.duringSceneGui -= OnSceneGui;

            if (IsEnabled)
            {
                SceneView.duringSceneGui += OnSceneGui;
            }
        }

        private static void OnSceneGui(SceneView view)
        {
            var volumes = Object.FindObjectsByType<ThermalVolumeComponent>(FindObjectsSortMode.None);
            if (volumes.Length == 0)
            {
                return;
            }

            var outdoor = ThermalWorld.Current != null
                ? ThermalWorld.Current.OutdoorCelsius
                : WeatherHost.Current?.AmbientCelsius ?? 0f;

            foreach (var volume in volumes)
            {
                DrawVolume(volume);
            }

            foreach (var portal in Object.FindObjectsByType<ThermalPortalComponent>(FindObjectsSortMode.None))
            {
                DrawPortal(portal);
            }

            DrawLegend(view, outdoor);
        }

        private static void DrawVolume(ThermalVolumeComponent volume)
        {
            var bounds = volume.WorldBounds;
            var celsius = Application.isPlaying ? volume.Celsius : 0f;
            var colour = ColourFor(celsius);

            Handles.color = new Color(colour.r, colour.g, colour.b, 0.10f);
            Handles.DrawSolidRectangleWithOutline(
                FloorQuad(bounds), new Color(colour.r, colour.g, colour.b, 0.10f), colour);

            Handles.color = colour;
            Handles.DrawWireCube(bounds.center, bounds.size);

            var label = Application.isPlaying
                ? $"{volume.name}\n{celsius:F1} C" + (volume.HeatInput > 0f ? "   (heated)" : string.Empty)
                : volume.name;

            Handles.Label(bounds.center + Vector3.up * (bounds.extents.y + 0.3f), label, LabelStyle(colour));
        }

        private static void DrawPortal(ThermalPortalComponent portal)
        {
            var colour = ColourForState(portal.State);
            Handles.color = colour;

            var position = portal.transform.position;
            Handles.DrawWireCube(position, new Vector3(1f, 2f, 0.12f));

            // A line to each side it joins, so a portal wired to the wrong room is obvious at a
            // glance rather than after half an hour of wondering why a room will not warm up.
            DrawLink(position, portal.VolumeA, colour);
            DrawLink(position, portal.VolumeB, colour);

            Handles.Label(
                position + Vector3.up * 1.4f,
                $"{portal.State}  {portal.AreaSquareMetres:F1} m2",
                LabelStyle(colour));
        }

        private static void DrawLink(Vector3 from, ThermalVolumeComponent to, Color colour)
        {
            if (to == null)
            {
                // Outdoors: a short stub upwards, meaning "this one vents to the sky".
                Handles.color = new Color(colour.r, colour.g, colour.b, 0.4f);
                Handles.DrawDottedLine(from, from + Vector3.up * 2.5f, 3f);
                return;
            }

            Handles.color = new Color(colour.r, colour.g, colour.b, 0.5f);
            Handles.DrawDottedLine(from, to.WorldBounds.center, 3f);
        }

        private static void DrawLegend(SceneView view, float outdoorCelsius)
        {
            Handles.BeginGUI();

            var rect = new Rect(10f, view.position.height - 86f, 250f, 62f);
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            var style = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = Color.white },
                fontSize = 11,
            };

            var text = Application.isPlaying
                ? $"Thermal visualiser\nOutdoors {outdoorCelsius:F1} C\nCold {ColdCelsius:F0} to warm {WarmCelsius:F0}"
                : "Thermal visualiser\nTemperatures appear in play mode";

            GUI.Label(new Rect(rect.x + 8f, rect.y + 6f, rect.width - 16f, rect.height - 12f), text, style);

            Handles.EndGUI();
        }

        private static Vector3[] FloorQuad(Bounds bounds)
        {
            var y = bounds.min.y + 0.02f;
            return new[]
            {
                new Vector3(bounds.min.x, y, bounds.min.z),
                new Vector3(bounds.max.x, y, bounds.min.z),
                new Vector3(bounds.max.x, y, bounds.max.z),
                new Vector3(bounds.min.x, y, bounds.max.z),
            };
        }

        private static Color ColourFor(float celsius)
        {
            var t = Mathf.InverseLerp(ColdCelsius, WarmCelsius, celsius);
            return Color.Lerp(Cold, Warm, t);
        }

        private static Color ColourForState(PortalState state)
        {
            return state switch
            {
                PortalState.Sealed => new Color(0.35f, 0.55f, 0.35f),
                PortalState.Closed => new Color(0.45f, 0.65f, 0.45f),
                PortalState.Boarded => new Color(0.75f, 0.62f, 0.30f),
                PortalState.Open => new Color(0.85f, 0.45f, 0.22f),
                _ => new Color(0.88f, 0.26f, 0.21f),
            };
        }

        private static GUIStyle LabelStyle(Color colour)
        {
            return new GUIStyle(EditorStyles.boldLabel)
            {
                normal = { textColor = colour },
                fontSize = 11,
            };
        }
    }
}
