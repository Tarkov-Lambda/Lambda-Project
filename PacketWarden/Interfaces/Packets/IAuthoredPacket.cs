using System;
using System.Reflection;
using EFT;

/// <summary>
/// Peer can send only for its own player, and server can send for any player
/// </summary>
public interface IAuthoredPacket
{
    public Player Player { get; set; }
}

// Reads IAuthoredPacket.Player through a constrained generic call, so struct packets are never boxed.
internal static class AuthoredPacket<T>
{
    public static readonly Func<T, Player> GetPlayer = typeof(IAuthoredPacket).IsAssignableFrom(typeof(T))
        ? (Func<T, Player>)Delegate.CreateDelegate(
            typeof(Func<T, Player>),
            typeof(AuthoredPacket<T>).GetMethod(nameof(Read), BindingFlags.NonPublic | BindingFlags.Static).MakeGenericMethod(typeof(T)))
        : null;

    private static Player Read<TAuthored>(TAuthored packet) where TAuthored : IAuthoredPacket => packet.Player;
}