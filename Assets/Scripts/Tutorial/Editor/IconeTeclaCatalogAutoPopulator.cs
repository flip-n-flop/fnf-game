using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEngine.UIElements;

/// <summary>
/// Editor tool to auto-populate an IconeTeclaCatalog by matching SVG filenames
/// (e.g. "keyboard_a", "keyboard_a_outline") against known Input System control paths.
/// Unmatched files are listed for manual assignment.
/// </summary>
public class IconeTeclaCatalogAutoPopulator : EditorWindow
{
    private IconeTeclaCatalog catalogoAlvo;
    private DefaultAsset pastaDeIcones;
    private bool preferirContornada = false;

    private List<string> naoAssociados = new();
    private int quantidadeAssociada = 0;

    [MenuItem("Tutoriais/Auto-Preencher Catálogo de Ícones")]
    public static void AbrirJanela()
    {
        GetWindow<IconeTeclaCatalogAutoPopulator>("Auto-Preencher Ícones");
    }

    private Vector2 _scrollPos; // novo campo, junto dos outros campos da classe

    private void OnGUI()
    {
        GUILayout.Label("Associação automática de ícones de tecla", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        catalogoAlvo = (IconeTeclaCatalog)EditorGUILayout.ObjectField(
            "Catálogo (destino)", catalogoAlvo, typeof(IconeTeclaCatalog), false);

        pastaDeIcones = (DefaultAsset)EditorGUILayout.ObjectField(
            "Pasta com os SVGs", pastaDeIcones, typeof(DefaultAsset), false);

        preferirContornada = EditorGUILayout.Toggle("Preferir versão contornada (_outline)", preferirContornada);

        EditorGUILayout.Space();

        if (GUILayout.Button("Associar automaticamente"))
        {
            if (catalogoAlvo == null || pastaDeIcones == null)
            {
                EditorUtility.DisplayDialog("Faltou algo", "Selecione o catálogo e a pasta de ícones primeiro.", "Ok");
                return;
            }
            Executar();
        }

        if (naoAssociados.Count > 0 || quantidadeAssociada > 0)
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                $"✅ {quantidadeAssociada} ícone(s) associado(s) automaticamente.\n" +
                $"⚠️ {naoAssociados.Count} arquivo(s) não reconhecido(s) — associe à mão no catálogo.",
                naoAssociados.Count > 0 ? MessageType.Warning : MessageType.Info);

            // Botão extra pra facilitar copiar a lista inteira pro chat, por exemplo
            if (GUILayout.Button("Copiar lista de não associados"))
            {
                EditorGUIUtility.systemCopyBuffer = string.Join("\n", naoAssociados);
                Debug.Log("Lista copiada pra área de transferência!");
            }

            EditorGUILayout.Space();

            // A lista agora rola dentro de uma área com altura limitada
            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos, GUILayout.ExpandHeight(true));
            foreach (var nome in naoAssociados)
                EditorGUILayout.LabelField("• " + nome);
            EditorGUILayout.EndScrollView();
        }
    }

    /// <summary>
    /// Mapping from filename "key" (after "keyboard_"/"mouse_" prefix, without variant suffix)
    /// to the corresponding Input System control path.
    /// </summary>
    private static readonly Dictionary<string, string> MapaNomeParaPath = new()
    {
        // Letras
        {"a","<Keyboard>/a"}, {"b","<Keyboard>/b"}, {"c","<Keyboard>/c"}, {"d","<Keyboard>/d"},
        {"e","<Keyboard>/e"}, {"f","<Keyboard>/f"}, {"g","<Keyboard>/g"}, {"h","<Keyboard>/h"},
        {"i","<Keyboard>/i"}, {"j","<Keyboard>/j"}, {"k","<Keyboard>/k"}, {"l","<Keyboard>/l"},
        {"m","<Keyboard>/m"}, {"n","<Keyboard>/n"}, {"o","<Keyboard>/o"}, {"p","<Keyboard>/p"},
        {"q","<Keyboard>/q"}, {"r","<Keyboard>/r"}, {"s","<Keyboard>/s"}, {"t","<Keyboard>/t"},
        {"u","<Keyboard>/u"}, {"v","<Keyboard>/v"}, {"w","<Keyboard>/w"}, {"x","<Keyboard>/x"},
        {"y","<Keyboard>/y"}, {"z","<Keyboard>/z"},

        // Números
        {"0","<Keyboard>/digit0"}, {"1","<Keyboard>/digit1"}, {"2","<Keyboard>/digit2"},
        {"3","<Keyboard>/digit3"}, {"4","<Keyboard>/digit4"}, {"5","<Keyboard>/digit5"},
        {"6","<Keyboard>/digit6"}, {"7","<Keyboard>/digit7"}, {"8","<Keyboard>/digit8"},
        {"9","<Keyboard>/digit9"},

        // Teclas especiais comuns
        {"space","<Keyboard>/space"}, {"space_icon","<Keyboard>/space"},
        {"enter","<Keyboard>/enter"}, {"return","<Keyboard>/enter"},
        {"escape","<Keyboard>/escape"}, {"esc","<Keyboard>/escape"},
        {"tab","<Keyboard>/tab"}, {"tab_icon","<Keyboard>/tab"},
        {"backspace","<Keyboard>/backspace"}, {"backspace_icon","<Keyboard>/backspace"},
        {"shift","<Keyboard>/shift"}, {"shift_icon","<Keyboard>/shift"},
        {"leftshift","<Keyboard>/leftShift"}, {"rightshift","<Keyboard>/rightShift"},
        {"ctrl","<Keyboard>/ctrl"}, {"leftctrl","<Keyboard>/leftCtrl"}, {"rightctrl","<Keyboard>/rightCtrl"},
        {"alt","<Keyboard>/alt"}, {"leftalt","<Keyboard>/leftAlt"}, {"rightalt","<Keyboard>/rightAlt"},
        {"option","<Keyboard>/leftAlt"}, // nome usado no Mac pro Alt

        // Setas
        {"up","<Keyboard>/upArrow"}, {"down","<Keyboard>/downArrow"},
        {"left","<Keyboard>/leftArrow"}, {"right","<Keyboard>/rightArrow"},
        {"arrow_up","<Keyboard>/upArrow"}, {"arrow_down","<Keyboard>/downArrow"},
        {"arrow_left","<Keyboard>/leftArrow"}, {"arrow_right","<Keyboard>/rightArrow"},

        // Pontuação e símbolos com tecla física própria
        {"apostrophe","<Keyboard>/quote"}, {"quote","<Keyboard>/quote"},
        {"bracket_open","<Keyboard>/leftBracket"}, {"bracket_close","<Keyboard>/rightBracket"},
        {"capslock","<Keyboard>/capsLock"}, {"capslock_icon","<Keyboard>/capsLock"},
        {"comma","<Keyboard>/comma"},
        {"delete","<Keyboard>/delete"},
        {"end","<Keyboard>/end"},
        {"equals","<Keyboard>/equals"},
        {"home","<Keyboard>/home"},
        {"insert","<Keyboard>/insert"},
        {"minus","<Keyboard>/minus"},
        {"numlock","<Keyboard>/numLock"},
        {"numpad_enter","<Keyboard>/numpadEnter"},
        {"numpad_plus","<Keyboard>/numpadPlus"},
        {"page_down","<Keyboard>/pageDown"}, {"page_up","<Keyboard>/pageUp"},
        {"pause","<Keyboard>/pause"}, {"pause_break","<Keyboard>/pause"},
        {"period","<Keyboard>/period"},
        {"printscreen","<Keyboard>/printScreen"},
        {"scroll_lock","<Keyboard>/scrollLock"},
        {"semicolon","<Keyboard>/semicolon"}, {"colon","<Keyboard>/semicolon"},
        {"slash_back","<Keyboard>/backslash"}, {"slash_forward","<Keyboard>/slash"},
        {"tilde","<Keyboard>/backquote"},
        {"win","<Keyboard>/leftWindows"},

        // Funções
        {"f1","<Keyboard>/f1"}, {"f2","<Keyboard>/f2"}, {"f3","<Keyboard>/f3"}, {"f4","<Keyboard>/f4"},
        {"f5","<Keyboard>/f5"}, {"f6","<Keyboard>/f6"}, {"f7","<Keyboard>/f7"}, {"f8","<Keyboard>/f8"},
        {"f9","<Keyboard>/f9"}, {"f10","<Keyboard>/f10"}, {"f11","<Keyboard>/f11"}, {"f12","<Keyboard>/f12"},

        // Mouse
        {"leftbutton","<Mouse>/leftButton"}, {"rightbutton","<Mouse>/rightButton"},
        {"middlebutton","<Mouse>/middleButton"}, {"move","<Mouse>/delta"}, {"scroll","<Mouse>/scroll"},
        {"side_back","<Mouse>/backButton"}, {"side_forward","<Mouse>/forwardButton"},
    };

    private void Executar()
    {
        naoAssociados.Clear();
        quantidadeAssociada = 0;

        string caminhoPasta = AssetDatabase.GetAssetPath(pastaDeIcones);
        string[] guids = AssetDatabase.FindAssets("t:VectorImage", new[] { caminhoPasta });

        // Agrupa arquivos por "nome base" (sem _outline), guardando as duas variantes se existirem
        var porNomeBase = new Dictionary<string, (string normal, string outline)>();

        foreach (var guid in guids)
        {
            string caminho = AssetDatabase.GUIDToAssetPath(guid);
            string nomeArquivo = Path.GetFileNameWithoutExtension(caminho).ToLowerInvariant();

            bool ehContornada = nomeArquivo.EndsWith("_outline");
            string nomeBase = ehContornada ? nomeArquivo.Substring(0, nomeArquivo.Length - "_outline".Length) : nomeArquivo;

            if (!porNomeBase.ContainsKey(nomeBase))
                porNomeBase[nomeBase] = (null, null);

            var par = porNomeBase[nomeBase];
            if (ehContornada) par.outline = caminho;
            else par.normal = caminho;
            porNomeBase[nomeBase] = par;
        }

        var novasEntradas = new List<IconeTeclaCatalog.Entrada>(catalogoAlvo.entradas ?? new List<IconeTeclaCatalog.Entrada>());

        foreach (var kvp in porNomeBase)
        {
            string nomeBase = kvp.Key; // ex: "keyboard_a", "mouse_leftbutton"
            string chave = ExtrairChave(nomeBase); // ex: "a", "leftbutton"

            if (chave == null || !MapaNomeParaPath.TryGetValue(chave, out string path))
            {
                naoAssociados.Add(nomeBase);
                continue;
            }

            string caminhoEscolhido = preferirContornada
                ? (kvp.Value.outline ?? kvp.Value.normal)
                : (kvp.Value.normal ?? kvp.Value.outline);

            if (caminhoEscolhido == null)
            {
                naoAssociados.Add(nomeBase);
                continue;
            }

            var vectorImage = AssetDatabase.LoadAssetAtPath<VectorImage>(caminhoEscolhido);
            if (vectorImage == null)
            {
                naoAssociados.Add(nomeBase);
                continue;
            }

            // Evita duplicar entrada já existente pro mesmo path
            novasEntradas.RemoveAll(e => e.path == path);
            novasEntradas.Add(new IconeTeclaCatalog.Entrada { path = path, icone = vectorImage });
            quantidadeAssociada++;
        }

        catalogoAlvo.entradas = novasEntradas;
        EditorUtility.SetDirty(catalogoAlvo);
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// Extracts the matching key from a base filename, e.g. "keyboard_a" -> "a", "mouse_leftbutton" -> "leftbutton".
    /// </summary>
    private string ExtrairChave(string nomeBase)
    {
        if (nomeBase.StartsWith("keyboard_")) return nomeBase.Substring("keyboard_".Length);
        if (nomeBase.StartsWith("mouse_")) return nomeBase.Substring("mouse_".Length);
        return null;
    }
}
