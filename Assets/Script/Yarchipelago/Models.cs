using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using YARG.Core;
using YARG.Core.Song;
using YARG.Gameplay;
using YARG.Song;

namespace YARG.Assets.Script.Yarchipelago
{
    public static class Models
    {
        public abstract class APSongData
        {
            public string SongName;
            public string SongSource;
            public string Artist;
            public long ItemID;
            public string Instrument;
            public SongEntry GetYargSongEntry() => SongContainer.Songs.FirstOrDefault(x => x.Name == SongName && x.Source.Original == SongSource);
            public bool HasReceivedSong() => Events.IsConnected &&
                Events.Session.Items.AllItemsReceived.Any(x => x.ItemId == ItemID);

            public bool UsedMatchingInstrument(GameManager gameManager)
            {
                if (Instrument is null) return true;
                var usedInstruments = new HashSet<string>();
                var harmonyCount = gameManager.Players.Count(x => x.Player.Profile.CurrentInstrument == Core.Instrument.Harmony);
                if (harmonyCount > 1) usedInstruments.Add("harmony2");
                if (harmonyCount > 2) usedInstruments.Add("harmony3");
                foreach (var player in gameManager.Players)
                    if (YargInstrumentToAPKey.TryGetValue(player.Player.Profile.CurrentInstrument, out var inst))
                        usedInstruments.Add(inst);
                return usedInstruments.Contains(Instrument);
            }

            public bool HasReceiveInstrumentItem()
            {
                if (!Events.IsConnected) return false;
                if (Instrument is null) return true;
                if (!APInstrumentKeyToName.TryGetValue(Instrument, out var inst)) { return false; }
                return Events.AllReceivedInstruments.Contains(inst);
            }

            public bool MatchesSongEntry(SongEntry entry) => SongName == entry.Name && SongSource == entry.Source.Original && Artist == entry.Artist;

            public abstract bool CanCompleteLocation();

            public abstract bool VisibleInSongList();

            public bool MetPlayRequirements(GameManager gameManager)
            {
                if (!UsedMatchingInstrument(gameManager))
                    return false;
                return true;
            }
        }

        public class APGoalSong : APSongData
        {
            public APGoalSong(int GoalItems, SongMetadata meta)
            {
                SongName = meta.Name;
                SongSource = meta.Source;
                Artist = meta.Artist;
                ItemID = meta.ItemId;
                GoalItemNeeded = GoalItems;
                Instrument = meta.Instrument;
            }
            public int GoalItemCount { get; private set; } = 0;
            public int GoalItemNeeded { get; }
            public bool HasEnoughYargGems() => GoalItemCount >= GoalItemNeeded;

            public void UpdateGoalItems() =>
                GoalItemCount = Events.IsConnected
                    ? Events.Session.Items.AllItemsReceived.Count(x => x.ItemId == (long) APFiller.YargGem)
                    : 0;

            public override bool CanCompleteLocation() => HasReceivedSong() && HasReceiveInstrumentItem() && HasEnoughYargGems();

            public override bool VisibleInSongList() =>
                Events.GoalDisplaySetting == GoalDisplaySetting.FULL ||
                (HasReceivedSong() && Events.GoalDisplaySetting == GoalDisplaySetting.SONG) ||
                (HasEnoughYargGems() && Events.GoalDisplaySetting == GoalDisplaySetting.GEMS) ||
                (HasReceivedSong() && HasEnoughYargGems() && Events.GoalDisplaySetting == GoalDisplaySetting.BOTH);
        }
        public class APSongLocation : APSongData
        {
            public APSongLocation(SongMetadata meta)
            {
                SongName = meta.Name;
                SongSource = meta.Source;
                Artist = meta.Artist;
                ItemID = meta.ItemId;
                LocationID1 = meta.Loc1Id;
                LocationID2 = meta.Loc2Id;
                LocationID3 = meta.Loc3Id;
                Instrument = meta.Instrument;
            }
            public long LocationID1;
            public long LocationID2;
            public long LocationID3;
            public bool AllLocationsChecked() => Events.IsConnected &&
                Events.Session.Locations.AllLocationsChecked.Contains(LocationID1) &&
                Events.Session.Locations.AllLocationsChecked.Contains(LocationID2) &&
                Events.Session.Locations.AllLocationsChecked.Contains(LocationID3);
            public override bool VisibleInSongList() => Events.IsConnected && HasReceivedSong() && !AllLocationsChecked();
            public override bool CanCompleteLocation() => Events.IsConnected && HasReceivedSong() && HasReceiveInstrumentItem() && !AllLocationsChecked();
        }

        public class ConnectionCache
        {
            public string IP;
            public int Port;
            public string SlotName;
            public string Password;
            public string ConnectionSuffix;
        }

        public static List<DeathLinkMessage> DeathLinkMessages = new()
        {
            new("got boo'd offstage."),
            new("tripped over a cable."),
            new("failed a stage dive."),
            new("dropped their pick.", new(){Instrument.FiveFretGuitar, Instrument.FiveFretBass, Instrument.FiveFretRhythm, Instrument.FiveFretCoopGuitar}),
            new("dropped their last drum sticks.", new(){Instrument.EliteDrums, Instrument.FourLaneDrums, Instrument.ProDrums, Instrument.FiveLaneDrums}),
            new("dropped the mic.", new(){Instrument.Vocals, Instrument.Harmony}),
            new("knocked over the keyboard.", new(){Instrument.Keys, Instrument.ProKeys}),
        };
        public class DeathLinkMessage
        {
            public DeathLinkMessage(string message, HashSet<Instrument> instruments = null)
            {
                Message = message;
                InstrumentTags = instruments ?? new HashSet<Instrument>();
            }
            public string Message;
            public HashSet<Instrument> InstrumentTags = new();
            public override string ToString() => Message;
            public bool Valid(Instrument instrument) => InstrumentTags.Count == 0 || InstrumentTags.Contains(instrument);
        }

