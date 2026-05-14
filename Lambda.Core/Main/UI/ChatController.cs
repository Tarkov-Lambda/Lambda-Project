using Comfort.Common;
using Lambda.Core.Networking;
using Lambda.Shared.Models;
using Lambda.UI;
using System;

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
        }

        public void Dispose()
        {
            Singleton<ChatMessagePacketWarden>.Instance.AfterPacketApplied -= OnMessageReceived;
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
