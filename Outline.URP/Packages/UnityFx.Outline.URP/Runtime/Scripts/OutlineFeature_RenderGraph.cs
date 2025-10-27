using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;

namespace UnityFx.Outline.URP
{
	public class OutlineFeature_RenderGraph : ScriptableRendererFeature
	{
	    [SerializeField] OutlineFeature_RenderGraphSettings settings;
	    RenderObjectsPass _renderObjectsPass;
	    RenderOutlinesPass  _renderOutlinesPass;

	    public class OutlineData : ContextItem
	    {
		    public TextureHandle MaskTexture;
		    public TextureHandle TempTexture;

		    public override void Reset()
		    {
			    MaskTexture = TextureHandle.nullHandle;
			    TempTexture = TextureHandle.nullHandle;
		    }

		    public void Init(RenderGraph renderGraph, RenderTextureDescriptor cameraTargetDesc)
		    {
				const RenderTextureFormat rtFormat = RenderTextureFormat.R8;

			    cameraTargetDesc.shadowSamplingMode = ShadowSamplingMode.None;
			    cameraTargetDesc.depthBufferBits = 0;
			    cameraTargetDesc.colorFormat = rtFormat;
			    cameraTargetDesc.msaaSamples = 1;

			    MaskTexture = UniversalRenderer.CreateRenderGraphTexture(
				    renderGraph, cameraTargetDesc, "Outline Mask", false, FilterMode.Bilinear);

				TempTexture = UniversalRenderer.CreateRenderGraphTexture(
					renderGraph, cameraTargetDesc, "Outline Mask", false, FilterMode.Bilinear);
		    }
	    }

	    /// <inheritdoc/>
	    public override void Create()
	    {
	        _renderObjectsPass = new RenderObjectsPass(settings);
			_renderOutlinesPass = new RenderOutlinesPass(settings);

	        _renderObjectsPass.renderPassEvent = settings._renderPassEvent;
			_renderOutlinesPass.renderPassEvent = settings._renderPassEvent;
	    }

	    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
	    {
		    if (!settings._outlineResources.OutlineMaterial)
			    return;

	        renderer.EnqueuePass(_renderObjectsPass);
	        renderer.EnqueuePass(_renderOutlinesPass);
	    }

	    [Serializable]
	    public class OutlineFeature_RenderGraphSettings
	    {
	        [Tooltip(OutlineResources.OutlineResourcesTooltip)]
			public OutlineResources _outlineResources;
			[Tooltip(OutlineResources.OutlineLayerCollectionTooltip)]
			public OutlineLayerCollection _outlineLayers;
			[SerializeField] internal OutlineSettingsWithLayerMask _outlineSettings;
			public RenderPassEvent _renderPassEvent = RenderPassEvent.AfterRenderingSkybox;
			public string[] _shaderPassNames;
			private readonly List<ShaderTagId> _shaderTagIdList = new();

			public List<ShaderTagId> GetShaderTagIDs()
			{
				if (ShouldCreateShaderTags())
					CreateShaderTags();

				return _shaderTagIdList;
			}


			private bool ShouldCreateShaderTags()
			{
				if (_shaderPassNames.Length != _shaderTagIdList.Count ||
				    _shaderTagIdList.Count == 0)
					return true;

				for (int i = 0; i < _shaderPassNames.Length; i++)
				{
					if (_shaderPassNames[i] != _shaderTagIdList[i].name)
						return true;
				}
				return false;
			}

			private void CreateShaderTags()
			{
				_shaderTagIdList.Clear();
				if (_shaderPassNames is { Length: > 0 })
				{
					foreach (var st in _shaderPassNames)
					{
						_shaderTagIdList.Add(new ShaderTagId(st));
					}
				}
				else
				{
					_shaderTagIdList.Add(new ShaderTagId("UniversalForward"));
					_shaderTagIdList.Add(new ShaderTagId("LightweightForward"));
					_shaderTagIdList.Add(new ShaderTagId("SRPDefaultUnlit"));
				}
			}
	    }

	    class RenderObjectsPass : ScriptableRenderPass
	    {
	        readonly OutlineFeature_RenderGraphSettings settings;

	        public RenderObjectsPass(OutlineFeature_RenderGraphSettings settings)
	        {
	            this.settings = settings;
	        }

	        // This class stores the data needed by the RenderGraph pass.
	        // It is passed as a parameter to the delegate function that executes the RenderGraph pass.
	        private class PassData
	        {
		        public RendererListHandle RendererList;
		        public OutlineResources OutlineResources;
		        public IOutlineSettings OutlineSettings;
	        }

