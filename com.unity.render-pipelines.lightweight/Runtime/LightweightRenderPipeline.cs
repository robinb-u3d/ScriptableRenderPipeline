using System;
using System.Collections.Generic;
using Unity.Collections;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Experimental.Rendering.LightweightPipeline;
#endif
using UnityEngine.Rendering;
using UnityEngine.Rendering.PostProcessing;
using UnityEngine.Experimental.GlobalIllumination;
using Lightmapping = UnityEngine.Experimental.GlobalIllumination.Lightmapping;

namespace UnityEngine.Experimental.Rendering.LightweightPipeline
{
    public interface IBeforeCameraRender
    {
        void ExecuteBeforeCameraRender(LightweightRenderPipeline pipelineInstance, ScriptableRenderContext context, Camera camera);
    }

    public sealed partial class LightweightRenderPipeline : RenderPipeline
    {
        static class PerFrameBuffer
        {
            public static int _GlossyEnvironmentColor;
            public static int _SubtractiveShadowColor;
        }

        static class PerCameraBuffer
        {
            // TODO: This needs to account for stereo rendering
            public static int _ProjMatrix;
            public static int _ViewProjMatrix;
            public static int _PrevViewProjMatrix;
            public static int _NonJitteredViewProjMatrix;
            public static int _InvCameraViewProj;

            public static int _ScaledScreenParams;
            public static int _ProjectionParams;
            // public static int _TaaFrameRotation; - MATT: Add TAA support
        }

        //    float4x4 _ViewMatrixStereo[2];
        //    float4x4 _ProjMatrixStereo[2];
        //    float4x4 _ViewProjMatrixStereo[2];
        //    float4x4 _PrevViewProjMatrixStereo[2];
        //    float4x4 _NonJitteredViewProjMatrixStereo[2];
        //    float4x4 _InvViewMatrixStereo[2];
        //    float4x4 _InvProjMatrixStereo[2];
        //    float4x4 _InvViewProjMatrixStereo[2];
        static class PerCameraStereoBuffer
        {
            public static int _ProjMatrixStereo;
            public static int _ViewProjMatrixStereo;
            public static int _NonJitteredViewProjMatrixStereo;
            public static int _PrevViewProjMatrixStereo;
            public static int _InvCameraViewProjMatrixStereo;

            public static int _CameraProjection;
            public static int _CameraInvProjection;
        }

        private static IRendererSetup s_DefaultRendererSetup;
        private static IRendererSetup defaultRendererSetup
        {
            get
            {
                if (s_DefaultRendererSetup == null)
                    s_DefaultRendererSetup = new DefaultRendererSetup();

                return s_DefaultRendererSetup;
            }
        }

        const string k_RenderCameraTag = "Render Camera";
        CullResults m_CullResults;

        public ScriptableRenderer renderer { get; private set; }
        PipelineSettings settings { get; set; }

        // Used to detect frame changes
        uint m_FrameCount;
        float m_LastTime, m_Time;

        internal struct PipelineSettings
        {
            public bool supportsCameraDepthTexture { get; private set; }
            public bool supportsCameraOpaqueTexture { get; private set; }
            public Downsampling opaqueDownsampling { get; private set; }
            public bool supportsCameraMotionVectorsTexture { get; private set; }
            public bool supportsHDR { get; private set; }
            public int msaaSampleCount { get; private set; }
            public float renderScale { get; private set; }
            public LightRenderingMode mainLightRenderingMode { get; private set; }
            public bool supportsMainLightShadows { get; private set; }
            public int mainLightShadowmapResolution { get; private set; }
            public LightRenderingMode additionalLightsRenderingMode { get; private set; }
            public int maxAdditionalLights { get; private set; }
            public bool supportsAdditionalLightShadows { get; private set; }
            public int additionalLightsShadowmapResolution { get; private set; }
            public float shadowDistance { get; private set; }
            public int cascadeCount { get; private set; }
            public float cascade2Split { get; private set; }
            public Vector3 cascade4Split { get; private set; }
            public float shadowDepthBias { get; private set; }
            public float shadowNormalBias { get; private set; }
            public bool supportsSoftShadows { get; private set; }
            public bool supportsDynamicBatching { get; private set; }
            public bool mixedLightingSupported { get; private set; }

