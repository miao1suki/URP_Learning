#ifndef BACKFACEOUTLINES_INCLUDED
#define BACKFACEOUTLINES_INCLUDED

//URP核心库
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

struct Attributes
{
    //网格中的顶点位置
    float4 positionOS : POSITION;
    //法线
    float3 normalOS : NORMAL;
#ifdef USE_PRECALCULATED_OUTLINE_NORMALS
    float3 smoothNormalsOS :TEXCOORD1;
#endif 
};

//顶点输出
struct VertexOutput
{
    //裁剪空间位置
    float4 positionCS : SV_POSITION;
};

//Properties
float _Thickness;
float4 _Color;
float _DepthOffset;

VertexOutput Vertex(Attributes input)
{
    VertexOutput output = (VertexOutput)0; 
    float3 normalOS = input.normalOS;

#ifdef USE_PRECALCULATED_OUTLINE_NORMALS
    normalOS = input.smoothNormalsOS;
#else
    normalOS = input.normalOS;
#endif 


    float3 posOS = input.positionOS.xyz + normalOS * _Thickness;

    output.positionCS = GetVertexPositionInputs(posOS).positionCS;

    float depthOffset = _DepthOffset;
#ifdef UNITY_REVERSED_Z
    depthOffset = -depthOffset;
#endif

    output.positionCS.z += depthOffset;
    return output;
}

float4 Fragment(VertexOutput input) : SV_TARGET
{
    return _Color;
}

#endif