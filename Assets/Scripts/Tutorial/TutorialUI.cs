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

    private void OnEnable() => LocalizationSettings.SelectedLocaleChanged += OnLocaleChanged;
    private void OnDisable() => LocalizationSettings.SelectedLocaleChanged -= OnLocaleChanged;

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

        var nomes = new List<string>();
        foreach (var binding in acaoRef.action.bindings)
        {
            if (binding.isComposite || binding.isPartOfComposite) continue;

            string path = binding.hasOverrides ? binding.overridePath : binding.path;
            if (string.IsNullOrEmpty(path)) continue;

            string legivel = InputControlPath.ToHumanReadableString(
                path, InputControlPath.HumanReadableStringOptions.OmitDevice);

            if (!nomes.Contains(legivel)) nomes.Add(legivel);
        }

        return nomes.Count > 0 ? string.Join(" ou ", nomes) : "???";
    }

    private void OnLocaleChanged(Locale _)
    {
        if (_dadosAtuais != null) Mostrar(_dadosAtuais);
    }
}
