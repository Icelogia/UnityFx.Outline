using System.Collections.Generic;
using System;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering;

namespace UnityFx.Outline.URP
{
	[Serializable]
	public class OutlineFeature_RenderGraphSettings
	{
		[Tooltip(OutlineResources.OutlineResourcesTooltip)]
		public OutlineResources OutlineResources;
		[Tooltip(OutlineResources.OutlineLayerCollectionTooltip)]
		public OutlineLayerCollection OutlineLayers;
		[SerializeField]
		internal OutlineSettingsWithLayerMask OutlineSettings;

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
}
