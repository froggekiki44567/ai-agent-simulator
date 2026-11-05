
using System.Threading;

namespace AgentTown.World;

public sealed class WorldState
{
    private long _currentTick = 0;

    public long CurrentTick => Interlocked.Read(ref _currentTick);

    public long AdvanceTick()
    {
        return Interlocked.Increment(ref _currentTick);
    }
}
