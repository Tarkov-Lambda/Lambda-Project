using UnityEngine;
using UnityEngine.UI;
using TMPro;

using Lambda.Shared.Models;
using System.Text;
using System.Collections.Generic;
using System;
using System.Linq;

namespace Lambda.UI
{
    public class Chat : MonoBehaviour
    {
        [SerializeField] private ScrollRect scrollRectMessages;
        [SerializeField] private TextMeshProUGUI prefabMessage;

        [SerializeField] private GameObject[] gameObjectsWhenFocused;
        [SerializeField] private TextMeshProUGUI textCurrentScope;
        [SerializeField] private TMP_InputField inputField;

        [SerializeField] private FactionColors factionColors;

        [SerializeField] private float timeMessageVisible = 12f;

        [SerializeField] private KeyCode keybindCycleScope = KeyCode.Tab;

        private Dictionary<TextMeshProUGUI, float> messages = new Dictionary<TextMeshProUGUI, float>();

        private bool _isFocused;
        private ChatMessageScope _scope;

        public event Action<ChatMessageScope, string> OnSubmit;

        void Start()
        {
            inputField.onSubmit.AddListener((message) => OnSubmit?.Invoke(_scope, message));
            SetCurrentScope(_scope);
        }

        public void FocusInput(bool focus)
        {
            _isFocused = focus;

            foreach (var go in gameObjectsWhenFocused)
            {
                if (go == null) continue;
                go.SetActive(focus);
            }

            if (focus)
                inputField.ActivateInputField();
            else
                inputField.DeactivateInputField();

            scrollRectMessages.verticalScrollbar.gameObject.SetActive(focus);
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

            messageRichText.Append(scope.ToString().ToUpperInvariant());
            messageRichText.Append(" ");

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
            messages.Add(newMessageObject, timeMessageVisible);

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

            foreach (var item in messages.Keys)
            {
                float timeVisibleLeft = messages[item];

                float alpha = 1f;
                if (!_isFocused && timeVisibleLeft < 1f)
                    alpha = Mathf.InverseLerp(timeMessageVisible, timeMessageVisible - 1f, timeVisibleLeft);

                messages[item] -= Time.deltaTime;
            }
        }

        public void Clear()
        {
            foreach (var item in messages.Keys.ToList())
            {
                Destroy(item.gameObject);
            }
            messages.Clear();
        }
    }
}
