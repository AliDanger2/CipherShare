using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace CipherShare.Services;

/// <summary>
/// A small token-bucket style throttle. Call WaitIfNeededAsync after every chunk you
/// read/write, telling it how many bytes just moved; it sleeps just enough to keep the
/// average rate at or below the configured limit. Pass 0 (or less) for "unlimited".
/// </summary>
public class BandwidthThrottle
{
    private readonly double _maxBytesPerSecond;
    private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
    private long _bytesSinceStart;

    public BandwidthThrottle(double maxMegabytesPerSecond)
    {
        _maxBytesPerSecond = maxMegabytesPerSecond > 0 ? maxMegabytesPerSecond * 1024 * 1024 : 0;
    }

    public async Task WaitIfNeededAsync(int bytesMoved, CancellationToken token)
    {
        if (_maxBytesPerSecond <= 0) return;

        _bytesSinceStart += bytesMoved;
        double expectedSeconds = _bytesSinceStart / _maxBytesPerSecond;
        double actualSeconds = _stopwatch.Elapsed.TotalSeconds;
        double delaySeconds = expectedSeconds - actualSeconds;

        if (delaySeconds > 0)
        {
            await Task.Delay(TimeSpan.FromSeconds(delaySeconds), token).ConfigureAwait(false);
        }
    }
}
