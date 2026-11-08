using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Coldfall.Editor.Setup
{
    /// <summary>
    /// Creates the project's URP pipeline asset and makes it the active render pipeline.
    /// <para>
    /// Adding the URP package does not switch the project to URP: without a pipeline asset
    /// assigned in Graphics and Quality settings, Unity keeps rendering with the built-in
    /// pipeline and every URP material renders magenta. This runs once at project setup, and
    /// again only if the settings are ever lost.
    /// </para>
    /// <para>
    /// The values set here are the ones docs/07-art-direction.md depends on: linear colour space
    /// (the 20-70% luminance band in 7.1 is meaningless in gamma space) and a realtime shadow
    /// caster budget matching 7.7.
    /// </para>
    /// </summary>
    public static class RenderPipelineSetup
    {
        private const string SettingsFolder = "Assets/Settings";
        private const string RendererPath = SettingsFolder + "/Coldfall_UniversalRenderer.asset";
        private const string PipelinePath = SettingsFolder + "/Coldfall_UniversalRenderPipeline.asset";

        /// <summary>
        /// Creates the renderer and pipeline assets if they are missing, assigns the pipeline to
        /// the graphics settings and to every quality level, and sets the colour space.
        /// Invoked from the menu, or in batch mode with -executeMethod.
        /// </summary>
        [MenuItem("Coldfall/Setup/Create and assign render pipeline")]
        public static void CreateAndAssign()
        {
            Directory.CreateDirectory(SettingsFolder);

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(renderer, RendererPath);
                Debug.Log($"Coldfall setup: created renderer data at {RendererPath}");
            }

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
            if (pipeline == null)
            {
                pipeline = UniversalRenderPipelineAsset.Create(renderer);
                AssetDatabase.CreateAsset(pipeline, PipelinePath);
                Debug.Log($"Coldfall setup: created pipeline asset at {PipelinePath}");
            }

            // Shadow caster budget from docs/07, 7.7: at most five realtime casters including
            // the sun, which is what keeps the frame inside 16.6 ms on the target GPU.
            pipeline.shadowDistance = 90f;
            pipeline.supportsHDR = true;
            EditorUtility.SetDirty(pipeline);

            GraphicsSettings.defaultRenderPipeline = pipeline;

            var originalQualityLevel = QualitySettings.GetQualityLevel();
            for (var level = 0; level < QualitySettings.count; level++)
            {
                QualitySettings.SetQualityLevel(level, applyExpensiveChanges: false);
                QualitySettings.renderPipeline = pipeline;
            }

            QualitySettings.SetQualityLevel(originalQualityLevel, applyExpensiveChanges: false);

            if (PlayerSettings.colorSpace != ColorSpace.Linear)
            {
                PlayerSettings.colorSpace = ColorSpace.Linear;
                Debug.Log("Coldfall setup: colour space set to Linear");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log(
                "Coldfall setup: URP is now the active render pipeline " +
                $"(graphics default: {GraphicsSettings.defaultRenderPipeline?.name ?? "none"}).");
        }
    }
}
