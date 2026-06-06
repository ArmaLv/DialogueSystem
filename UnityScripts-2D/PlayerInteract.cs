using UnityEngine;

[RequireComponent(typeof(PlayerController2D))]
public class PlayerInteract : MonoBehaviour
{
    [Header("Detection")]
    [Tooltip("Overlap radius used to find nearby interactables.")]
    public float detectRadius = 2f;

    [Tooltip("Layers that NPC / Ladder colliders live on.")]
    public LayerMask interactLayer = ~0;

    private PlayerInputActions _input;
    private NPCDialogue _nearestNPC;
    private LadderTraversal _nearestLadder;
    private readonly Collider2D[] _hits = new Collider2D[8];

    private void Awake()
    {
        _input = new PlayerInputActions();
    }

    private void OnEnable() => _input.Player.Enable();
    private void OnDisable() => _input.Player.Disable();

    private void Update()
    {
        if (DialogueManager.Instance != null && DialogueManager.Instance.IsOpen)
        {
            _nearestNPC = null;
            _nearestLadder = null;
            return;
        }

        if (LadderTraversal.IsTraversing)
        {
            _nearestNPC = null;
            _nearestLadder = null;
            return;
        }

        FindNearestInteractables();

        if (!_input.Player.Interact.WasPressedThisFrame()) return;

        if (_nearestLadder != null)
            _nearestLadder.TryTraverse(gameObject);
        else if (_nearestNPC != null)
            _nearestNPC.TryStartDialogue();
    }

    private void FindNearestInteractables()
    {
        int count = Physics2D.OverlapCircleNonAlloc(
            transform.position, detectRadius, _hits, interactLayer);

        _nearestNPC = null;
        _nearestLadder = null;
        float bestNPCDist = float.MaxValue;
        float bestLadderDist = float.MaxValue;

        for (int i = 0; i < count; i++)
        {
            if (_hits[i] == null || _hits[i].gameObject == gameObject) continue;

            float dist = Vector2.Distance(transform.position, _hits[i].transform.position);

            LadderTraversal ladder = _hits[i].GetComponent<LadderTraversal>();
            if (ladder != null && dist < bestLadderDist)
            {
                bestLadderDist = dist;
                _nearestLadder = ladder;
                continue;
            }

            NPCDialogue npc = _hits[i].GetComponent<NPCDialogue>();
            if (npc != null && dist < bestNPCDist)
            {
                bestNPCDist = dist;
                _nearestNPC = npc;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, detectRadius);
    }
}