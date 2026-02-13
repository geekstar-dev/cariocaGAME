namespace CariocaRuntime
{
    public static class CariocaDragBus
    {
        public struct DropInfo
        {
            public CardView view;
            public DropZone zone;
        }

        public static DropInfo? LastDrop;
        public static void Clear() => LastDrop = null;
    }
}