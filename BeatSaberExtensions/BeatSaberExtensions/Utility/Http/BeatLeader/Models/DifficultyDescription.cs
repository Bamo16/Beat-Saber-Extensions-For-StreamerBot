using BeatSaberExtensions.Enums;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace BeatSaberExtensions.Utility.Http.BeatLeader.Models;

public class DifficultyDescription
{
    public int Id { get; set; }
    public int Value { get; set; }
    public int Mode { get; set; }

    [JsonConverter(typeof(StringEnumConverter))]
    public DifficultyName DifficultyName { get; set; }
    public string ModeName { get; set; }
    public int NominatedTime { get; set; }
    public int QualifiedTime { get; set; }
    public int RankedTime { get; set; }
    public double? Stars { get; set; }
    public int Type { get; set; }
    public double Njs { get; set; }
    public double Nps { get; set; }
    public int Notes { get; set; }
    public int Bombs { get; set; }
    public int Walls { get; set; }
    public int MaxScore { get; set; }
    public double Duration { get; set; }
}
