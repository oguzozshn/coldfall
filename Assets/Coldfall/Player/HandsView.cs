using Coldfall.Sim;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Coldfall.Player
{
    /// <summary>
    /// The hands of docs/08-audio-and-ui.md, 8.5: the fastest status check in the game.
    /// <para>
    /// Held, not toggled, and it occupies the hands exactly as the condition card does. Looking
    /// at your hands is a deliberate input with a cost, which is what stops it being a free
    /// always-on readout.
    /// </para>
    /// <para>
    /// <b>The mesh is a placeholder.</b> This drives colour, wetness, blood, a bandage and a
    /// tremor on whatever transform it is given. Dropping a real hand model in later changes the
    /// reference and nothing else, because every decision about what the hands should look like
    /// lives in <see cref="HandsState"/> and is unit tested there.
    /// </para>
    /// </summary>
    [RequireComponent(typeof(SurvivalCharacter))]
    public sealed class HandsView : MonoBehaviour
    {
        private static readonly int BaseColour = Shader.PropertyToID("_BaseColor");
        private static readonly int Smoothness = Shader.PropertyToID("_Smoothness");

        [Tooltip("The hand or arm renderer. A placeholder box until a hand model exists.")]
        [SerializeField] private Renderer handRenderer;

        [Tooltip("The transform the hands are moved and shaken on.")]
        [SerializeField] private Transform handTransform;

        [Tooltip("Shown when an arm wound is bandaged.")]
        [SerializeField] private GameObject bandage;

        [Header("Poses, local to the camera rig")]
        [SerializeField] private Vector3 restPosition = new Vector3(0.28f, -0.42f, 0.35f);
        [SerializeField] private Vector3 raisedPosition = new Vector3(0.12f, -0.14f, 0.34f);
        [SerializeField] private Vector3 restEuler = new Vector3(10f, -12f, 0f);
        [SerializeField] private Vector3 raisedEuler = new Vector3(-62f, -6f, 8f);

        [SerializeField] private float raiseSeconds = 0.45f;

        [Header("Appearance")]
        [SerializeField] private Color skin = new Color(0.52f, 0.41f, 0.34f);
        [SerializeField] private Color coldSkin = new Color(0.62f, 0.33f, 0.31f);
        [SerializeField] private Color frozenSkin = new Color(0.63f, 0.62f, 0.64f);
        [SerializeField] private Color bloodColour = new Color(0.35f, 0.09f, 0.07f);

        [Tooltip("Maximum shake in metres at full tremor.")]
        [SerializeField] private float tremorAmplitude = 0.012f;

        private SurvivalCharacter _character;
        private MaterialPropertyBlock _properties;
        private float _raised;
        private Vector3 _noiseSeed;

        /// <summary>How far the hands are raised, 0 to 1.</summary>
        public float Raised => _raised;

        /// <summary>True while looking at the hands occupies them.</summary>
        public bool OccupiesHands => _raised > 0.01f;

        /// <summary>The state being rendered, for tests and debug tools.</summary>
        public HandsState State { get; private set; }

        private void Awake()
        {
            _character = GetComponent<SurvivalCharacter>();
            _properties = new MaterialPropertyBlock();
            _noiseSeed = new Vector3(Random.value * 100f, Random.value * 100f, Random.value * 100f);

            if (bandage != null)
            {
                bandage.SetActive(false);
            }
        }

        private void Update()
        {
            var held = Keyboard.current != null && Keyboard.current.hKey.isPressed;

            _raised = Mathf.MoveTowards(
                _raised, held ? 1f : 0f, Time.deltaTime / Mathf.Max(0.05f, raiseSeconds));

            State = HandsState.Build(
                _character.Core, _character.Needs, _character.Garments, _character.Body);

            ApplyPose();
            ApplyAppearance();
        }

        private void ApplyPose()
        {
            if (handTransform == null)
            {
                return;
            }

            var eased = Mathf.SmoothStep(0f, 1f, _raised);

            var position = Vector3.Lerp(restPosition, raisedPosition, eased);
            var rotation = Quaternion.Slerp(
                Quaternion.Euler(restEuler), Quaternion.Euler(raisedEuler), eased);

            // Perlin rather than random, so the shake is a tremble rather than static. It is
            // applied whether or not the hands are raised, because a shaking weapon is the same
            // information arriving without the player asking for it.
            if (State.Tremor > 0f)
            {
                var t = Time.time * 14f;
                var shake = new Vector3(
                    Mathf.PerlinNoise(_noiseSeed.x + t, 0f) - 0.5f,
                    Mathf.PerlinNoise(_noiseSeed.y + t, 0f) - 0.5f,
                    Mathf.PerlinNoise(_noiseSeed.z + t, 0f) - 0.5f);

                position += shake * (tremorAmplitude * 2f * State.Tremor);
            }

            handTransform.localPosition = position;
            handTransform.localRotation = rotation;
        }

        private void ApplyAppearance()
        {
            if (handRenderer == null)
            {
                return;
            }

            var colour = State.Frostbitten
                ? Color.Lerp(coldSkin, frozenSkin, 0.75f)
                : Color.Lerp(skin, coldSkin, State.Cold);

            colour = Color.Lerp(colour, bloodColour, State.Blood * 0.8f);

            handRenderer.GetPropertyBlock(_properties);
            _properties.SetColor(BaseColour, colour);

            // Wet skin is shiny. It is the only cue for wetness the player gets on the body
            // itself, so it runs most of the smoothness range.
            _properties.SetFloat(Smoothness, Mathf.Lerp(0.12f, 0.75f, State.Wet));
            handRenderer.SetPropertyBlock(_properties);

            if (bandage != null && bandage.activeSelf != State.Bandaged)
            {
                bandage.SetActive(State.Bandaged);
            }
        }

        /// <summary>
        /// Sets the placeholder references. Used by the greybox scene generator.
        /// </summary>
        public void Configure(Transform hand, Renderer renderer, GameObject bandageObject)
        {
            handTransform = hand;
            handRenderer = renderer;
            bandage = bandageObject;
        }
    }
}
