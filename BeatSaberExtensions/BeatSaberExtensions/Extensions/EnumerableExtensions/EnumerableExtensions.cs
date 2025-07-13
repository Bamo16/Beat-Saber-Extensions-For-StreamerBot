using System;
using System.Collections.Generic;
using System.Linq;
using BeatSaberExtensions.Utility;
using BeatSaberExtensions.Utility.BeatSaberPlus.Models;

namespace BeatSaberExtensions.Extensions.EnumerableExtensions;

public static class EnumerableExtensions
{
    public static IEnumerable<IEnumerable<T>> ChunkBy<T>(this IEnumerable<T> source, int size) =>
        source
            .Select((item, index) => (Item: item, Index: index))
            .GroupBy(element => element.Index / size)
            .Select(group => group.Select(element => element.Item));

    public static TimeSpan GetEstimatedWaitTime(
        this IEnumerable<QueueItem> queue,
        QueueItem request
    ) =>
        queue
            .Take(request.Position - 1)
            .Aggregate(
                TimeSpan.Zero,
                (total, current) => total.Add(current.Beatmap.Metadata.Duration)
            );
}
