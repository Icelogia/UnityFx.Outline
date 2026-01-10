using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace UnityFx.Outline.URP
{
	public class OutlineFeature_RenderGraph : ScriptableRendererFeature
	{
	    [SerializeField] OutlineFeature_RenderGraphSettings settings;
		RenderLayerOutlineCollectionPass _layerOutlineCollectionPass;

	    /// <inheritdoc/>
	    public override void Create()
	    {
			_layerOutlineCollectionPass = new RenderLayerOutlineCollectionPass(settings);
			_layerOutlineCollectionPass.renderPassEvent = settings._renderPassEvent;
	    }

	    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
	    {
		    if (!settings.OutlineResources)
			    return;

		    if (!settings.OutlineResources.OutlineMaterial)
			    return;

			if(settings.OutlineLayers)
				renderer.EnqueuePass(_layerOutlineCollectionPass);
	    }
	}
}
