using UnityEditor;
using UnityEngine;
using System;
using System.Linq.Expressions;
using System.Reflection;
using UnityEngine.Experimental.Rendering.HDPipeline;
using UnityEngine.Rendering;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Collections.Generic;

namespace UnityEditor.Experimental.Rendering.HDPipeline
{
    public class HDWizard : EditorWindow
    {
        //reflect internal legacy enum
        enum LightmapEncodingQualityCopy
        {
            Low = 0,
            Normal = 1,
            High = 2
        }

        static class Style
        {
            public static readonly GUIContent hdrpProjectSettingsPath = EditorGUIUtility.TrTextContent("Default Resources Folder");
            public static readonly GUIContent firstTimeInit = EditorGUIUtility.TrTextContent("Populate");
            public static readonly GUIContent defaultVolumeProfile = EditorGUIUtility.TrTextContent("Default Volume Profile", "Shared Volume Profile assigned on new created Volumes.");
            public static readonly GUIContent haveStartPopup = EditorGUIUtility.TrTextContent("Show on start");

            //configuration debugger
            public static readonly GUIContent ok = EditorGUIUtility.TrIconContent("Collab");
            public static readonly GUIContent fail = EditorGUIUtility.TrIconContent("CollabError");
            public static readonly GUIContent okForCurrentQuality = EditorGUIUtility.TrTextContent("OK for the current quality settings.");
            public static readonly GUIContent resolve = EditorGUIUtility.TrTextContent("Fix");
            public static readonly GUIContent resolveAll = EditorGUIUtility.TrTextContent("Fix All");
            public static readonly GUIContent resolveAllQuality = EditorGUIUtility.TrTextContent("Fix All Qualities");
            public static readonly GUIContent resolveCurrentQuality = EditorGUIUtility.TrTextContent("Fix Current Quality");
            public static readonly GUIContent allConfigurationLabel = EditorGUIUtility.TrTextContent("HDRP configuration");
            public const string allConfigurationError = "There is issue in your configuration. (See below for detail)";
            public static readonly GUIContent colorSpaceLabel = EditorGUIUtility.TrTextContent("Color space");
            public const string colorSpaceError = "Only linear color space supported!";
            public static readonly GUIContent lightmapLabel = EditorGUIUtility.TrTextContent("Lightmap encoding");
            public const string lightmapError = "Only high quality lightmap supported!";
            public static readonly GUIContent shadowLabel = EditorGUIUtility.TrTextContent("Shadows");
            public const string shadowError = "Shadow must be set to activated! (either on hard or soft)";
            public static readonly GUIContent shadowMaskLabel = EditorGUIUtility.TrTextContent("Shadowmask mode");
            public const string shadowMaskError = "Only distance shadowmask supported at the project level! (You can still change this per light.)";
            public static readonly GUIContent scriptingRuntimeVersionLabel = EditorGUIUtility.TrTextContent("Script runtime version");
            public const string scriptingRuntimeVersionError = "Script runtime version must be .Net 4.x or earlier!";
            public static readonly GUIContent hdrpAssetLabel = EditorGUIUtility.TrTextContent("Asset configuration");
            public const string hdrpAssetError = "There are issues in the HDRP asset configuration. (see below)";
            public static readonly GUIContent hdrpAssetUsedLabel = EditorGUIUtility.TrTextContent("Assigned");
            public const string hdrpAssetUsedError = "There is no HDRP asset assigned to the render pipeline!";
            public static readonly GUIContent hdrpAssetRuntimeResourcesLabel = EditorGUIUtility.TrTextContent("Runtime resources");
            public const string hdrpAssetRuntimeResourcesError = "There is an issue with the runtime resources!";
            public static readonly GUIContent hdrpAssetEditorResourcesLabel = EditorGUIUtility.TrTextContent("Editor resources");
            public const string hdrpAssetEditorResourcesError = "There is an issue with the editor resources!";
            public static readonly GUIContent hdrpAssetDiffusionProfileLabel = EditorGUIUtility.TrTextContent("Diffusion profile");
            public const string hdrpAssetDiffusionProfileError = "There is no diffusion profile assigned in the HDRP asset!";
            public static readonly GUIContent defaultVolumeProfileLabel = EditorGUIUtility.TrTextContent("Default volume profile");
            public const string defaultVolumeProfileError = "Default volume profile must be set to save disk space and share settings!";
        }

