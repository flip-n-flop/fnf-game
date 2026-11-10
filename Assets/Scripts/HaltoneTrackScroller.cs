using UnityEngine;

public class HalftoneTrackScroller : MonoBehaviour
{
    [Tooltip("Velocidade do movimento")]
    [SerializeField] private float speed = 2f;

    [Tooltip("Ajuste manual para puxar a esteira para trás/frente no trilho")]
    [SerializeField] private float startOffset = 0f;

    [Tooltip("Sobreposição entre peças para casar o passo da grade de bolinhas (0.57 para Halftone 2000px)")]
    [SerializeField] private float overlap = 0.57f;

    [Tooltip("Arraste as 3 peças filhas aqui na ordem")]
    [SerializeField] private Transform[] pieces;

    private float pieceWidth;
    private float trackCenterX;
    private float initialY;
    private float initialZ;

    void Start()
    {
        if (pieces == null || pieces.Length == 0)
        {
            pieces = new Transform[transform.childCount];
            for (int i = 0; i < transform.childCount; i++)
                pieces[i] = transform.GetChild(i);
        }

        if (pieces.Length == 0) return;

        initialY = pieces[0].localPosition.y;
        initialZ = pieces[0].localPosition.z;

        SpriteRenderer sr = pieces[0].GetComponent<SpriteRenderer>();
        if (sr != null && sr.sprite != null)
        {
            pieceWidth = (sr.sprite.rect.width / sr.sprite.pixelsPerUnit) - overlap;
        }

        // Se estiver dentro de uma SpriteMask (ex: Gray_Box do InputLabel), centraliza a esteira
        // exatamente na posição da máscara no espaço local do Scroller para nunca faltar peça nas pontas.
        SpriteMask parentMask = GetComponentInParent<SpriteMask>();
        if (parentMask != null)
        {
            Vector3 maskLocalPos = transform.InverseTransformPoint(parentMask.transform.position);
            trackCenterX = maskLocalPos.x + startOffset;
        }
        else
        {
            trackCenterX = startOffset;
        }

        float leftThreshold = trackCenterX - (pieces.Length * 0.5f) * pieceWidth;

        for (int i = 0; i < pieces.Length; i++)
        {
            float xPos = leftThreshold + (i + 0.5f) * pieceWidth;
            pieces[i].localPosition = new Vector3(xPos, initialY, initialZ);
        }
    }

    void Update()
    {
        if (pieceWidth <= 0f || pieces.Length == 0) return;

        float totalWidth = pieceWidth * pieces.Length;
        float leftThreshold = trackCenterX - (pieces.Length * 0.5f) * pieceWidth;

        for (int i = 0; i < pieces.Length; i++)
        {
            Vector3 pos = pieces[i].localPosition;
            pos.x -= speed * Time.deltaTime;

            // Quando o centro da peça passa da metade esquerda da fila, salta para a ponta direita
            while (pos.x <= leftThreshold)
            {
                pos.x += totalWidth;
            }

            pos.y = initialY;
            pos.z = initialZ;
            pieces[i].localPosition = pos;
        }
    }
}
