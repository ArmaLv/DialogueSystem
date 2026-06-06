using System;
using System.Collections.Generic;
using UnityEngine;
using Dialogue;

public enum ConversationConditionType
{
    None,
    FlagSet,
    FlagNotSet,
    QuestComplete
}

[Serializable]
public class ConversationEntry
{
    [Tooltip("Must match an 'id' in dialogues.json exactly.")]
    public string dialogueTreeId;

    [Tooltip("How to decide whether this conversation is available right now.")]
    public ConversationConditionType condition = ConversationConditionType.None;

    [Tooltip("Flag or quest id used by the condition check (leave blank for None).")]
    public string conditionValue;
}

public class NPCDialogue : MonoBehaviour
{
    [Header("Conversations (evaluated top-to-bottom — first match wins)")]
    [Tooltip("Add one entry per possible dialogue tree this NPC can speak. Order matters.")]
    public List<ConversationEntry> conversations = new List<ConversationEntry>();

    [Header("Interaction")]
    [Tooltip("World-space radius in which the player can start the interaction.")]
    public float interactionRadius = 2f;

    [Tooltip("If set, only shown while the player is within interactionRadius. " +
             "Assign a world-space Canvas with a prompt label.")]
    public GameObject interactionPromptUI;

    [Tooltip("Optionally face the player when a dialogue starts (requires a SpriteRenderer on this NPC).")]
    public bool facePlayerOnTalk = true;

    private Transform _player;
    private bool _promptVisible;
    private bool _inDialogue;

    private void Start()
    {
        GameObject playerGo = GameObject.FindGameObjectWithTag("Player");
        if (playerGo != null) _player = playerGo.transform;
        SetPromptVisible(false);
    }

    private void Update()
    {
        if (_player == null || _inDialogue) return;
        float dist = Vector2.Distance(transform.position, _player.position);
        bool inRange = dist <= interactionRadius;
        if (inRange != _promptVisible)
            SetPromptVisible(inRange);
    }

    public bool TryStartDialogue()
    {
        if (_inDialogue) return false;
        ConversationEntry entry = FindAvailableConversation();
        if (entry == null)
        {
            Debug.Log($"[NPCDialogue] '{gameObject.name}': no available conversation.");
            return false;
        }
        DialogueTree tree = DialogueManager.GetTree(entry.dialogueTreeId);
        if (tree == null)
        {
            Debug.LogWarning($"[NPCDialogue] Tree '{entry.dialogueTreeId}' not found in database.");
            return false;
        }
        _inDialogue = true;
        SetPromptVisible(false);
        if (facePlayerOnTalk && _player != null)
            FacePlayer();
        DialogueManager.Instance.StartDialogue(tree, OnDialogueClosed);
        return true;
    }

    public bool IsPlayerInRange()
    {
        if (_player == null) return false;
        return Vector2.Distance(transform.position, _player.position) <= interactionRadius;
    }

    private ConversationEntry FindAvailableConversation()
    {
        foreach (ConversationEntry entry in conversations)
        {
            if (EvaluateCondition(entry))
                return entry;
        }
        return null;
    }

    private bool EvaluateCondition(ConversationEntry entry)
    {
        switch (entry.condition)
        {
            case ConversationConditionType.None:
                return true;

            case ConversationConditionType.FlagSet:
            case ConversationConditionType.QuestComplete:
                // Replace GameFlags.IsSet with your own flag/quest system.
                return GameFlags.IsSet(entry.conditionValue);

            case ConversationConditionType.FlagNotSet:
                return !GameFlags.IsSet(entry.conditionValue);

            default:
                return true;
        }
    }

    private void OnDialogueClosed()
    {
        _inDialogue = false;
    }

    private void SetPromptVisible(bool visible)
    {
        _promptVisible = visible;
        if (interactionPromptUI != null)
            interactionPromptUI.SetActive(visible);
    }

    private void FacePlayer()
    {
        if (_player == null) return;
        float dir = _player.position.x - transform.position.x;
        Vector3 s = transform.localScale;
        s.x = Mathf.Abs(s.x) * (dir >= 0f ? 1f : -1f);
        transform.localScale = s;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactionRadius);
    }
}
