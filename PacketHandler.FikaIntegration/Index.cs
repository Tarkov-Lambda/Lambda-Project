using MemoryPack;

public static class FikaBootstrap
{
    public static INetworkBackend Initialize()
    {
        MemoryPackFormatterProvider.Register(new PlayerFormatter());
        MemoryPackFormatterProvider.Register(new ItemFormatter());
        MemoryPackFormatterProvider.Register(new InventoryDescriptorClassFormatter());
        MemoryPackFormatterProvider.Register(new ItemAddressFormatter());

        return new FikaBackend();
    }
}