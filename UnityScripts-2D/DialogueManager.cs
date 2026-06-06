using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Dialogue;

public class DialogueManager : MonoBehaviour
{
    public static DialogueManager Instance { get; private set; }

    [Header("Dialogue Panel")]
    [Tooltip("Root panel GameObject — shown during a conversation, hidden otherwise.")]
    public GameObject dialoguePanel;

    [Tooltip("Displays the speaking NPC's name.")]
    public TMP_Text speakerNameText;

    [Tooltip("Displays the body text of the current node.")]
    public TMP_Text bodyText;

    [Header("Choice Buttons")]
    [Tooltip("Container that holds the dynamically-spawned choice buttons.")]
    public Transform choiceContainer;

    [Tooltip("Prefab for a single choice button. Must have a Button + TMP_Text child.")]
    public GameObject choiceButtonPrefab;

    [Header("Continue / Dismiss")]
    [Tooltip("Button shown when a node has no choices (end-of-branch / end-of-tree).")]
    public Button continueButton;

    [Header("Typewriter")]
    [Tooltip("Characters revealed per second. Set to 0 to disable the effect.")]
    public float typewriterSpeed = 40f;

    private DialogueTree _activeTree;
    private DialogueNode _currentNode;
    private Action _onDialogueClose;
    private bool _isOpen;
    private bool _typewriterRunning;
    private string _fullText;
    private Coroutine _typewriterCoroutine;
    private readonly List<GameObject> _choicePool = new List<GameObject>();

    private static DialogueDatabase _database;

    public static DialogueDatabase Database
    {
        get
        {
            if (_database == null)
                LoadDatabase();
            return _database;
        }
    }

    private static void LoadDatabase()
    {
        TextAsset asset = Resources.Load<TextAsset>("dialogues");
        if (asset == null)
        {
            Debug.LogError("[DialogueManager] Could not find 'dialogues.json' in any Resources folder.");
            _database = new DialogueDatabase { dialogues = new List<DialogueTree>() };
            return;
        }
        _database = JsonUtility.FromJson<DialogueDatabase>(asset.text);
        Debug.Log($"[DialogueManager] Loaded {_database.dialogues.Count} dialogue tree(s).");
    }

    public static DialogueTree GetTree(string id)
    {
        if (Database?.dialogues == null) return null;
        return Database.dialogues.Find(t => t.id == id);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);

