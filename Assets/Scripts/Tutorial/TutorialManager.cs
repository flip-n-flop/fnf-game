using System.Collections.Generic;
using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    [Header("Timers padrão (usados quando o TutorialData não define um valor próprio)")]
    [SerializeField] private float tempoOciosidadeParaReexibir = 5f;
    [SerializeField] private float atrasoAoAvancarParaEsconder = 2f;

    private readonly List<TutorialData> _pilha = new();

    private static readonly HashSet<string> _idsVisitados = new();
    private static readonly HashSet<string> _idsConcluidos = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public bool JaVisitou(string tutorialId) => _idsVisitados.Contains(tutorialId);
    public void MarcarVisitado(string tutorialId)
    {
        if (!string.IsNullOrEmpty(tutorialId)) _idsVisitados.Add(tutorialId);
    }

    public bool JaConcluiu(string tutorialId) => _idsConcluidos.Contains(tutorialId);
    public void MarcarConcluido(string tutorialId)
    {
        if (!string.IsNullOrEmpty(tutorialId)) _idsConcluidos.Add(tutorialId);
    }

    /// <summary>Resolve o tempo de ociosidade efetivo: customizado no asset, ou o padrão do manager.</summary>
    public float ResolverTempoOciosidade(TutorialData dados)
    {
        return dados.tempoOciosidadeCustomizado >= 0f
            ? dados.tempoOciosidadeCustomizado
            : tempoOciosidadeParaReexibir;
    }

    /// <summary>Resolve o atraso de esconder efetivo: customizado no asset, ou o padrão do manager.</summary>
    public float ResolverAtrasoAoAvancar(TutorialData dados)
    {
        return dados.atrasoAoAvancarCustomizado >= 0f
            ? dados.atrasoAoAvancarCustomizado
            : atrasoAoAvancarParaEsconder;
    }

    public void Mostrar(TutorialData dados)
    {
        if (dados == null) return;
        _pilha.Add(dados);
        AtualizarExibicao();
    }

    public void Esconder(TutorialData dados)
    {
        if (dados == null) return;
        _pilha.Remove(dados);
        AtualizarExibicao();
    }

    private void AtualizarExibicao()
    {
        if (_pilha.Count == 0)
        {
            TutorialUI.Instance.Esconder();
            return;
        }

        var atual = _pilha[_pilha.Count - 1];
        TutorialUI.Instance.Mostrar(atual);
    }
}