        static VolumeProfile s_DefaultVolumeProfile;

        Vector2 scrollPos;

        VolumeProfile defaultVolumeProfile;

        static Func<BuildTargetGroup, LightmapEncodingQualityCopy> GetLightmapEncodingQualityForPlatformGroup;
        static Action<BuildTargetGroup, LightmapEncodingQualityCopy> SetLightmapEncodingQualityForPlatformGroup;

        static HDWizard()
        {
            Type playerSettingsType = typeof(PlayerSettings);
            Type LightEncodingQualityType = playerSettingsType.Assembly.GetType("UnityEditor.LightmapEncodingQuality");
            var qualityVariable = Expression.Variable(LightEncodingQualityType, "quality_internal");
            var buildTargetGroupParameter = Expression.Parameter(typeof(BuildTargetGroup), "platformGroup");
            var qualityParameter = Expression.Parameter(typeof(LightmapEncodingQualityCopy), "quality");
            var getLightmapEncodingQualityForPlatformGroupInfo = playerSettingsType.GetMethod("GetLightmapEncodingQualityForPlatformGroup", BindingFlags.Static | BindingFlags.NonPublic);
            var setLightmapEncodingQualityForPlatformGroupInfo = playerSettingsType.GetMethod("SetLightmapEncodingQualityForPlatformGroup", BindingFlags.Static | BindingFlags.NonPublic);
            var getLightmapEncodingQualityForPlatformGroupBlock = Expression.Block(
                new[] { qualityVariable },
                Expression.Assign(qualityVariable, Expression.Call(getLightmapEncodingQualityForPlatformGroupInfo, buildTargetGroupParameter)),
                Expression.Convert(qualityVariable, typeof(LightmapEncodingQualityCopy))
                );
            var setLightmapEncodingQualityForPlatformGroupBlock = Expression.Block(
                new[] { qualityVariable },
                Expression.Assign(qualityVariable, Expression.Convert(qualityParameter, LightEncodingQualityType)),
                Expression.Call(setLightmapEncodingQualityForPlatformGroupInfo, buildTargetGroupParameter, qualityVariable)
                );
            var getLightmapEncodingQualityForPlatformGroupLambda = Expression.Lambda<Func<BuildTargetGroup, LightmapEncodingQualityCopy>>(getLightmapEncodingQualityForPlatformGroupBlock, buildTargetGroupParameter);
            var setLightmapEncodingQualityForPlatformGroupLambda = Expression.Lambda<Action<BuildTargetGroup, LightmapEncodingQualityCopy>>(setLightmapEncodingQualityForPlatformGroupBlock, buildTargetGroupParameter, qualityParameter);
            GetLightmapEncodingQualityForPlatformGroup = getLightmapEncodingQualityForPlatformGroupLambda.Compile();
            SetLightmapEncodingQualityForPlatformGroup = setLightmapEncodingQualityForPlatformGroupLambda.Compile();
        }

        [InitializeOnLoadMethod, Callbacks.DidReloadScripts]
        static void InitializeVolume()
        {
            Volume.defaultVolumeProfile = HDProjectSettings.defaultVolumeProfile;
        }

        [MenuItem("Window/Analysis/HDRP Wizard", priority = 113)]
        static void OpenWindow()
        {
            var window = GetWindow<HDWizard>("HDRP Wizard");
        }

