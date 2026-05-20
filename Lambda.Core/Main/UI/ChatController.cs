using Comfort.Common;
using Cysharp.Threading.Tasks;
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

            Singleton<AnnouncementPacketWarden>.Instance.AfterPacketApplied += OnAnnouncementReceived;
            Singleton<ChatMessagePacketWarden>.Instance.AfterPacketApplied += OnMessageReceived;
            UnityTicker.OnUpdate += Update;
            EventBus.OnEnter += OnMatchStateEnter;
        }

        public void Dispose()
        {
            Singleton<AnnouncementPacketWarden>.Instance.AfterPacketApplied -= OnAnnouncementReceived;
            Singleton<ChatMessagePacketWarden>.Instance.AfterPacketApplied -= OnMessageReceived;
            UnityTicker.OnUpdate -= Update;
            EventBus.OnEnter -= OnMatchStateEnter;
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

        void OnMatchStateEnter(MatchState matchState)
        {
            if (matchState == MatchState.Warmup && H.Gamemode is IGMTeam)
            {
                SetChatFocus(false);
            }
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

        private async void OnAnnouncementReceived(AnnouncementPacket announcementPacket)
        {
            if (announcementPacket.specificFaction == null || announcementPacket.specificFaction == H.MainPlayerScore.Faction)
                _chat.PopAnnouncementMessage(announcementPacket.msg);
        }

        private async void OnMessageReceived(ChatMessagePacket chatPacket)
        {
            PlayerContext playerScore = H.GetPlayerContext(chatPacket.Player);

            // await UniTask.DelayFrame(1);

            if (chatPacket.scope == ChatMessageScope.Team && chatPacket.Player.GetContext().Faction != H.MainPlayerScore.Faction)
                return;

            _chat.PopMessage(
                chatPacket.scope,
                playerScore.Faction,
                chatPacket.Player.Profile.Nickname,
                chatPacket.msg);
        }
    }
}
