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
            // Evict expired cached beatmaps
            _cachedBeatmaps
                .Where(kvp => kvp is { Value.ShouldEvict: true })
                .Select(kvp => kvp.Key)
                .ToList()
                .ForEach(id => _cachedBeatmaps.Remove(id));

            var distinctIds = ids.Distinct(StringComparer.OrdinalIgnoreCase).ToList();

            var cachedBeatmaps = distinctIds
                .FindAll(id =>
                    _cachedBeatmaps.TryGetValue(id, out var cachedBeatmap)
                    && cachedBeatmap is { ShouldRefresh: false }
                )
                .ConvertAll(id => _cachedBeatmaps[id]);

            var cachedIds = cachedBeatmaps
                .Select(beatmap => beatmap.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            return cachedBeatmaps
                .Concat(
                    (
                        distinctIds.Where(id => !cachedIds.Contains(id)).ToList() switch
                        {
                            null or { Count: 0 } => [],

                            { Count: 1 } => SendHttpRequest<Beatmap>(
                                relativePath: $"/maps/id/{distinctIds.Single()}"
                            )
                                is { } singleBeatmap
                                ? [singleBeatmap]
                                : [],

                            not null => distinctIds
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
}
