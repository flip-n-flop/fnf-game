using System.Collections.Generic;
using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    [Header("Timers (aplicados a todos os tutoriais interativos)")]
    [SerializeField] private float tempoOciosidadeParaReexibir = 5f;
    [SerializeField] private float atrasoAoAvancarParaEsconder = 2f;

    public float TempoOciosidadeParaReexibir => tempoOciosidadeParaReexibir;
    public float AtrasoAoAvancarParaEsconder => atrasoAoAvancarParaEsconder;

    private readonly List<TutorialData> _pilha = new();

    /// <summary>
    /// Ids de tutoriais já visitados pelo jogador. Static: sobrevive a reload de cena
    /// (ex: reinício de fase), mas reseta ao fechar o jogo — até integrarmos com save.
    /// </summary>
    private static readonly HashSet<string> _idsVisitados = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
    }

    public bool JaVisitou(string tutorialId) => _idsVisitados.Contains(tutorialId);

    public void MarcarVisitado(string tutorialId)
    {
        if (!string.IsNullOrEmpty(tutorialId))
            _idsVisitados.Add(tutorialId);
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
