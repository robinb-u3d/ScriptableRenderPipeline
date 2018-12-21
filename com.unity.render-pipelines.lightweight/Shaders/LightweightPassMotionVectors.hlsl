#ifndef LIGHTWEIGHT_PASS_MOTION_VECTORS_INCLUDED
#define LIGHTWEIGHT_PASS_MOTION_VECTORS_INCLUDED

#include "Packages/com.unity.render-pipelines.lightweight/ShaderLibrary/Core.hlsl"

#if defined(USING_STEREO_MATRICES)
#define MOTION_VECTORS_MATRIX_PREV_VP _PrevViewProjMatrixStereo[unity_StereoEyeIndex]
#define MOTION_VECTORS_MATRIX_NOJ_VP _NonJitteredViewProjMatrixStereo[unity_StereoEyeIndex]
#else
#define MOTION_VECTORS_MATRIX_PREV_VP _PrevViewProjMatrix
#define MOTION_VECTORS_MATRIX_NOJ_VP _NonJitteredViewProjMatrix
#endif

struct Attributes
{
    float4 position : POSITION;
    float3 positionOld : TEXCOORD4;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 clipPos : TEXCOORD0;
    float4 previousClipPos : TEXCOORD1;
    float4 pos : SV_POSITION;
    UNITY_VERTEX_OUTPUT_STEREO
};

Varyings MotionVectorsVertex(Attributes v)
{
    Varyings o;
    UNITY_SETUP_INSTANCE_ID(v);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
    o.pos = TransformObjectToHClip(v.position.xyz);

    // this works around an issue with dynamic batching
    // potentially remove in 5.4 when we use instancing
#if defined(UNITY_REVERSED_Z)
    o.pos.z -= unity_MotionVectorsParams.z * o.pos.w;
#else
    o.pos.z += unity_MotionVectorsParams.z * o.pos.w;
#endif

//#if defined(USING_STEREO_MATRICES)
//    o.clipPos = mul(_StereoNonJitteredVP[unity_StereoEyeIndex], mul(unity_ObjectToWorld, v.vertex));
//    o.previousClipPos = mul(_StereoPreviousVP[unity_StereoEyeIndex], mul(_PreviousM, _HasLastPositionData ? float4(v.oldPos, 1) : v.vertex));
//#else
    o.clipPos = mul(MOTION_VECTORS_MATRIX_NOJ_VP, mul(UNITY_MATRIX_M, v.position));
    o.previousClipPos = mul(MOTION_VECTORS_MATRIX_PREV_VP, mul(unity_MatrixPreviousM, unity_MotionVectorsParams.x == 1 ? float4(v.positionOld, 1) : v.position));
//#endif
    return o;
}

half4 MotionVectorsFragment(Varyings IN) : SV_Target
{
#ifndef SHADER_API_MOBILE
    // Note: unity_MotionVectorsParams.y is 0 is forceNoMotion is enabled
    if (unity_MotionVectorsParams.y == 0.0)
        return float4(0.0, 0.0, 0.0, 0.0);
#endif

    IN.clipPos.xy = IN.clipPos.xy / IN.clipPos.w;
    IN.previousClipPos.xy = IN.previousClipPos.xy / IN.previousClipPos.w;

    float2 velocity = (IN.clipPos.xy - IN.previousClipPos.xy);
#if UNITY_UV_STARTS_AT_TOP
    velocity.y = -velocity.y;
#endif

    // Convert from Clip space (-1..1) to NDC 0..1 space.
    // Note it doesn't mean we don't have negative value, we store negative or positive offset in NDC space.
    // Note: ((positionCS * 0.5 + 0.5) - (previousPositionCS * 0.5 + 0.5)) = (velocity * 0.5)

    velocity = velocity.xy * 0.5;

#ifdef SHADER_API_MOBILE
    velocity.xy *= unity_MotionVectorsParams.y;
#endif

    return float4(velocity.xy, 0, 0);
}

#endif
