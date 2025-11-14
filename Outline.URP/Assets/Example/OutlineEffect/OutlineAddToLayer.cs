using UnityEngine;
using UnityFx.Outline;

public class OutlineAddToLayer : MonoBehaviour
{
	[SerializeField] private OutlineEffect outlineEffect;
	[SerializeField] private int layerIndex = 0;

	private void OnEnable()
	{
		outlineEffect.AddGameObject(gameObject, layerIndex);
	}
}
