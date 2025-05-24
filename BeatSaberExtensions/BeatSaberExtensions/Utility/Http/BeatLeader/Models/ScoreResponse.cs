using System.Collections.Generic;

namespace BeatSaberExtensions.Utility.Http.BeatLeader.Models;

public class ScoreResponse
{
    public Metadata Metadata { get; set; }
    public ICollection<Score> Data { get; set; }
}
