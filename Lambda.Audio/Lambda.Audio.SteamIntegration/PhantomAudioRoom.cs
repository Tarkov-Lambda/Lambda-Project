using System.Collections.Generic;
using Audio.AmbientSubsystem;
using Audio.SpatialSystem;
using UnityEngine;

namespace Lambda.Audio.SteamIntegration.AudioRooms;

public class PhantomAudioRoom : ISpatialAudioRoom
{
    public short ID { get; set; } = 870;
    public EAudioRoomTypeMask Type { get; set; } = EAudioRoomTypeMask.IndoorBasement;
    public string Name => "Phantom_Command_Room";

    public BoxCollider[] Colliders { get; } = new BoxCollider[0];

    public bool IsOutdoor => false;
    public bool IsIsolated => Type == EAudioRoomTypeMask.IndoorIsolated;
    public float RoomSize => 5f;
    public Bounds Bounds { get; set; } = new Bounds(Vector3.zero, Vector3.one * 1000f);
    public bool IsValid => true;

    public RoomAmbientData RoomAmbientData { get; set; } = new RoomAmbientData
    {
        PrecipitationVolume = 1f,
        OutdoorAmbientVolume = 0f,
        RoomToneVolume = 1f
    };

    public bool CheckIsolationRelation(ISpatialAudioRoom roomToCheck) => false;

    public void PlayRoomToneSound() { }
    public void StopRoomToneSound() { }

    public Vector3 Position => Vector3.zero;
    public EAudioRoomTypeMask RoomType => Type;

    public List<ISpatialPortal> GetPortals() => [];
}