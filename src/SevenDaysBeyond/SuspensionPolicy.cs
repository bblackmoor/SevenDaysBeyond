namespace SevenDaysBeyond
{
    internal static class SuspensionPolicy
    {
        internal const float GraceSeconds = 10;
        internal static bool MustRun(int players, bool joining, bool transfer, bool preparation, float now, float lastNeeded)
        { return players > 0 || joining || transfer || preparation || now - lastNeeded < GraceSeconds; }
    }
}