            public static PipelineSettings Create(LightweightRenderPipelineAsset asset)
            {
                var cache = new PipelineSettings();
                // General settings
                cache.supportsCameraDepthTexture = asset.supportsCameraDepthTexture;
                cache.supportsCameraOpaqueTexture = asset.supportsCameraOpaqueTexture;
                cache.opaqueDownsampling = asset.opaqueDownsampling;
                cache.supportsCameraMotionVectorsTexture = asset.supportsCameraMotionVectorsTexture;

                // Quality settings
                cache.msaaSampleCount = asset.msaaSampleCount;
                cache.supportsHDR = asset.supportsHDR;
                cache.renderScale = asset.renderScale;

                // Main directional light settings
                cache.mainLightRenderingMode = asset.mainLightRenderingMode;
                cache.supportsMainLightShadows = asset.supportsMainLightShadows;
                cache.mainLightShadowmapResolution = asset.mainLightShadowmapResolution;

                // Additional light settings
                cache.additionalLightsRenderingMode = asset.additionalLightsRenderingMode;
                cache.maxAdditionalLights = asset.maxAdditionalLightsCount;
                cache.supportsAdditionalLightShadows = asset.supportsAdditionalLightShadows;
                cache.additionalLightsShadowmapResolution = asset.additionalLightsShadowmapResolution;

                // Shadow settings
                cache.shadowDistance = asset.shadowDistance;
                cache.cascadeCount = asset.cascadeCount;
                cache.cascade2Split = asset.cascade2Split;
                cache.cascade4Split = asset.cascade4Split;
                cache.shadowDepthBias = asset.shadowDepthBias;
                cache.shadowNormalBias = asset.shadowNormalBias;
                cache.supportsSoftShadows = asset.supportsSoftShadows;

                // Advanced settings
                cache.supportsDynamicBatching = asset.supportsDynamicBatching;
                cache.mixedLightingSupported = asset.supportsMixedLighting;

                return cache;
            }
        }

        public LightweightRenderPipeline(LightweightRenderPipelineAsset asset)
        {
            settings = PipelineSettings.Create(asset);
            renderer = new ScriptableRenderer(asset);

            SetSupportedRenderingFeatures();

            PerFrameBuffer._GlossyEnvironmentColor = Shader.PropertyToID("_GlossyEnvironmentColor");
            PerFrameBuffer._SubtractiveShadowColor = Shader.PropertyToID("_SubtractiveShadowColor");

            PerCameraBuffer._InvCameraViewProj = Shader.PropertyToID("_InvCameraViewProj");
            PerCameraBuffer._ScaledScreenParams = Shader.PropertyToID("_ScaledScreenParams");
            PerCameraBuffer._NonJitteredViewProjMatrix = Shader.PropertyToID("_NonJitteredViewProjMatrix");
            PerCameraBuffer._PrevViewProjMatrix = Shader.PropertyToID("_PrevViewProjMatrix");

            PerCameraBuffer._ProjMatrix = Shader.PropertyToID("_ProjMatrix");
            PerCameraBuffer._ViewProjMatrix = Shader.PropertyToID("_ViewProjMatrix");
            PerCameraBuffer._ProjectionParams = Shader.PropertyToID("_ProjectionParams");

            // Stereo matricies
            PerCameraStereoBuffer._InvCameraViewProjMatrixStereo = Shader.PropertyToID("_InvCameraViewProjStereo");
            PerCameraStereoBuffer._NonJitteredViewProjMatrixStereo = Shader.PropertyToID("_NonJitteredViewProjMatrixStereo");
            PerCameraStereoBuffer._PrevViewProjMatrixStereo = Shader.PropertyToID("_PrevViewProjMatrixStereo");
            PerCameraStereoBuffer._ProjMatrixStereo = Shader.PropertyToID("_ProjMatrixStereo");
            PerCameraStereoBuffer._ViewProjMatrixStereo = Shader.PropertyToID("_ViewProjMatrixStereo");
            PerCameraStereoBuffer._CameraProjection = Shader.PropertyToID("_CameraProjection");
            PerCameraStereoBuffer._CameraInvProjection = Shader.PropertyToID("_CameraInvProjection");

            //PerCameraBuffer._TaaFrameRotation = Shader.PropertyToID("_TaaFrameRotation"); - MATT: Add TAA support

            // Let engine know we have MSAA on for cases where we support MSAA backbuffer
            if (QualitySettings.antiAliasing != settings.msaaSampleCount)
                QualitySettings.antiAliasing = settings.msaaSampleCount;

            Shader.globalRenderPipeline = "LightweightPipeline";

            Lightmapping.SetDelegate(lightsDelegate);
        }

        public sealed override void Dispose()
        {
            base.Dispose();
            Shader.globalRenderPipeline = "";
            SupportedRenderingFeatures.active = new SupportedRenderingFeatures();

#if UNITY_EDITOR
            SceneViewDrawMode.ResetDrawMode();
#endif

            renderer.Dispose();

            Lightmapping.ResetDelegate();

            ClearAllMotionVectorData();
        }

