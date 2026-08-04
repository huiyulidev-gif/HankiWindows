using System.Diagnostics;
using System.Threading;

namespace Hanki.Infrastructure.Windows;

public enum SingleInstanceRole
{
    PrimaryInstance,
    SecondaryInstance
}

public sealed class SingleInstanceManager : IDisposable
{
    private static readonly TimeSpan DefaultSignalTimeout = TimeSpan.FromMilliseconds(1200);
    private readonly Mutex? _mutex;
    private readonly EventWaitHandle? _activationEvent;
    private RegisteredWaitHandle? _registeredWait;
    private int _disposed;

    private SingleInstanceManager(
        SingleInstanceRole role,
        Mutex? mutex,
        EventWaitHandle? activationEvent,
        bool activationSignalSent)
    {
        Role = role;
        _mutex = mutex;
        _activationEvent = activationEvent;
        ActivationSignalSent = activationSignalSent;
    }

    public SingleInstanceRole Role { get; }
    public bool ActivationSignalSent { get; }

    public static SingleInstanceManager Start(
        string mutexName,
        string activationEventName,
        TimeSpan? signalTimeout = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(mutexName);
        ArgumentException.ThrowIfNullOrWhiteSpace(activationEventName);

        var mutex = new Mutex(initiallyOwned: false, mutexName);
        var ownsMutex = false;
        try
        {
            try
            {
                ownsMutex = mutex.WaitOne(TimeSpan.Zero);
            }
            catch (AbandonedMutexException)
            {
                ownsMutex = true;
            }

            if (!ownsMutex)
            {
                mutex.Dispose();
                var signalSent = TrySignalExisting(
                    activationEventName,
                    signalTimeout ?? DefaultSignalTimeout);
                return new SingleInstanceManager(
                    SingleInstanceRole.SecondaryInstance,
                    mutex: null,
                    activationEvent: null,
                    signalSent);
            }

            try
            {
                var activationEvent = new EventWaitHandle(
                    initialState: false,
                    EventResetMode.AutoReset,
                    activationEventName);
                return new SingleInstanceManager(
                    SingleInstanceRole.PrimaryInstance,
                    mutex,
                    activationEvent,
                    activationSignalSent: false);
            }
            catch
            {
                mutex.ReleaseMutex();
                mutex.Dispose();
                throw;
            }
        }
        catch
        {
            if (!ownsMutex)
                mutex.Dispose();
            throw;
        }
    }

    public void StartListening(Action activationRequested)
    {
        ArgumentNullException.ThrowIfNull(activationRequested);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        if (Role != SingleInstanceRole.PrimaryInstance || _activationEvent is null)
            throw new InvalidOperationException("Only the primary instance can listen for activation.");
        if (_registeredWait is not null)
            throw new InvalidOperationException("The activation listener has already started.");

        _registeredWait = ThreadPool.RegisterWaitForSingleObject(
            _activationEvent,
            (_, timedOut) =>
            {
                if (!timedOut && Volatile.Read(ref _disposed) == 0)
                    activationRequested();
            },
            state: null,
            Timeout.InfiniteTimeSpan,
            executeOnlyOnce: false);
    }

    public static bool TrySignalExisting(string activationEventName, TimeSpan timeout)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(activationEventName);
        if (timeout < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(timeout));

        var stopwatch = Stopwatch.StartNew();
        do
        {
            try
            {
                using var activationEvent = EventWaitHandle.OpenExisting(activationEventName);
                return activationEvent.Set();
            }
            catch (WaitHandleCannotBeOpenedException)
            {
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
            catch (IOException)
            {
            }

            var remaining = timeout - stopwatch.Elapsed;
            if (remaining <= TimeSpan.Zero)
                break;
            Thread.Sleep(remaining < TimeSpan.FromMilliseconds(40)
                ? remaining
                : TimeSpan.FromMilliseconds(40));
        }
        while (true);

        return false;
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
            return;

        var registeredWait = Interlocked.Exchange(ref _registeredWait, null);
        if (registeredWait is not null)
        {
            using var callbacksCompleted = new ManualResetEvent(initialState: false);
            if (registeredWait.Unregister(callbacksCompleted))
                callbacksCompleted.WaitOne(TimeSpan.FromSeconds(1));
        }

        _activationEvent?.Dispose();
        if (_mutex is not null)
        {
            _mutex.ReleaseMutex();
            _mutex.Dispose();
        }
    }
}
