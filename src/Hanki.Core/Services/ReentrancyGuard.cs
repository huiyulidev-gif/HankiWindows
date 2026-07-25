namespace Hanki.Core.Services;

public sealed class ReentrancyGuard(TimeSpan cooldown)
{
    private int _active;
    private long _blockedUntilTicks;

    public bool TryEnter(out IDisposable? lease)
    {
        lease = null;
        var now = DateTimeOffset.UtcNow.UtcTicks;
        if (now < Interlocked.Read(ref _blockedUntilTicks))
            return false;
        if (Interlocked.CompareExchange(ref _active, 1, 0) != 0)
            return false;

        lease = new Lease(this);
        return true;
    }

    private void Exit()
    {
        Interlocked.Exchange(ref _blockedUntilTicks, DateTimeOffset.UtcNow.Add(cooldown).UtcTicks);
        Interlocked.Exchange(ref _active, 0);
    }

    private sealed class Lease(ReentrancyGuard owner) : IDisposable
    {
        private ReentrancyGuard? _owner = owner;
        public void Dispose() => Interlocked.Exchange(ref _owner, null)?.Exit();
    }
}