        public override void Render(ScriptableRenderContext renderContext, Camera[] cameras)
        {
            base.Render(renderContext, cameras);
            BeginFrameRendering(cameras);

            {
                // SRP.Render() can be called several times per frame.
                // Also, most Time variables do not consistently update in the Scene View.
                // This makes reliable detection of the start of the new frame VERY hard.
                // One of the exceptions is 'Time.realtimeSinceStartup'.
                // Therefore, outside of the Play Mode we update the time at 60 fps,
                // and in the Play Mode we rely on 'Time.frameCount'.
                float t = Time.realtimeSinceStartup;
                uint c = (uint)Time.frameCount;
                bool newFrame;
                if (Application.isPlaying)
                {
                    newFrame = m_FrameCount != c;
                    m_FrameCount = c;
                }
                else
                {
                    newFrame = (t - m_Time) > 0.0166f;
                    if (newFrame)
                        m_FrameCount++;
                }
                if (newFrame)
                {
                    CleanUnusedMotionVectorData();
                    // Make sure both are never 0.
                    m_LastTime = (m_Time > 0) ? m_Time : t;
                    m_Time = t;
                }
            }


            GraphicsSettings.lightsUseLinearIntensity = true;
            SetupPerFrameShaderConstants();

            SortCameras(cameras);
            foreach (Camera camera in cameras)
            {
                BeginCameraRendering(camera);

                foreach (var beforeCamera in camera.GetComponents<IBeforeCameraRender>())
                    beforeCamera.ExecuteBeforeCameraRender(this, renderContext, camera);

                RenderSingleCamera(this, renderContext, camera, ref m_CullResults, camera.GetComponent<IRendererSetup>());
            }
        }

        public static void RenderSingleCamera(LightweightRenderPipeline pipelineInstance, ScriptableRenderContext context, Camera camera, ref CullResults cullResults, IRendererSetup setup = null)
        {
            if (pipelineInstance == null)
            {
                Debug.LogError("Trying to render a camera with an invalid render pipeline instance.");
                return;
            }

            ScriptableCullingParameters cullingParameters;
            if (!CullResults.GetCullingParameters(camera, IsStereoEnabled(camera), out cullingParameters))
                return;

            CommandBuffer cmd = CommandBufferPool.Get(k_RenderCameraTag);
            using (new ProfilingSample(cmd, k_RenderCameraTag))
            {
                CameraData cameraData;
                PipelineSettings settings = pipelineInstance.settings;
                ScriptableRenderer renderer = pipelineInstance.renderer;

                InitializeCameraData(settings, camera, out cameraData);

                if(cameraData.requiresMotionVectorsTexture)
                    UpdateMotionVectorData(cameraData, renderer);

                SetupPerCameraShaderConstants(cameraData);

                cullingParameters.shadowDistance = Mathf.Min(cameraData.maxShadowDistance, camera.farClipPlane);

                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();

#if UNITY_EDITOR

                // Emit scene view UI
                if (cameraData.isSceneViewCamera)
                    ScriptableRenderContext.EmitWorldGeometryForSceneView(camera);
#endif
                CullResults.Cull(ref cullingParameters, context, ref cullResults);

                RenderingData renderingData;
                InitializeRenderingData(settings, ref cameraData, ref cullResults,
                    renderer.maxVisibleAdditionalLights, renderer.maxPerObjectAdditionalLights, out renderingData);

                var setupToUse = setup;
                if (setupToUse == null)
                    setupToUse = defaultRendererSetup;

                renderer.Clear();
                setupToUse.Setup(renderer, ref renderingData);
                renderer.Execute(context, ref renderingData);
            }

            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
            context.Submit();
#if UNITY_EDITOR
            Handles.DrawGizmos(camera);
#endif
        }

        static void SetSupportedRenderingFeatures()
        {
#if UNITY_EDITOR
            SupportedRenderingFeatures.active = new SupportedRenderingFeatures()
            {
                reflectionProbeSupportFlags = SupportedRenderingFeatures.ReflectionProbeSupportFlags.None,
                defaultMixedLightingMode = SupportedRenderingFeatures.LightmapMixedBakeMode.Subtractive,
                supportedMixedLightingModes = SupportedRenderingFeatures.LightmapMixedBakeMode.Subtractive,
                supportedLightmapBakeTypes = LightmapBakeType.Baked | LightmapBakeType.Mixed,
                supportedLightmapsModes = LightmapsMode.CombinedDirectional | LightmapsMode.NonDirectional,
                rendererSupportsLightProbeProxyVolumes = false,
                rendererSupportsMotionVectors = true,
                rendererSupportsReceiveShadows = false,
                rendererSupportsReflectionProbes = true
            };
            SceneViewDrawMode.SetupDrawMode();
#endif
        }

