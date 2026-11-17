using Coldfall.Sim;
using Coldfall.World;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Coldfall.Player
{
    /// <summary>
    /// The condition card of docs/08-audio-and-ui.md, 8.5, read as a physical object.
    /// <para>
    /// Held, not toggled. Raising it takes a second and a half and occupies the hands, which is
    /// the whole point: the player cannot read it mid-fight. Docs/08 calls it the game's only
    /// concession to an explicit status screen, and the cost of reading it is what keeps that
    /// concession honest.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(SurvivalCharacter))]
    public sealed class ConditionCardView : MonoBehaviour
    {
        /// <summary>Seconds to raise the card fully. Docs/08: a second and a half.</summary>
        public const float RaiseSeconds = 1.5f;

        [Tooltip("Lowering is faster than raising: putting it away to fight must not feel sticky.")]
        [SerializeField] private float lowerSeconds = 0.4f;

        private SurvivalCharacter _character;
        private ConditionReport _report;
        private float _raised;
        private float _refreshTimer;
        private GUIStyle _cardStyle;
        private GUIStyle _warningStyle;

        /// <summary>How far the card is raised, 0 to 1.</summary>
        public float Raised => _raised;

        /// <summary>
        /// True while the card occupies the hands. Melee and other two-handed actions read this.
        /// </summary>
        public bool OccupiesHands => _raised > 0.01f;

        private void Awake()
        {
            _character = GetComponent<SurvivalCharacter>();
        }

        private void Update()
        {
            var held = Keyboard.current != null && Keyboard.current.cKey.isPressed;

            var target = held ? 1f : 0f;
            var speed = held ? 1f / RaiseSeconds : 1f / Mathf.Max(0.05f, lowerSeconds);
            _raised = Mathf.MoveTowards(_raised, target, speed * Time.deltaTime);

            if (!OccupiesHands)
            {
                _report = null;
                return;
            }

            // Refreshed while held rather than every frame: the card is a thing the character
            // wrote a moment ago, not a live gauge.
            _refreshTimer -= Time.deltaTime;
            if (_report == null || _refreshTimer <= 0f)
            {
                _report = ConditionCard.Build(
                    _character.Core, _character.Needs, _character.Garments, SimClockHost.Current,
                    _character.Body, _character.Illnesses);
                _refreshTimer = 1f;
            }
        }

        private void OnGUI()
        {
            if (_report == null || _raised <= 0.01f)
            {
                return;
            }

            BuildStyles();

            // Only legible once fully raised, so the read genuinely costs the time.
            var legibility = Mathf.InverseLerp(0.65f, 1f, _raised);

            const float width = 420f;
            const float height = 300f;

            var x = (Screen.width - width) * 0.5f;
            var restY = Screen.height;
            var upY = (Screen.height - height) * 0.62f;
            var y = Mathf.Lerp(restY, upY, Mathf.SmoothStep(0f, 1f, _raised));

            var card = new Rect(x, y, width, height);

            GUI.color = new Color(0.86f, 0.83f, 0.76f, 0.97f * _raised);
            GUI.DrawTexture(card, Texture2D.whiteTexture);

            GUI.color = new Color(1f, 1f, 1f, legibility);

            var inner = new Rect(card.x + 22f, card.y + 20f, card.width - 44f, card.height - 40f);
            GUILayout.BeginArea(inner);

            GUILayout.Label("CONDITION", _cardStyle);
            GUILayout.Label(_report.Timestamp, _cardStyle);
            GUILayout.Space(12f);

            foreach (var line in _report.Body)
            {
                Line(line);
            }

            GUILayout.Space(10f);

            foreach (var line in _report.Needs)
            {
                Line(line);
            }

            GUILayout.EndArea();
            GUI.color = Color.white;
        }

        private void Line(ConditionLine line)
        {
            GUILayout.Label(
                $"{line.Label.PadRight(10)}{line.Text}",
                line.IsWarning ? _warningStyle : _cardStyle);
        }

        private void BuildStyles()
        {
            if (_cardStyle != null)
            {
                return;
            }

            _cardStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 15,
                // Ink on paper, not UI text.
                normal = { textColor = new Color(0.16f, 0.14f, 0.12f) },
            };

            _warningStyle = new GUIStyle(_cardStyle)
            {
                normal = { textColor = new Color(0.48f, 0.17f, 0.13f) },
                fontStyle = FontStyle.Bold,
            };
        }
    }
}
