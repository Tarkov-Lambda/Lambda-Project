using System;

public static class MapLoadEvent
{
    public static Action OnBeginLoad; // Does not include lobby load
    public static Action OnSuccessfulLoad; // Does not include lobby load
    public static Action OnBeginUnload;
    public static Action OnUnload;
}