        private static void UpdateMotionVectorDataStereo(CameraData cameraData, ScriptableRenderer renderer, bool taaEnabled)
        {
            Camera camera = cameraData.camera;
            PostProcessLayer postProcessLayer = cameraData.postProcessLayer;

            for (Camera.StereoscopicEye eye = Camera.StereoscopicEye.Left; eye <= Camera.StereoscopicEye.Right; ++eye)
            {
                var nonJitteredCameraProj = camera.GetStereoProjectionMatrix(eye);
                var cameraProj = taaEnabled
                    ? postProcessLayer.temporalAntialiasing.GetStereoJitteredProjectionMatrix(camera.pixelWidth, camera.pixelHeight, camera, eye) // TODO: This is wrong as we should use the XRSettings eye texture width and height as double wide will effect the cmera pixel width.
                    : nonJitteredCameraProj;

                var gpuProj = GL.GetGPUProjectionMatrix(cameraProj, true); // Had to change this from 'false'
                var gpuView = camera.GetStereoViewMatrix(eye);
                var gpuNonJitteredProj = GL.GetGPUProjectionMatrix(nonJitteredCameraProj, true);
                var gpuVP = gpuNonJitteredProj * gpuView;

                MotionVectorData motionVectorData = GetMotionVectorData(camera);

                // A camera could be rendered multiple times per frame, only updates the previous view proj & pos if needed
                if (motionVectorData.lastFrameActive != Time.frameCount)
                {
                    if (motionVectorData.isFirstFrame)
                        motionVectorData.previousNonJitteredViewProjMatrixStereo[(int)eye] = gpuVP;
                    else
                        motionVectorData.previousNonJitteredViewProjMatrixStereo[(int)eye] = motionVectorData.nonJitteredViewProjMatrixStereo[(int)eye];
                }

                //taaFrameIndex = taaEnabled ? (uint)postProcessLayer.temporalAntialiasing.sampleIndex : 0; - MATT: Add TAA support
                //taaFrameRotation = new Vector2(Mathf.Sin(taaFrameIndex * (0.5f * Mathf.PI)),
                //        Mathf.Cos(taaFrameIndex * (0.5f * Mathf.PI)));

                motionVectorData.viewMatrixStereo[(int)eye] = gpuView;
                motionVectorData.projMatrixStereo[(int)eye] = gpuProj;
                motionVectorData.nonJitteredProjMatrixStereo[(int)eye] = gpuNonJitteredProj;
                motionVectorData.viewProjMatrixStereo[(int) eye] = gpuProj * gpuView;
                motionVectorData.nonJitteredViewProjMatrixStereo[(int) eye] = gpuNonJitteredProj * gpuView;
            }
        }

        public static void UpdateMotionVectorData(CameraData cameraData, ScriptableRenderer renderer)
        {
            Camera camera = cameraData.camera;
            PostProcessLayer postProcessLayer = cameraData.postProcessLayer;

            // If TAA is enabled projMatrix will hold a jittered projection matrix. The original,
            // non-jittered projection matrix can be accessed via nonJitteredProjMatrix.
            bool taaEnabled = camera.cameraType == CameraType.Game &&
                postProcessLayer != null && postProcessLayer.enabled &&
                postProcessLayer.antialiasingMode == PostProcessLayer.Antialiasing.TemporalAntialiasing &&
                postProcessLayer.temporalAntialiasing.IsSupported() &&
                cameraData.postProcessEnabled;

            UpdateMotionVectorDataStereo(cameraData, renderer, taaEnabled);

            var nonJitteredCameraProj = camera.projectionMatrix;
            var cameraProj = taaEnabled
                ? postProcessLayer.temporalAntialiasing.GetJitteredProjectionMatrix(camera)
                : nonJitteredCameraProj;

            // The actual projection matrix used in shaders is actually massaged a bit to work across all platforms
            // (different Z value ranges etc.)

            var gpuProj = GL.GetGPUProjectionMatrix(cameraProj, true); // Had to change this from 'false'
            var gpuView = camera.worldToCameraMatrix;
            var gpuNonJitteredProj = GL.GetGPUProjectionMatrix(nonJitteredCameraProj, true);
            var gpuVP = gpuNonJitteredProj * gpuView;

            MotionVectorData motionVectorData = GetMotionVectorData(camera);

            // A camera could be rendered multiple times per frame, only updates the previous view proj & pos if needed
            if (motionVectorData.lastFrameActive != Time.frameCount)
            {
                if (motionVectorData.isFirstFrame)
                    motionVectorData.previousNonJitteredViewProjMatrix = gpuVP;
                else
                    motionVectorData.previousNonJitteredViewProjMatrix = motionVectorData.nonJitteredViewProjMatrix;
                motionVectorData.isFirstFrame = false;
            }

            //taaFrameIndex = taaEnabled ? (uint)postProcessLayer.temporalAntialiasing.sampleIndex : 0; - MATT: Add TAA support
            //taaFrameRotation = new Vector2(Mathf.Sin(taaFrameIndex * (0.5f * Mathf.PI)),
            //        Mathf.Cos(taaFrameIndex * (0.5f * Mathf.PI)));

            motionVectorData.viewMatrix = gpuView;
            motionVectorData.projMatrix = gpuProj;
            motionVectorData.nonJitteredProjMatrix = gpuNonJitteredProj;

            motionVectorData.lastFrameActive = Time.frameCount;
        }


