using Lambda.UI;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChatTester : MonoBehaviour
{
    [SerializeField] private Chat chat;
    [SerializeField] private string sender = "EditorPlayer";
    [SerializeField] private string message = "test message";

    void Start()
    {
        chat.OnSubmit += Chat_OnSubmit;  
    }

    private void Chat_OnSubmit(Lambda.Shared.Models.ChatMessageScope arg1, string arg2)
    {
        chat.PopMessage(arg1, Faction.T, "me", arg2);
        chat.FocusInput(false);
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Y))
        {
            chat.FocusInput(!chat.IsFocused);
        }

        if (Input.GetKeyDown(KeyCode.J))
        {
            chat.PopMessage(Lambda.Shared.Models.ChatMessageScope.All, Faction.T, sender, message);
        }
    }
}
