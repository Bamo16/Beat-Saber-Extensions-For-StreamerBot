using System;
using System.Collections.Generic;
using System.Linq;
using BeatSaberExtensions.Utility.Http.BeatSaver;
using BeatSaberExtensions.Utility.Http.BeatSaver.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Streamer.bot.Plugin.Interface;

namespace BeatSaberExtensions.Utility.BeatSaberPlus.Models;

public class DatabaseJsonConverter(IInlineInvokeProxy cph, BeatSaverClient beatSaverClient)
    : JsonConverter<DatabaseJson>
{
    public override bool CanWrite => false;

    public JsonSerializer GetSerializer() =>
        JsonSerializer.Create(new JsonSerializerSettings { Converters = [this] });

    public override DatabaseJson ReadJson(
        JsonReader reader,
        Type objectType,
        DatabaseJson existingValue,
        bool hasExistingValue,
        JsonSerializer serializer
    ) => ParseDatabaseJson(JObject.Load(reader), serializer);

    public override void WriteJson(
        JsonWriter writer,
        DatabaseJson value,
        JsonSerializer serializer
    ) => throw new NotImplementedException();

    private DatabaseJson ParseDatabaseJson(JObject root, JsonSerializer serializer)
    {
        var internalData = root.ToObject<DatabaseJsonInternal>(serializer);
        var beatmaps = beatSaverClient.GetBeatmaps(internalData.Queue.Select(item => item.Id));

        return new DatabaseJson
        {
            Queue =
            [
                .. internalData.Queue.Select(
                    (item, index) => item.ConvertToQueueItem(index + 1, beatmaps, cph)
                ),
            ],
            History = internalData
                .History.Select(item => item.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            Blacklist = internalData
                .Blacklist.Select(item => item.Id)
                .ToHashSet(StringComparer.OrdinalIgnoreCase),
            BannedUsers = internalData.BannedUsers.ToHashSet(StringComparer.OrdinalIgnoreCase),
            BannedMappers = internalData.BannedMappers.ToHashSet(StringComparer.OrdinalIgnoreCase),
            Remaps = internalData
                .Remaps.GroupBy(remap => remap.From)
                .ToDictionary(
                    group => group.Key,
                    group => group.First().To.ToLowerInvariant(),
                    StringComparer.OrdinalIgnoreCase
                ),
        };
    }

    private class DatabaseJsonInternal
    {
        [JsonProperty("queue")]
        public List<QueueItemInternal> Queue { get; set; }

        [JsonProperty("history")]
        public List<DatabaseJsonBeatmapInternal> History { get; set; }

        [JsonProperty("blacklist")]
        public List<DatabaseJsonBeatmapInternal> Blacklist { get; set; }

        [JsonProperty("bannedusers")]
        public List<string> BannedUsers { get; set; }

        [JsonProperty("bannedmappers")]
        public List<string> BannedMappers { get; set; }

        [JsonProperty("remaps")]
        public List<RemapInternal> Remaps { get; set; }
    }

    private class QueueItemInternal
    {
        [JsonProperty("key")]
        public string Id { get; private set; }

        [JsonProperty("rqn")]
        public string UserLogin { get; private set; }

        [JsonProperty("msg")]
        public string SongMessage { get; private set; }

        public QueueItem ConvertToQueueItem(
            int index,
            Dictionary<string, Beatmap> beatmaps,
            IInlineInvokeProxy cph
        ) => new(Id, UserLogin, SongMessage, index, beatmaps, cph);
    }

    private class DatabaseJsonBeatmapInternal
    {
        [JsonProperty("key")]
        public string Id { get; set; }
    }

    private class RemapInternal
    {
        [JsonProperty("l")]
        public string From { get; set; }

        [JsonProperty("r")]
        public string To { get; set; }
    }
}
