using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "TutorialCatalog", menuName = "Tutoriais/Catálogo")]
public class TutorialCatalog : ScriptableObject
{
    public List<TutorialData> tutoriais;

    private Dictionary<string, TutorialData> _porId;

    public TutorialData ObterPorId(string id)
    {
        if (_porId == null)
        {
            _porId = new Dictionary<string, TutorialData>();
            foreach (var t in tutoriais)
            {
                if (t == null || string.IsNullOrEmpty(t.id)) continue;
                if (!_porId.ContainsKey(t.id)) _porId.Add(t.id, t);
                else Debug.LogWarning($"TutorialCatalog: id duplicado '{t.id}' ignorado.");
            }
        }

        if (_porId.TryGetValue(id, out var dados)) return dados;

        Debug.LogWarning($"TutorialCatalog: tutorial com id '{id}' não encontrado.");
        return null;
    }
}
