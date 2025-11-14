using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace UnityFx.Outline.URP
{
	public class OutlineFeature_RenderGraph : ScriptableRendererFeature
	{
	    [SerializeField] OutlineFeature_RenderGraphSettings settings;
	    RenderObjectsPass _renderObjectsPass;
	    RenderOutlinesPass  _renderOutlinesPass;
		RenderLayerOutlineCollectionPass _layerOutlineCollectionPass;

	    /// <inheritdoc/>
	    public override void Create()
	    {
	        _renderObjectsPass = new RenderObjectsPass(settings);
			_renderOutlinesPass = new RenderOutlinesPass(settings);
			_layerOutlineCollectionPass = new RenderLayerOutlineCollectionPass(settings);


			_renderObjectsPass.renderPassEvent = settings._renderPassEvent;
			_renderOutlinesPass.renderPassEvent = settings._renderPassEvent;
			_layerOutlineCollectionPass.renderPassEvent = settings._renderPassEvent;
	    }

	    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
	    {
		    if (!settings.OutlineResources)
			    return;

		    if (!settings.OutlineResources.OutlineMaterial)
			    return;

	        renderer.EnqueuePass(_renderObjectsPass);
	        renderer.EnqueuePass(_renderOutlinesPass);

			if(settings.OutlineLayers)
				renderer.EnqueuePass(_layerOutlineCollectionPass);
	    }
	}
}
