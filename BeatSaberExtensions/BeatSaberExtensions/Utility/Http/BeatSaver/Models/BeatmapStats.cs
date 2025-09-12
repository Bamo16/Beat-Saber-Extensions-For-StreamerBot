namespace BeatSaberExtensions.Utility.Http.BeatSaver.Models;

public class BeatmapStats
{
    public int Plays { get; private set; }
    public int Downloads { get; private set; }
    public int Upvotes { get; private set; }
    public int Downvotes { get; private set; }
    public double Score { get; private set; }
}
