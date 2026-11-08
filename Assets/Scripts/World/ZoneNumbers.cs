namespace AshenRealm.World
{
    /// <summary>Stable zone ids; side rooms are appended so existing stairs keep their destinations.</summary>
    public static class ZoneNumbers
    {
        public const int Valley = 0;
        public const int City = 1;
        public const int Tavern = 2;
        public const int Forge = 3;
        public const int Chapel = 4;
        public const int FirstDepth = 5;
        public const int DepthCount = 5;
        public const int FirstHome = FirstDepth + DepthCount;
        public const int HomeCount = 3;
    }
}
