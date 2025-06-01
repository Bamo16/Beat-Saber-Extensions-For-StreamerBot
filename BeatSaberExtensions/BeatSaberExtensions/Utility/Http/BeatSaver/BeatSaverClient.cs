using System;
using System.Collections.Generic;
using System.Linq;
using BeatSaberExtensions.Extensions.EnumerableExtensions;
using BeatSaberExtensions.Utility.Http.BeatSaver.Models;
using BeatSaberExtensions.Utility.Logging;

namespace BeatSaberExtensions.Utility.Http.BeatSaver;

public class BeatSaverClient(StreamerBotLogger logger, bool logWhenSuccessful = false)
    : BaseHttpClient(new Uri(BeatSaverBaseUri), logger, logWhenSuccessful)
{
    private const string BeatSaverBaseUri = "https://api.beatsaver.com/";

    private static readonly object _lock = new();

    private static readonly Dictionary<string, Beatmap> _cachedBeatmaps = new(
        StringComparer.OrdinalIgnoreCase
    );

    public Beatmap GetBeatmap(string id) => GetBeatmaps([id]).Values.FirstOrDefault();

    public Dictionary<string, Beatmap> GetBeatmaps(IEnumerable<string> ids)
    {
        lock (_lock)
        {
            var (cachedBeatmaps, idsToFetch) = GetCachedBeatmapsAndIdsToFetch(ids);

            return cachedBeatmaps
                .Concat(
                    (
                        idsToFetch switch
                        {
                            not { Count: > 0 } => [],

                            { Count: 1 } => SendHttpRequest<Beatmap>(
                                relativePath: $"/maps/id/{idsToFetch.Single()}"
                            )
                                is { } singleBeatmap
                                ? [singleBeatmap]
                                : [],

                            _ => idsToFetch
                                .ChunkBy(50)
                                .Select(chunk =>
                                    $"/maps/ids/{Uri.EscapeDataString(string.Join(",", chunk))}"
                                )
                                .Select(relativePath =>
                                    SendHttpRequest<Dictionary<string, Beatmap>>(
                                        relativePath: relativePath,
                                        defaultValue: []
                                    )
                                )
                                .SelectMany(response => response.Values),
                        }
                    ).Select(beatmap => _cachedBeatmaps[beatmap.Id] = beatmap)
                )
                .ToDictionary(
                    beatmap => beatmap.Id,
                    beatmap => beatmap,
                    StringComparer.OrdinalIgnoreCase
                );
        }
    }

    private (List<Beatmap> CachedBeatmaps, List<string> IdsToFetch) GetCachedBeatmapsAndIdsToFetch(
        IEnumerable<string> ids
    )
    {
        // Evict expired cached beatmaps
        _cachedBeatmaps
            .Where(kvp => kvp is { Value.ShouldEvict: true })
            .Select(kvp => kvp.Key)
            .ToList()
            .ForEach(id => _cachedBeatmaps.Remove(id));

        var cachedBeatmaps = new List<Beatmap>();
        var idsToFetch = new List<string>();

        foreach (var id in ids.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (
                _cachedBeatmaps.TryGetValue(id, out var cachedBeatmap)
                && cachedBeatmap is { ShouldRefresh: false }
            )
                cachedBeatmaps.Add(cachedBeatmap);
            else
                idsToFetch.Add(id.ToLower());
        }

        return (cachedBeatmaps, idsToFetch);
    }
}
