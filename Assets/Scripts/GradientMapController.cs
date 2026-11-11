using UnityEngine;

[ExecuteAlways]
public class GradientMapController : MonoBehaviour
{
    public static GradientMapController Instance { get; private set; }

    [Tooltip("Arraste aqui o material 'M_Halftone_Gradient'")]
    [SerializeField] private Material targetMaterial;

    [Tooltip("Gradiente base (a luminosidade de cada ponto é preservada, e a matiz e saturação são ajustadas pelos controles)")]
    [SerializeField] private Gradient stageGradient = new Gradient();

    [Range(0.1f, 2.0f)]
    [SerializeField] private float intensity = 1.0f;

    [Header("Saturation Controls (0 = Grayscale / B&W, 1 = Full Saturation)")]
    [Tooltip("Saturação global aplicada ao gradiente")]
    [Range(0f, 1f)]
    [SerializeField] private float saturation = 0.35f;

    [Tooltip("Se ativado, utiliza os controles individuais de saturação por ponto abaixo ao invés do valor global.")]
    [SerializeField] private bool usePerPointSaturation = false;

    [Tooltip("Saturação do Ponto 1 (extrema esquerda)")]
    [Range(0f, 1f)]
    [SerializeField] private float point1Saturation = 0.35f;

    [Tooltip("Saturação do Ponto 2 (meio-esquerda)")]
    [Range(0f, 1f)]
    [SerializeField] private float point2Saturation = 0.35f;

    [Tooltip("Saturação do Ponto 3 (meio-direita)")]
    [Range(0f, 1f)]
    [SerializeField] private float point3Saturation = 0.35f;

    [Tooltip("Saturação do Ponto 4 (extrema direita)")]
    [Range(0f, 1f)]
    [SerializeField] private float point4Saturation = 0.35f;

    [Header("Absolute Point Hue Controls (0 = Red, 0.33 = Green, 0.66 = Blue, 0.75 = Purple)")]
    [Tooltip("Matiz absoluta do Ponto 1 (extrema esquerda)")]
    [Range(0f, 1f)]
    [SerializeField] private float point1Hue = 0.75f;

    [Tooltip("Matiz absoluta do Ponto 2 (meio-esquerda)")]
    [Range(0f, 1f)]
    [SerializeField] private float point2Hue = 0.75f;

    [Tooltip("Matiz absoluta do Ponto 3 (meio-direita)")]
    [Range(0f, 1f)]
    [SerializeField] private float point3Hue = 0.75f;

    [Tooltip("Matiz absoluta do Ponto 4 (extrema direita)")]
    [Range(0f, 1f)]
    [SerializeField] private float point4Hue = 0.75f;

    [Header("Signal Line Fade Color Reference")]
    [Tooltip("Índice da chave de cor do gradiente usada para o fade do SignalLineRenderer (0 = 1ª cor, 1 = 2ª cor, etc.)")]
    [SerializeField] private int backgroundColorKeyIndex = 1;

    private Texture2D rampTexture;

    /// <summary>
    /// Retorna a cor de fundo atual avaliada no ponto escolhido do gradiente (usada pelo SignalLineRenderer).
    /// </summary>
    public Color CurrentBackgroundColor
    {
        get
        {
            if (stageGradient == null) return Color.black;

            GradientColorKey[] keys = stageGradient.colorKeys;
            if (keys != null && keys.Length > 0)
            {
                int index = Mathf.Clamp(backgroundColorKeyIndex, 0, keys.Length - 1);
                return keys[index].color;
            }

            return stageGradient.Evaluate(0f);
        }
    }

    private void Awake()
    {
        Instance = this;
        UpdateRamp();
    }

    private void OnEnable()
    {
        Instance = this;
        UpdateRamp();
    }

    private void OnDisable()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void OnValidate()
    {
        Instance = this;
        UpdateRamp();
    }

    public void UpdateRamp()
    {
        if (targetMaterial == null) return;

        if (rampTexture == null)
        {
            rampTexture = new Texture2D(256, 1, TextureFormat.RGBA32, false);
            rampTexture.wrapMode = TextureWrapMode.Clamp;
            rampTexture.filterMode = FilterMode.Bilinear;
        }

        GradientColorKey[] keys = stageGradient.colorKeys;
        float[] hueParams = { point1Hue, point2Hue, point3Hue, point4Hue };
        float[] satParams = { point1Saturation, point2Saturation, point3Saturation, point4Saturation };

        for (int i = 0; i < keys.Length; i++)
        {
            // Extrai o H, S e V atuais do ponto para manter apenas a luminosidade (v) original
            Color.RGBToHSV(keys[i].color, out float _, out float _, out float v);

            // Usa diretamente a matiz escolhida no slider correspondente
            float newHue = (i < hueParams.Length) ? hueParams[i] : 0f;

            // Determina a saturação escolhida (global ou individual por ponto)
            float newSat = usePerPointSaturation
                ? ((i < satParams.Length) ? satParams[i] : saturation)
                : saturation;

            // Reconstrói a cor mantendo apenas a Luminosidade (v) e aplicando o novo Hue e Saturação
            Color newPointColor = Color.HSVToRGB(newHue, Mathf.Clamp01(newSat), v);
            keys[i] = new GradientColorKey(newPointColor, keys[i].time);
        }

        // Atualiza o próprio stageGradient para refletir a cor na barra do Inspector
        stageGradient.SetKeys(keys, stageGradient.alphaKeys);

        for (int i = 0; i < 256; i++)
        {
            float t = (float)i / 255f;
            rampTexture.SetPixel(i, 0, stageGradient.Evaluate(t));
        }
        rampTexture.Apply();

        targetMaterial.SetTexture("_GradientMap", rampTexture);
        targetMaterial.SetFloat("_Intensity", intensity);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            SignalLineRenderer slr = FindFirstObjectByType<SignalLineRenderer>();
            if (slr != null)
            {
                slr.RefreshFadeColorFromGradient();
            }
        }
#endif
    }

    // Métodos para controlar a saturação via código
    public void SetUniformSaturation(float targetSaturation)
    {
        saturation = targetSaturation;
        usePerPointSaturation = false;
        point1Saturation = targetSaturation;
        point2Saturation = targetSaturation;
        point3Saturation = targetSaturation;
        point4Saturation = targetSaturation;
        UpdateRamp();
    }

    public void SetPointSaturations(float s1, float s2, float s3, float s4)
    {
        usePerPointSaturation = true;
        point1Saturation = s1;
        point2Saturation = s2;
        point3Saturation = s3;
        point4Saturation = s4;
        UpdateRamp();
    }

    // Método útil caso você queira mudar todos os 4 pontos para uma mesma matiz via código
    public void SetUniformHue(float targetHue)
    {
        point1Hue = targetHue;
        point2Hue = targetHue;
        point3Hue = targetHue;
        point4Hue = targetHue;
        UpdateRamp();
    }

    // Método para definir matizes individuais via código
    public void SetPointHues(float h1, float h2, float h3, float h4)
    {
        point1Hue = h1;
        point2Hue = h2;
        point3Hue = h3;
        point4Hue = h4;
        UpdateRamp();
    }

    public void SetStageGradient(Gradient newGradient)
    {
        stageGradient = newGradient;
        UpdateRamp();
    }
}