        static void InitializeCameraData(PipelineSettings settings, Camera camera, out CameraData cameraData)
        {
            const float kRenderScaleThreshold = 0.05f;
            cameraData.camera = camera;

            bool msaaEnabled = camera.allowMSAA && settings.msaaSampleCount > 1;
            if (msaaEnabled)
                cameraData.msaaSamples = (camera.targetTexture != null) ? camera.targetTexture.antiAliasing : settings.msaaSampleCount;
            else
                cameraData.msaaSamples = 1;

            cameraData.isSceneViewCamera = camera.cameraType == CameraType.SceneView;
            cameraData.isOffscreenRender = camera.targetTexture != null && !cameraData.isSceneViewCamera;
            cameraData.isStereoEnabled = IsStereoEnabled(camera);

            cameraData.isHdrEnabled = camera.allowHDR && settings.supportsHDR;

            cameraData.postProcessLayer = camera.GetComponent<PostProcessLayer>();
            cameraData.postProcessEnabled = cameraData.postProcessLayer != null && cameraData.postProcessLayer.isActiveAndEnabled;

            Rect cameraRect = camera.rect;
            cameraData.isDefaultViewport = (!(Math.Abs(cameraRect.x) > 0.0f || Math.Abs(cameraRect.y) > 0.0f ||
                Math.Abs(cameraRect.width) < 1.0f || Math.Abs(cameraRect.height) < 1.0f));

            // If XR is enabled, use XR renderScale.
            // Discard variations lesser than kRenderScaleThreshold.
            // Scale is only enabled for gameview.
            float usedRenderScale = XRGraphics.enabled ? XRGraphics.eyeTextureResolutionScale : settings.renderScale;
            cameraData.renderScale = (Mathf.Abs(1.0f - usedRenderScale) < kRenderScaleThreshold) ? 1.0f : usedRenderScale;
            cameraData.renderScale = (camera.cameraType == CameraType.Game) ? cameraData.renderScale : 1.0f;

            cameraData.opaqueTextureDownsampling = settings.opaqueDownsampling;

            bool anyShadowsEnabled = settings.supportsMainLightShadows || settings.supportsAdditionalLightShadows;
            cameraData.maxShadowDistance = (anyShadowsEnabled) ? settings.shadowDistance : 0.0f;
            cameraData.requiresMotionVectorsTexture = SystemInfo.supportsMotionVectors && settings.supportsCameraMotionVectorsTexture;

            LWRPAdditionalCameraData additionalCameraData = camera.gameObject.GetComponent<LWRPAdditionalCameraData>();
            if (additionalCameraData != null)
            {
                cameraData.maxShadowDistance = (additionalCameraData.renderShadows) ? cameraData.maxShadowDistance : 0.0f;
                cameraData.requiresDepthTexture = additionalCameraData.requiresDepthTexture;
                cameraData.requiresOpaqueTexture = additionalCameraData.requiresColorTexture;
                cameraData.requiresMotionVectorsTexture &= additionalCameraData.requiresMotionVectorsTexture;
            }
            else
            {
                cameraData.requiresDepthTexture = settings.supportsCameraDepthTexture;
                cameraData.requiresOpaqueTexture = settings.supportsCameraOpaqueTexture;
                cameraData.requiresMotionVectorsTexture = settings.supportsCameraMotionVectorsTexture;
            }

            cameraData.requiresDepthTexture |= cameraData.isSceneViewCamera || cameraData.postProcessEnabled;

            var commonOpaqueFlags = SortFlags.CommonOpaque;
            var noFrontToBackOpaqueFlags = SortFlags.SortingLayer | SortFlags.RenderQueue | SortFlags.OptimizeStateChanges | SortFlags.CanvasOrder;
            bool hasHSRGPU = SystemInfo.hasHiddenSurfaceRemovalOnGPU;
            bool canSkipFrontToBackSorting = (camera.opaqueSortMode == OpaqueSortMode.Default && hasHSRGPU) || camera.opaqueSortMode == OpaqueSortMode.NoDistanceSort;

            cameraData.defaultOpaqueSortFlags = canSkipFrontToBackSorting ? noFrontToBackOpaqueFlags : commonOpaqueFlags;
        }

        static void InitializeRenderingData(PipelineSettings settings, ref CameraData cameraData, ref CullResults cullResults,
            int maxVisibleAdditionalLights, int maxPerObjectAdditionalLights, out RenderingData renderingData)
        {
            List<VisibleLight> visibleLights = cullResults.visibleLights;

            int mainLightIndex = GetMainLight(settings, visibleLights);
            bool mainLightCastShadows = false;
            bool additionalLightsCastShadows = false;

            if (cameraData.maxShadowDistance > 0.0f)
            {
                mainLightCastShadows = (mainLightIndex != -1 && visibleLights[mainLightIndex].light != null &&
                                        visibleLights[mainLightIndex].light.shadows != LightShadows.None);

                // If additional lights are shaded per-pixel they cannot cast shadows
                if (settings.additionalLightsRenderingMode == LightRenderingMode.PerPixel)
                {
                    for (int i = 0; i < visibleLights.Count; ++i)
                    {
                        if (i == mainLightIndex)
                            continue;

                        Light light = visibleLights[i].light;

                        // LWRP doesn't support additional directional lights or point light shadows yet
                        if (visibleLights[i].lightType == LightType.Spot && light != null && light.shadows != LightShadows.None)
                        {
                            additionalLightsCastShadows = true;
                            break;
                        }
                    }
                }
            }

            renderingData.cullResults = cullResults;
            renderingData.cameraData = cameraData;
            InitializeLightData(settings, visibleLights, mainLightIndex, maxVisibleAdditionalLights, maxPerObjectAdditionalLights, out renderingData.lightData);
            InitializeShadowData(settings, visibleLights, mainLightCastShadows, additionalLightsCastShadows && !renderingData.lightData.shadeAdditionalLightsPerVertex, out renderingData.shadowData);
            renderingData.supportsDynamicBatching = settings.supportsDynamicBatching;
        }

