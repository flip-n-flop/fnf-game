using UnityEngine;

[ExecuteAlways]
public class GradientMapController : MonoBehaviour
{
    [Tooltip("Arraste aqui o material 'M_Halftone_Gradient'")]
    [SerializeField] private Material targetMaterial;

    [Tooltip("Dê dois cliques na barra para abrir o seletor de cores da Unity")]
    [SerializeField] private Gradient stageGradient = new Gradient();

    [Range(0.1f, 2.0f)]
    [SerializeField] private float intensity = 1.0f;

    private Texture2D rampTexture;

    void OnEnable()
    {
        UpdateRamp();
    }

    void OnValidate()
    {
        UpdateRamp();
    }

    public void UpdateRamp()
    {
        if (targetMaterial == null) return;

        // Cria a textura 1D de rampa se ainda não existir
        if (rampTexture == null)
        {
            rampTexture = new Texture2D(256, 1, TextureFormat.RGBA32, false);
            rampTexture.wrapMode = TextureWrapMode.Clamp;
            rampTexture.filterMode = FilterMode.Bilinear;
        }

        // Preenche a textura com base nas cores do Gradient da Unity
        for (int i = 0; i < 256; i++)
        {
            float t = (float)i / 255f;
            rampTexture.SetPixel(i, 0, stageGradient.Evaluate(t));
        }
        rampTexture.Apply();

        // Envia a textura gerada e a intensidade direto para o Shader
        targetMaterial.SetTexture("_GradientMap", rampTexture);
        targetMaterial.SetFloat("_Intensity", intensity);
    }

    // Método público útil se você quiser trocar a paleta via código ao trocar de fase
    public void SetStageGradient(Gradient newGradient)
    {
        stageGradient = newGradient;
        UpdateRamp();
    }
}