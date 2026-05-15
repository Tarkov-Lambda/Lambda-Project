using System;
using Audio.SpatialSystem;
using Audio.SpatialSystem.Data;
using Comfort.Common;

public class LambdaAudioRoomController : Singleton<LambdaAudioRoomController>, IDisposable
{
    public ISpatialAudioRoom audioRoom = new PhantomAudioRoom();

    public GClass3573 RoomChangeEvent { get; private set; }

    public LambdaAudioRoomController()
    {
        RoomChangeEvent = new()
        {
            Room = audioRoom,
            CurrentRoomType = audioRoom.Type,
            CurrentOutdoorRoomID = 0,
            InteractionState = EPlayerRoomInteractionState.Enter
        };

    }

    public void TriggerChange()
    {
        GlobalEventHandlerClass.Instance.method_0(typeof(GClass3573), RoomChangeEvent);
    }

    public void Dispose() { }
}