using UnityEngine;

namespace Coldfall.World
{
    /// <summary>
    /// Marks a collider as a legitimate melee target rather than as scenery.
    /// <para>
    /// The distinction is the whole point of the M0 melee test: a swing that lands on one of
    /// these is a clean hit, and a swing that lands on anything else has clipped the
    /// environment and is cancelled. Nothing else in the greybox carries this component, so
    /// every wall, doorframe and step is automatically scenery.
    /// </para>
    /// <para>
    /// A placeholder for the zombie hit zones of M2. It keeps no health and resolves no damage;
    /// it only reports that it was struck and shows it, because M0 is about feel.
    /// </para>
    /// </summary>
    public sealed class MeleeTarget : MonoBehaviour
    {
        [Tooltip("How long the struck colour lasts, in seconds.")]
        [SerializeField] private float flashDuration = 0.35f;

        [Tooltip("Colour shown on a clean hit. Inside the 20-70% luminance band of docs/07, 7.1.")]
        [SerializeField] private Color struckColour = new Color(0.58f, 0.25f, 0.21f);

        private MeshRenderer _renderer;
        private MaterialPropertyBlock _properties;
        private Color _restColour;
        private float _flashRemaining;

        /// <summary>Number of clean hits this target has taken since the scene loaded.</summary>
        public int HitCount { get; private set; }

        private void Awake()
        {
            _renderer = GetComponentInChildren<MeshRenderer>();
            if (_renderer == null)
            {
                return;
            }

            _properties = new MaterialPropertyBlock();
            _restColour = _renderer.sharedMaterial != null
                ? _renderer.sharedMaterial.GetColor(BaseColour)
                : Color.grey;
        }

        private static readonly int BaseColour = Shader.PropertyToID("_BaseColor");

        /// <summary>
        /// Called by the swing when the blade reaches this target.
        /// </summary>
        /// <param name="charge">Swing charge, 0 to 1, for scaling the reaction.</param>
        /// <param name="point">World position of the contact.</param>
        public void ReceiveHit(float charge, Vector3 point)
        {
            HitCount++;
            _flashRemaining = flashDuration;
            Apply(Color.Lerp(_restColour, struckColour, Mathf.Clamp01(0.35f + charge * 0.65f)));
        }

        private void Update()
        {
            if (_flashRemaining <= 0f || _renderer == null)
            {
                return;
            }

            _flashRemaining -= Time.deltaTime;
            if (_flashRemaining > 0f)
            {
                return;
            }

            Apply(_restColour);
        }

        private void Apply(Color colour)
        {
            if (_renderer == null)
            {
                return;
            }

            // A property block rather than a material instance, so striking a target does not
            // break batching or leak a material per object.
            _renderer.GetPropertyBlock(_properties);
            _properties.SetColor(BaseColour, colour);
            _renderer.SetPropertyBlock(_properties);
        }
    }
}
