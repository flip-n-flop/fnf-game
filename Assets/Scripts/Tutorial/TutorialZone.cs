using UnityEngine;

/// <summary>
/// Marks a trigger zone that shows a tutorial while the Player is inside it,
/// and hides it on exit. No success/failure logic — purely presence-based.
/// Meant to be spawned by <see cref="TutorialSpawner"/> from level JSON data,
/// mirroring how <see cref="ObstacleSpawner"/> spawns obstacle GameObjects.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class TutorialZone : MonoBehaviour
{
    public TutorialData tutorial;

    private bool _jogadorDentro;
    private bool _visivelAtualmente;
    private bool _aguardandoParaEsconder;
    private bool _entrouAlgumaVez; // controla o "instantâneo só na primeira vez"
    private float _tempoParado;
    private float _contagemParaEsconder;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player") || tutorial == null) return;

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

        bool parado = EstaOciosoParaEsteTutorial();
        _tempoParado = parado ? _tempoParado + Time.deltaTime : 0f;

        if (!_visivelAtualmente && !_aguardandoParaEsconder &&
            _tempoParado >= TutorialManager.Instance.TempoOciosidadeParaReexibir)
        {
            Mostrar();
        }

        if (_visivelAtualmente && !_aguardandoParaEsconder &&
            tutorial.acaoInput != null && tutorial.acaoInput.action != null &&
            tutorial.acaoInput.action.triggered)
        {
            _aguardandoParaEsconder = true;
            _contagemParaEsconder = TutorialManager.Instance.AtrasoAoAvancarParaEsconder;
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

    /// <summary>
    /// "Ocioso" agora é relativo apenas à ação que este tutorial específico ensina —
    /// pressionar outras ações do jogo não afeta este timer.
    /// </summary>
    private bool EstaOciosoParaEsteTutorial()
    {
        if (tutorial.acaoInput != null && tutorial.acaoInput.action != null)
            return !tutorial.acaoInput.action.IsPressed();

        // Fallback, caso o tutorial não tenha uma ação associada (não deveria acontecer
        // em tutoriais Interativos, mas evita erro caso o campo fique vazio por engano).
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
