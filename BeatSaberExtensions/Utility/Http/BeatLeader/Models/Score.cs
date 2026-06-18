using System;
using BeatSaberExtensions.Enums;

namespace BeatSaberExtensions.Utility.Http.BeatLeader.Models;

public class Score
{
    public int Id { get; set; }
    public LeaderboardResponse Leaderboard { get; set; }
    public double Accuracy { get; set; }
    public string PlayerId { get; set; }
    public double Pp { get; set; }
    public double BonusPp { get; set; }
    public int Rank { get; set; }
    public int CountryRank { get; set; }
    public string Country { get; set; }
    public string Replay { get; set; }
    public string Modifiers { get; set; }
    public int Pauses { get; set; }
    public bool FullCombo { get; set; }
    public int MaxCombo { get; set; }
    public string LeaderboardId { get; set; }
    public long? Timepost { get; set; }

    public DateTime? Timestamp =>
        Timepost is { } longValue
            ? DateTimeOffset.FromUnixTimeSeconds(longValue).UtcDateTime
            : null;

    public string Grade =>
        Accuracy switch
        {
            1 => "SSS",
            >= 0.90 => "SS",
            >= 0.80 => "S",
            >= 0.65 => "A",
            >= 0.50 => "B",
            >= 0.35 => "C",
            >= 0.20 => "D",
            _ => "E",
        };

    public string GetDifficultyShortForm() =>
        Leaderboard.Difficulty.DifficultyName switch
        {
            DifficultyName.Easy => "E",
            DifficultyName.Normal => "N",
            DifficultyName.Hard => "H",
            DifficultyName.Expert => "Ex",
            DifficultyName.ExpertPlus => "E+",
            var difficulty => throw new InvalidOperationException(
                $"Invalid DIfficulty value: \"{difficulty}\"."
            ),
        };

    public string GetFormattedScore() => $"{Grade} ({Accuracy:P2})";
}
