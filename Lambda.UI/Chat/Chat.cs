using System;
using System.Text;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.UI;
using TMPro;

using Lambda.Shared.Models;

namespace Lambda.UI
{
    public class Chat : MonoBehaviour
    {
        private class ChatMessageData
        {
            public TextMeshProUGUI TextComponent;
            public float TimeRemaining;
        }

        [SerializeField] private ScrollRect scrollRectMessages;
        [SerializeField] private TextMeshProUGUI prefabMessage;

        [SerializeField] private GameObject[] gameObjectsWhenFocused;
        [SerializeField] private TextMeshProUGUI textCurrentScope;
        [SerializeField] private TMP_InputField inputField;

        [SerializeField] private FactionColors factionColors;

        [SerializeField] private float timeMessageVisible = 12f;
        [SerializeField] private int maxMessages = 50;

        [SerializeField] private KeyCode keybindCycleScope = KeyCode.Tab;
        
        private Queue<ChatMessageData> messages = new Queue<ChatMessageData>();

        public bool IsFocused { get; private set; }
        private ChatMessageScope _scope;

        public event Action<ChatMessageScope, string> OnSubmit;

        void Start()
        {
            inputField.SetTextWithoutNotify(string.Empty);

            inputField.onSubmit.AddListener((inputText) => { 
                inputField.SetTextWithoutNotify(string.Empty);
                OnSubmit?.Invoke(_scope, inputText);
                });

            SetCurrentScope(_scope);

            FocusInput(false);
        }

        public void FocusInput(bool focus)
        {
            IsFocused = focus;

            foreach (var go in gameObjectsWhenFocused)
            {
                if (go == null) continue;
                go.SetActive(focus);
            }

            if (focus)
                inputField.ActivateInputField();
            else
                inputField.DeactivateInputField();

            scrollRectMessages.verticalScrollbar.transform.GetChild(0).gameObject.SetActive(focus);
        }

        void SetCurrentScope(ChatMessageScope scope)
        {
            _scope = scope;

            textCurrentScope.text = $"[{scope.ToString().ToUpperInvariant()}]";
        }

        public void PopMessage(ChatMessageScope scope, Faction senderFaction, string senderName, string msg)
        {
            StringBuilder messageRichText = new StringBuilder();

            if (scope == ChatMessageScope.Team)
            {
                messageRichText.Append("<color=#");
                messageRichText.Append(factionColors.GetHtmlString(senderFaction));
                messageRichText.Append(">");
            }

            messageRichText.Append("[");
            messageRichText.Append(scope.ToString().ToUpperInvariant());
            messageRichText.Append("] ");

            messageRichText.Append("<color=#");
            messageRichText.Append(factionColors.GetHtmlString(senderFaction));
            messageRichText.Append(">");

            messageRichText.Append(senderName);
            messageRichText.Append(": ");

            messageRichText.Append("</color>");

            messageRichText.Append(msg);

            if (scope == ChatMessageScope.Team)
                messageRichText.Append("</color>");

            TextMeshProUGUI newMessageObject = Instantiate(prefabMessage.gameObject, scrollRectMessages.content).GetComponent<TextMeshProUGUI>();
            newMessageObject.text = messageRichText.ToString();

            messages.Enqueue(new ChatMessageData
            {
                TextComponent = newMessageObject,
                TimeRemaining = timeMessageVisible
            });

            while (messages.Count > maxMessages)
            {
                ChatMessageData oldestMessage = messages.Dequeue();
                Destroy(oldestMessage.TextComponent.gameObject);
            }

            LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRectMessages.content);
            scrollRectMessages.normalizedPosition = Vector2.zero;
        }

        void Update()
        {
            if (Input.GetKeyDown(keybindCycleScope))
            {
                if (_scope == ChatMessageScope.All)
                    SetCurrentScope(ChatMessageScope.Team);
                else
                    SetCurrentScope(ChatMessageScope.All);
            }

            foreach (var msg in messages)
            {
                msg.TimeRemaining -= Time.deltaTime;

                float alpha = 1f;

                if (!IsFocused)
                {
                    alpha = Mathf.Clamp01(msg.TimeRemaining);
                }

                msg.TextComponent.alpha = alpha;
            }
        }

        public void Clear()
        {
            while (messages.Count > 0)
            {
                ChatMessageData msg = messages.Dequeue();
                if (msg.TextComponent != null)
                {
                    Destroy(msg.TextComponent.gameObject);
                }
            }
        }
    }
}
