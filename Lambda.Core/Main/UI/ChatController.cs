using Comfort.Common;
using EFT;
using EFT.UI;
using EFT.UI.Screens;
using Lambda.Core.Main.Gamemode;
using Lambda.Core.Networking;
using Lambda.Shared.Models;
using Lambda.UI;
using System;
using UnityEngine;

namespace Lambda.Core.Main.UI
{
    internal class ChatController : IDisposable
    {
        readonly Chat _chat;

        internal ChatController(Chat chat)
        {
            _chat = chat;

            chat.OnSubmit += OnSubmit;

            Singleton<ChatMessagePacketWarden>.Instance.AfterPacketApplied += OnMessageReceived;
            UnityTicker.OnUpdate += Update;
        }

        public void Dispose()
        {
            Singleton<ChatMessagePacketWarden>.Instance.AfterPacketApplied -= OnMessageReceived;
            UnityTicker.OnUpdate -= Update;
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.Return))
                SetChatFocus(!_chat.IsFocused);

            if (Input.GetKeyDown(KeyCode.Tab))
                _chat.CycleScope();

            if (Input.GetKeyDown(KeyCode.Escape))
                SetChatFocus(false);
        }

        void SetChatFocus(bool enable)
        {
            if (enable)
            {
                if (!EftScreenManager.Instance.CheckCurrentScreen(EEftScreenType.BattleUI))
                    return;
            }
            else
            {
                if (!_chat.IsFocused)
                    return;
            }

            _chat.FocusInput(enable);
            GamePlayerOwner.SetIgnoreInputWithKeepResetLook(enable);

            if (enable)
                UIEventSystem.Instance.Enable();
            else
                UIEventSystem.Instance.Disable();
        }

        private void OnSubmit(ChatMessageScope scope, string msg)
        {
            if (string.IsNullOrEmpty(msg))
                return;
            Singleton<ChatMessagePacketWarden>.Instance.Send(scope, msg);
        }

        private void OnMessageReceived(ChatMessagePacket chatMessage)
        {
            PlayerScore playerScore = H.GetPlayerScore(chatMessage.Player);

            _chat.PopMessage(
                chatMessage.scope, 
                playerScore.Faction, 
                chatMessage.Player.Profile.Nickname, 
                chatMessage.msg);
        }
    }
}
