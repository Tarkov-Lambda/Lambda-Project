using EFT;

public static class PlayerExtensions
{
    public static PlayerScore GetScore(this Player player)
    {
        return H.GetPlayerScore(player);
    }
}