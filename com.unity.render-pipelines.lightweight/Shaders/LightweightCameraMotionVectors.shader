Shader "Hidden/LightweightPipeline/CameraMotionVectors"
{
    SubShader
    {
        Tags { "RenderPipeline" = "LightweightPipeline"}

        Pass
        {
            //Name "Default"

            // Only perform camera motion velocity where there is no object velocity
            //Stencil
            //{
            //    ReadMask 128
            //    Ref  128 // ObjectVelocity
            //    Comp NotEqual
            //    Pass Keep
            //}

            Cull Off ZWrite Off ZTest Always

            HLSLPROGRAM
            #pragma exclude_renderers gles
            #pragma target 3.0

            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.lightweight/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_CameraDepthTexture);       SAMPLER(sampler_CameraDepthTexture);

            struct VertexInput
            {
                uint vertexID : SV_VertexID;
            };

            struct VertexOutput
            {
                float4 position : SV_POSITION;
            };

            VertexOutput vert(VertexInput i)
            {
                VertexOutput o;
                o.position = GetFullScreenTriangleVertexPosition(i.vertexID);
                return o;
            }

            float4 frag(VertexOutput i) : SV_Target
            {
                float depth = LOAD_TEXTURE2D(_CameraDepthTexture, i.position.xy).x;

                float2 screenSize = float2(1 / _ScaledScreenParams.x, 1 / _ScaledScreenParams.y);
                PositionInputs posInput = GetPositionInput(i.position.xy, screenSize, depth, UNITY_MATRIX_I_VP, UNITY_MATRIX_V);

                float4 worldPos = float4(posInput.positionWS, 1.0);
                float4 prevPos = worldPos;
                
                float4 prevClipPos = mul(_PrevViewProjMatrix, prevPos);
                float4 curClipPos = mul(_NonJitteredViewProjMatrix, worldPos);

                float2 previousPositionCS = prevClipPos.xy / prevClipPos.w;
                float2 positionCS = curClipPos.xy / curClipPos.w;

                // Convert from Clip space (-1..1) to NDC 0..1 space
                float2 velocity = (positionCS - previousPositionCS);
#if UNITY_UV_STARTS_AT_TOP
                velocity.y = -velocity.y;
#endif
                // Convert velocity from Clip space (-1..1) to NDC 0..1 space
                // Note it doesn't mean we don't have negative value, we store negative or positive offset in NDC space.
                // Note: ((positionCS * 0.5 + 0.5) - (previousPositionCS * 0.5 + 0.5)) = (velocity * 0.5)
                velocity.xy * 0.5;
                return float4(velocity.xy, 0, 0);
            }

            ENDHLSL
        }
    }
}
