using TMPro;
using UnityEngine;

/// <summary>
/// Animates each character of a <see cref="TMP_Text"/> component in a continuous wave motion.
/// Uses <see cref="Time.unscaledTime"/> so the animation keeps running even when <see cref="Time.timeScale"/> is zero.
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class LoadingWaveText : MonoBehaviour
{
    [Header("Wave Animation")]
    [Tooltip("Speed of the wave oscillation.")]
    [SerializeField] private float waveSpeed = 6f;

    [Tooltip("Vertical displacement amplitude in local text units.")]
    [SerializeField] private float waveAmplitude = 0.08f;

    [Tooltip("Phase delay between consecutive characters.")]
    [SerializeField] private float characterPhaseOffset = 0.45f;

    [Tooltip("When true, letters only bounce upward and rest at baseline; when false, they oscillate up and down symmetrically.")]
    [SerializeField] private bool bounceUpOnly = true;

    private TMP_Text tmpText;

    private void Awake()
    {
        tmpText = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        RefreshMeshCache();
    }

    private void OnDisable()
    {
        if (tmpText != null)
        {
            tmpText.ForceMeshUpdate();
        }
    }

    private void LateUpdate()
    {
        if (tmpText == null) return;

        tmpText.ForceMeshUpdate();
        TMP_TextInfo textInfo = tmpText.textInfo;
        if (textInfo == null || textInfo.characterCount == 0 || textInfo.meshInfo == null) return;

        float time = Time.unscaledTime * waveSpeed;

        for (int i = 0; i < textInfo.characterCount; i++)
        {
            TMP_CharacterInfo charInfo = textInfo.characterInfo[i];
            if (!charInfo.isVisible) continue;

            int materialIndex = charInfo.materialReferenceIndex;
            if (materialIndex < 0 || materialIndex >= textInfo.meshInfo.Length) continue;

            int vertexIndex = charInfo.vertexIndex;
            Vector3[] vertices = textInfo.meshInfo[materialIndex].vertices;
            if (vertices == null || vertexIndex + 3 >= vertices.Length) continue;

            float rawWave = Mathf.Sin(time - i * characterPhaseOffset);
            float offsetY = (bounceUpOnly ? Mathf.Max(0f, rawWave) : rawWave) * waveAmplitude;
            Vector3 offset = new Vector3(0f, offsetY, 0f);

            vertices[vertexIndex + 0] += offset;
            vertices[vertexIndex + 1] += offset;
            vertices[vertexIndex + 2] += offset;
            vertices[vertexIndex + 3] += offset;
        }

        for (int i = 0; i < textInfo.meshInfo.Length; i++)
        {
            TMP_MeshInfo meshInfo = textInfo.meshInfo[i];
            if (meshInfo.mesh == null || meshInfo.vertices == null) continue;
            meshInfo.mesh.vertices = meshInfo.vertices;
            tmpText.UpdateGeometry(meshInfo.mesh, i);
        }
    }

    /// <summary>
    /// Refreshes the underlying TMP mesh after changing the text string.
    /// </summary>
    public void RefreshMeshCache()
    {
        if (tmpText == null) tmpText = GetComponent<TMP_Text>();
        if (tmpText == null) return;

        tmpText.ForceMeshUpdate();
    }
}
