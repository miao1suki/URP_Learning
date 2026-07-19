using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

public class BlitMaterialFeature : ScriptableRendererFeature
{
    [SerializeField] BlitMaterialFeatureSettings settings = new();
    BlitMaterialFeaturePass m_ScriptablePass;
    /*
    //用于临时写死Shader
    Material m_Material;
    */

    /// <inheritdoc/>
    public override void Create()
    {
        /*
        //用于临时写死Shader
        Shader shader = Shader.Find("Shader Graphs/Desaturate");
        if(shader !=null)
            m_Material = CoreUtils.CreateEngineMaterial(shader);
        */

    m_ScriptablePass = new BlitMaterialFeaturePass(name,settings);

        m_ScriptablePass.renderPassEvent = settings.renderPassEvent;

        //开启深度图和颜色图
        m_ScriptablePass.ConfigureInput(
            ScriptableRenderPassInput.Depth |
            ScriptableRenderPassInput.Color
            );

        m_ScriptablePass.requiresIntermediateTexture = true;
    }

    //添加渲染pass，每个相机每次渲染时调用
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        /*
        //用于临时写死Shader
        if (m_Material == null)
            return;
        */
        
        renderer.EnqueuePass(m_ScriptablePass);
    }

    protected override void Dispose(bool disposing)
    {
        /*
        //用于临时写死Shader
        CoreUtils.Destroy(m_Material);
        */
    }

    // Use this class to pass around settings from the feature to the pass
    [Serializable]
    public class BlitMaterialFeatureSettings
    {
        public Material material;
        public int materialpassIndex = 0; // -1代表渲染所有pass
        public RenderPassEvent renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
    }

    class BlitMaterialFeaturePass : ScriptableRenderPass
    {
        readonly BlitMaterialFeatureSettings settings;
        readonly Material material;
        readonly string name;
        readonly int materialpassIndex;

        public BlitMaterialFeaturePass(string name, BlitMaterialFeatureSettings settings)
        {
            this.settings = settings;
            this.material = settings.material;
            this.name = name;
            this.materialpassIndex = settings.materialpassIndex;
        }

        // This class stores the data needed by the RenderGraph pass.
        // It is passed as a parameter to the delegate function that executes the RenderGraph pass.
        private class PassData
        {
            
        }

        // This static method is passed as the RenderFunc delegate to the RenderGraph render pass.
        // It is used to execute draw commands.
        static void ExecutePass(PassData data, RasterGraphContext context)
        {
            
        }

        // RecordRenderGraph is where the RenderGraph handle can be accessed, through which render passes can be added to the graph.
        // FrameData is a context container through which URP resources can be accessed and managed.
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            if (material == null)
                return;

            UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();

            if (resourceData.isActiveTargetBackBuffer)
                return;

            TextureHandle source = resourceData.activeColorTexture;

            RenderTextureDescriptor descriptor = cameraData.cameraTargetDescriptor;
            descriptor.depthBufferBits = 0;

            TextureHandle temp = UniversalRenderer.CreateRenderGraphTexture(
                renderGraph,
                descriptor,
                "_DesaturateTempTexture(用于debug的纹理名字)",
                false
            );

            var blitToTemp = new RenderGraphUtils.BlitMaterialParameters(
                source,
                temp,
                material,
                materialpassIndex
            );

            renderGraph.AddBlitPass(blitToTemp, "Desaturate Blit To Temp");

            var blitBack = new RenderGraphUtils.BlitMaterialParameters(
                temp,
                source,
                Blitter.GetBlitMaterial(TextureDimension.Tex2D),
                0
            );

            renderGraph.AddBlitPass(blitBack, "Copy Back");
        }
    }
}
