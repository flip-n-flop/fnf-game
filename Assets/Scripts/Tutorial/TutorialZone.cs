using UnityEngine;

/// <summary>
/// Marks a trigger zone that shows a tutorial while the Player is inside it,
/// and hides it on exit. No success/failure logic — purely presence-based.
/// Meant to be spawned by <see cref="TutorialSpawner"/> from level JSON data,
/// mirroring how <see cref="ObstacleSpawner"/> spawns obstacle GameObjects.
/// </summary>
[RequireComponent(typeof(Collider2D))]
public class TutorialZone : MonoBehaviour
{
    public TutorialData tutorial;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;

        TutorialManager.Instance.Mostrar(tutorial);
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (!other.CompareTag("Player")) return;
        TutorialManager.Instance.Esconder(tutorial);
    }

    private void OnDrawGizmos()
    {
        var collider = GetComponent<BoxCollider2D>();
        if (collider == null) return;

        Gizmos.color = new Color(0f, 1f, 0.6f, 0.25f); // verde translúcido
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.DrawCube(collider.offset, collider.size);

        Gizmos.color = new Color(0f, 1f, 0.6f, 1f);
        Gizmos.DrawWireCube(collider.offset, collider.size);
    }
}