	        // This static method is passed as the RenderFunc delegate to the RenderGraph render pass.
	        // It is used to execute draw commands.
	        static void ExecutePass(PassData data, RasterGraphContext context)
	        {
		        context.cmd.ClearRenderTarget(false, true, Color.clear);

		        if (data.OutlineSettings.IsAlphaTestingEnabled())
		        {
			        context.cmd.SetGlobalFloat(
				        data.OutlineResources.AlphaCutoffId,
				        data.OutlineSettings.OutlineAlphaCutoff);
		        }
		        context.cmd.DrawRendererList(data.RendererList);
	        }

	        // RecordRenderGraph is where the RenderGraph handle can be accessed, through which render passes can be added to the graph.
	        // FrameData is a context container through which URP resources can be accessed and managed.
	        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
	        {
	            const string passName = "Outline Pass: Render Objects";

	            // This adds a raster render pass to the graph, specifying the name and the data type that will be passed to the ExecutePass function.
	            using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData))
	            {
		            builder.AllowGlobalStateModification(true);
		            passData.OutlineResources = settings._outlineResources;
		            passData.OutlineSettings = settings._outlineSettings;

		            var outlineData = frameData.Create<OutlineData>();
		            var resourceData = frameData.Get<UniversalResourceData>();
		            var cameraData = frameData.Get<UniversalCameraData>();
		            var lightData = frameData.Get<UniversalLightData>();
		            var renderingData = frameData.Get<UniversalRenderingData>();

		            outlineData.Init(renderGraph, cameraData.cameraTargetDescriptor);

		            var sortFlags = cameraData.defaultOpaqueSortFlags;
		            var filteringSettings = new FilteringSettings(
			            RenderQueueRange.all,
			            settings._outlineSettings.OutlineLayerMask,
			            settings._outlineSettings.OutlineRenderingLayerMask);

		            var shaderTags = settings.GetShaderTagIDs();

		            var drawingSettings = RenderingUtils.CreateDrawingSettings(
			            shaderTags, renderingData, cameraData, lightData, sortFlags);

		            drawingSettings.enableDynamicBatching = true;
		            drawingSettings.overrideMaterial = settings._outlineResources.RenderMaterial;

		            if (settings._outlineSettings.IsAlphaTestingEnabled())
		            {
			            drawingSettings.overrideMaterialPassIndex = OutlineResources.RenderShaderAlphaTestPassId;
		            }
		            else
		            {
			            drawingSettings.overrideMaterialPassIndex = OutlineResources.RenderShaderDefaultPassId;
		            }

		            var param = new RendererListParams(renderingData.cullResults, drawingSettings, filteringSettings);
		            passData.RendererList = renderGraph.CreateRendererList(param);

		            builder.SetRenderAttachment(outlineData.MaskTexture, 0);

		            // TODO: set depth buffer to an empty target when not using depth testing
		            //if (settings._outlineSettings.IsDepthTestingEnabled())
		            //{
					//	builder.SetRenderAttachmentDepth(resourceData.activeDepthTexture);
		            //}
		            //else
		            //{
			        //    builder.SetRenderAttachmentDepth(???);
		            //}
		            builder.UseRendererList(passData.RendererList);

		            builder.SetRenderFunc((PassData data, RasterGraphContext context)
		                => ExecutePass(data, context));
	            }
	        }
	    }

	    class RenderOutlinesPass : ScriptableRenderPass
	    {
	        private readonly OutlineFeature_RenderGraphSettings settings;

	        public RenderOutlinesPass(OutlineFeature_RenderGraphSettings settings)
	        {
		        this.settings = settings;
	        }

	        class HPassData
	        {
		        public TextureHandle SourceTexture;
		        public OutlineResources OutlineResources;
		        public IOutlineSettings OutlineSettings;
		        public Material Material;
		        public MaterialPropertyBlock PropertyBlock;
	        }

	        static void ExecuteHPass(HPassData data, RasterGraphContext context)
	        {
		        context.cmd.SetGlobalFloatArray(data.OutlineResources.GaussSamplesId,
			        data.OutlineResources.GetGaussSamples(data.OutlineSettings.OutlineWidth));

				Blit(context.cmd, data.SourceTexture, OutlineResources.OutlineShaderHPassId,
			        data.Material, data.PropertyBlock, data.OutlineResources);
	        }

	        class VPassData
	        {
		        public TextureHandle SourceTexture;
		        public TextureHandle MaskTexture;
		        public OutlineResources OutlineResources;
		        public IOutlineSettings OutlineSettings;
		        public Material Material;
		        public MaterialPropertyBlock PropertyBlock;
	        }

	        static void ExecuteVPass(VPassData data, RasterGraphContext context)
	        {
		        context.cmd.SetGlobalTexture(data.OutlineResources.MaskTexId, data.MaskTexture);

		        Blit(context.cmd, data.SourceTexture, OutlineResources.OutlineShaderVPassId,
			        data.Material, data.PropertyBlock, data.OutlineResources);
	        }

	        [MethodImpl(MethodImplOptions.AggressiveInlining)]
	        static void Blit(
		        RasterCommandBuffer cmd, TextureHandle src, int shaderPass,
		        Material mat, MaterialPropertyBlock props, OutlineResources outlineResources)
	        {
		        // Set source texture as _MainTex to match Blit behavior.
		        cmd.SetGlobalTexture(outlineResources.MainTexId, src);

		        // NOTE: SystemInfo.graphicsShaderLevel check is not enough sometimes (esp. on mobiles), so there is SystemInfo.supportsInstancing
		        // check and a flag for forcing DrawMesh.
		        if (SystemInfo.graphicsShaderLevel >= 35 && SystemInfo.supportsInstancing &&
		            !outlineResources.UseFullscreenTriangleMesh)
		        {
			        cmd.DrawProcedural(Matrix4x4.identity, mat, shaderPass, MeshTopology.Triangles, 3, 1, props);
		        }
		        else
		        {
			        cmd.DrawMesh(outlineResources.FullscreenTriangleMesh, Matrix4x4.identity, mat, 0, shaderPass,
				        props);
		        }
	        }

	        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
	        {
	            var outlineData =  frameData.Get<OutlineData>();
	            var resourceData = frameData.Get<UniversalResourceData>();
	            var mat = settings._outlineResources.OutlineMaterial;
	            var props = settings._outlineResources.GetProperties(settings._outlineSettings);

	            using (var builder = renderGraph.AddRasterRenderPass("Render Outline: H Pass", out HPassData data))
	            {
		            data.SourceTexture = outlineData.MaskTexture;
		            data.OutlineResources = settings._outlineResources;
		            data.OutlineSettings = settings._outlineSettings;
		            data.Material = mat;
		            data.PropertyBlock = props;

		            builder.AllowGlobalStateModification(true);
					builder.UseTexture(outlineData.MaskTexture);
		            builder.SetRenderAttachment(outlineData.TempTexture, 0);

		            builder.SetRenderFunc((HPassData passData, RasterGraphContext context)
						=> ExecuteHPass(passData, context));
	            }

	            using (var builder = renderGraph.AddRasterRenderPass("Render Outline: V Pass", out VPassData data))
	            {
		            data.SourceTexture = outlineData.TempTexture;
		            data.MaskTexture = outlineData.MaskTexture;
		            data.OutlineResources = settings._outlineResources;
		            data.OutlineSettings = settings._outlineSettings;
		            data.Material = mat;
		            data.PropertyBlock = props;

		            builder.AllowGlobalStateModification(true);
		            builder.UseTexture(outlineData.TempTexture);
		            builder.UseTexture(outlineData.MaskTexture);
		            builder.SetRenderAttachment(resourceData.activeColorTexture, 0);

		            builder.SetRenderFunc((VPassData passData, RasterGraphContext context)
			            => ExecuteVPass(passData, context));
	            }
/*
	            var hParams = new RenderGraphUtils.BlitMaterialParameters(
		            outlineData.MaskTexture,
		            outlineData.TempTexture,
		            mat, OutlineResources.OutlineShaderHPassId, props,
		            RenderGraphUtils.FullScreenGeometryType.Mesh,
		            Shader.PropertyToID("_MainTex"));

				renderGraph.AddBlitPass(hParams, "Outline Pass: Render Outlines H");

				var vParams = new RenderGraphUtils.BlitMaterialParameters(
					outlineData.TempTexture,
					resourceData.activeColorTexture,
					mat, OutlineResources.OutlineShaderVPassId, props,
					RenderGraphUtils.FullScreenGeometryType.Mesh,
					Shader.PropertyToID("_MainTex"));

				renderGraph.AddBlitPass(vParams, "Outline Pass: Render Outlines V");
				*/
	        }
	    }
	}
}
