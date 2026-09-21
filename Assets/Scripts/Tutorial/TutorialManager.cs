using System.Collections.Generic;
using UnityEngine;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    // Pilha de tutoriais "dentro dos quais" o jogador está no momento.
    // Sempre exibimos apenas o mais recente (topo), garantindo um por vez.
    private readonly List<TutorialData> _pilha = new();

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
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
        _pilha.Remove(dados); // remove essa entrada específica, onde quer que esteja na pilha
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