        if (continueButton != null)
            continueButton.onClick.AddListener(OnContinueClicked);
    }

    private void Update()
    {
        if (!_isOpen) return;

        // Skip typewriter on any key / gamepad confirm
        if (_typewriterRunning && (Input.GetKeyDown(KeyCode.Space)
                                   || Input.GetKeyDown(KeyCode.Return)
                                   || Input.GetKeyDown(KeyCode.JoystickButton0)))
        {
            SkipTypewriter();
        }
    }

    public void StartDialogue(DialogueTree tree, Action onClose = null)
    {
        if (tree == null)
        {
            Debug.LogWarning("[DialogueManager] StartDialogue called with a null tree.");
            return;
        }

        _activeTree = tree;
        _onDialogueClose = onClose;
        _isOpen = true;

        if (dialoguePanel != null)
            dialoguePanel.SetActive(true);

        DialogueNode startNode = tree.nodes?.Find(n => n.nodeId == "start");
        if (startNode == null)
        {
            Debug.LogError($"[DialogueManager] Tree '{tree.id}' has no 'start' node.");
            CloseDialogue();
            return;
        }

        ShowNode(startNode);
    }

    public void CloseDialogue()
    {
        if (_typewriterCoroutine != null)
            StopCoroutine(_typewriterCoroutine);

        _isOpen = false;

        HideChoices();

        if (dialoguePanel != null)
            dialoguePanel.SetActive(false);

        _onDialogueClose?.Invoke();
        _onDialogueClose = null;
        _activeTree      = null;
        _currentNode     = null;
    }

    public bool IsOpen => _isOpen;

    private void ShowNode(DialogueNode node)
    {
        _currentNode = node;

        if (speakerNameText != null)
            speakerNameText.text = node.speaker ?? string.Empty;

        HandleSideEffects(node);

        _fullText = node.text ?? string.Empty;

        if (typewriterSpeed > 0f)
        {
            if (_typewriterCoroutine != null)
                StopCoroutine(_typewriterCoroutine);
            _typewriterCoroutine = StartCoroutine(TypewriterRoutine(_fullText));
        }
        else
        {
            if (bodyText != null) bodyText.text = _fullText;
            RevealChoices(node);
        }
    }

    private IEnumerator TypewriterRoutine(string fullText)
    {
        _typewriterRunning = true;
        HideChoices();

        if (continueButton != null) continueButton.gameObject.SetActive(false);
        if (bodyText != null) bodyText.text = string.Empty;
        float delay = 1f / typewriterSpeed;

        for (int i = 0; i <= fullText.Length; i++)
        {
            if (bodyText != null)
                bodyText.text = fullText.Substring(0, i);
            yield return new WaitForSeconds(delay);
        }

        _typewriterRunning   = false;
        _typewriterCoroutine = null;
        RevealChoices(_currentNode);
    }

    private void SkipTypewriter()
    {
        if (_typewriterCoroutine != null)
            StopCoroutine(_typewriterCoroutine);

        _typewriterRunning   = false;
        _typewriterCoroutine = null;

        if (bodyText != null) bodyText.text = _fullText;
        RevealChoices(_currentNode);
    }

    private void RevealChoices(DialogueNode node)
    {
        HideChoices();

        bool hasOptions = node.options != null && node.options.Count > 0;

        if (hasOptions)
        {
            int count = Mathf.Min(node.options.Count, 4);
            RectTransform containerRect = choiceContainer as RectTransform;
            float containerHeight = containerRect != null ? containerRect.rect.height : 0f;
            float slotHeight = count > 0 ? containerHeight / count : containerHeight;

            for (int i = 0; i < count; i++)
            {
                DialogueOption opt = node.options[i];
                GameObject btnGo;
                if (i < _choicePool.Count)
                {
                    btnGo = _choicePool[i];
                    btnGo.SetActive(true);
                }
                else
                {
                    btnGo = Instantiate(choiceButtonPrefab, choiceContainer);
                    _choicePool.Add(btnGo);
                }

                RectTransform rt = btnGo.GetComponent<RectTransform>();
                if (rt != null && containerRect != null)
                {
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(1f, 1f);
                    rt.pivot = new Vector2(0.5f, 1f);
                    rt.offsetMin = new Vector2(0f, rt.offsetMin.y);
                    rt.offsetMax = new Vector2(0f, rt.offsetMax.y);

                    float buttonHeight = rt.rect.height > 0f ? rt.rect.height : slotHeight * 0.8f;
                    float slotTop = -slotHeight * i;
                    float centreOffset = (slotHeight - buttonHeight) * 0.5f;
                    rt.anchoredPosition = new Vector2(0f, slotTop - centreOffset);
                }

                TMP_Text label = btnGo.GetComponentInChildren<TMP_Text>();
                if (label != null) label.text = opt.label;

                Button btn = btnGo.GetComponent<Button>();
                btn.onClick.RemoveAllListeners();
                string nextId = opt.next;
                btn.onClick.AddListener(() => OnOptionChosen(nextId));
            }

            if (continueButton != null)
                continueButton.gameObject.SetActive(false);
        }
        else
        {
            // No options — show the continue/dismiss button
            if (continueButton != null)
                continueButton.gameObject.SetActive(true);
        }
    }

    private void HideChoices()
    {
        foreach (GameObject go in _choicePool)
            go.SetActive(false);
    }

    private void OnOptionChosen(string nextNodeId)
    {
        if (string.IsNullOrEmpty(nextNodeId))
        {
            CloseDialogue();
            return;
        }

        DialogueNode next = _activeTree?.nodes?.Find(n => n.nodeId == nextNodeId);
        if (next == null)
        {
            Debug.LogWarning($"[DialogueManager] Node '{nextNodeId}' not found in tree '{_activeTree?.id}'. Closing.");
            CloseDialogue();
            return;
        }

        ShowNode(next);
    }

    private void OnContinueClicked()
    {
        CloseDialogue();
    }

    private void HandleSideEffects(DialogueNode node)
    {
        if (!string.IsNullOrEmpty(node.giveItem))
            OnGiveItem(node.giveItem);

        if (!string.IsNullOrEmpty(node.setFlag))
            OnSetFlag(node.setFlag);

        if (!string.IsNullOrEmpty(node.openShop))
            OnOpenShop(node.openShop);
    }

    protected virtual void OnGiveItem(string itemId)
    {
        Debug.Log($"[DialogueManager] Give item: {itemId}");
    }

    protected virtual void OnSetFlag(string flagId)
    {
        Debug.Log($"[DialogueManager] Set flag: {flagId}");
    }

    protected virtual void OnOpenShop(string shopId)
    {
        Debug.Log($"[DialogueManager] Open shop: {shopId}");
    }
}