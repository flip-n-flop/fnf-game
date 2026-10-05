using UnityEngine;
using UnityEngine.InputSystem;

public enum TipoTutorial { Interativo, Observacional }

[CreateAssetMenu(fileName = "NovoTutorial", menuName = "Tutoriais/Tutorial Data")]
public class TutorialData : ScriptableObject
{
    [Header("Identificação")]
    [Tooltip("Também usado como chave na String Table 'InGameTutorial' para buscar o texto localizado.")]
    public string id;

    [Header("Comportamento")]
    public TipoTutorial tipo;

    [Header("Interativo (se aplicável)")]
    public InputActionReference acaoInput;

    [Header("Timer (opcional)")]
    [Tooltip("Se true, o tutorial só aparece após o jogador ficar parado, mesmo ao entrar na zona " +
             "(ex: tutorial de movimento). Se false, aparece instantaneamente ao entrar na zona.")]
    public bool aguardarOciosidadeParaExibir = false;

    [Header("Timers customizados (opcional)")]
    [Tooltip("-1 = usa o padrão definido no TutorialManager.")]
    public float tempoOciosidadeCustomizado = -1f;

    [Tooltip("-1 = usa o padrão definido no TutorialManager.")]
    public float atrasoAoAvancarCustomizado = -1f;

    [Header("Repetição")]
    [Tooltip("Se true, depois que o jogador executar a ação uma vez, este tutorial nunca mais é exibido.")]
    public bool mostrarApenasUmaVez = false;
}
