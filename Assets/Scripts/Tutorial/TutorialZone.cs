using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class TutorialZone : MonoBehaviour
{
    public TutorialData tutorial;

    private bool _jogadorDentro;
    private bool _visivelAtualmente;
    private bool _aguardandoParaEsconder;
    private float _tempoParado;
    private float _contagemParaEsconder;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || tutorial == null) return;
        if (tutorial.mostrarApenasUmaVez && TutorialManager.Instance.JaConcluiu(tutorial.id)) return;

        _jogadorDentro = true;
        _tempoParado = 0f;

        bool primeiraVez = !TutorialManager.Instance.JaVisitou(tutorial.id);
        TutorialManager.Instance.MarcarVisitado(tutorial.id);

        if (primeiraVez && !tutorial.aguardarOciosidadeParaExibir)
            Mostrar();
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || tutorial == null) return;

        _jogadorDentro = false;
        _aguardandoParaEsconder = false;

        if (_visivelAtualmente)
        {
            // Observacional não tem "ação" pra marcar conclusão — conta como concluído
            // ao terminar de ler e sair da zona.
            if (tutorial.tipo == TipoTutorial.Observacional)
                TutorialManager.Instance.MarcarConcluido(tutorial.id);

            Esconder();
        }
    }

    private void Update()
    {
        if (!_jogadorDentro || tutorial == null) return;
        if (tutorial.mostrarApenasUmaVez && TutorialManager.Instance.JaConcluiu(tutorial.id)) return;

        bool parado = EstaOciosoParaEsteTutorial();
        _tempoParado = parado ? _tempoParado + Time.deltaTime : 0f;

        float tempoOciosidade = TutorialManager.Instance.ResolverTempoOciosidade(tutorial);

        if (!_visivelAtualmente && !_aguardandoParaEsconder && _tempoParado >= tempoOciosidade)
        {
            Mostrar();
        }

        // Só tutoriais Interativos têm uma "ação" que dispara o esconder antecipado
        if (tutorial.tipo == TipoTutorial.Interativo &&
            _visivelAtualmente && !_aguardandoParaEsconder &&
            tutorial.acaoInput != null && tutorial.acaoInput.action != null &&
            tutorial.acaoInput.action.triggered)
        {
            TutorialManager.Instance.MarcarConcluido(tutorial.id);
            _aguardandoParaEsconder = true;
            _contagemParaEsconder = TutorialManager.Instance.ResolverAtrasoAoAvancar(tutorial);
        }

        if (_aguardandoParaEsconder)
        {
            _contagemParaEsconder -= Time.deltaTime;
            if (_contagemParaEsconder <= 0f)
            {
                _aguardandoParaEsconder = false;
                Esconder();
                _tempoParado = 0f;
            }
        }
    }

    private bool EstaOciosoParaEsteTutorial()
    {
        if (tutorial.tipo == TipoTutorial.Interativo &&
            tutorial.acaoInput != null && tutorial.acaoInput.action != null)
        {
            return !tutorial.acaoInput.action.IsPressed();
        }

        // Observacional (ou Interativo sem ação definida): ocioso = o jogador não fez
        // nenhum input geral. Para Observacional, isso cobre exatamente o seu pedido:
        // "aguardar o jogador ficar um tempo parado ali".
        return InputDeviceTracker.Instance == null || !InputDeviceTracker.Instance.HouveInputEsteFrame;
    }

    private void Mostrar()
    {
        TutorialManager.Instance.Mostrar(tutorial);
        _visivelAtualmente = true;
    }

    private void Esconder()
    {
        TutorialManager.Instance.Esconder(tutorial);
        _visivelAtualmente = false;
    }
}
