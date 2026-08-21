using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class SelectiveRenderScaleFeature : ScriptableRendererFeature
{
    [System.Serializable]
    public class Settings
    {
        [Header("Low Resolution Rendering")]
        [Range(0.05f, 1.0f)] public float renderScale = 0.5f;
        public LayerMask lowResolutionLayerMask;
        [Tooltip("Opaque full-resolution layers that must occlude low-resolution transparent objects. Usually set this to the same layers enabled in Universal Renderer Data > Filtering > Opaque Layer Mask.")] 
		public LayerMask highResolutionOpaqueOccluderMask = ~0;
        [Header("Rendering")]
        [Tooltip("Show the effect in Scene View.")] public bool renderInSceneView = true;
        public bool renderInReflectionCameras = true;
        [Header("Shaders")]
        [Tooltip("Shader: Hidden/LowResWorld/UpscaleDepth")] public Shader depthUpscaleShader;
        [Tooltip("Shader: Hidden/LowResWorld/CopyOpaqueDepth")] public Shader copyOpaqueDepthShader;
        [Tooltip("Shader: Hidden/LowResWorld/CompositeTransparent")] public Shader transparentCompositeShader;
    }

    public Settings settings = new Settings();

    private LowResTargets targets;
    private LowResOpaquePass lowResOpaquePass;
    private OpaqueCompositePass opaqueCompositePass;
    private DepthUpscalePass depthUpscalePass;
    private PrepareTransparentDepthPass prepareTransparentDepthPass;
    private LowResTransparentPass lowResTransparentPass;
    private TransparentCompositePass transparentCompositePass;
    private Material depthUpscaleMaterial;
    private Material copyOpaqueDepthMaterial;
    private Material transparentCompositeMaterial;

    private static readonly int LowResWorldDepthId = Shader.PropertyToID("_LowResWorldDepth");
    private static readonly int LowResSourceDepthId = Shader.PropertyToID("_LowResSourceDepth");

    public override void Create()
    {
        targets?.Dispose();
        targets = null;
        DestroyMaterials();
        if (settings.depthUpscaleShader == null) settings.depthUpscaleShader = Shader.Find("Hidden/LowResWorld/UpscaleDepth");
        if (settings.copyOpaqueDepthShader == null) settings.copyOpaqueDepthShader = Shader.Find("Hidden/LowResWorld/CopyOpaqueDepth");
        if (settings.transparentCompositeShader == null) settings.transparentCompositeShader = Shader.Find("Hidden/LowResWorld/CompositeTransparent");
        if (settings.depthUpscaleShader != null) depthUpscaleMaterial = CoreUtils.CreateEngineMaterial(settings.depthUpscaleShader);
        if (settings.copyOpaqueDepthShader != null) copyOpaqueDepthMaterial = CoreUtils.CreateEngineMaterial(settings.copyOpaqueDepthShader);
        if (settings.transparentCompositeShader != null) transparentCompositeMaterial = CoreUtils.CreateEngineMaterial(settings.transparentCompositeShader);
        if (depthUpscaleMaterial == null) Debug.LogError("[SelectiveRenderScaleFeature] Missing shader: Hidden/LowResWorld/UpscaleDepth.");
        if (copyOpaqueDepthMaterial == null) Debug.LogError("[SelectiveRenderScaleFeature] Missing shader: Hidden/LowResWorld/CopyOpaqueDepth.");
        if (transparentCompositeMaterial == null) Debug.LogError("[SelectiveRenderScaleFeature] Missing shader: Hidden/LowResWorld/CompositeTransparent.");
        targets = new LowResTargets(settings);
        lowResOpaquePass = new LowResOpaquePass(settings, targets);
        opaqueCompositePass = new OpaqueCompositePass(targets);
        depthUpscalePass = new DepthUpscalePass(targets, depthUpscaleMaterial);
        prepareTransparentDepthPass = new PrepareTransparentDepthPass(settings, targets, copyOpaqueDepthMaterial);
        lowResTransparentPass = new LowResTransparentPass(settings, targets);
        transparentCompositePass = new TransparentCompositePass(targets, transparentCompositeMaterial);
        lowResOpaquePass.renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.AfterRenderingPrePasses + 1);
        opaqueCompositePass.renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.AfterRenderingPrePasses + 2);
        depthUpscalePass.renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.AfterRenderingPrePasses + 3);
        prepareTransparentDepthPass.renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.AfterRenderingSkybox + 1);
        lowResTransparentPass.renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.AfterRenderingSkybox + 2);
        transparentCompositePass.renderPassEvent = (RenderPassEvent)((int)RenderPassEvent.AfterRenderingSkybox + 3);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (!ShouldRender(in renderingData)) return;
        if (depthUpscaleMaterial == null || copyOpaqueDepthMaterial == null || transparentCompositeMaterial == null) return;
        renderer.EnqueuePass(lowResOpaquePass);
        renderer.EnqueuePass(opaqueCompositePass);
        renderer.EnqueuePass(depthUpscalePass);
        renderer.EnqueuePass(prepareTransparentDepthPass);
        renderer.EnqueuePass(lowResTransparentPass);
        renderer.EnqueuePass(transparentCompositePass);
    }

    public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
    {
        if (!ShouldRender(in renderingData)) return;
        if (depthUpscaleMaterial == null || copyOpaqueDepthMaterial == null || transparentCompositeMaterial == null) return;
        RTHandle cameraColor = renderer.cameraColorTargetHandle;
        RTHandle cameraDepth = renderer.cameraDepthTargetHandle;
        opaqueCompositePass.SetDestination(cameraColor);
        depthUpscalePass.SetTargets(cameraColor, cameraDepth);
        transparentCompositePass.SetDestination(cameraColor);
    }

    private bool ShouldRender(in RenderingData renderingData)
    {
        Camera camera = renderingData.cameraData.camera;
        CameraType cameraType = renderingData.cameraData.cameraType;
        if (cameraType == CameraType.Preview) return false;
        if (cameraType == CameraType.Reflection && !settings.renderInReflectionCameras) return false;
        if (cameraType == CameraType.SceneView && !settings.renderInSceneView) return false;
        if ((camera.cullingMask & settings.lowResolutionLayerMask.value) == 0) return false;
        return true;
    }

    protected override void Dispose(bool disposing)
    {
        targets?.Dispose();
        targets = null;
        lowResOpaquePass = null;
        opaqueCompositePass = null;
        depthUpscalePass = null;
        prepareTransparentDepthPass = null;
        lowResTransparentPass = null;
        transparentCompositePass = null;
        DestroyMaterials();
    }

    private void DestroyMaterials()
    {
        if (depthUpscaleMaterial != null) CoreUtils.Destroy(depthUpscaleMaterial);
        if (copyOpaqueDepthMaterial != null) CoreUtils.Destroy(copyOpaqueDepthMaterial);
        if (transparentCompositeMaterial != null) CoreUtils.Destroy(transparentCompositeMaterial);
        depthUpscaleMaterial = null;
        copyOpaqueDepthMaterial = null;
        transparentCompositeMaterial = null;
    }

    private sealed class LowResTargets
    {
        private readonly Settings settings;
        public RTHandle OpaqueColor;
        public RTHandle OpaqueDepth;
        public RTHandle TransparentColor;
        public RTHandle TransparentDepth;

        public LowResTargets(Settings settings)
        {
            this.settings = settings;
        }

        public void Ensure(ref RenderingData renderingData)
        {
            RenderTextureDescriptor cameraDescriptor = renderingData.cameraData.cameraTargetDescriptor;
            float renderScale = Mathf.Clamp(settings.renderScale, 0.05f, 1.0f);
            int width = Mathf.Max(1, Mathf.RoundToInt(cameraDescriptor.width * renderScale));
            int height = Mathf.Max(1, Mathf.RoundToInt(cameraDescriptor.height * renderScale));
            RenderTextureDescriptor opaqueColorDescriptor = cameraDescriptor;
            opaqueColorDescriptor.width = width;
            opaqueColorDescriptor.height = height;
            opaqueColorDescriptor.depthBufferBits = 0;
            opaqueColorDescriptor.depthStencilFormat = GraphicsFormat.None;
            opaqueColorDescriptor.msaaSamples = 1;
            opaqueColorDescriptor.useMipMap = false;
            opaqueColorDescriptor.autoGenerateMips = false;
            RenderTextureDescriptor transparentColorDescriptor = opaqueColorDescriptor;
            if (!GraphicsFormatUtility.HasAlphaChannel(transparentColorDescriptor.graphicsFormat)) transparentColorDescriptor.graphicsFormat = GraphicsFormatUtility.ConvertToAlphaFormat(transparentColorDescriptor.graphicsFormat);
            if (!GraphicsFormatUtility.HasAlphaChannel(transparentColorDescriptor.graphicsFormat)) transparentColorDescriptor.graphicsFormat = GraphicsFormat.R16G16B16A16_SFloat;
            RenderTextureDescriptor depthDescriptor = cameraDescriptor;
            depthDescriptor.width = width;
            depthDescriptor.height = height;
            depthDescriptor.graphicsFormat = GraphicsFormat.None;
            depthDescriptor.depthStencilFormat = GraphicsFormat.D24_UNorm_S8_UInt;
            depthDescriptor.depthBufferBits = 24;
            depthDescriptor.msaaSamples = 1;
            depthDescriptor.useMipMap = false;
            depthDescriptor.autoGenerateMips = false;
            RenderingUtils.ReAllocateIfNeeded(ref OpaqueColor, opaqueColorDescriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_LowResOpaqueColor");
            RenderingUtils.ReAllocateIfNeeded(ref OpaqueDepth, depthDescriptor, FilterMode.Point, TextureWrapMode.Clamp, isShadowMap: false, name: "_LowResOpaqueDepth");
            RenderingUtils.ReAllocateIfNeeded(ref TransparentColor, transparentColorDescriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_LowResTransparentColor");
            RenderingUtils.ReAllocateIfNeeded(ref TransparentDepth, depthDescriptor, FilterMode.Point, TextureWrapMode.Clamp, isShadowMap: false, name: "_LowResTransparentDepth");
        }

        public void Dispose()
        {
            if (OpaqueColor != null) OpaqueColor.Release();
            if (OpaqueDepth != null) OpaqueDepth.Release();
            if (TransparentColor != null) TransparentColor.Release();
            if (TransparentDepth != null) TransparentDepth.Release();
            OpaqueColor = null;
            OpaqueDepth = null;
            TransparentColor = null;
            TransparentDepth = null;
        }
    }

    private sealed class LowResOpaquePass : ScriptableRenderPass
    {
        private readonly Settings settings;
        private readonly LowResTargets targets;
        private readonly List<ShaderTagId> shaderTags = new List<ShaderTagId> { new ShaderTagId("UniversalForward"), new ShaderTagId("UniversalForwardOnly"), new ShaderTagId("SRPDefaultUnlit") };

        public LowResOpaquePass(Settings settings, LowResTargets targets)
        {
            this.settings = settings;
            this.targets = targets;
            profilingSampler = new ProfilingSampler("Low Res Opaque World");
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            targets.Ensure(ref renderingData);
            ConfigureTarget(targets.OpaqueColor, targets.OpaqueDepth);
            ConfigureClear(ClearFlag.All, renderingData.cameraData.camera.backgroundColor);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (targets.OpaqueColor == null || targets.OpaqueDepth == null) return;
            CommandBuffer cmd = CommandBufferPool.Get("Low Res Opaque World");
            using (new ProfilingScope(cmd, profilingSampler))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                DrawingSettings drawingSettings = CreateDrawingSettings(shaderTags, ref renderingData, renderingData.cameraData.defaultOpaqueSortFlags);
                FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.opaque, settings.lowResolutionLayerMask);
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    private sealed class OpaqueCompositePass : ScriptableRenderPass
    {
        private readonly LowResTargets targets;
        private RTHandle destination;

        public OpaqueCompositePass(LowResTargets targets)
        {
            this.targets = targets;
            profilingSampler = new ProfilingSampler("Composite Low Res Opaque Color");
        }

        public void SetDestination(RTHandle destination)
        {
            this.destination = destination;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if (destination == null) return;
            ConfigureTarget(destination);
            ConfigureClear(ClearFlag.None, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (targets.OpaqueColor == null || destination == null) return;
            CommandBuffer cmd = CommandBufferPool.Get("Composite Low Res Opaque Color");
            using (new ProfilingScope(cmd, profilingSampler))
            {
                Blitter.BlitCameraTexture(cmd, targets.OpaqueColor, destination);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    private sealed class DepthUpscalePass : ScriptableRenderPass
    {
        private readonly LowResTargets targets;
        private readonly Material material;
        private RTHandle cameraColor;
        private RTHandle cameraDepth;

        public DepthUpscalePass(LowResTargets targets, Material material)
        {
            this.targets = targets;
            this.material = material;
            profilingSampler = new ProfilingSampler("Upscale Low Res Opaque Depth");
        }

        public void SetTargets(RTHandle color, RTHandle depth)
        {
            cameraColor = color;
            cameraDepth = depth;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if (cameraColor == null || cameraDepth == null) return;
            ConfigureTarget(cameraColor, cameraDepth);
            ConfigureClear(ClearFlag.None, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (targets.OpaqueDepth == null || cameraColor == null || cameraDepth == null || material == null) return;
            CommandBuffer cmd = CommandBufferPool.Get("Upscale Low Res Opaque Depth");
            using (new ProfilingScope(cmd, profilingSampler))
            {
                cmd.SetGlobalTexture(LowResWorldDepthId, targets.OpaqueDepth);
                CoreUtils.DrawFullScreen(cmd, material);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    private sealed class PrepareTransparentDepthPass : ScriptableRenderPass
    {
        private readonly Settings settings;
        private readonly LowResTargets targets;
        private readonly Material copyDepthMaterial;
        private readonly List<ShaderTagId> depthShaderTags = new List<ShaderTagId> { new ShaderTagId("DepthOnly") };

        public PrepareTransparentDepthPass(Settings settings, LowResTargets targets, Material copyDepthMaterial)
        {
            this.settings = settings;
            this.targets = targets;
            this.copyDepthMaterial = copyDepthMaterial;
            profilingSampler = new ProfilingSampler("Prepare Low Res Transparent Depth");
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if (targets.TransparentColor == null || targets.TransparentDepth == null) return;
            ConfigureTarget(targets.TransparentColor, targets.TransparentDepth);
            ConfigureClear(ClearFlag.All, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (targets.OpaqueDepth == null || targets.TransparentColor == null || targets.TransparentDepth == null || copyDepthMaterial == null) return;
            CommandBuffer cmd = CommandBufferPool.Get("Prepare Low Res Transparent Depth");
            using (new ProfilingScope(cmd, profilingSampler))
            {
                cmd.SetGlobalTexture(LowResSourceDepthId, targets.OpaqueDepth);
                CoreUtils.DrawFullScreen(cmd, copyDepthMaterial);
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                int occluderMask = settings.highResolutionOpaqueOccluderMask.value & ~settings.lowResolutionLayerMask.value;
                DrawingSettings drawingSettings = CreateDrawingSettings(depthShaderTags, ref renderingData, renderingData.cameraData.defaultOpaqueSortFlags);
                FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.opaque, occluderMask);
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    private sealed class LowResTransparentPass : ScriptableRenderPass
    {
        private readonly Settings settings;
        private readonly LowResTargets targets;
        private readonly List<ShaderTagId> shaderTags = new List<ShaderTagId> { new ShaderTagId("UniversalForward"), new ShaderTagId("UniversalForwardOnly"), new ShaderTagId("SRPDefaultUnlit") };

        public LowResTransparentPass(Settings settings, LowResTargets targets)
        {
            this.settings = settings;
            this.targets = targets;
            profilingSampler = new ProfilingSampler("Low Res Transparent World");
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if (targets.TransparentColor == null || targets.TransparentDepth == null) return;
            ConfigureTarget(targets.TransparentColor, targets.TransparentDepth);
            ConfigureClear(ClearFlag.None, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (targets.TransparentColor == null || targets.TransparentDepth == null) return;
            CommandBuffer cmd = CommandBufferPool.Get("Low Res Transparent World");
            using (new ProfilingScope(cmd, profilingSampler))
            {
                context.ExecuteCommandBuffer(cmd);
                cmd.Clear();
                DrawingSettings drawingSettings = CreateDrawingSettings(shaderTags, ref renderingData, SortingCriteria.CommonTransparent);
                FilteringSettings filteringSettings = new FilteringSettings(RenderQueueRange.transparent, settings.lowResolutionLayerMask);
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }

    private sealed class TransparentCompositePass : ScriptableRenderPass
    {
        private readonly LowResTargets targets;
        private readonly Material material;
        private RTHandle destination;

        public TransparentCompositePass(LowResTargets targets, Material material)
        {
            this.targets = targets;
            this.material = material;
            profilingSampler = new ProfilingSampler("Composite Low Res Transparent Color");
        }

        public void SetDestination(RTHandle destination)
        {
            this.destination = destination;
        }

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if (destination == null) return;
            ConfigureTarget(destination);
            ConfigureClear(ClearFlag.None, Color.clear);
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (targets.TransparentColor == null || destination == null || material == null) return;
            CommandBuffer cmd = CommandBufferPool.Get("Composite Low Res Transparent Color");
            using (new ProfilingScope(cmd, profilingSampler))
            {
                Blitter.BlitCameraTexture(cmd, targets.TransparentColor, destination, material, 0);
            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }
    }
}