using System.Reflection;
using EFT;
using Fika.Core.Main.Players;
using HarmonyLib;
using UnityEngine;

public static class ObservedPlayerExtensions
{
    public static void ResetSnapshotter(this ObservedPlayer observedPlayer)
    {
        observedPlayer.Snapshotter?.Clear();

        var traverse = Traverse.Create(observedPlayer);
        var stateField = traverse.Field("CurrentPlayerState");
        if (stateField.FieldExists())
        {
            var stateObject = stateField.GetValue();
            if (stateObject != null)
            {
                var stateType = stateObject.GetType();
                var ctor = stateType.GetConstructor(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, [typeof(Vector3), typeof(Vector2)], null);
                if (ctor != null)
                {
                    var newState = ctor.Invoke([observedPlayer.Position, observedPlayer.Rotation]);
                    stateField.SetValue(newState);
                }
            }
        }
    }
}