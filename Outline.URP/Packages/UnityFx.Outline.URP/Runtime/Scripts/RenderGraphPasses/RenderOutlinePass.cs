using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

namespace UnityFx.Outline.URP
{
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
			var outlineData = frameData.Get<OutlineData>();
			var resourceData = frameData.Get<UniversalResourceData>();
			var mat = settings.OutlineResources.OutlineMaterial;
			var props = settings.OutlineResources.GetProperties(settings.OutlineSettings);

			using (var builder = renderGraph.AddRasterRenderPass("Render Outline: H Pass", out HPassData data))
			{
				data.SourceTexture = outlineData.MaskTexture;
				data.OutlineResources = settings.OutlineResources;
				data.OutlineSettings = settings.OutlineSettings;
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
				data.OutlineResources = settings.OutlineResources;
				data.OutlineSettings = settings.OutlineSettings;
				data.Material = mat;
				data.PropertyBlock = props;

				builder.AllowGlobalStateModification(true);
				builder.UseTexture(outlineData.TempTexture);
				builder.UseTexture(outlineData.MaskTexture);
				builder.SetRenderAttachment(resourceData.activeColorTexture, 0);

				builder.SetRenderFunc((VPassData passData, RasterGraphContext context)
					=> ExecuteVPass(passData, context));
			}
		}
	}
}
