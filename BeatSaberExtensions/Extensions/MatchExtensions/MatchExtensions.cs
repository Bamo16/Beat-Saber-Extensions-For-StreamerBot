using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace BeatSaberExtensions.Extensions.MatchExtensions;

public static class MatchExtensions
{
    public static List<Group> GetNamedCaptureGroups(this Match match) =>
        [
            .. match
                .Groups.Cast<Group>()
                .OrderBy(group => group.Index)
                .Where(group => group is { Success: true, Name: not "0" }),
        ];
}
