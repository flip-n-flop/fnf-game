using UnityEngine;
using UnityEngine.UIElements;

public class DialogueManager : MonoBehaviour
{
    [SerializeField] private UIDocument hudDocument;

    private VisualElement dialogueRoot;
    private Label characterName;
    private Label dialogueText;

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

    public void IniciarDialogo(string nome, string fala)
    {
        if (DialogoAtivo)
            return;

        DialogoAtivo = true;

        escalaTempoAnterior = Time.timeScale;
        Time.timeScale = 0f;

        // --------------
        // TEMPORÁRIO
        // --------------
        if (characterName != null)
            characterName.text = nome;

        dialogueText.text = fala;

        dialogueRoot.style.display = DisplayStyle.Flex;
    }

    public void FinalizarDialogo()
    {
        if (!DialogoAtivo)
            return;

        dialogueRoot.style.display = DisplayStyle.None;

        Time.timeScale = escalaTempoAnterior;
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
        IniciarDialogo(
            "Dr. Nefário",
            "Olá! Hoje vamos aprender sobre flip-flops!"
        );
    }

    [ContextMenu("Encerrar diálogo")]
    private void EncerrarTeste()
    {
        FinalizarDialogo();
    }
}