        void OnGUI()
        {
            scrollPos = GUILayout.BeginScrollView(scrollPos);

            GUILayout.BeginHorizontal();
            EditorGUI.BeginChangeCheck();
            string changedProjectSettingsFolderPath = EditorGUILayout.DelayedTextField(Style.hdrpProjectSettingsPath, HDProjectSettings.projectSettingsFolderPath);
            if (EditorGUI.EndChangeCheck())
            {
                //TODO: ask if want to migrate folder content

                HDProjectSettings.projectSettingsFolderPath = changedProjectSettingsFolderPath;
            }
            if (GUILayout.Button(Style.firstTimeInit, EditorStyles.miniButton, GUILayout.Width(100), GUILayout.ExpandWidth(false)))
            {
                //TODO: create folder if needed and populate content
            }
            GUILayout.EndHorizontal();

            EditorGUI.BeginChangeCheck();
            VolumeProfile changedVolumeProfile = EditorGUILayout.ObjectField(Style.defaultVolumeProfile, HDProjectSettings.defaultVolumeProfile, typeof(VolumeProfile), allowSceneObjects: false) as VolumeProfile;
            if (EditorGUI.EndChangeCheck())
            {
                HDProjectSettings.defaultVolumeProfile = changedVolumeProfile;
                InitializeVolume();
            }

            EditorGUILayout.Space();
            DrawConfigInfo();
            
            GUILayout.FlexibleSpace();
            EditorGUI.BeginChangeCheck();
            bool changedHaveStatPopup = EditorGUILayout.Toggle(Style.haveStartPopup, HDProjectSettings.haveStartPopup);
            if (EditorGUI.EndChangeCheck())
            {
                HDProjectSettings.haveStartPopup = changedHaveStatPopup;
            }

            GUILayout.EndScrollView();
        }

