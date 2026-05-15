using System;
using System.Collections.Specialized;
using System.Linq;
using BeatSaberExtensions.Utility.Http.BeatLeader.Models;
using BeatSaberExtensions.Utility.Http.BeatSaver.Models;

namespace BeatSaberExtensions.Utility.Http.BeatLeader;

public class BeatLeaderClient(bool logWhenSuccessful = true)
    : BaseHttpClient(new Uri(BeatLeaderBaseUri), logWhenSuccessful)
{
    private const string BeatLeaderBaseUri = "https://api.beatleader.com/";
    private const int MaxPages = 10;

    public Score GetBeatLeaderRecentScore(string beatLeaderId, Beatmap beatmap)
    {
        int? maxPages = null;

        for (var page = 1; maxPages is not { } max || page <= max; page++)
        {
            var response = SendHttpRequest<ScoreResponse>(
                relativePath: $"/player/{beatLeaderId}/scoresstats",
                queryParams: new NameValueCollection
                {
                    ["sortBy"] = "date",
                    ["search"] = beatmap.Metadata.SongName,
                    ["page"] = page.ToString(),
                }
            );

            if (response.Data.FirstOrDefault(beatmap.IsMatchingHash) is { } match)
            {
                return match;
            }

            maxPages ??= Math.Min(
                (int)Math.Ceiling((double)response.Metadata.Total / response.Metadata.ItemsPerPage),
                MaxPages
            );
        }

        return null;
    }
}