        static void InitializeShadowData(PipelineSettings settings, List<VisibleLight> visibleLights, bool mainLightCastShadows, bool additionalLightsCastShadows, out ShadowData shadowData)
        {
            m_ShadowBiasData.Clear();

            for (int i = 0; i < visibleLights.Count; ++i)
            {
                Light light = visibleLights[i].light;
                LWRPAdditionalLightData data =
                    (light != null) ? light.gameObject.GetComponent<LWRPAdditionalLightData>() : null;

                if (data && !data.usePipelineSettings)
                    m_ShadowBiasData.Add(new Vector4(light.shadowBias, light.shadowNormalBias, 0.0f, 0.0f));
                else
                    m_ShadowBiasData.Add(new Vector4(settings.shadowDepthBias, settings.shadowNormalBias, 0.0f, 0.0f));
            }

            shadowData.bias = m_ShadowBiasData;

            // Until we can have keyword stripping forcing single cascade hard shadows on gles2
            bool supportsScreenSpaceShadows = SystemInfo.graphicsDeviceType != GraphicsDeviceType.OpenGLES2;

            shadowData.supportsMainLightShadows = settings.supportsMainLightShadows && mainLightCastShadows;

            // we resolve shadows in screenspace when cascades are enabled to save ALU as computing cascade index + shadowCoord on fragment is expensive
            shadowData.requiresScreenSpaceShadowResolve = shadowData.supportsMainLightShadows && supportsScreenSpaceShadows && settings.cascadeCount > 1;
            shadowData.mainLightShadowCascadesCount = (shadowData.requiresScreenSpaceShadowResolve) ? settings.cascadeCount : 1;
            shadowData.mainLightShadowmapWidth = settings.mainLightShadowmapResolution;
            shadowData.mainLightShadowmapHeight = settings.mainLightShadowmapResolution;

            switch (shadowData.mainLightShadowCascadesCount)
            {
                case 1:
                    shadowData.mainLightShadowCascadesSplit = new Vector3(1.0f, 0.0f, 0.0f);
                    break;

                case 2:
                    shadowData.mainLightShadowCascadesSplit = new Vector3(settings.cascade2Split, 1.0f, 0.0f);
                    break;

                default:
                    shadowData.mainLightShadowCascadesSplit = settings.cascade4Split;
                    break;
            }

            shadowData.supportsAdditionalLightShadows = settings.supportsAdditionalLightShadows && additionalLightsCastShadows;
            shadowData.additionalLightsShadowmapWidth = shadowData.additionalLightsShadowmapHeight = settings.additionalLightsShadowmapResolution;
            shadowData.supportsSoftShadows = settings.supportsSoftShadows && (shadowData.supportsMainLightShadows || shadowData.supportsAdditionalLightShadows);
            shadowData.shadowmapDepthBufferBits = 16;
        }

        static void InitializeLightData(PipelineSettings settings, List<VisibleLight> visibleLights, int mainLightIndex, int maxAdditionalLights,
            int maxPerObjectAdditionalLights, out LightData lightData)
        {
            lightData.mainLightIndex = mainLightIndex;

            if (settings.additionalLightsRenderingMode != LightRenderingMode.Disabled)
            {
                lightData.additionalLightsCount =
                    Math.Min((mainLightIndex != -1) ? visibleLights.Count - 1 : visibleLights.Count,
                        maxAdditionalLights);
                lightData.maxPerObjectAdditionalLightsCount = Math.Min(settings.maxAdditionalLights, maxPerObjectAdditionalLights);
            }
            else
            {
                lightData.additionalLightsCount = 0;
                lightData.maxPerObjectAdditionalLightsCount = 0;
            }

            lightData.shadeAdditionalLightsPerVertex = settings.additionalLightsRenderingMode == LightRenderingMode.PerVertex;
            lightData.visibleLights = visibleLights;
            lightData.supportsMixedLighting = settings.mixedLightingSupported;
        }

        // Main Light is always a directional light
        static int GetMainLight(PipelineSettings settings, List<VisibleLight> visibleLights)
        {
            int totalVisibleLights = visibleLights.Count;

            if (totalVisibleLights == 0 || settings.mainLightRenderingMode != LightRenderingMode.PerPixel)
                return -1;

            for (int i = 0; i < totalVisibleLights; ++i)
            {
                VisibleLight currLight = visibleLights[i];

                // Particle system lights have the light property as null. We sort lights so all particles lights
                // come last. Therefore, if first light is particle light then all lights are particle lights.
                // In this case we either have no main light or already found it.
                if (currLight.light == null)
                    break;

                // In case no shadow light is present we will return the brightest directional light
                if (currLight.lightType == LightType.Directional)
                    return i;
            }

            return -1;
        }

