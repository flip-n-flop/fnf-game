using UnityEngine;
using UnityEngine.InputSystem;

public enum TipoTutorial { Interativo, Observacional }

[CreateAssetMenu(fileName = "NovoTutorial", menuName = "Tutoriais/Tutorial Data")]
public class TutorialData : ScriptableObject
{
    [Header("Identificação")]
    [Tooltip("Também usado como chave na String Table 'Tutorial' para buscar o texto localizado.")]
    public string id;

    [Header("Comportamento")]
    public TipoTutorial tipo;

    [Header("Interativo (se aplicável)")]
    [Tooltip("Referência à InputAction real do jogo — a mesma usada no RebindManager. " +
             "O texto exibido reflete o binding atual, incluindo rebinds do jogador.")]
    public InputActionReference acaoInput;
}
