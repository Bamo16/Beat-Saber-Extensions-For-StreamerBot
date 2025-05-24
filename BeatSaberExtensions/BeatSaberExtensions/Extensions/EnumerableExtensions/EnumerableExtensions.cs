using System;
using System.Collections.Generic;
using System.Linq;

namespace BeatSaberExtensions.Extensions.EnumerableExtensions;

public static class EnumerableExtensions
{
    public static IEnumerable<IEnumerable<T>> ChunkBy<T>(this IEnumerable<T> source, int size) =>
        source
            .Select((item, index) => (Item: item, Index: index))
            .GroupBy(element => element.Index / size)
            .Select(group => group.Select(element => element.Item));

    public static TimeSpan AccumulateDuration<T>(
        this IEnumerable<T> items,
        Func<T, TimeSpan> selector
    ) => items.Select(selector).Aggregate(TimeSpan.Zero, (total, duration) => total.Add(duration));
}
