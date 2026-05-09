using System;
using System.Collections.Generic;
using System.Linq;
using BeatSaberExtensions.Extensions.EnumerableExtensions;
using BeatSaberExtensions.Utility.Http.BeatSaver.Models;

namespace BeatSaberExtensions.Utility.Http.BeatSaver;

#nullable enable

public class BeatSaverClient(bool logWhenSuccessful = false)
    : BaseHttpClient(new Uri(BeatSaverBaseUri), logWhenSuccessful)
{
    private const string BeatSaverBaseUri = "https://api.beatsaver.com/";

    private readonly object _lock = new object();
    private readonly Dictionary<string, Beatmap> _cachedBeatmaps = new Dictionary<string, Beatmap>(
        StringComparer.OrdinalIgnoreCase
    );

    public Beatmap? GetBeatmap(string id) => GetBeatmaps([id]).Values.FirstOrDefault();

    public Dictionary<string, Beatmap> GetBeatmaps(IEnumerable<string> ids)
    {
        lock (_lock)
        {
            var distinctIds = ids.Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(id => id.ToLower())
                .ToList();

            var idsToFetch = distinctIds
                .Where(id =>
                    !_cachedBeatmaps.TryGetValue(id, out var cached) || cached.ShouldRefresh
                )
                .ToList();

            FetchBeatmaps(idsToFetch);

            // Cache entries are kept indefinitely. A failed refresh leaves the
            // existing entry intact; stale info is strictly better than nothing.
            return distinctIds
                .Where(_cachedBeatmaps.ContainsKey)
                .ToDictionary(
                    id => id,
                    id => _cachedBeatmaps[id],
                    StringComparer.OrdinalIgnoreCase
                );
        }
    }

    private void FetchBeatmaps(List<string> idsToFetch)
    {
        if (idsToFetch is not { Count: > 0 })
        {
            return;
        }

        var beatmaps = idsToFetch is { Count: 1 }
            ? SendHttpRequest<Beatmap>(
                relativePath: $"/maps/id/{idsToFetch.Single()}",
                defaultValue: null
            )
                is { Id: not null } singleBeatmap
                ? [singleBeatmap]
                : []
            : GetMultiBeatmapRelativePath(idsToFetch)
                .Select(relativePath =>
                    SendHttpRequest<Dictionary<string, Beatmap>>(
                        relativePath: relativePath,
                        defaultValue: []
                    )
                )
                .OfType<Dictionary<string, Beatmap>>()
                .SelectMany(response =>
                    response.Values.Where(beatmap => beatmap is { Id: not null })
                );

        foreach (var beatmap in beatmaps)
        {
            _cachedBeatmaps[beatmap.Id] = beatmap;
        }
    }

    private static IEnumerable<string> GetMultiBeatmapRelativePath(IEnumerable<string> ids) =>
        ids.ChunkBy(50).Select(chunk => $"/maps/ids/{string.Join(",", chunk)}");
}
