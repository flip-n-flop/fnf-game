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

        if (tutorial.tipo == TipoTutorial.Observacional)
        {
            Mostrar();
            return;
        }

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
        if (_visivelAtualmente) Esconder();
    }

    private void Update()
    {
        if (!_jogadorDentro || tutorial == null) return;
        if (tutorial.tipo != TipoTutorial.Interativo) return;

        bool bloqueadoPorMostrarUmaVez = tutorial.mostrarApenasUmaVez && TutorialManager.Instance.JaConcluiu(tutorial.id);

        bool parado = EstaOciosoParaEsteTutorial();
        _tempoParado = parado ? _tempoParado + Time.deltaTime : 0f;

        float tempoOciosidade = TutorialManager.Instance.ResolverTempoOciosidade(tutorial);

        // Só impede NOVAS exibições por ociosidade se já foi concluído e a flag estiver ativa
        if (!bloqueadoPorMostrarUmaVez && !_visivelAtualmente && !_aguardandoParaEsconder &&
            _tempoParado >= tempoOciosidade)
        {
            Mostrar();
        }

        if (_visivelAtualmente && !_aguardandoParaEsconder &&
            tutorial.acaoInput != null && tutorial.acaoInput.action != null &&
            tutorial.acaoInput.action.triggered)
        {
            TutorialManager.Instance.MarcarConcluido(tutorial.id);
            _aguardandoParaEsconder = true;
            _contagemParaEsconder = TutorialManager.Instance.ResolverAtrasoAoAvancar(tutorial);
        }

        // Esse bloco roda SEMPRE, mesmo já concluído — garante que o tutorial atual termine de sumir
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
        if (tutorial.acaoInput != null && tutorial.acaoInput.action != null)
            return !tutorial.acaoInput.action.IsPressed();

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
