using Coldfall.Player;
using Coldfall.Sim;
using UnityEngine;

namespace Coldfall.Presentation
{
    /// <summary>
    /// Two of the always-on cues of docs/07-art-direction.md, 7.10: visible breath below
    /// freezing-ish temperatures, and water on the lens when the character is soaked.
    /// <para>
    /// Breath is the more important of the two. It is the only continuous signal that says
    /// "it is cold here" without a number, it appears before the player is in any danger, and
    /// it disappears the moment they step into a heated room, which teaches the thermal volume
    /// model without a word of explanation.
    /// </para>
    /// <para>
    /// <b>The lens water is a placeholder.</b> Docs/07 asks for droplets and streaking, which
    /// wants a fullscreen shader; this draws a generated droplet texture over the view instead.
    /// It reads correctly and it is honest about what it is.
    /// </para>
    /// </summary>
    public sealed class BreathAndLensEffects : MonoBehaviour
    {
        /// <summary>Ambient temperature below which breath becomes visible.</summary>
        public const float BreathVisibleCelsius = 5f;

        [SerializeField] private SurvivalCharacter character;

        [Tooltip("Where the breath comes from. Defaults to this transform.")]
        [SerializeField] private Transform mouth;

        [Tooltip("Breaths per minute at rest. Rises with exertion and exhaustion.")]
        [SerializeField] private float restingBreathsPerMinute = 14f;

        private ParticleSystem _breath;
        private float _sinceBreath;
        private Texture2D _droplets;
        private float _lensWetness;

        private void Awake()
        {
            character ??= FindFirstObjectByType<SurvivalCharacter>();
            mouth ??= transform;

            BuildBreath();
            BuildDroplets();
        }

        private void OnDestroy()
        {
            if (_droplets != null)
            {
                Destroy(_droplets);
            }
        }

        private void Update()
        {
            if (character == null)
            {
                return;
            }

            StepBreath();

            // Eased rather than snapped: drying off should feel like the view clearing, not
            // like an effect being switched off.
            var target = Mathf.InverseLerp(0.35f, 1f, character.MeanWetness);
            if (character.IsIndoors)
            {
                target *= 0.6f;
            }

            _lensWetness = Mathf.MoveTowards(_lensWetness, target, Time.deltaTime * 0.25f);
        }

        private void StepBreath()
        {
            if (_breath == null)
            {
                return;
            }

            if (character.AmbientCelsius > BreathVisibleCelsius)
            {
                _sinceBreath = 0f;
                return;
            }

            // Colder air makes a bigger, longer-lived cloud, so the player can read roughly how
            // cold it is from their own breath.
            var chill = Mathf.InverseLerp(BreathVisibleCelsius, -20f, character.AmbientCelsius);

            var breathsPerMinute = restingBreathsPerMinute
                                   * (character.Activity == ActivityLevel.Hard ? 2.2f
                                       : character.Activity == ActivityLevel.Light ? 1.4f : 1f)
                                   * (character.Body.BreathingNoiseMultiplier);

            _sinceBreath += Time.deltaTime;

            if (_sinceBreath < 60f / Mathf.Max(1f, breathsPerMinute))
            {
                return;
            }

            _sinceBreath = 0f;

            var main = _breath.main;
            main.startSize = Mathf.Lerp(0.16f, 0.42f, chill);
            main.startLifetime = Mathf.Lerp(0.7f, 2.2f, chill);
            main.startColor = new Color(0.92f, 0.94f, 0.96f, Mathf.Lerp(0.18f, 0.5f, chill));

            _breath.Emit(Mathf.RoundToInt(Mathf.Lerp(3f, 9f, chill)));
        }

        private void OnGUI()
        {
            if (_droplets == null || _lensWetness <= 0.02f)
            {
                return;
            }

            GUI.color = new Color(1f, 1f, 1f, _lensWetness * 0.5f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _droplets, ScaleMode.StretchToFill);
            GUI.color = Color.white;
        }

        private void BuildBreath()
        {
            var host = new GameObject("Breath");
            host.transform.SetParent(mouth, worldPositionStays: false);
            host.transform.localPosition = new Vector3(0f, -0.05f, 0.18f);
            host.hideFlags = HideFlags.DontSave;

            _breath = host.AddComponent<ParticleSystem>();

            var main = _breath.main;
            main.playOnAwake = false;
            main.loop = false;
            main.startSpeed = 0.5f;
            main.startSize = 0.2f;
            main.startLifetime = 1.2f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 64;

            var emission = _breath.emission;
            emission.enabled = false;

            var shape = _breath.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 14f;
            shape.radius = 0.02f;

            var size = _breath.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.4f, 1f, 1.6f));

            var colour = _breath.colorOverLifetime;
            colour.enabled = true;
            colour.color = new ParticleSystem.MinMaxGradient(FadeOutGradient());

            var renderer = host.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
        }

        private static Gradient FadeOutGradient()
        {
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
            return gradient;
        }

        /// <summary>
        /// Generates a droplet texture once, rather than shipping an asset for a placeholder.
        /// </summary>
        private void BuildDroplets()
        {
            const int size = 256;
            _droplets = new Texture2D(size, size, TextureFormat.RGBA32, mipChain: false)
            {
                hideFlags = HideFlags.DontSave,
                wrapMode = TextureWrapMode.Clamp,
            };

            var pixels = new Color32[size * size];
            for (var i = 0; i < pixels.Length; i++)
            {
                pixels[i] = new Color32(255, 255, 255, 0);
            }

            var random = new System.Random(20261120);

            for (var drop = 0; drop < 90; drop++)
            {
                var cx = random.Next(size);
                var cy = random.Next(size);
                var radius = 2 + random.Next(7);
                var streak = random.Next(4, 26);

                for (var y = -radius; y <= radius + streak; y++)
                for (var x = -radius; x <= radius; x++)
                {
                    // A round head with a tail running down the glass.
                    var inHead = x * x + y * y <= radius * radius;
                    var inTail = y > 0 && Mathf.Abs(x) <= Mathf.Max(1, radius - y / 6);
                    if (!inHead && !inTail)
                    {
                        continue;
                    }

                    var px = cx + x;
                    var py = cy - y;
                    if (px < 0 || px >= size || py < 0 || py >= size)
                    {
                        continue;
                    }

                    var fade = inHead ? 1f : Mathf.Clamp01(1f - y / (float)(radius + streak));
                    var alpha = (byte)Mathf.Clamp(fade * 150f, 0f, 255f);

                    var index = py * size + px;
                    if (pixels[index].a < alpha)
                    {
                        pixels[index] = new Color32(235, 240, 245, alpha);
                    }
                }
            }

            _droplets.SetPixels32(pixels);
            _droplets.Apply(updateMipmaps: false);
        }
    }
}
