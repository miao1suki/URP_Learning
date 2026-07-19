using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using System.Collections.Generic;

public class DepthNormalsFeature : ScriptableRendererFeature
{
    DepthNormalsFeaturePass m_ScriptablePass;
    Material m_Material;

    /// <inheritdoc/>
    public override void Create()
    {
        CoreUtils.Destroy(m_Material);

        //创建临时材质
        m_Material = CoreUtils.CreateEngineMaterial("Hidden/Internal-DepthNormalsTexture");

        m_ScriptablePass = new DepthNormalsFeaturePass(m_Material);
        

        //渲染时机为预通道之后，用于渲染不透明对象
        m_ScriptablePass.renderPassEvent = RenderPassEvent.AfterRenderingPrePasses;


    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (m_Material == null || m_ScriptablePass == null)
            return;

        //将渲染通道入队
        renderer.EnqueuePass(m_ScriptablePass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(m_Material);
        m_Material = null;
    }

    class DepthNormalsFeaturePass : ScriptableRenderPass
    {
        static readonly int DepthNormalsTextureId =
        Shader.PropertyToID("_DepthNormalsTexture");

        readonly Material material;
        readonly List<ShaderTagId> shaderTags;
        readonly FilteringSettings filteringSettings;

        public DepthNormalsFeaturePass(Material material)
        {
            this.material = material;
            this.shaderTags = new List<ShaderTagId>()
            {
                new ShaderTagId("DepthOnly"),
                //new ShaderTagId("SRPDefaultUnlit"),
                //new ShaderTagId("UniversalForward"),
                //new ShaderTagId("LightweightForward"),
            };

            //设置过滤条件精确控制渲染对象
            this.filteringSettings = new FilteringSettings(RenderQueueRange.opaque);


        }


        private class PassData
        {
            public RendererListHandle rendererList;
        }


        static void ExecutePass(PassData data, RasterGraphContext context)
        {
            context.cmd.DrawRendererList(data.rendererList);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null)
                return;

            UniversalResourceData resourceData =
                frameData.Get<UniversalResourceData>();

            UniversalRenderingData renderingData =
                frameData.Get<UniversalRenderingData>();

            UniversalCameraData cameraData =
                frameData.Get<UniversalCameraData>();

            UniversalLightData lightData =
                frameData.Get<UniversalLightData>();

            RenderTextureDescriptor descriptor =
                cameraData.cameraTargetDescriptor;


            // DepthNormalsTexture 本身是存放编码结果的颜色纹理。
            descriptor.depthBufferBits = 0;
            descriptor.msaaSamples = 1;

            // 对应旧版 cmd.GetTemporaryRT(...)。
            // 创建临时纹理
            TextureHandle destination =
                UniversalRenderer.CreateRenderGraphTexture(
                    renderGraph,
                    descriptor,
                    "_DepthNormalsTexture",
                    true,               // 创建后清理纹理
                    FilterMode.Point
                );


            const string passName = "Depth Normals Pass";

         
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData))
            {
                // 对应旧版 CreateDrawingSettings。
                DrawingSettings drawingSettings =
                    RenderingUtils.CreateDrawingSettings(
                        shaderTags,
                        renderingData,
                        cameraData,
                        lightData,
                        cameraData.defaultOpaqueSortFlags
                    );

                // 对应旧版 overrideMaterial。
                drawingSettings.overrideMaterial = material;
                drawingSettings.overrideMaterialPassIndex = 0;

                // 对应旧版：
                // renderingData.cullResults + filteringSettings
                RendererListParams rendererListParams =
                    new RendererListParams(
                        renderingData.cullResults,
                        drawingSettings,
                        filteringSettings
                    );

                passData.rendererList =
                    renderGraph.CreateRendererList(rendererListParams);

                if (!passData.rendererList.IsValid())
                    return;

                // 告诉 RenderGraph 当前 Pass 会使用这个 RendererList。
                builder.UseRendererList(passData.rendererList);

                // 深度法线编码结果写入这张颜色纹理。
                builder.SetRenderAttachment(destination, 0);

                // 使用相机深度进行遮挡测试。
                builder.SetRenderAttachmentDepth(
                    resourceData.activeDepthTexture,
                    AccessFlags.ReadWrite
                );

                // 让后续 Shader 可以按名称采样这张纹理。
                builder.SetGlobalTextureAfterPass(
                    destination,
                    DepthNormalsTextureId
                );

                // 当前还没有图内消费者，防止学习阶段被 RenderGraph 剔除。
                builder.AllowPassCulling(false);

                //显式注册ExecutePass
                builder.SetRenderFunc(
                    static (
                        PassData data,
                        RasterGraphContext context) =>
                        ExecutePass(data, context)
                );
            }
        }
    }
}
