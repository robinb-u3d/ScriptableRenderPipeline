using UnityEngine.Rendering;

namespace UnityEngine.Experimental.Rendering.LightweightPipeline
{
    /// <summary>
    /// Copy the given color buffer to the given destination color buffer.
    ///
    /// You can use this pass to copy a color buffer to the destination,
    /// so you can use it later in rendering. For example, you can copy
    /// the opaque texture to use it for distortion effects.
    /// </summary>
    public class MotionVectorsPass : ScriptableRenderPass
    {
        const string k_MotionVectorTag = "Motion Vectors";

        private RenderTargetHandle destination { get; set; }

        FilterRenderersSettings m_MotionVectorsFilterSettings;

        /// <summary>
        /// Create the MotionVectorPass
        /// </summary>
        public MotionVectorsPass()
        {
            RegisterShaderPassName("MotionVectors");

            m_MotionVectorsFilterSettings = new FilterRenderersSettings(true)
            {
                renderQueueRange = RenderQueueRange.opaque,
            };
        }

        /// <summary>
        /// Configure the pass with the source and destination to execute on.
        /// </summary>
        /// <param name="source">Source Render Target</param>
        /// <param name="destination">Destination Render Target</param>
        public void Setup(RenderTargetHandle destination)
        {
            this.destination = destination;
        }

        /// <inheritdoc/>
        public override void Execute(ScriptableRenderer renderer, ScriptableRenderContext context, ref RenderingData renderingData)
        {
            Camera camera = renderingData.cameraData.camera;

            {
                // These flags are still required in SRP or the engine won't compute previous model matrices...
                // If the flag hasn't been set yet on this camera, motion vectors will skip a frame.
                camera.depthTextureMode |= DepthTextureMode.MotionVectors | DepthTextureMode.Depth;

                CommandBuffer cmd = CommandBufferPool.Get(k_MotionVectorTag);

                RenderTextureDescriptor motionVectorDesc = ScriptableRenderer.CreateRenderTextureDescriptor(ref renderingData.cameraData);
                motionVectorDesc.colorFormat = RenderTextureFormat.RGHalf;

                RenderTargetIdentifier motionVectorRT = destination.Identifier();
                cmd.GetTemporaryRT(destination.id, motionVectorDesc, FilterMode.Point);
                cmd.SetRenderTarget(motionVectorRT);

                // Draw fullscreen quad
                Material cameraMotionVectorsMaterial = renderer.GetMaterial(MaterialHandle.CameraMotionVectors);
                cmd.DrawProcedural(Matrix4x4.identity, cameraMotionVectorsMaterial, 0, MeshTopology.Triangles, 3, 1);

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }

            {
                // These flags are still required in SRP or the engine won't compute previous model matrices...
                // If the flag hasn't been set yet on this camera, motion vectors will skip a frame.
                camera.depthTextureMode |= DepthTextureMode.MotionVectors | DepthTextureMode.Depth;

                // Draw objects
                var sortFlags = renderingData.cameraData.defaultOpaqueSortFlags;
                var drawSettings = CreateDrawRendererSettings(camera, sortFlags, RendererConfiguration.PerObjectMotionVectors, false);
                context.DrawRenderers(renderingData.cullResults.visibleRenderers, ref drawSettings, m_MotionVectorsFilterSettings);
            }

            {
                CommandBuffer cmd = CommandBufferPool.Get(k_MotionVectorTag);
                cmd.SetRenderTarget(BuiltinRenderTextureType.CameraTarget);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }

        /// <inheritdoc/>
        public override void FrameCleanup(CommandBuffer cmd)
        {
            if (destination != RenderTargetHandle.CameraTarget)
            {
                cmd.ReleaseTemporaryRT(destination.id);
                destination = RenderTargetHandle.CameraTarget;
            }
        }
    }
}
