
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Localization;
using UnityEngine;
using UnityEngine.Localization.Tables;

public static class DialogueImporter
{
    private const string TableName = "InGameDialogue";
    private const string LocaleCode = "pt-BR";
    private const string OutputFolder = "Assets/Dialogue/Data";

    private struct ParsedLine
    {
        public string personagem;
        public string texto;
    }

    [MenuItem("Tools/FlipNFlop/Importar Roteiro")]
    public static void ImportarRoteiro()
    {
        string caminho = EditorUtility.OpenFilePanel("Selecionar roteiro", Application.dataPath, "txt");

        if (string.IsNullOrEmpty(caminho))
            return;

        string id = Path.GetFileNameWithoutExtension(caminho);

        // Evita chaves com espaços ou caracteres especiais.
        if (!Regex.IsMatch(id, @"^[a-zA-Z0-9_-]+$"))
        {
            Debug.LogError("O nome do arquivo deve conter apenas letras, números, hífens ou underscores.");
            return;
        }

        string conteudo = File.ReadAllText(caminho, Encoding.UTF8);

        if (!TentarLerRoteiro(conteudo, out var falas))
            return;

        var collection =
            LocalizationEditorSettings.GetStringTableCollection(TableName);

        if (collection == null)
        {
            Debug.LogError($"String Table Collection '{TableName}' não encontrada. Crie a coleção antes de importar.");
            return;
        }

        StringTable tabela = null;

        foreach (var item in collection.StringTables)
        {
            if (item.LocaleIdentifier.Code == LocaleCode)
            {
                tabela = item;
                break;
            }
        }

        if (tabela == null)
        {
            Debug.LogError($"A coleção '{TableName}' não possui uma tabela {LocaleCode}.");
            return;
        }

        if (!AssetDatabase.IsValidFolder(OutputFolder))
        {
            Debug.LogError($"Crie a pasta '{OutputFolder}' antes de importar.");
            return;
        }

        string assetPath = $"{OutputFolder}/{id}.asset";

        DialogueData dados = AssetDatabase.LoadAssetAtPath<DialogueData>(assetPath);

        bool novoAsset = dados == null;

        if (novoAsset)
            dados = ScriptableObject.CreateInstance<DialogueData>();

        // Atualiza ou cria as entradas da String Table.
        var novasFalas = new List<DialogueLine>();

        for (int i = 0; i < falas.Count; i++)
        {
            string chave = $"{id}_{i + 1:000}";
            ParsedLine fala = falas[i];

            if (collection.SharedData.GetEntry(chave) == null)
                collection.SharedData.AddKey(chave);

            var entrada = tabela.GetEntry(chave);

            if (entrada == null)
                tabela.AddEntry(chave, fala.texto);
            else
                entrada.Value = fala.texto;

            novasFalas.Add(new DialogueLine
            {
                personagem = fala.personagem,
                chaveTexto = chave
            });
        }

        dados.id = id;
        dados.falas = novasFalas;

        if (novoAsset)
            AssetDatabase.CreateAsset(dados, assetPath);

        EditorUtility.SetDirty(dados);
        EditorUtility.SetDirty(tabela);
        EditorUtility.SetDirty(collection.SharedData);

        AssetDatabase.SaveAssets();

        Debug.Log($"Roteiro '{id}' importado com sucesso! {falas.Count} falas cadastradas.");

        Selection.activeObject = dados;
        EditorGUIUtility.PingObject(dados);
    }

    private static bool TentarLerRoteiro(
        string conteudo,
        out List<ParsedLine> falas)
    {
        falas = new List<ParsedLine>();

        string[] linhas = conteudo.Replace("\r\n", "\n").Replace('\r', '\n').TrimStart('\uFEFF').Split('\n');

        string personagemAtual = null;

        for (int i = 0; i < linhas.Length; i++)
        {
            string linha = linhas[i].Trim();

            // Ignora linhas vazias entre falas.
            if (string.IsNullOrWhiteSpace(linha))
                continue;

            // Identifica [Personagem].
            Match personagem = Regex.Match(linha, @"^\[([^\[\]]+)\]$");

            if (personagem.Success)
            {
                if (personagemAtual != null)
                {
                    Debug.LogError($"Linha {i + 1}: o personagem anterior não possui uma fala.");
                    return false;
                }

                personagemAtual = personagem.Groups[1].Value.Trim();
                continue;
            }

            if (personagemAtual == null)
            {
                Debug.LogError($"Linha {i + 1}: esperava [Personagem], mas encontrou '{linha}'.");
                return false;
            }

            falas.Add(new ParsedLine
            {
                personagem = personagemAtual,
                texto = linha
            });

            personagemAtual = null;
        }

        if (personagemAtual != null)
        {
            Debug.LogError($"O personagem '{personagemAtual}' está sem fala.");
            return false;
        }

        if (falas.Count == 0)
        {
            Debug.LogError("O roteiro não contém falas.");
            return false;
        }

        return true;
    }
}
