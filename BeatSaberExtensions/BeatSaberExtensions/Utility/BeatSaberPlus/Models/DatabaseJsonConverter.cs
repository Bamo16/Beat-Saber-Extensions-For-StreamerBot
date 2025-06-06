using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    )
    {
        var root = JObject.Load(reader);
        var internalData = root.ToObject<DatabaseJsonInternal>(serializer);
        var beatmaps = beatSaverClient.GetBeatmaps(internalData.Queue.Select(item => item.Id));
        var queue = internalData.Queue.Select(
            (item, index) => item.ConvertToQueueItem(index, beatmaps, cph)
        );
        var remaps = internalData
            .Remaps.GroupBy(remap => remap.From)
            .Select(group => (From: group.Key, group.First().To));

        return new DatabaseJson(
            queue,
            internalData.History.Select(item => item.Id),
            internalData.Blacklist.Select(item => item.Id),
            internalData.BannedUsers,
            internalData.BannedMappers,
            remaps
        );
    }

    public override void WriteJson(
        JsonWriter writer,
        DatabaseJson value,
        JsonSerializer serializer
    ) => throw new NotImplementedException();

    private class DatabaseJsonInternal
    {
        [JsonProperty("queue")]
        public ReadOnlyCollection<QueueItemInternal> Queue { get; set; }

        [JsonProperty("history")]
        public ReadOnlyCollection<BeatmapInternal> History { get; set; }

        [JsonProperty("blacklist")]
        public ReadOnlyCollection<BeatmapInternal> Blacklist { get; set; }

        [JsonProperty("bannedusers")]
        public ReadOnlyCollection<string> BannedUsers { get; set; }

        [JsonProperty("bannedmappers")]
        public ReadOnlyCollection<string> BannedMappers { get; set; }

        [JsonProperty("remaps")]
        public ReadOnlyCollection<RemapInternal> Remaps { get; set; }
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

    private class BeatmapInternal
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
