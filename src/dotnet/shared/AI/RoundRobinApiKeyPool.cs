using System;
using System.Collections.Generic;
using System.Threading;

namespace Shared.AI;

public class RoundRobinApiKeyPool<T>(IReadOnlyList<T> keys) : IApiKeyPool<T>
{
    private int _index = -1;

    public T GetNext()
    {
        if (keys == null || keys.Count == 0)
            throw new InvalidOperationException("API key pool is empty.");

        var next = (int)((uint)Interlocked.Increment(ref _index) % keys.Count);
        return keys[next];
    }
}
