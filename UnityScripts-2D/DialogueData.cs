using System;
using System.Collections.Generic;

namespace Dialogue
{
    [Serializable]
    public class DialogueDatabase
    {
        public List<DialogueTree> dialogues;
    }

    [Serializable]
    public class DialogueTree
    {
        public string id;
        public List<DialogueNode> nodes;
    }

    [Serializable]
    public class DialogueNode
    {
        public string nodeId;
        public string speaker;
        public string text;
        public List<DialogueOption> options;

        // Side-effects (optional)
        public string giveItem;
        public string setFlag;
        public string openShop;
    }

    [Serializable]
    public class DialogueOption
    {
        public string label;
        public string next;
    }
}
