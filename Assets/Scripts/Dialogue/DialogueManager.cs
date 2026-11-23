using UnityEngine;
using UnityEngine.UIElements;
using UnityEngine.Localization;

public class DialogueManager : MonoBehaviour
{
    [Header("Interface")]
    [SerializeField] private UIDocument hudDocument;

    [Header("Teste")]
    [SerializeField] private DialogueData dialogoTeste;

    private VisualElement dialogueRoot;
    private Label characterName;
    private Label dialogueText;

    private DialogueData dialogoAtual;
    private int indiceFalaAtual;

    private const string NomeStringTable = "InGameDialogue";
    private LocalizedString falaLocalizada;

    private float escalaTempoAnterior;

    public bool DialogoAtivo { get; private set; }

    private void OnEnable()
    {
        VisualElement root = hudDocument.rootVisualElement;

        dialogueRoot = root.Q<VisualElement>("DialogueRoot");
        characterName = root.Q<Label>("CharacterName");
        dialogueText = root.Q<Label>("DialogueText");

        dialogueRoot.style.display = DisplayStyle.None;
    }

    public void IniciarDialogo(DialogueData dialogo)
    {
        if (DialogoAtivo || dialogo == null || dialogo.falas == null || dialogo.falas.Count == 0)
            return;

        dialogoAtual = dialogo;
        indiceFalaAtual = 0;

        DialogoAtivo = true;

        escalaTempoAnterior = Time.timeScale;
        Time.timeScale = 0f;

        dialogueRoot.style.display = DisplayStyle.Flex;

        ExibirFalaAtual();
    }

    private void ExibirFalaAtual()
    {
        DialogueLine fala = dialogoAtual.falas[indiceFalaAtual];

        if (characterName != null)
            characterName.text = fala.personagem;

        if (falaLocalizada != null)
        {
            falaLocalizada.StringChanged -= AtualizarTextoLocalizado;
            falaLocalizada = null;
        }

        falaLocalizada = new LocalizedString(NomeStringTable, fala.chaveTexto);

        dialogueText.text = "";

        falaLocalizada.StringChanged += AtualizarTextoLocalizado;
    }

    private void AtualizarTextoLocalizado(string texto)
    {
        if (!DialogoAtivo)
            return;

        dialogueText.text = texto;
    }

    public void AvancarDialogo()
    {
        if (!DialogoAtivo)
            return;

        indiceFalaAtual++;

        if (indiceFalaAtual >= dialogoAtual.falas.Count)
        {
            FinalizarDialogo();
            return;
        }

        ExibirFalaAtual();
    }

    public void FinalizarDialogo()
    {
        if (!DialogoAtivo)
            return;

        if (falaLocalizada != null)
        {
            falaLocalizada.StringChanged -= AtualizarTextoLocalizado;
            falaLocalizada = null;
        }

        dialogueRoot.style.display = DisplayStyle.None;

        Time.timeScale = escalaTempoAnterior;

        dialogoAtual = null;
        indiceFalaAtual = 0;
        DialogoAtivo = false;
    }

    private void OnDisable()
    {
        if (DialogoAtivo)
            FinalizarDialogo();
    }

    [ContextMenu("Testar diálogo")]
    private void TestarDialogo()
    {
        IniciarDialogo(dialogoTeste);
    }

    [ContextMenu("Avançar diálogo")]
    private void AvancarTeste()
    {
        AvancarDialogo();
    }

    [ContextMenu("Encerrar diálogo")]
    private void EncerrarTeste()
    {
        FinalizarDialogo();
    }
}
