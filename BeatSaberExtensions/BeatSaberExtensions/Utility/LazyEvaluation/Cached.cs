using System;

namespace BeatSaberExtensions.Utility.LazyEvaluation;

public class Cached<T>(
    Func<T> valueFactory,
    TimeSpan reevaluationInterval,
    Func<T, bool> refreshCondition = null
)
{
    private readonly object _lock = new object();
    private Lazy<T> _lazy = new Lazy<T>(valueFactory);
    private DateTime _lastEvaluated = DateTime.MinValue;

    public Cached(Func<T> valueFactory, TimeSpan recalculationInterval, Func<bool> refreshCondition)
        : this(
            valueFactory,
            recalculationInterval,
            refreshCondition is not null ? _ => refreshCondition.Invoke() : null
        ) { }

    public T Value
    {
        get
        {
            lock (_lock)
            {
                // Reset if the value is too old
                if (
                    _lazy is { IsValueCreated: true, Value: var value }
                    && (
                        DateTime.UtcNow - _lastEvaluated > reevaluationInterval
                        || (refreshCondition?.Invoke(value) ?? false)
                    )
                )
                {
                    _lazy = new Lazy<T>(valueFactory);
                }

                // If value has not yet been evaluated, set the _lastEvaluated value
                if (_lazy is { IsValueCreated: false })
                {
                    _lastEvaluated = DateTime.UtcNow;
                }

                // If the value is not created yet, this is the moment it gets evaluated
                return _lazy.Value;
            }
        }
    }

    public void Refresh()
    {
        lock (_lock)
            _lazy = new Lazy<T>(valueFactory);
    }
}