        void DrawConfigInfo()
        {
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(Style.allConfigurationLabel, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            if (!allTester() && GUILayout.Button(Style.resolveAll, EditorStyles.miniButton, GUILayout.Width(100), GUILayout.ExpandWidth(false)))
                allResolver();
            EditorGUILayout.EndHorizontal();

            ++EditorGUI.indentLevel;
            DrawConfigInfoLine(Style.scriptingRuntimeVersionLabel, Style.scriptingRuntimeVersionError, Style.ok, Style.resolve, scriptRuntimeVersionTester, scriptRuntimeVersionResolver);
            DrawConfigInfoLine(Style.colorSpaceLabel, Style.colorSpaceError, Style.ok, Style.resolve, colorSpaceTester, colorSpaceResolver);
            DrawConfigInfoLine(Style.lightmapLabel, Style.lightmapError, Style.ok, Style.resolve, lightmapTester, lightmapResolver);
            DrawConfigInfoLine(Style.shadowLabel, Style.shadowError, Style.okForCurrentQuality, Style.resolveAllQuality, shadowTester, shadowResolver);
            DrawConfigInfoLine(Style.shadowMaskLabel, Style.shadowMaskError, Style.okForCurrentQuality, Style.resolveAllQuality, shadowmaskTester, shadowmaskResolver);
            DrawConfigInfoLine(Style.hdrpAssetLabel, Style.hdrpAssetError, Style.ok, Style.resolveAll, hdrpAssetTester, hdrpAssetResolver);
            ++EditorGUI.indentLevel;
            DrawConfigInfoLine(Style.hdrpAssetUsedLabel, Style.hdrpAssetUsedError, Style.ok, Style.resolve, hdrpAssetUsedTester, hdrpAssetUsedResolver);
            DrawConfigInfoLine(Style.hdrpAssetRuntimeResourcesLabel, Style.hdrpAssetRuntimeResourcesError, Style.ok, Style.resolve, hdrpAssetRuntimeResourcesTester, hdrpAssetRuntimeResourcesResolver);
            DrawConfigInfoLine(Style.hdrpAssetEditorResourcesLabel, Style.hdrpAssetEditorResourcesError, Style.ok, Style.resolve, hdrpAssetEditorResourcesTester, hdrpAssetEditorResourcesResolver);
            DrawConfigInfoLine(Style.hdrpAssetDiffusionProfileLabel, Style.hdrpAssetDiffusionProfileError, Style.ok, Style.resolve, hdrpAssetDiffusionProfileTester, hdrpAssetDiffusionProfileResolver);
            --EditorGUI.indentLevel;
            DrawConfigInfoLine(Style.defaultVolumeProfileLabel, Style.defaultVolumeProfileError, Style.ok, Style.resolve, defaultVolumeProfileTester, defaultVolumeProfileResolver);
            --EditorGUI.indentLevel;
        }

        void DrawConfigInfoLine(GUIContent label, string error, GUIContent ok, GUIContent resolverButtonLabel, Func<bool> tester, Action resolver, GUIContent AdditionalCheckButtonLabel = null, Func<bool> additionalTester = null)
        {
            bool wellConfigured = tester();
            EditorGUILayout.LabelField(label, wellConfigured ? Style.ok : Style.fail);
            if (wellConfigured)
                return;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.HelpBox(error, MessageType.Error);
            EditorGUILayout.BeginVertical(GUILayout.Width(108), GUILayout.ExpandWidth(false));
            EditorGUILayout.Space();
            if (GUILayout.Button(resolverButtonLabel, EditorStyles.miniButton))
                resolver();
            EditorGUILayout.EndVertical();
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.Space();
        }

        //T CreateOrLoad<T>(GUIContent label)
        //{

        //}

        bool allTester() =>
            scriptRuntimeVersionTester()
            && lightmapTester()
            && shadowTester()
            && shadowmaskTester()
            && colorSpaceTester()
            && hdrpAssetTester()
            && defaultVolumeProfileTester();
        void allResolver()
        {
            if (scriptRuntimeVersionTester())
                scriptRuntimeVersionResolver();
            if (colorSpaceTester())
                colorSpaceResolver();
            if (lightmapTester())
                lightmapResolver();
            if (shadowTester())
                shadowResolver();
            if (shadowmaskTester())
                shadowmaskResolver();
            if (hdrpAssetTester())
                hdrpAssetResolver();
            if (defaultVolumeProfileTester())
                defaultVolumeProfileResolver();
        }

        bool hdrpAssetTester() =>
            hdrpAssetUsedTester()
            && hdrpAssetRuntimeResourcesTester()
            && hdrpAssetEditorResourcesTester()
            && hdrpAssetDiffusionProfileTester();
        void hdrpAssetResolver()
        {
            if (hdrpAssetUsedTester())
                hdrpAssetUsedResolver();
            if (hdrpAssetRuntimeResourcesTester())
                hdrpAssetRuntimeResourcesResolver();
            if (hdrpAssetEditorResourcesTester())
                hdrpAssetEditorResourcesResolver();
            if (hdrpAssetDiffusionProfileTester())
                hdrpAssetDiffusionProfileResolver();
        }

        bool colorSpaceTester() => PlayerSettings.colorSpace == ColorSpace.Linear;
        void colorSpaceResolver() => PlayerSettings.colorSpace = ColorSpace.Linear;

        bool lightmapTester()
        {
            // Shame alert: plateform supporting Encodement are partly hardcoded
            // in editor (Standalone) and for the other part, it is all in internal code.
            return GetLightmapEncodingQualityForPlatformGroup(BuildTargetGroup.Standalone) == LightmapEncodingQualityCopy.High
                && GetLightmapEncodingQualityForPlatformGroup(BuildTargetGroup.Android) == LightmapEncodingQualityCopy.High
                && GetLightmapEncodingQualityForPlatformGroup(BuildTargetGroup.Lumin) == LightmapEncodingQualityCopy.High
                && GetLightmapEncodingQualityForPlatformGroup(BuildTargetGroup.WSA) == LightmapEncodingQualityCopy.High;
        }
        void lightmapResolver()
        {
            SetLightmapEncodingQualityForPlatformGroup(BuildTargetGroup.Standalone, LightmapEncodingQualityCopy.High);
            SetLightmapEncodingQualityForPlatformGroup(BuildTargetGroup.Android, LightmapEncodingQualityCopy.High);
            SetLightmapEncodingQualityForPlatformGroup(BuildTargetGroup.Lumin, LightmapEncodingQualityCopy.High);
            SetLightmapEncodingQualityForPlatformGroup(BuildTargetGroup.WSA, LightmapEncodingQualityCopy.High);
        }

        bool shadowTester()
        {
            //QualitySettings.SetQualityLevel.set quality is too costy to be use at frame
            return QualitySettings.shadows == ShadowQuality.All;
        }
        void shadowResolver()
        {
            int currentQuality = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; ++i)
            {
                QualitySettings.SetQualityLevel(i, applyExpensiveChanges: false);
                QualitySettings.shadows = ShadowQuality.All;
            }
            QualitySettings.SetQualityLevel(currentQuality, applyExpensiveChanges: false);
        }