        static void SetupPerFrameShaderConstants()
        {
            // When glossy reflections are OFF in the shader we set a constant color to use as indirect specular
            SphericalHarmonicsL2 ambientSH = RenderSettings.ambientProbe;
            Color linearGlossyEnvColor = new Color(ambientSH[0, 0], ambientSH[1, 0], ambientSH[2, 0]) * RenderSettings.reflectionIntensity;
            Color glossyEnvColor = CoreUtils.ConvertLinearToActiveColorSpace(linearGlossyEnvColor);
            Shader.SetGlobalVector(PerFrameBuffer._GlossyEnvironmentColor, glossyEnvColor);

            // Used when subtractive mode is selected
            Shader.SetGlobalVector(PerFrameBuffer._SubtractiveShadowColor, CoreUtils.ConvertSRGBToActiveColorSpace(RenderSettings.subtractiveShadowColor));
        }

        static void SetupPerCameraShaderConstantsStereo(CameraData cameraData)
        {
            Camera camera = cameraData.camera;
            Matrix4x4[] invViewProjMatrixStereo = new Matrix4x4[2];
            Matrix4x4[] viewProjMatrixStereo = new Matrix4x4[2];
            Matrix4x4[] projMatrixStereo = new Matrix4x4[2];
            Matrix4x4[] invProjMatrixStereo = new Matrix4x4[2];

            for (Camera.StereoscopicEye eye = Camera.StereoscopicEye.Left; eye <= Camera.StereoscopicEye.Right; ++eye)
            {

                Matrix4x4 projMatrix = GL.GetGPUProjectionMatrix(camera.GetStereoProjectionMatrix(eye), true);
                if (cameraData.requiresMotionVectorsTexture)
                {
                    MotionVectorData motionVectorData = GetMotionVectorData(camera);
                    projMatrix = motionVectorData.projMatrixStereo[(int)eye]; // Use the possibly jittered version.
                }

                Matrix4x4 viewMatrix = camera.GetStereoViewMatrix(eye);
                viewProjMatrixStereo[(int)eye] = projMatrix * viewMatrix;
                invViewProjMatrixStereo[(int)eye] = Matrix4x4.Inverse(viewProjMatrixStereo[(int)eye]);
                projMatrixStereo[(int) eye] = projMatrix;
                invProjMatrixStereo[(int)eye] = Matrix4x4.Inverse(projMatrix);
            }

            Shader.SetGlobalMatrixArray(PerCameraStereoBuffer._InvCameraViewProjMatrixStereo, invViewProjMatrixStereo);

            Shader.SetGlobalMatrixArray(PerCameraStereoBuffer._ViewProjMatrixStereo, viewProjMatrixStereo);
            Shader.SetGlobalMatrixArray(PerCameraStereoBuffer._ProjMatrixStereo, projMatrixStereo);

            Shader.SetGlobalMatrixArray(PerCameraStereoBuffer._CameraProjection, projMatrixStereo);
            Shader.SetGlobalMatrixArray(PerCameraStereoBuffer._CameraInvProjection, projMatrixStereo);

            if (cameraData.requiresMotionVectorsTexture)
            {
                MotionVectorData motionVectorData = GetMotionVectorData(camera);

                //Shader.SetGlobalMatrixArray(PerCameraStereoBuffer._ProjMatrixStereo, motionVectorData.projMatrixStereo);
                //Shader.SetGlobalMatrixArray(PerCameraStereoBuffer._ViewProjMatrixStereo, motionVectorData.viewProjMatrixStereo);
                Shader.SetGlobalMatrixArray(PerCameraStereoBuffer._NonJitteredViewProjMatrixStereo, motionVectorData.nonJitteredViewProjMatrixStereo);
                Shader.SetGlobalMatrixArray(PerCameraStereoBuffer._PrevViewProjMatrixStereo, motionVectorData.previousNonJitteredViewProjMatrixStereo);
                //Shader.SetGlobalVector(PerCameraBuffer._TaaFrameRotation, motionVectorData.taaFrameRotation); - MATT: Add TAA support
            }
        }

