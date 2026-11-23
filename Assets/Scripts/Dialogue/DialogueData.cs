using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public class DialogueLine
{
    [Tooltip("Identificação do personagem que está falando.")]
    public string personagem;

    [Tooltip("Chave da fala na String Table de localização.")]
    public string chaveTexto;
}

[CreateAssetMenu(fileName = "NewDialogue", menuName = "FlipNFlop/Dialogue")]

public class DialogueData : ScriptableObject
{
    [Tooltip("Identificador único desta conversa.")]
    public string id;

    [Tooltip("Falas apresentadas na ordem da lista.")]
    public List<DialogueLine> falas = new();
}
