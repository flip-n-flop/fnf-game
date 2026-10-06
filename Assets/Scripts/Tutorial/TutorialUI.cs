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

    private void OnBindingsChanged() { if (_dadosAtuais != null) Mostrar(_dadosAtuais); }
    private void OnDispositivoAlterado(TipoDispositivoInput _) { if (_dadosAtuais != null) Mostrar(_dadosAtuais); }
    private void OnLocaleChanged(Locale _) { if (_dadosAtuais != null) Mostrar(_dadosAtuais); }

    public void Mostrar(TutorialData dados)
    {
        if (_container == null || dados == null) return;

        _dadosAtuais = dados;
        _container.style.display = DisplayStyle.Flex;

        var operacao = LocalizationSettings.StringDatabase.GetLocalizedStringAsync(tabelaLocalizacao, dados.id);
        operacao.Completed += handle => AplicarTexto(dados, handle.Result);
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

        int indice = textoLocalizado.IndexOf("{TECLA}");
        if (indice >= 0 && dados.tipo == TipoTutorial.Interativo)
        {
            _textoAntes.text = textoLocalizado.Substring(0, indice);
            _textoDepois.text = textoLocalizado.Substring(indice + "{TECLA}".Length);
            MontarIcones(dados.acaoInput);
        }
        else
        {
            _textoAntes.text = textoLocalizado;
            _textoDepois.text = "";
            _icones.Clear();
        }
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
