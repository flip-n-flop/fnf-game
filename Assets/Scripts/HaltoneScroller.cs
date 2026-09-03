using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(RawImage))]
public class HalftoneScroller : MonoBehaviour
{
    [Tooltip("Velocidade horizontal. Positivo = move para a esquerda, Negativo = move para a direita")]
    [SerializeField] private float horizontalSpeed = 0.1f;

    private RawImage rawImage;

    void Awake()
    {
        rawImage = GetComponent<RawImage>();
    }

    void Update()
    {
        if (rawImage == null) return;

        // Mesma lógica original: move o UV continuamente
        Rect uv = rawImage.uvRect;
        uv.x += horizontalSpeed * Time.deltaTime;
        rawImage.uvRect = uv;
    }
}