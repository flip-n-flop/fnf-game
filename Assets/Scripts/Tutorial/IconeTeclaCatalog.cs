using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

[CreateAssetMenu(fileName = "IconeTeclaCatalog", menuName = "Tutoriais/Catálogo de Ícones de Tecla")]
public class IconeTeclaCatalog : ScriptableObject
{
    [System.Serializable]
    public class Entrada
    {
        [Tooltip("Path exato do Input System, ex: <Keyboard>/space, <Gamepad>/buttonSouth")]
        public string path;
        public VectorImage icone;
    }

    public List<Entrada> entradas;

    private Dictionary<string, VectorImage> _mapa;

    public VectorImage ObterIcone(string path)
    {
        if (_mapa == null)
        {
            _mapa = new Dictionary<string, VectorImage>();
            foreach (var e in entradas)
            {
                if (e == null || string.IsNullOrEmpty(e.path) || e.icone == null) continue;
                if (!_mapa.ContainsKey(e.path)) _mapa.Add(e.path, e.icone);
                else Debug.LogWarning($"IconeTeclaCatalog: path duplicado '{e.path}' ignorado.");
            }
        }

        if (_mapa.TryGetValue(path, out var icone)) return icone;

        Debug.LogWarning($"IconeTeclaCatalog: nenhum ícone cadastrado para '{path}'.");
        return null;
    }
}
