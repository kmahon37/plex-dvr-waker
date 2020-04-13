using System;

namespace PlexDvrWaker.Plex.Contracts
{
    internal class ScheduledRecording
    {
        public int SubscriptionId { get; set; }
        public MetadataType SubscriptionMetadataType { get; set; }
        public string RemoteId { get; set; }

        public string ShowTitle { get; set; }

        public int SeasonNumber { get; set; }
        public string SeasonTitle { get; set; }

        public int EpisodeNumber { get; set; }
        public string EpisodeTitle { get; set; }

        public int YearOriginallyAvailable { get; set; }

        public DateTime? StartTime { get; set; }
        public DateTime? StartTimeWithOffset {
            get
            {
                return StartTime.HasValue ? StartTime.Value.AddMinutes(-1 * StartOffsetMinutes) : StartTime;
            }
        }
        public int StartOffsetMinutes { get; set; }

        public DateTime? EndTime { get; set; }
        public DateTime? EndTimeWithOffset {
            get
            {
                return EndTime.HasValue ? EndTime.Value.AddMinutes(EndOffsetMinutes) : EndTime;
            }
        }
        public int EndOffsetMinutes { get; set; }
    }

    internal enum MetadataType
    {
        Movie = 1,
        Show = 2,
        Season = 3,
        Episode = 4,
        Artist = 8,
        Album = 9,
        Song = 10,
        //Video = 12, // ???
        Picture = 13,
        Folder = 14,
        Playlist = 15,
        //iTunes = 16 // ???
        Collection = 18
        //??? = 42
    }
}