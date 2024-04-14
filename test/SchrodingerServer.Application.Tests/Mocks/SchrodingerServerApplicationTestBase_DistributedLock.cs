using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Moq;
using Volo.Abp.DistributedLocking;

namespace SchrodingerServer;

public partial class SchrodingerServerApplicationTestBase
{
    private readonly Mock<IAbpDistributedLock> _mockDistributedLock = new();
    private new Dictionary<string, DateTime> _keyRequestTimes = new();


    protected IAbpDistributedLock MockDistributeLock()
    {
        return _mockDistributedLock.Object;
    }

    
    protected void MockAbpDistributedLockAlwaysSuccess()
    {
        _mockDistributedLock
            .Setup(x => x.TryAcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns<string, TimeSpan, CancellationToken>((name, timeSpan, cancellationToken) => 
                Task.FromResult<IAbpDistributedLockHandle>(new LocalAbpDistributedLockHandle(new SemaphoreSlim(0))));
    }
    
    
    protected void MockAbpDistributedLockWithTimeout(long timeout = 1000)
    {
        _mockDistributedLock
            .Setup(x => x.TryAcquireAsync(It.IsAny<string>(), It.IsAny<TimeSpan>(), It.IsAny<CancellationToken>()))
            .Returns<string, TimeSpan, CancellationToken>((name, timeSpan, cancellationToken) =>
            {
                lock (_keyRequestTimes)
                {
                    if (_keyRequestTimes.TryGetValue(name, out var lastRequestTime))
                        if ((DateTime.Now - lastRequestTime).TotalMilliseconds <= timeout)
                            return Task.FromResult<IAbpDistributedLockHandle>(null);
                    _keyRequestTimes[name] = DateTime.Now;
                    var handleMock = new Mock<IAbpDistributedLockHandle>();
                    handleMock.Setup(h => h.DisposeAsync()).Callback(() =>
                    {
                        lock (_keyRequestTimes)
                            _keyRequestTimes.Remove(name);
                    });
                    return Task.FromResult(handleMock.Object);
                }
            });
    }
    
}