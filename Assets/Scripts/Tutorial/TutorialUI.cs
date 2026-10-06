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

    [Tooltip("Nome da String Table Collection usada para os textos de tutorial.")]
    [SerializeField] private string tabelaLocalizacao = "InGameTutorial";

    private VisualElement _container;
    private Label _texto;
    private TutorialData _dadosAtuais;

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;

        var root = GetComponent<UIDocument>().rootVisualElement;
        _container = root.Q<VisualElement>("TutorialContainer");
        _texto = root.Q<Label>("TutorialText");
    }

    private void OnEnable()
    {
        LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
        RebindManager.OnBindingsChanged += OnBindingsChanged;

        if (InputDeviceTracker.Instance != null)
            InputDeviceTracker.Instance.OnDispositivoAlterado += OnDispositivoAlterado;
    }

    private void OnDisable()
    {
        LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;
        RebindManager.OnBindingsChanged -= OnBindingsChanged;

        if (InputDeviceTracker.Instance != null)
            InputDeviceTracker.Instance.OnDispositivoAlterado -= OnDispositivoAlterado;
    }

    private void OnBindingsChanged()
    {
        if (_dadosAtuais != null) Mostrar(_dadosAtuais);
    }

    private void OnDispositivoAlterado(TipoDispositivoInput _)
    {
        if (_dadosAtuais != null) Mostrar(_dadosAtuais); // reexibe com o texto/tecla atualizados
    }

    public void Mostrar(TutorialData dados)
    {
        if (_container == null || dados == null) return;

        _dadosAtuais = dados;
        _container.style.display = DisplayStyle.Flex;

        var operacao = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(tabelaLocalizacao, dados.id);
        operacao.Completed += handle =>
        {
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
        if (_texto == null || textoLocalizado == null || _dadosAtuais != dados) return;

        string final = textoLocalizado;
        if (dados.tipo == TipoTutorial.Interativo)
            final = final.Replace("{TECLA}", ObterTeclaDisplay(dados.acaoInput));

        _texto.text = final;
    }

    private string ObterTeclaDisplay(InputActionReference acaoRef)
    {
        if (acaoRef == null || acaoRef.action == null) return "???";

        TipoDispositivoInput dispositivoAtivo = InputDeviceTracker.Instance != null
            ? InputDeviceTracker.Instance.DispositivoAtual
            : TipoDispositivoInput.TecladoMouse;

        var bindings = acaoRef.action.bindings;
        var nomes = new List<string>();
        int i = 0;

        while (i < bindings.Count)
        {
            var binding = bindings[i];

            if (binding.isComposite)
            {
                var partes = new List<string>();
                int j = i + 1;
                while (j < bindings.Count && bindings[j].isPartOfComposite)
                {
                    string pathParte = bindings[j].hasOverrides ? bindings[j].overridePath : bindings[j].path;
                    if (!string.IsNullOrEmpty(pathParte) && ClassificarPath(pathParte) == dispositivoAtivo)
                    {
                        partes.Add(InputControlPath.ToHumanReadableString(
                            pathParte, InputControlPath.HumanReadableStringOptions.OmitDevice));
                    }
                    j++;
                }

                if (partes.Count > 0)
                    nomes.Add(string.Join("/", partes));

                i = j;
                continue;
            }

            if (!binding.isPartOfComposite)
            {
                string path = binding.hasOverrides ? binding.overridePath : binding.path;
                if (!string.IsNullOrEmpty(path) && ClassificarPath(path) == dispositivoAtivo)
                {
                    string legivel = InputControlPath.ToHumanReadableString(
                        path, InputControlPath.HumanReadableStringOptions.OmitDevice);
                    if (!nomes.Contains(legivel)) nomes.Add(legivel);
                }
            }

            i++;
        }

        return nomes.Count > 0 ? string.Join(" ou ", nomes) : "???";
    }

    /// <summary>
    /// Maps a binding path to its device group, based on the Input System control-path prefix.
    /// Returns null for unrecognized device groups (excluded from the tutorial text).
    /// </summary>
    private TipoDispositivoInput? ClassificarPath(string path)
    {
        if (path.Contains("<Keyboard>") || path.Contains("<Mouse>")) return TipoDispositivoInput.TecladoMouse;
        if (path.Contains("<Gamepad>")) return TipoDispositivoInput.Gamepad;
        if (path.Contains("<Joystick>")) return TipoDispositivoInput.Joystick;
        if (path.Contains("<XRController>")) return TipoDispositivoInput.XRController;
        return null;
    }

    private void OnLocaleChanged(Locale _)
    {
        if (_dadosAtuais != null) Mostrar(_dadosAtuais);
    }
}
