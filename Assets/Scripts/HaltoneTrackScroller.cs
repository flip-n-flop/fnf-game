using UnityEngine;

public class HalftoneTrackScroller : MonoBehaviour
{
    [Header("Dimensões")]
    [Tooltip("Largura de UMA imagem no RectTransform (Width)")]
    [SerializeField] private float pieceWidth = 1000f;

    [Tooltip("Overlap em pixels entre as imagens")]
    [SerializeField] private float overlapPixels = 15f;

    [Header("Movimento")]
    [Tooltip("Velocidade em pixels/segundo (positivo = esquerda, negativo = direita)")]
    [SerializeField] private float speed = 100f;

    private RectTransform rectTransform;
    private float startPosX;
    private float cycleDistance;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        startPosX = rectTransform.anchoredPosition.x;
        // Distância exata até o próximo encaixe perfeito
        cycleDistance = pieceWidth - overlapPixels;
    }

    void Update()
    {
        Vector2 pos = rectTransform.anchoredPosition;

        // Desloca continuamente
        pos.x -= speed * Time.deltaTime;

        // Quando deslocar exatamente a largura útil de 1 imagem, reseta a distância
        float traveled = pos.x - startPosX;
        if (Mathf.Abs(traveled) >= cycleDistance)
        {
            float direction = Mathf.Sign(traveled);
            pos.x -= direction * cycleDistance;
            startPosX = pos.x; // Atualiza para manter precisão contínua
        }

        rectTransform.anchoredPosition = pos;
    }
}