Shader "Outlines/BackFaceOutlines/BackFaceOutlines"
{
    Properties
    {
        _Color("Color", Color) = (1, 1, 1, 1)
        _Thickness("Thickness", float) = 1
        _DepthOffset("Depth offset",Range(0,1)) = 0 //利用z偏移解决描边前后关系
        [Toggle(USE_PRECALCULATED_OUTLINE_NORMALS)]_PrecalculateNormals("Use UV1 normals",Float) = 0 //是否启用平滑法线
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }

        Pass
        {
            Name "Outlines"

            //正面剔除
            Cull Front


            HLSLPROGRAM
            //标准URP平台需求编译指令
            #pragma prefer-hlslcc gles
            #pragma exclude_renderers d3d11_9x

            #pragma shader_feature USE_PRECALCULATED_OUTLINE_NORMALS

            #pragma vertex Vertex
            #pragma fragment Fragment

            #include "BackFaceOutlines.hlsl"


            ENDHLSL
        }
    }
}
