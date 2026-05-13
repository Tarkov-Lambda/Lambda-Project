namespace PacketWarden.FikaIntegration;

public static class FikaBootstrap
{
    public static INetworkBackend Initialize() => new FikaBackend();
}