        static void SetupPerCameraShaderConstants(CameraData cameraData)
        {
            Camera camera = cameraData.camera;
            float cameraWidth = (float)camera.pixelWidth * cameraData.renderScale;
            float cameraHeight = (float)camera.pixelHeight * cameraData.renderScale;
            Shader.SetGlobalVector(PerCameraBuffer._ScaledScreenParams, new Vector4(cameraWidth, cameraHeight, 1.0f + 1.0f / cameraWidth, 1.0f + 1.0f / cameraHeight));

            Matrix4x4 projMatrix = GL.GetGPUProjectionMatrix(camera.projectionMatrix, true);

            if (cameraData.requiresMotionVectorsTexture)
            {
                MotionVectorData motionVectorData = GetMotionVectorData(camera);
                projMatrix = motionVectorData.projMatrix; // Use the possibly jittered version.
            }

            Matrix4x4 viewMatrix = camera.worldToCameraMatrix;
            Matrix4x4 viewProjMatrix = projMatrix * viewMatrix;
            Matrix4x4 invViewProjMatrix = Matrix4x4.Inverse(viewProjMatrix);
            Shader.SetGlobalMatrix(PerCameraBuffer._InvCameraViewProj, invViewProjMatrix);

            Shader.SetGlobalMatrix(PerCameraBuffer._ViewProjMatrix, viewProjMatrix);
            Shader.SetGlobalMatrix(PerCameraBuffer._ProjMatrix, projMatrix);

            float n = camera.nearClipPlane;
            float f = camera.farClipPlane;

            // Analyze the projection matrix.
            // p[2][3] = (reverseZ ? 1 : -1) * (depth_0_1 ? 1 : 2) * (f * n) / (f - n)
            float scale     = projMatrix[2, 3] / (f * n) * (f - n);
            bool  depth_0_1 = Mathf.Abs(scale) < 1.5f;
            bool  reverseZ  = scale > 0;
            bool  flipProj  = projMatrix.inverse.MultiplyPoint(new Vector3(0, 1, 0)).y < 0;

//            // http://www.humus.name/temp/Linearize%20depth.txt
//            if (reverseZ)
//            {
//                zBufferParams = new Vector4(-1 + f / n, 1, -1 / f + 1 / n, 1 / f);
//            }
//            else
//            {
//                zBufferParams = new Vector4(1 - f / n, f / n, 1 / f - 1 / n, 1 / n);
//            }

            Vector4 projectionParams = new Vector4(1/*flipProj ? -1 : 1*/, n, f, 1.0f / f);
            Shader.SetGlobalVector(PerCameraBuffer._ProjectionParams, projectionParams);

            if (cameraData.requiresMotionVectorsTexture)
            {
                MotionVectorData motionVectorData = GetMotionVectorData(camera);

                Shader.SetGlobalMatrix(PerCameraBuffer._NonJitteredViewProjMatrix, motionVectorData.nonJitteredViewProjMatrix);
                Shader.SetGlobalMatrix(PerCameraBuffer._PrevViewProjMatrix, motionVectorData.previousNonJitteredViewProjMatrix);
                //Shader.SetGlobalVector(PerCameraBuffer._TaaFrameRotation, motionVectorData.taaFrameRotation); - MATT: Add TAA support
            }

            SetupPerCameraShaderConstantsStereo(cameraData);
        }

        public static Lightmapping.RequestLightsDelegate lightsDelegate = (Light[] requests, NativeArray<LightDataGI> lightsOutput) =>
        {
            LightDataGI lightData = new LightDataGI();

            for (int i = 0; i < requests.Length; i++)
            {
                Light light = requests[i];
                switch (light.type)
                {
                    case LightType.Directional:
                        DirectionalLight directionalLight = new DirectionalLight();
                        LightmapperUtils.Extract(light, ref directionalLight); lightData.Init(ref directionalLight);
                        break;
                    case LightType.Point:
                        PointLight pointLight = new PointLight();
                        LightmapperUtils.Extract(light, ref pointLight); lightData.Init(ref pointLight);
                        break;
                    case LightType.Spot:
                        SpotLight spotLight = new SpotLight();
                        LightmapperUtils.Extract(light, ref spotLight); lightData.Init(ref spotLight);
                        break;
                    case LightType.Area:
                        RectangleLight rectangleLight = new RectangleLight();
                        LightmapperUtils.Extract(light, ref rectangleLight); lightData.Init(ref rectangleLight);
                        break;
                    default:
                        lightData.InitNoBake(light.GetInstanceID());
                        break;
                }

                lightData.falloff = FalloffType.InverseSquared;
                lightsOutput[i] = lightData;
            }
        };

        // ---------------------------------------------------------
        // Motion Vectors
        // ---------------------------------------------------------

        static Dictionary<int, MotionVectorData> s_MotionVectorDatas = new Dictionary<int, MotionVectorData>();

        static List<int> s_Cleanup = new List<int>(); // Recycled to reduce GC pressure

        public static MotionVectorData GetMotionVectorData(Camera camera)
        {
            int instanceID = camera.GetInstanceID();
            MotionVectorData motionVectorData;
            if (!s_MotionVectorDatas.TryGetValue(instanceID, out motionVectorData))
            {
                motionVectorData = CreateMotionVectorData(instanceID);
            }
            return motionVectorData;
        }

        public static MotionVectorData CreateMotionVectorData(int instanceID)
        {
            MotionVectorData motionVectorData = new MotionVectorData();
            s_MotionVectorDatas.Add(instanceID, motionVectorData);
            return motionVectorData;
        }

        public static void ClearAllMotionVectorData()
        {
            s_MotionVectorDatas.Clear();
            s_Cleanup.Clear();
        }

        // Look for any camera that hasn't been used in the last frame and remove them from the pool.
        public static void CleanUnusedMotionVectorData()
        {
            int frameCheck = Time.frameCount - 1;
            foreach (var kvp in s_MotionVectorDatas)
            {
                if (kvp.Value.lastFrameActive < frameCheck)
                    s_Cleanup.Add(kvp.Key);
            }
            foreach (var cam in s_Cleanup)
                s_MotionVectorDatas.Remove(cam);
            s_Cleanup.Clear();
        }
    }
}