        public static string GetDescription(this Enum value)
        {
            var field = value.GetType().GetField(value.ToString());
            var attribute = (DescriptionAttribute) Attribute.GetCustomAttribute(field, typeof(DescriptionAttribute));
            return attribute?.Description ?? value.ToString();
        }

        public enum APFiller
        {
            YargGem = 1,
            StarPower = 2
        }
        public static DeathLinkType[] DeathLinkValues = Enum.GetValues(typeof(DeathLinkType)).Cast<DeathLinkType>().ToArray();
        public enum DeathLinkType
        {
            [Description("Disabled")]
            DISABLED = 0,
            [Description("One Hit")]
            ONE_HIT = 1,
            [Description("Instant")]
            INSTANT = 2
        }
        public static EnergyLinkType[] EnergyLinkValues = Enum.GetValues(typeof(EnergyLinkType)).Cast<EnergyLinkType>().ToArray();
        public enum EnergyLinkType
        {
            [Description("Disabled")]
            DISABLED = 0,
            [Description("Enabled")]
            ENABLED = 1
        }

        public enum GoalDisplaySetting
        {
            FULL = 0,
            SONG = 1,
            GEMS = 2,
            BOTH = 3
        }
        public class SongMetadata
        {
            public string Name { get; set; }
            public long Loc1Id { get; set; }
            public long Loc2Id { get; set; }
            public long Loc3Id { get; set; }
            public long ItemId { get; set; }
            public string Source { get; set; }
            public string Artist { get; set; }
            public string Instrument { get; set; }

            public static SongMetadata FromArray(JArray array)
            {
                var result = new SongMetadata
                {
                    Name = array[0].ToObject<string>(),
                    Loc1Id = array[1].ToObject<long>(),
                    Source = array[2].ToObject<string>(),
                    Artist = array[3].ToObject<string>()
                };
                result.Loc2Id = result.Loc1Id + 1;
                result.Loc3Id = result.Loc1Id + 2;
                result.ItemId = (result.Loc3Id / 3) + 11; // 11 is how many static items there are, I hate to magic number this but not really an easier way to get it.

                if (array.Count > 4)
                    result.Instrument = array[4].ToObject<string>();
                else
                    result.Instrument = null;

                return result;
            }
        }

        public class YargSlotData
        {
            public string GoalSong { get; set; }
            public string GoalSongSource { get; set; }
            public string GoalSongArtist { get; set; }
            public Dictionary<string, SongMetadata> Songlist { get; set; }
            public int GemsRequired { get; set; }
            public int GoalSongVisibility { get; set; }
            public int DeathLink { get; set; }
            public int EnergyLink { get; set; }
            public int InstrumentShuffle { get; set; }

            public static YargSlotData Parse(Dictionary<string, object> slotData)
            {
                var result = new YargSlotData
                {
                    GoalSong = slotData["Goal Song"].ToString(),
                    GoalSongSource = slotData["Goal Song Source"].ToString(),
                    GoalSongArtist = slotData["Goal Song Artist"].ToString(),
                    GemsRequired = Convert.ToInt32(slotData["Gems Required"]),
                    GoalSongVisibility = Convert.ToInt32(slotData["Goal Song Visibility"]),
                    DeathLink = Convert.ToInt32(slotData["Death Link"]),
                    EnergyLink = Convert.ToInt32(slotData["Energy Link"]),
                    InstrumentShuffle = Convert.ToInt32(slotData["Instrument Shuffle"]),
                    Songlist = new Dictionary<string, SongMetadata>()
                };

                var songlistJson = slotData["songlist"] as JObject;
                foreach (var song in songlistJson)
                    result.Songlist[song.Key] = SongMetadata.FromArray(song.Value as JArray);

                return result;
            }
        }
        public static readonly Dictionary<Instrument, string> YargInstrumentToAPKey = new Dictionary<Instrument, string>
        {
            { Instrument.FiveFretGuitar, "guitar5F" },
            { Instrument.FiveFretBass, "bass5F" },
            { Instrument.FiveFretRhythm, "rhythm5F" },
            { Instrument.FiveFretCoopGuitar, "coop5F" },

            { Instrument.SixFretGuitar, "guitar6F" },
            { Instrument.SixFretBass, "bass6F" },
            { Instrument.SixFretRhythm, "rhythm6F" },
            { Instrument.SixFretCoopGuitar, "coop6F" },

            { Instrument.FourLaneDrums, "drums" },
            { Instrument.ProDrums, "drums" },
            { Instrument.FiveLaneDrums, "drums" },
            { Instrument.EliteDrums, "drumsElite" },

            { Instrument.Keys, "keys5F" },
            { Instrument.ProKeys, "keysPro" },

            { Instrument.Vocals, "vocals" },
            //{ Instrument.Harmony, "harmony2" }
        };

        public static Dictionary<string, string> APInstrumentKeyToName = new Dictionary<string, string>
        {
            { "guitar5F", "Guitar" },
            { "bass5F", "Bass" },
            { "rhythm5F", "Rhythm" },
            { "coop5F", "Co-op"},
            { "guitar6F", "6 Fret Guitar" },
            { "bass6F", "6 Fret Bass" },
            { "rhythm6F", "6 Fret Rhythm" },
            { "coop6F", "6 Fret Co-op"},
            { "drums", "Drums" },
            { "drumsElite", "Elite Drums" },
            { "keys5F", "Keys" },
            { "keysPro", "Pro Keys" },
            { "vocals", "Vocals" },
            { "harmony2", "2 Part Harmony" },
            { "harmony3", "3 Part Harmony" }
        };
    }
}
