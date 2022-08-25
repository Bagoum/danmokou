using System;
using Danmokou.Core;
using Danmokou.Services;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Danmokou.Graphics {

//This is an attempt at writing a very trivial blit feature.
// It doesn't really work properly.
// You can instead capture EndCameraRendering and use normal Graphics.Blit, as is done in SeijaCamera.

//Note issues:
// https://issuetracker.unity3d.com/issues/performance-adding-an-empty-renderer-feature-causes-urp-to-do-an-extra-unnecessary-blit
public class URPBlitRender : ScriptableRendererFeature {

    [Serializable]
    public class Settings {
        public RenderPassEvent insertion = RenderPassEvent.AfterRendering;
        public string profilerTag = "Blit URP render";
        
    }

    public Settings settings = new();
    private RenderPass renderPass = null!;

    public override void Create() {
        renderPass = new(this);
    }

    public override void AddRenderPasses(ScriptableRenderer r, ref RenderingData data) {
        if (URPCameraManager.Find(data.cameraData.camera) is SeijaCamera sc) {
            renderPass.ConfigureInput(ScriptableRenderPassInput.Color);
            renderPass.PerCameraSetup(sc, r);
            r.EnqueuePass(renderPass);
        }
    }

    public class RenderPass : ScriptableRenderPass {
        private readonly URPBlitRender feature;
        private readonly ProfilingSampler profiler;
        private SeijaCamera sc = null!;
        private ScriptableRenderer renderer = null!;
        private RTHandle tempTex = null!;

        public RenderPass(URPBlitRender feature) {
            this.feature = feature;
            this.profiler = new(feature.settings.profilerTag);
            this.renderPassEvent = feature.settings.insertion;
        }

        public void PerCameraSetup(SeijaCamera s, ScriptableRenderer r) {
            this.sc = s;
            this.renderer = r;
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor rtDesc) {
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData data) {
            var desc = data.cameraData.cameraTargetDescriptor;
            //HACK-- if you don't do this, it creates a depth texture
            desc.depthBufferBits = 0;
            RenderingUtils.ReAllocateIfNeeded(ref tempTex, desc, FilterMode.Bilinear, TextureWrapMode.Clamp,
                name: "_URPBlitRenderTex1");
            CoreUtils.SetRenderTarget(cmd, renderer.cameraColorTargetHandle);
            ConfigureTarget(renderer.cameraColorTargetHandle);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData data) {
            var cmd = CommandBufferPool.Get(feature.settings.profilerTag);
            using (new ProfilingScope(cmd, profiler)) {
                cmd.Blit(renderer.cameraColorTargetHandle, tempTex, sc.SeijaMaterial);
                cmd.Blit(tempTex, renderer.cameraColorTargetHandle);
                //Blitter.Blit causes really strange issues with the sizes being incorrect due to MaxSize handling
                // on RTSystem
                //Blitter.BlitCameraTexture(cmd, renderer.cameraColorTargetHandle, tempTex);
                //Blitter.BlitCameraTexture(cmd, tempTex, renderer.cameraColorTargetHandle);
            }
            context.ExecuteCommandBuffer(cmd);
            cmd.Clear();
            CommandBufferPool.Release(cmd);
        }
    }
}

}