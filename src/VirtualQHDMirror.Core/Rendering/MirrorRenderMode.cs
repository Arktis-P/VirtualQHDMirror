namespace VirtualQHDMirror.Core.Rendering;

public enum MirrorRenderMode
{
    Adaptive,
    Fixed30,
    Fixed60,
    Experimental120
}

public static class MirrorFrameTimingPolicy
{
    public static int GetIntervalMilliseconds(
        MirrorRenderMode mode,
        TimeSpan idleTime)
    {
        if (idleTime < TimeSpan.Zero)
        {
            idleTime = TimeSpan.Zero;
        }

        return mode switch
        {
            MirrorRenderMode.Experimental120 => 8,
            MirrorRenderMode.Fixed60 => 16,
            MirrorRenderMode.Fixed30 => 33,
            _ when idleTime < TimeSpan.FromSeconds(0.8) => 16,
            _ when idleTime < TimeSpan.FromSeconds(8) => 33,
            _ => 66
        };
    }
}
