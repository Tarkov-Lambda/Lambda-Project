using System;
using Comfort.Common;
using EFT;
using ifp.arena.bep.Core.Gamemode;
using UnityEngine;

namespace ifp.arena.bep.Core.Debug;

public class HandsAnimatorDebugger : Singleton<HandsAnimatorDebugger>, IDisposable
{
    private Player _player;
    
    private string _lastControllerType = "None";
    private string _lastOperationName = "None";
    private Player.EOperationState? _lastOperationState = null;

    public void Init(Player player)
    {
        _player = player;

        UnityTicker.OnUpdate += Update;
    }

    public void Dispose()
    {
        UnityTicker.OnUpdate -= Update;
        Release(this);
    }

    private void Update()
    {
        if (_player == null || _player.HandsController == null)
            return;

        var currentController = _player.HandsController;
        string currentControllerType = currentController.GetType().Name;
        
        string currentOpName = "None";
        Player.EOperationState currentOpState = Player.EOperationState.Finished;

        // Extract the current HandsOperation (Spawn, Reload, Fire, Drop, etc.)
        if (currentController is Player.ItemHandsController itemController)
        {
            var op = itemController.CurrentHandsOperation;
            if (op != null)
            {
                currentOpName = op.GetType().Name;
                currentOpState = op.State;
            }
        }

        // Only log if the Controller, Operation, or the Operation's State has changed
        if (_lastControllerType != currentControllerType || 
            _lastOperationName != currentOpName || 
            _lastOperationState != currentOpState)
        {
            string itemName = currentController.Item != null ? currentController.Item.ShortName.Localized() : "Empty";

            D.Log($"[AnimatorDebug] {_player.Profile.Nickname} | " +
                                         $"Item: {itemName} | " +
                                         $"Controller: {currentControllerType} | " +
                                         $"Operation: {currentOpName} | " +
                                         $"State: {currentOpState}");

            _lastControllerType = currentControllerType;
            _lastOperationName = currentOpName;
            _lastOperationState = currentOpState;
        }
    }
}