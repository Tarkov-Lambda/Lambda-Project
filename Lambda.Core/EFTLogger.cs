using System;
using UnityEngine;
using BepInEx.Logging;

using EFT.Communications;
using Diz.Utils;
using Comfort.Common;
using EFT.UI;

namespace Lambda.Core
{
    public class EFTLogger : ManualLogSource
    {
        readonly Func<bool> displayNotification;

        public EFTLogger(string sourceName, Func<bool> displayNotification) : base(sourceName)
        {
            this.displayNotification = displayNotification;

            this.LogEvent += DisplayEFTNotification;
            this.LogEvent += LogEFTConsole;
        }

        void LogEFTConsole(object sender, LogEventArgs args)
        {
            if (!MonoBehaviourSingleton<PreloaderUI>.Instantiated)
                return;

            AsyncWorker.RunInMainTread(() =>
            {
                string text = $"{SourceName}: {args.Data}";

                switch (args.Level)
                {
                    case LogLevel.Warning:
                        ConsoleScreen.LogWarning(text);
                        break;
                    case LogLevel.Error:
                    case LogLevel.Fatal:
                        ConsoleScreen.LogError(text);
                        break;
                    default:
                        ConsoleScreen.Log(text);
                        break;
                }
            });
        }

        void DisplayEFTNotification(object sender, LogEventArgs args)
        {
            if (!Singleton<NotificationManager>.Instantiated)
                return;

            AsyncWorker.RunInMainTread(() =>
            {
                try
                {
                    if (displayNotification == null || !displayNotification())
                        return;

                    string text = $"<alpha=#44>{SourceName}:<alpha=#FF> {args.Data}";

                    var icon = ENotificationIconType.Default;
                    var color = Color.white;
                    switch (args.Level)
                    {
                        case LogLevel.Info:
                            icon = ENotificationIconType.WishlistQuest;
                            break;
                        case LogLevel.Warning:
                            icon = ENotificationIconType.WishlistQuest;
                            color = Color.yellow;
                            break;
                        case LogLevel.Error:
                        case LogLevel.Fatal:
                            icon = ENotificationIconType.Alert;
                            color = Color.red;
                            break;
                    }

                    NotificationManager.DisplayMessageNotification(
                        text,
                        duration: default,
                        iconType: icon,
                        textColor: color);
                }
                catch { }
            });
        }
    }
}