        bool shadowmaskTester()
        {
            //QualitySettings.SetQualityLevel.set quality is too costy to be use at frame
            return QualitySettings.shadowmaskMode == ShadowmaskMode.DistanceShadowmask;
        }
        void shadowmaskResolver()
        {
            int currentQuality = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; ++i)
            {
                QualitySettings.SetQualityLevel(i, applyExpensiveChanges: false);
                QualitySettings.shadowmaskMode = ShadowmaskMode.DistanceShadowmask;
            }
            QualitySettings.SetQualityLevel(currentQuality, applyExpensiveChanges: false);
        }

        bool scriptRuntimeVersionTester() => PlayerSettings.scriptingRuntimeVersion == ScriptingRuntimeVersion.Latest;
        void scriptRuntimeVersionResolver() => PlayerSettings.scriptingRuntimeVersion = ScriptingRuntimeVersion.Latest;

        bool hdrpAssetUsedTester() => GraphicsSettings.renderPipelineAsset != null && GraphicsSettings.renderPipelineAsset is HDRenderPipelineAsset;
        void hdrpAssetUsedResolver()
        {
            //ask to use one or create one
        }

        bool hdrpAssetRuntimeResourcesTester() =>
            hdrpAssetUsedTester()
            || (GraphicsSettings.renderPipelineAsset as HDRenderPipelineAsset).renderPipelineResources != null;
        void hdrpAssetRuntimeResourcesResolver()
        {
            if (!hdrpAssetUsedTester())
                hdrpAssetUsedResolver();
            (GraphicsSettings.renderPipelineAsset as HDRenderPipelineAsset).renderPipelineResources
                = AssetDatabase.LoadAssetAtPath<RenderPipelineResources>(HDUtils.GetHDRenderPipelinePath() + "Runtime/RenderPipelineResources/HDRenderPipelineResources.asset");
        }

        bool hdrpAssetEditorResourcesTester() =>
            hdrpAssetUsedTester()
            || (GraphicsSettings.renderPipelineAsset as HDRenderPipelineAsset).renderPipelineEditorResources != null;
        void hdrpAssetEditorResourcesResolver()
        {
            if (!hdrpAssetUsedTester())
                hdrpAssetUsedResolver();
            (GraphicsSettings.renderPipelineAsset as HDRenderPipelineAsset).renderPipelineEditorResources
                = AssetDatabase.LoadAssetAtPath<HDRenderPipelineEditorResources>(HDUtils.GetHDRenderPipelinePath() + "Editor/RenderPipelineResources/HDRenderPipelineEditorResources.asset");
        }

        bool hdrpAssetDiffusionProfileTester() =>
            hdrpAssetUsedTester()
            || (GraphicsSettings.renderPipelineAsset as HDRenderPipelineAsset).diffusionProfileSettings != null;
        void hdrpAssetDiffusionProfileResolver()
        {
            if (!hdrpAssetUsedTester())
                hdrpAssetUsedResolver();

            //ask to use one or create one
        }

        bool defaultVolumeProfileTester() => HDProjectSettings.defaultVolumeProfile != null;
        void defaultVolumeProfileResolver()
        {
            //ask to use one
        }
    }
}
