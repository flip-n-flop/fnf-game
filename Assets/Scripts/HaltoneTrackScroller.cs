using UnityEngine;

public class HalftoneTrackScroller : MonoBehaviour
{
    [Tooltip("Velocidade do movimento")]
    [SerializeField] private float speed = 2f;

    [Tooltip("Ajuste manual para puxar a esteira para trás/frente no trilho")]
    [SerializeField] private float startOffset = 0f;

    [Tooltip("Pequena sobreposição para evitar linhas pretas/gaps")]
    [SerializeField] private float overlap = 0.02f;

    [Tooltip("Arraste as 3 peças filhas aqui na ordem")]
    [SerializeField] private Transform[] pieces;

    private float pieceWidth;

    void Start()
    {
        if (pieces == null || pieces.Length == 0)
        {
            pieces = new Transform[transform.childCount];
            for (int i = 0; i < transform.childCount; i++)
                pieces[i] = transform.GetChild(i);
        }

        if (pieces.Length == 0) return;

        SpriteRenderer sr = pieces[0].GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            pieceWidth = (sr.sprite.rect.width / sr.sprite.pixelsPerUnit) - overlap;
        }

        // Em vez de começar em 0, centraliza a fila (ex: com 3 peças, começa em -1, 0, +1)
        // Isso faz uma peça já nascer cobrindo o canto inferior/esquerdo
        float startIndex = -Mathf.Floor(pieces.Length / 2f);

        for (int i = 0; i < pieces.Length; i++)
        {
            float xPos = (startIndex + i) * pieceWidth + startOffset;
            pieces[i].localPosition = new Vector3(xPos, 0, 0);
        }
    }

    void Update()
    {
        if (pieceWidth <= 0f || pieces.Length == 0) return;

        float totalWidth = pieceWidth * pieces.Length;
        // Limite baseado em onde a fila foi centralizada
        float leftThreshold = (-Mathf.Floor(pieces.Length / 2f) - 1f) * pieceWidth + startOffset;

        for (int i = 0; i < pieces.Length; i++)
        {
            pieces[i].localPosition += Vector3.left * (speed * Time.deltaTime);

            // Quando passar completamente do limite esquerdo, pula para o final
            if (pieces[i].localPosition.x <= leftThreshold)
            {
                pieces[i].localPosition += new Vector3(totalWidth, 0, 0);
            }
        }
    }
}