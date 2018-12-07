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
            public static readonly GUIContent firstTimeInit = EditorGUIUtility.TrTextContent("Populate / Reset");
            public static readonly GUIContent defaultVolumeProfile = EditorGUIUtility.TrTextContent("Default Volume Profile", "Shared Volume Profile assigned on new created Volumes.");
            public static readonly GUIContent haveStartPopup = EditorGUIUtility.TrTextContent("Show on start");

            //configuration debugger
            public static readonly GUIContent ok = EditorGUIUtility.TrIconContent("Collab");
            public static readonly GUIContent fail = EditorGUIUtility.TrIconContent("CollabError");
            public static readonly GUIContent resolve = EditorGUIUtility.TrTextContent("Fix");
            public static readonly GUIContent resolveAll = EditorGUIUtility.TrTextContent("Fix All");
            public static readonly GUIContent resolveAllQuality = EditorGUIUtility.TrTextContent("Fix All Qualities");
            public static readonly GUIContent resolveAllBuildTarget = EditorGUIUtility.TrTextContent("Fix All Platforms");
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

            public const string hdrpAssetDisplayDialogTitle = "Create or Load HDRenderPipelineAsset";
            public const string hdrpAssetDisplayDialogContent = "Do you want to create a fresh HDRenderPipelineAsset in the default resource folder and automatically assign it?";
            public const string diffusionProfileSettingsDisplayDialogTitle = "Create or Load DiffusionProfileSettings";
            public const string diffusionProfileSettingsDisplayDialogContent = "Do you want to create a fresh DiffusionProfileSettings in the default resource folder and automatically assign it?";
            public const string displayDialogCreate = "Create One";
            public const string displayDialogLoad = "Load One";
        }

        //utility class to show only non scene object selection
        static class ObjectSelector
        {
            static Action<UnityEngine.Object, Type> ShowObjectSelector;
            static Func<UnityEngine.Object> GetCurrentObject;
            static Func<int> GetSelectorID;
            static Action<int> SetSelectorID;

            const string ObjectSelectorUpdatedCommand = "ObjectSelectorUpdated";

            static int id;

            static int selectorID { get => GetSelectorID(); set => SetSelectorID(value); }

            static ObjectSelector()
            {
                Type playerSettingsType = typeof(PlayerSettings);
                Type objectSelectorType = playerSettingsType.Assembly.GetType("UnityEditor.ObjectSelector");
                var instanceObjectSelectorInfo = objectSelectorType.GetProperty("get", BindingFlags.Static | BindingFlags.Public);
                var showInfo = objectSelectorType.GetMethod("Show", new[] { typeof(UnityEngine.Object), typeof(Type), typeof(SerializedProperty), typeof(bool) });
                var objectSelectorVariable = Expression.Variable(objectSelectorType, "objectSelector");
                var objectParameter = Expression.Parameter(typeof(UnityEngine.Object), "unityObject");
                var typeParameter = Expression.Parameter(typeof(Type), "type");
                var showObjectSelectorBlock = Expression.Block(
                    new[] { objectSelectorVariable },
                    Expression.Assign(objectSelectorVariable, Expression.Call(null, instanceObjectSelectorInfo.GetGetMethod())),
                    Expression.Call(objectSelectorVariable, showInfo, objectParameter, typeParameter, Expression.Constant(null, typeof(SerializedProperty)), Expression.Constant(false))
                    );
                var showObjectSelectorLambda = Expression.Lambda<Action<UnityEngine.Object, Type>>(showObjectSelectorBlock, objectParameter, typeParameter);
                ShowObjectSelector = showObjectSelectorLambda.Compile();

                var instanceCall = Expression.Call(null, instanceObjectSelectorInfo.GetGetMethod());
                var objectSelectorIDField = Expression.Field(instanceCall, "objectSelectorID");
                var getSelectorIDLambda = Expression.Lambda<Func<int>>(objectSelectorIDField);
                GetSelectorID = getSelectorIDLambda.Compile();

                var inSelectorIDParam = Expression.Parameter(typeof(int), "value");
                var setSelectorIDLambda = Expression.Lambda<Action<int>>(Expression.Assign(objectSelectorIDField, inSelectorIDParam), inSelectorIDParam);
                SetSelectorID = setSelectorIDLambda.Compile();

                var getCurrentObjectInfo = objectSelectorType.GetMethod("GetCurrentObject");
                var getCurrentObjectLambda = Expression.Lambda<Func<UnityEngine.Object>>(Expression.Call(null, getCurrentObjectInfo));
                GetCurrentObject = getCurrentObjectLambda.Compile();
            }

            public static void Show(UnityEngine.Object obj, Type type)
            {
                id = GUIUtility.GetControlID("s_ObjectFieldHash".GetHashCode(), FocusType.Keyboard);
                GUIUtility.keyboardControl = id;
                ShowObjectSelector(obj, type);
                selectorID = id;
            }

            public static void CheckAssignationEvent<T>(Action<T> assignator)
                where T : UnityEngine.Object
            {
                Event evt = Event.current;
                if (evt.type != EventType.ExecuteCommand)
                    return;
                string commandName = evt.commandName;
                if (commandName != ObjectSelectorUpdatedCommand || selectorID != id)
                    return;
                T current = GetCurrentObject() as T;
                if (current == null)
                    return;
                assignator(current);
                GUI.changed = true;
                evt.Use();
            }
        }

        static VolumeProfile s_DefaultVolumeProfile;

        Vector2 scrollPos;
        Rect lastVolumeRect;

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
                HDProjectSettings.projectSettingsFolderPath = changedProjectSettingsFolderPath;
            }
            if (GUILayout.Button(Style.firstTimeInit, EditorStyles.miniButton, GUILayout.Width(100), GUILayout.ExpandWidth(false)))
            {
                if (!AssetDatabase.IsValidFolder("Assets/" + HDProjectSettings.projectSettingsFolderPath))
                    AssetDatabase.CreateFolder("Assets", HDProjectSettings.projectSettingsFolderPath);
                var hdrpAsset = ScriptableObject.CreateInstance<HDRenderPipelineAsset>();
                hdrpAsset.name = "HDRenderPipelineAsset";

                string defaultVolumeProfilePath = "Assets/" + HDProjectSettings.projectSettingsFolderPath + "/" + hdrpAsset.renderPipelineEditorResources.defaultVolumeProfile.name + ".asset";
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(hdrpAsset.renderPipelineEditorResources.defaultVolumeProfile), defaultVolumeProfilePath);
                string defaultDiffusionProfileSettingsPath = "Assets/" + HDProjectSettings.projectSettingsFolderPath + "/" + hdrpAsset.renderPipelineEditorResources.defaultDiffusionProfileSettings.name + ".asset";
                AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(hdrpAsset.renderPipelineEditorResources.defaultDiffusionProfileSettings), defaultDiffusionProfileSettingsPath);
                AssetDatabase.Refresh();
                
                HDProjectSettings.defaultVolumeProfile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(defaultVolumeProfilePath);

                var defaultDiffusionProfile = AssetDatabase.LoadAssetAtPath<DiffusionProfileSettings>(defaultDiffusionProfileSettingsPath);
                hdrpAsset.diffusionProfileSettings = defaultDiffusionProfile;

                AssetDatabase.CreateAsset(hdrpAsset, "Assets/" + HDProjectSettings.projectSettingsFolderPath + "/" + hdrpAsset.name + ".asset");
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();

                GraphicsSettings.renderPipelineAsset = hdrpAsset;
                if (!hdrpAssetRuntimeResourcesTester())
                    hdrpAssetRuntimeResourcesResolver();
                if (!hdrpAssetEditorResourcesTester())
                    hdrpAssetEditorResourcesResolver();
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

            // check assignation resolution from Selector
            ObjectSelector.CheckAssignationEvent<VolumeProfile>(x => HDProjectSettings.defaultVolumeProfile = x);
            ObjectSelector.CheckAssignationEvent<HDRenderPipelineAsset>(x => GraphicsSettings.renderPipelineAsset = x);
            ObjectSelector.CheckAssignationEvent<DiffusionProfileSettings>(x =>
            {
                if (GraphicsSettings.renderPipelineAsset == null || !(GraphicsSettings.renderPipelineAsset is HDRenderPipelineAsset))
                    return;
                (GraphicsSettings.renderPipelineAsset as HDRenderPipelineAsset).diffusionProfileSettings = x;
            });
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
            DrawConfigInfoLine(Style.lightmapLabel, Style.lightmapError, Style.ok, Style.resolveAllBuildTarget, lightmapTester, lightmapResolver);
            DrawConfigInfoLine(Style.shadowLabel, Style.shadowError, Style.ok, Style.resolveAllQuality, shadowTester, shadowResolver);
            DrawConfigInfoLine(Style.shadowMaskLabel, Style.shadowMaskError, Style.ok, Style.resolveAllQuality, shadowmaskTester, shadowmaskResolver);
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

        void CreateOrLoad<T>()
            where T : ScriptableObject
        {
            string title;
            string content;
            UnityEngine.Object target;
            if (typeof(T) == typeof(HDRenderPipelineAsset))
            {
                title = Style.hdrpAssetDisplayDialogTitle;
                content = Style.hdrpAssetDisplayDialogContent;
                target = GraphicsSettings.renderPipelineAsset as HDRenderPipelineAsset;
            }
            else if (typeof(T) == typeof(DiffusionProfileSettings))
            {
                if (GraphicsSettings.renderPipelineAsset == null || !(GraphicsSettings.renderPipelineAsset is HDRenderPipelineAsset))
                    throw new Exception("Cannot resolve diffusion profile while HDRenderPipeline is not set!");
                title = Style.diffusionProfileSettingsDisplayDialogTitle;
                content = Style.diffusionProfileSettingsDisplayDialogContent;
                target = (GraphicsSettings.renderPipelineAsset as HDRenderPipelineAsset).diffusionProfileSettings;
            }
            else
                throw new ArgumentException("Unknown type used");

            switch(EditorUtility.DisplayDialogComplex(title, content, Style.displayDialogCreate, "Cancel", Style.displayDialogLoad))
            {
                case 0: //create
                    if (!AssetDatabase.IsValidFolder("Assets/" + HDProjectSettings.projectSettingsFolderPath))
                        AssetDatabase.CreateFolder("Assets", HDProjectSettings.projectSettingsFolderPath);
                    var asset = ScriptableObject.CreateInstance<T>();
                    asset.name = typeof(T).Name;
                    AssetDatabase.CreateAsset(asset, "Assets/" + HDProjectSettings.projectSettingsFolderPath + "/" + asset.name + ".asset");
                    AssetDatabase.SaveAssets();
                    AssetDatabase.Refresh();

                    if (typeof(T) == typeof(HDRenderPipelineAsset))
                        GraphicsSettings.renderPipelineAsset = asset as HDRenderPipelineAsset;
                    else if (typeof(T) == typeof(DiffusionProfileSettings))
                        (GraphicsSettings.renderPipelineAsset as HDRenderPipelineAsset).diffusionProfileSettings = asset as DiffusionProfileSettings;
                    break;
                case 1: //cancel
                    break;
                case 2: //Load
                    ObjectSelector.Show(target, typeof(T));
                    break;
                default:
                    throw new ArgumentException("Unrecognized option");
            }
        }

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
            if (!scriptRuntimeVersionTester())
                scriptRuntimeVersionResolver();
            if (!colorSpaceTester())
                colorSpaceResolver();
            if (!lightmapTester())
                lightmapResolver();
            if (!shadowTester())
                shadowResolver();
            if (!shadowmaskTester())
                shadowmaskResolver();
            if (!hdrpAssetTester())
                hdrpAssetResolver();
            if (!defaultVolumeProfileTester())
                defaultVolumeProfileResolver();
        }

        bool hdrpAssetTester() =>
            hdrpAssetUsedTester()
            && hdrpAssetRuntimeResourcesTester()
            && hdrpAssetEditorResourcesTester()
            && hdrpAssetDiffusionProfileTester();
        void hdrpAssetResolver()
        {
            if (!hdrpAssetUsedTester())
                hdrpAssetUsedResolver();
            if (!hdrpAssetRuntimeResourcesTester())
                hdrpAssetRuntimeResourcesResolver();
            if (!hdrpAssetEditorResourcesTester())
                hdrpAssetEditorResourcesResolver();
            if (!hdrpAssetDiffusionProfileTester())
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
        void hdrpAssetUsedResolver() => CreateOrLoad<HDRenderPipelineAsset>();

        bool hdrpAssetRuntimeResourcesTester() =>
            hdrpAssetUsedTester()
            && (GraphicsSettings.renderPipelineAsset as HDRenderPipelineAsset).renderPipelineResources != null;
        void hdrpAssetRuntimeResourcesResolver()
        {
            if (!hdrpAssetUsedTester())
                hdrpAssetUsedResolver();
            (GraphicsSettings.renderPipelineAsset as HDRenderPipelineAsset).renderPipelineResources
                = AssetDatabase.LoadAssetAtPath<RenderPipelineResources>(HDUtils.GetHDRenderPipelinePath() + "Runtime/RenderPipelineResources/HDRenderPipelineResources.asset");
        }

        bool hdrpAssetEditorResourcesTester() =>
            hdrpAssetUsedTester()
            && (GraphicsSettings.renderPipelineAsset as HDRenderPipelineAsset).renderPipelineEditorResources != null;
        void hdrpAssetEditorResourcesResolver()
        {
            if (!hdrpAssetUsedTester())
                hdrpAssetUsedResolver();
            (GraphicsSettings.renderPipelineAsset as HDRenderPipelineAsset).renderPipelineEditorResources
                = AssetDatabase.LoadAssetAtPath<HDRenderPipelineEditorResources>(HDUtils.GetHDRenderPipelinePath() + "Editor/RenderPipelineResources/HDRenderPipelineEditorResources.asset");
        }

        bool hdrpAssetDiffusionProfileTester() =>
            hdrpAssetUsedTester()
            && (GraphicsSettings.renderPipelineAsset as HDRenderPipelineAsset).diffusionProfileSettings != null;
        void hdrpAssetDiffusionProfileResolver()
        {
            if (!hdrpAssetUsedTester())
                hdrpAssetUsedResolver();

            CreateOrLoad<DiffusionProfileSettings>();
        }

        bool defaultVolumeProfileTester() => HDProjectSettings.defaultVolumeProfile != null;
        void defaultVolumeProfileResolver() => ObjectSelector.Show(HDProjectSettings.defaultVolumeProfile, typeof(VolumeProfile));
    }
}

