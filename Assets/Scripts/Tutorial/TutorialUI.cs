using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.UIElements;

[RequireComponent(typeof(UIDocument))]
public class TutorialUI : MonoBehaviour
{
    public static TutorialUI Instance { get; private set; }

    [SerializeField] private string tabelaLocalizacao = "InGameTutorial";
    [SerializeField] private IconeTeclaCatalog catalogoIcones;

    private VisualElement _container;
    private Label _textoAntes;
    private Label _textoDepois;
    private VisualElement _icones;
    private TutorialData _dadosAtuais;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        var root = GetComponent<UIDocument>().rootVisualElement;
        _container = root.Q<VisualElement>("TutorialContainer");
        _textoAntes = root.Q<Label>("TutorialTextoAntes");
        _textoDepois = root.Q<Label>("TutorialTextoDepois");
        _icones = root.Q<VisualElement>("TutorialIcones");
    }

    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        RebindManager.OnBindingsChanged += OnBindingsChanged;
        SignalColorManager.OnColorsChanged += OnCoresAlteradas; // novo

        if (InputDeviceTracker.Instance != null)
            InputDeviceTracker.Instance.OnDispositivoAlterado += OnDispositivoAlterado;
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        RebindManager.OnBindingsChanged -= OnBindingsChanged;
        SignalColorManager.OnColorsChanged -= OnCoresAlteradas; // novo

        if (InputDeviceTracker.Instance != null)
            InputDeviceTracker.Instance.OnDispositivoAlterado -= OnDispositivoAlterado;
    }

    private void OnCoresAlteradas() { if (_dadosAtuais != null) Mostrar(_dadosAtuais); }

    private void OnBindingsChanged() { if (_dadosAtuais != null) Mostrar(_dadosAtuais); }
    private void OnDispositivoAlterado(TipoDispositivoInput _) { if (_dadosAtuais != null) Mostrar(_dadosAtuais); }
    private void OnLocaleChanged(Locale _) { if (_dadosAtuais != null) Mostrar(_dadosAtuais); }

    public void Mostrar(TutorialData dados)
    {
        if (_container == null || dados == null) return;

        Debug.Log($"Mostrar chamado. textoAntes null? {_textoAntes == null}, textoDepois null? {_textoDepois == null}, icones null? {_icones == null}, catalogo null? {catalogoIcones == null}");

        _dadosAtuais = dados;
        _container.style.display = DisplayStyle.Flex;

        var operacao = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(tabelaLocalizacao, dados.id);
        operacao.Completed += handle =>
        {
            Debug.Log($"Localization completou: status={handle.Status}, resultado='{handle.Result}'");
            AplicarTexto(dados, handle.Result);
        };
        if (operacao.IsDone) AplicarTexto(dados, operacao.Result);
    }

    public void Esconder()
    {
        _dadosAtuais = null;
        if (_container == null) return;
        _container.style.display = DisplayStyle.None;
    }

    private void AplicarTexto(TutorialData dados, string textoLocalizado)
    {
        if (_textoAntes == null || textoLocalizado == null || _dadosAtuais != dados) return;

        string textoComCores = ProcessarMarcadoresDeCor(textoLocalizado);

        int indice = textoLocalizado.IndexOf("{TECLA}");
        Debug.Log($"Texto recebido: '{textoLocalizado}', índice de {{TECLA}}: {indice}, tipo: {dados.tipo}");

        if (indice >= 0 && dados.tipo == TipoTutorial.Interativo)
        {
            _textoAntes.text = textoComCores.Substring(0, indice);
            _textoDepois.text = textoComCores.Substring(indice + "{TECLA}".Length);
            MontarIcones(dados.acaoInput);
        }
        else
        {
            _textoAntes.text = textoComCores;
            _textoDepois.text = "";
            _icones.Clear();
        }
    }

    /// <summary>
    /// Substitui marcadores {COR_SINAL}texto{FIM} por rich text <color> usando a cor
    /// atual do sinal correspondente no SignalColorManager (reflete customização do jogador).
    /// </summary>
    private string ProcessarMarcadoresDeCor(string texto)
    {
        var regex = new System.Text.RegularExpressions.Regex(
            @"\{COR_(\w+)\}(.*?)\{FIM\}",
            System.Text.RegularExpressions.RegexOptions.Singleline);

        return regex.Replace(texto, match =>
        {
            string sinal = match.Groups[1].Value.ToUpperInvariant();
            string conteudo = match.Groups[2].Value;

            Color cor = ObterCorDoSinal(sinal);
            string hex = ColorUtility.ToHtmlStringRGB(cor);

            return $"<color=#{hex}>{conteudo}</color>";
        });
    }

    private Color ObterCorDoSinal(string sinal)
    {
        var manager = SignalColorManager.Instance;
        return sinal switch
        {
            "J" => manager.ColorJ,
            "K" => manager.ColorK,
            "CLK" => manager.ColorCLK,
            "PRESET" => manager.ColorPreset,
            "CLEAR" => manager.ColorClear,
            "FEEDBACKSUCCESS" => manager.ColorFeedbackSuccess,
            "FEEDBACKFAILURE" => manager.ColorFeedbackFailure,
            _ => Color.white // marcador desconhecido — não quebra, só não colore
        };
    }

    private void MontarIcones(InputActionReference acaoRef)
    {
        _icones.Clear();
        if (acaoRef == null || acaoRef.action == null || catalogoIcones == null) return;

        TipoDispositivoInput dispositivoAtivo = InputDeviceTracker.Instance != null
            ? InputDeviceTracker.Instance.DispositivoAtual
            : TipoDispositivoInput.TecladoMouse;

        var bindings = acaoRef.action.bindings;
        int i = 0;

        while (i < bindings.Count)
        {
            var binding = bindings[i];

            if (binding.isComposite)
            {
                int j = i + 1;
                while (j < bindings.Count && bindings[j].isPartOfComposite)
                {
                    AdicionarIconeSePertence(bindings[j], dispositivoAtivo);
                    j++;
                }
                i = j;
                continue;
            }

            if (!binding.isPartOfComposite)
                AdicionarIconeSePertence(binding, dispositivoAtivo);

            i++;
        }
    }

    private void AdicionarIconeSePertence(InputBinding binding, TipoDispositivoInput dispositivoAtivo)
    {
        string path = binding.hasOverrides ? binding.overridePath : binding.path;
        if (string.IsNullOrEmpty(path)) return;
        if (ClassificarPath(path) != dispositivoAtivo) return;

        var icone = catalogoIcones.ObterIcone(path);
        if (icone == null) return; // sem ícone cadastrado pra essa tecla específica — ignora silenciosamente

        var img = new Image { vectorImage = icone };
        img.AddToClassList("tutorial-icone");
        _icones.Add(img);
    }

    private TipoDispositivoInput? ClassificarPath(string path)
    {
        if (path.Contains("<Keyboard>") || path.Contains("<Mouse>")) return TipoDispositivoInput.TecladoMouse;
        if (path.Contains("<Gamepad>")) return TipoDispositivoInput.Gamepad;
        if (path.Contains("<Joystick>")) return TipoDispositivoInput.Joystick;
        if (path.Contains("<XRController>")) return TipoDispositivoInput.XRController;
        return null;
    }
}
