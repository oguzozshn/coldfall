using Coldfall.Sim;
using Coldfall.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Coldfall.Player
{
    /// <summary>
    /// A temporary on-screen readout of the simulation, for tuning.
    /// <para>
    /// <b>This is not the interface.</b> Docs/08, 8.5 is explicit that the game has no numeric
    /// HUD: the player reads their condition from screen effects, their hands and the condition
    /// card. That arrives at the end of M1. Until the numbers are tuned they have to be visible,
    /// and pretending otherwise would mean tuning a thermal model by feel alone.
    /// </para>
    /// <para>Delete this component when the condition card lands.</para>
    /// </summary>
    [RequireComponent(typeof(SurvivalCharacter))]
    public sealed class SurvivalDebugReadout : MonoBehaviour
    {
        [Tooltip("Off by default now that the condition card exists. F1 brings it back for tuning.")]
        [SerializeField] private bool visible;

        private SurvivalCharacter _character;
        private GUIStyle _style;

        private void Awake()
        {
            _character = GetComponent<SurvivalCharacter>();
        }

        private void Update()
        {
            // The new Input System, like the rest of the project. UnityEngine.Input is disabled
            // whenever the active input handling is set to the new backend.
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
            {
                visible = !visible;
            }
        }

        private void OnGUI()
        {
            if (!visible || _character == null)
            {
                return;
            }

            _style ??= new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                normal = { textColor = Color.white },
            };

            var clock = SimClockHost.Current;
            var weather = WeatherHost.Current;
            var core = _character.Core;
            var needs = _character.Needs;
            var balance = core.LastBalance;

            var text =
                $"<b>time</b>  day {clock?.Day ?? 0}  {clock?.HourOfDay ?? 0:00}:{clock?.MinuteOfHour ?? 0:00}\n" +
                $"<b>weather</b>  {weather?.Current}  outdoor {weather?.AmbientCelsius ?? 0:F1} C" +
                $"  wind {weather?.WindKilometresPerHour ?? 0:F0} km/h" +
                (weather != null && weather.IsTelegraphing
                    ? $"   >> {weather.Next} in {weather.MinutesUntilFront:F0} min"
                    : string.Empty) + "\n" +
                $"<b>here</b>  {( _character.IsIndoors ? "indoors" : "outdoors")}" +
                $"  air {_character.AmbientCelsius:F1} C  wind {_character.WindKilometresPerHour:F0}" +
                $"  radiant {_character.RadiantHeat:F1}\n" +
                $"<b>core</b>  {core.Celsius:F2} C   {core.Stage}\n" +
                $"   metabolic {balance.Metabolic:F1}  shiver {balance.Shivering:F1}" +
                $"  radiant {balance.Radiant:F1}\n" +
                $"   convective -{balance.Convective:F1}  evaporative -{balance.Evaporative:F1}" +
                $"  respiratory -{balance.Respiratory:F1}\n" +
                $"   net {balance.DegreesPerHour:F2} C/h\n" +
                $"<b>wetness</b>  {_character.MeanWetness:P0}\n" +
                $"<b>needs</b>  {needs.Calories:F0} kcal   water {needs.HydrationPercent:F0}%" +
                $"   fatigue {needs.FatiguePercent:F0}%\n" +
                $"<b>doing</b>  {_character.Activity}\n" +
                $"\n<i>F1 hides this. It is not the interface; the condition card replaces it.</i>";

            var size = _style.CalcSize(new GUIContent(text));
            var rect = new Rect(12f, 12f, Mathf.Max(460f, size.x + 16f), size.y + 16f);

            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;

            _style.richText = true;
            GUI.Label(new Rect(rect.x + 8f, rect.y + 8f, rect.width - 16f, rect.height - 16f), text, _style);
        }
    }
}
