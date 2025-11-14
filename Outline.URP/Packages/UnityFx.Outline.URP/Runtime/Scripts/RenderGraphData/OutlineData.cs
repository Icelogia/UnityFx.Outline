using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

namespace UnityFx.Outline.URP
{
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
}
