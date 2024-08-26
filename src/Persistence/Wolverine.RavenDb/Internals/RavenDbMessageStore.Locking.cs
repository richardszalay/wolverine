using Raven.Client.Documents.Operations.CompareExchange;
using Wolverine.Runtime;

namespace Wolverine.RavenDb.Internals;

public partial class RavenDbMessageStore
{
    private string _leaderLockId;
    private string _scheduledLockId;
    private IWolverineRuntime _runtime;

    private DistributedLock? _leaderLock;
    private long _lastLockIndex = 0;

    public bool HasLeadershipLock()
    {
        if (_leaderLock == null) return false;
        if (_leaderLock.ExpirationTime < DateTimeOffset.UtcNow) return false;
        return true;
    }

    public async Task<bool> TryAttainLeadershipLockAsync(CancellationToken token)
    {
        var newLock = new DistributedLock
        {
            NodeId = _runtime!.Options.UniqueNodeId,
            ExpirationTime = DateTimeOffset.UtcNow.AddMinutes(5),
        };
        
        if (_leaderLock == null)
        {
            var result = await _store.Operations.SendAsync(new PutCompareExchangeValueOperation<DistributedLock>(_leaderLockId, newLock, 0), token: token);
            if (result.Successful)
            {
                _leaderLock = newLock;
                _lastLockIndex = result.Index;
                return true;
            }

            return false;
        }
        
        var result2 = await _store.Operations.SendAsync(new PutCompareExchangeValueOperation<DistributedLock>(_leaderLockId, newLock, _lastLockIndex), token: token);
        if (result2.Successful)
        {
            _leaderLock = newLock;
            _lastLockIndex = result2.Index;
            return true;
        }

        return false;
    }

    public async Task ReleaseLeadershipLockAsync()
    {
        if (_leaderLock == null) return;
        await _store.Operations.SendAsync(new DeleteCompareExchangeValueOperation<DistributedLock>(_leaderLockId, _lastLockIndex));
        _leaderLock = null;
    }
}

public class DistributedLock
{
    public Guid NodeId { get; set; }
    public DateTimeOffset ExpirationTime { get; set; } 
}