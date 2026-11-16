using Coldfall.Player;
using Coldfall.Sim;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Coldfall.Presentation
{
    /// <summary>
    /// The always-on status channel of docs/08-audio-and-ui.md, 8.5: screen effects authored as
    /// gameplay signals rather than as mood.
    /// <para>
    /// Docs/07, 7.10 sets the rule these follow, and one of them is a deliberate cruelty. At
    /// severe hypothermia the grade <b>calms down</b>: colour returns, the image warms, and the
    /// only thing that worsens is the tunnelling. The player who feels the screen settle is
    /// being told they are about to die, in the language of how it actually feels. Nothing here
    /// may fully obscure a threat.
    /// </para>
    /// <para>
    /// The volume and its profile are built at runtime, so no one has to remember to author a
    /// post-processing asset for a scene to report the player's condition correctly.
    /// </para>
    /// </summary>
    public sealed class ConditionScreenEffects : MonoBehaviour
    {
        [SerializeField] private SurvivalCharacter character;

        [Tooltip("How fast the effects follow the condition. Slow enough to feel like a body.")]
        [SerializeField] private float responseSpeed = 0.6f;

        private Volume _volume;
        private ColorAdjustments _colour;
        private Vignette _vignette;

        private float _saturation;
        private float _vignetteIntensity;
        private float _warmth;

        private void Awake()
        {
            character ??= FindFirstObjectByType<SurvivalCharacter>();

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "Coldfall Condition (runtime)";

            _colour = profile.Add<ColorAdjustments>(overrides: true);
            _colour.saturation.overrideState = true;
            _colour.saturation.value = 0f;

            _vignette = profile.Add<Vignette>(overrides: true);
            _vignette.intensity.overrideState = true;
            _vignette.intensity.value = 0.15f;
            _vignette.smoothness.overrideState = true;
            _vignette.smoothness.value = 0.5f;

            var host = new GameObject("ConditionVolume");
            host.transform.SetParent(transform, worldPositionStays: false);
            host.hideFlags = HideFlags.DontSave;

            _volume = host.AddComponent<Volume>();
            _volume.isGlobal = true;
            _volume.priority = 100f;
            _volume.profile = profile;
        }

        private void Update()
        {
            if (character == null || _colour == null)
            {
                return;
            }

            var stage = character.Core.Stage;

            var targetSaturation = SaturationFor(stage);
            var targetVignette = VignetteFor(stage);
            var targetWarmth = WarmthFor(stage);

            var t = 1f - Mathf.Exp(-responseSpeed * Time.deltaTime);
            _saturation = Mathf.Lerp(_saturation, targetSaturation, t);
            _vignetteIntensity = Mathf.Lerp(_vignetteIntensity, targetVignette, t);
            _warmth = Mathf.Lerp(_warmth, targetWarmth, t);

            _colour.saturation.value = _saturation;
            _colour.postExposure.overrideState = true;
            _colour.postExposure.value = _warmth * 0.01f;
            _vignette.intensity.value = _vignetteIntensity;
        }

        /// <summary>
        /// Saturation by stage. Falls as the cold deepens, then <b>partly returns</b> at severe:
        /// that return is the paradoxical warmth of docs/03, 3.3 made visible.
        /// </summary>
        public static float SaturationFor(HypothermiaStage stage)
        {
            return stage switch
            {
                HypothermiaStage.Normal => 0f,
                HypothermiaStage.Cold => -12f,
                HypothermiaStage.Mild => -34f,
                HypothermiaStage.Moderate => -58f,
                HypothermiaStage.Severe => -30f,
                _ => -80f,
            };
        }

        /// <summary>
        /// Vignette by stage. The only thing that keeps worsening at severe, because tunnel
        /// vision is the one honest signal left.
        /// </summary>
        public static float VignetteFor(HypothermiaStage stage)
        {
            return stage switch
            {
                HypothermiaStage.Normal => 0.15f,
                HypothermiaStage.Cold => 0.20f,
                HypothermiaStage.Mild => 0.28f,
                HypothermiaStage.Moderate => 0.36f,
                HypothermiaStage.Severe => 0.52f,
                _ => 0.65f,
            };
        }

        /// <summary>Exposure lift by stage. Rises at severe, which is the lie.</summary>
        public static float WarmthFor(HypothermiaStage stage)
        {
            return stage switch
            {
                HypothermiaStage.Severe => 18f,
                HypothermiaStage.Moderate => -6f,
                HypothermiaStage.Mild => -4f,
                _ => 0f,
            };
        }
    }
}
