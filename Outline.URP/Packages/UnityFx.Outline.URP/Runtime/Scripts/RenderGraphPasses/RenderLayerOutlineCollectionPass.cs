using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace UnityFx.Outline.URP
{
	public class RenderLayerOutlineCollectionPass : ScriptableRenderPass
	{
		private readonly OutlineFeature_RenderGraphSettings _settings;
		private readonly List<OutlineRenderObject> _renderObjects = new List<OutlineRenderObject>();

		public RenderLayerOutlineCollectionPass(OutlineFeature_RenderGraphSettings settings)
		{
			this._settings = settings;
		}

		public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameContext)
		{
			var urpResourceData = frameContext.Get<UniversalResourceData>();
			var renderingData = frameContext.Get<UniversalRenderingData>();
			var camData = frameContext.Get<UniversalCameraData>();
			var lightData = frameContext.Get<UniversalLightData>();
			var outlineResources = _settings.OutlineResources;
			var outlineSettings = _settings.OutlineSettings;
			var colCopyDesc = renderGraph.GetTextureDesc(urpResourceData.activeColorTexture);

			var sortingCriteria = camData.defaultOpaqueSortFlags;
			var filterSettings = new FilteringSettings(RenderQueueRange.all, outlineSettings.OutlineLayerMask, outlineSettings.OutlineRenderingLayerMask);
			var shaderTagId = _settings.GetShaderTagIDs();

			var drawingSettings = RenderingUtils.CreateDrawingSettings(shaderTagId, renderingData, camData, lightData, sortingCriteria);

			if (outlineSettings.IsAlphaTestingEnabled())
			{
				drawingSettings.overrideMaterialPassIndex = OutlineResources.RenderShaderAlphaTestPassId;
			}
			else
			{
				drawingSettings.overrideMaterialPassIndex = OutlineResources.RenderShaderDefaultPassId;
			}

			var rendererListParams = new RendererListParams(renderingData.cullResults, drawingSettings, filterSettings);

			using (var builder = renderGraph.AddUnsafePass<OutlineLayerPassData>(OutlineResources.EffectName, out OutlineLayerPassData passData))
			{
				passData.colorHandle = urpResourceData.activeColorTexture;
				passData.depthHandle = urpResourceData.activeDepthTexture;
				passData.descriptor = camData.cameraTargetDescriptor;
				passData.outlineResources = outlineResources;

				builder.UseTexture(passData.colorHandle, AccessFlags.Write);

				builder.SetRenderFunc((OutlineLayerPassData passData, UnsafeGraphContext context) =>
				{
					CommandBuffer cmd = CommandBufferHelpers.GetNativeCommandBuffer(context.cmd);

					using (var renderer = new OutlineRenderer(cmd, passData.outlineResources, passData.colorHandle, passData.depthHandle, passData.descriptor))
					{
						_renderObjects.Clear();
						_settings.OutlineLayers.GetRenderObjects(_renderObjects);
						renderer.Render(_renderObjects);
					}
				});
			}
		}

		public class OutlineLayerPassData
		{
			public IOutlineSettings outlineSettings;
			public OutlineResources outlineResources;
			public RenderTextureDescriptor descriptor;
			public TextureHandle colorHandle;
			public TextureHandle depthHandle;
		}
	}
}
