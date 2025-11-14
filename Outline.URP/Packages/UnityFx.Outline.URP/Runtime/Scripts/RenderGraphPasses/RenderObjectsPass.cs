using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

namespace UnityFx.Outline.URP
{
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
				passData.OutlineResources = settings.OutlineResources;
				passData.OutlineSettings = settings.OutlineSettings;

				var outlineData = frameData.GetOrCreate<OutlineData>();
				var resourceData = frameData.Get<UniversalResourceData>();
				var cameraData = frameData.Get<UniversalCameraData>();
				var lightData = frameData.Get<UniversalLightData>();
				var renderingData = frameData.Get<UniversalRenderingData>();

				outlineData.Init(renderGraph, cameraData.cameraTargetDescriptor);

				var sortFlags = cameraData.defaultOpaqueSortFlags;
				var filteringSettings = new FilteringSettings(
					RenderQueueRange.all,
					settings.OutlineSettings.OutlineLayerMask,
					settings.OutlineSettings.OutlineRenderingLayerMask);

				var shaderTags = settings.GetShaderTagIDs();

				var drawingSettings = RenderingUtils.CreateDrawingSettings(
					shaderTags, renderingData, cameraData, lightData, sortFlags);

				drawingSettings.enableDynamicBatching = true;
				drawingSettings.overrideMaterial = settings.OutlineResources.RenderMaterial;

				if (settings.OutlineSettings.IsAlphaTestingEnabled())
				{
					drawingSettings.overrideMaterialPassIndex = OutlineResources.RenderShaderAlphaTestPassId;
				}
				else
				{
					drawingSettings.overrideMaterialPassIndex = OutlineResources.RenderShaderDefaultPassId;
				}

				var param = new RendererListParams(renderingData.cullResults, drawingSettings, filteringSettings);
				passData.RendererList = renderGraph.CreateRendererList(param);

				builder.UseTexture(resourceData.activeDepthTexture);
				builder.SetRenderAttachment(outlineData.MaskTexture, 0);

				builder.UseRendererList(passData.RendererList);

				builder.SetRenderFunc((PassData data, RasterGraphContext context)
					=> ExecutePass(data, context));
			}
		}
	}

}
