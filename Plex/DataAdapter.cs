using PlexDvrWaker.Common;
using PlexDvrWaker.Plex.Contracts;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Data.SQLite;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace PlexDvrWaker.Plex
{
    /// <summary>
    /// Class for reading the Plex library databases
    /// </summary>
    internal class DataAdapter
    {
        private readonly string _libraryDatabaseFileName;
        private readonly Dictionary<string, ScheduledRecording> _scheduledRecordings = new Dictionary<string, ScheduledRecording>();
        private readonly object _scheduledRecordingsLock = new object();

        public DataAdapter()
        {
            _libraryDatabaseFileName = Settings.LibraryDatabaseFileName;
        }

        public ReadOnlyCollection<ScheduledRecording> GetScheduledRecordings()
        {
            Logger.LogInformation("Getting scheduled recordings");

            lock (_scheduledRecordingsLock)
            {
                _scheduledRecordings.Clear();

                LoadSubscriptions();

                if (_scheduledRecordings.Any())
                {
                    LoadEpgInfo();
                    RemoveUnschedulableItems();
                    RemoveExistingTvShows();
                    RemoveExistingMovies();
                }

                var count = _scheduledRecordings.Count();
                var msg = $"Found {count} upcoming scheduled recordings";
                if (count > 0)
                {
                    var nextRecTime = GetNextScheduledRecording_Internal(_scheduledRecordings.Values).StartTimeWithOffset;
                    msg += $" starting at {nextRecTime}";
                }
                Logger.LogInformation(msg);

                return new ReadOnlyCollection<ScheduledRecording>(_scheduledRecordings.Values.ToArray());
            }
        }

        public ScheduledRecording GetNextScheduledRecording()
        {
            var recs = GetScheduledRecordings();
            return GetNextScheduledRecording_Internal(recs);
        }

        private ScheduledRecording GetNextScheduledRecording_Internal(IEnumerable<ScheduledRecording> scheduledRecordings)
        {
            return scheduledRecordings
                .Where(r => r.StartTimeWithOffset >= DateTime.Now)
                .OrderBy(r => r.StartTimeWithOffset)
                .FirstOrDefault();
        }

        public DateTime? GetNextScheduledRecordingTime()
        {
            var nextRec = GetNextScheduledRecording();
            return nextRec?.StartTimeWithOffset;
        }

        public ScheduledMaintenance GetNextScheduledMaintenance()
        {
            Logger.LogInformation("Getting next Plex maintenance time");

            var scheduledMaintenance = new ScheduledMaintenance
            {
                StartHour = Settings.ButlerStartHour,
                EndHour = Settings.ButlerEndHour
            };

            Logger.LogInformation($"Plex maintenance is {scheduledMaintenance.StartHourString} to {scheduledMaintenance.EndHourString} every day");
            Logger.LogInformation($"Next scheduled maintenance time is {scheduledMaintenance.StartTime} to {scheduledMaintenance.EndTime}");

            return scheduledMaintenance;
        }

        public DateTime GetNextScheduledMaintenanceTime()
        {
            var scheduledMaintenance = GetNextScheduledMaintenance();
            return scheduledMaintenance.StartTime;
        }

        public DateTime GetNextWakeupTime()
        {
            var nextRecordingTime = GetNextScheduledRecordingTime();
            var nextMaintenanceTime = GetNextScheduledMaintenanceTime();
            DateTime wakeupTime;

            if (nextRecordingTime.HasValue && DateTime.Compare(nextRecordingTime.Value, nextMaintenanceTime) < 0)
            {
                wakeupTime = nextRecordingTime.Value;
            }
            else
            {
                wakeupTime = nextMaintenanceTime;
            }

            return wakeupTime;
        }

        public void PrintScheduledRecordings()
        {
            var recs = GetScheduledRecordings();
            if (recs.Any())
            {
                var startDateColLength = recs.Max(r => r.StartTimeWithOffset.ToString().Length);
                var endDateColLength = recs.Max(r => r.EndTimeWithOffset.ToString().Length);
                string getDateColHeader(string headerName, int dateColLength)
                {
                    return headerName + new string(' ', dateColLength - headerName.Length);
                }
                string getHeaderDivider(int length)
                {
                    return new string('-', length);
                }
                Console.WriteLine($"{getDateColHeader("Start Time", startDateColLength)}\t{getDateColHeader("End Time", endDateColLength)}\tTitle");
                Console.WriteLine($"{getHeaderDivider(startDateColLength)}\t{getHeaderDivider(endDateColLength)}\t{getHeaderDivider(50)}");

                foreach (var rec in recs.OrderBy(r => r.StartTimeWithOffset))
                {
                    var parts = new[]
                    {
                        rec.ShowTitle,
                        rec.SeasonTitle,
                        (rec.SubscriptionMetadataType == MetadataType.Episode || rec.SubscriptionMetadataType == MetadataType.Show) && rec.EpisodeNumber > 0
                            ? rec.SeasonNumber.ToString("'S'00") + rec.EpisodeNumber.ToString("'E'00")
                            : string.Empty,
                        rec.EpisodeTitle
                    };
                    var title = string.Join(" - ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));

                    Console.WriteLine($"{rec.StartTimeWithOffset}\t{rec.EndTimeWithOffset}\t{title}");
                }
            }
            else
            {
                Console.WriteLine("No upcoming scheduled recordings.");
            }

            Console.WriteLine();
        }

        public void PrintNextMaintenanceTime()
        {
            var scheduledMaintenance = GetNextScheduledMaintenance();

            Console.WriteLine($"Plex maintenance is {scheduledMaintenance.StartHourString} to {scheduledMaintenance.EndHourString} every day");
            Console.WriteLine($"Next scheduled maintenance time is {scheduledMaintenance.StartTime} to {scheduledMaintenance.EndTime}");
            Console.WriteLine();
        }

        private void LoadSubscriptions()
        {
            Logger.LogInformation("  Loading subscriptions");

            // Get scheduled show/episode ids from library
            var sql = new StringBuilder()
                .AppendLine("select distinct")
                .AppendLine("  media_subscriptions.id as sub_id,")
                .AppendLine("  media_subscriptions.metadata_type,")
                .AppendLine($"  coalesce((case media_subscriptions.metadata_type when {(int)MetadataType.Show} then metadata_items.title when {(int)MetadataType.Episode} then null end), '') as show_title,")
                .AppendLine($"  coalesce((case media_subscriptions.metadata_type when {(int)MetadataType.Show} then null when {(int)MetadataType.Episode} then metadata_items.title end), '') as episode_title,")
                .AppendLine("  metadata_subscription_desired_items.remote_id,")
                .AppendLine("  media_subscriptions.extra_data")
                .AppendLine("from media_subscriptions")
                .AppendLine("inner join metadata_subscription_desired_items on metadata_subscription_desired_items.sub_id = media_subscriptions.id")
                .AppendLine("left join metadata_items on metadata_items.id = media_subscriptions.target_metadata_item_id")
                .AppendLine($"where media_subscriptions.metadata_type in ({(int)MetadataType.Movie}, {(int)MetadataType.Show}, {(int)MetadataType.Episode})");

            using (var conn = new SQLiteConnection($"Data Source={_libraryDatabaseFileName};Version=3;Read Only=True;"))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = sql.ToString();
                    var reader = cmd.ExecuteReader();

                    while (reader.Read())
                    {
                        var remoteId = Uri.UnescapeDataString(reader.GetString(4));

                        if (!_scheduledRecordings.ContainsKey(remoteId))
                        {
                            var startOffsetMinutes = 0;
                            var endOffsetMinutes = 0;

                            // Parse "extra_data" field for some advanced recording info.  We need to be
                            // careful of unexpected data structures, so carefully grab what we need from it.
                            var extraDataString = !reader.IsDBNull(5) ? reader.GetString(5) : string.Empty;
                            if (!string.IsNullOrWhiteSpace(extraDataString))
                            {
                                var extraDataArray = Uri.UnescapeDataString(extraDataString).Split('&');
                                if (extraDataArray.Any())
                                {
                                    bool tryGetExtraDataValue(string key, out string value)
                                    {
                                        var arrayValue = extraDataArray.FirstOrDefault(s => s.StartsWith(key + "=", true, CultureInfo.InvariantCulture));
                                        if (!string.IsNullOrWhiteSpace(arrayValue))
                                        {
                                            var parts = arrayValue.Split('=', 2);
                                            if (parts.Length == 2 && parts.All(s => !string.IsNullOrWhiteSpace(s)))
                                            {
                                                value = parts[1];
                                                return true;
                                            }
                                        }

                                        value = null;
                                        return false;
                                    }

                                    if (tryGetExtraDataValue("pr:startOffsetMinutes", out string startOffsetMinutesString))
                                    {
                                        int.TryParse(startOffsetMinutesString, out startOffsetMinutes);
                                    }

                                    if (tryGetExtraDataValue("pr:endOffsetMinutes", out string endOffsetMinutesString))
                                    {
                                        int.TryParse(endOffsetMinutesString, out endOffsetMinutes);
                                    }
                                }
                            }

                            var rec = new ScheduledRecording()
                            {
                                SubscriptionId = reader.GetInt32(0),
                                SubscriptionMetadataType = (MetadataType)Enum.Parse(typeof(MetadataType), reader.GetInt32(1).ToString()),
                                ShowTitle = !reader.IsDBNull(2) ? reader.GetString(2) : string.Empty,
                                EpisodeTitle = !reader.IsDBNull(3) ? reader.GetString(3) : string.Empty,
                                RemoteId = remoteId,
                                StartOffsetMinutes = startOffsetMinutes,
                                EndOffsetMinutes = endOffsetMinutes
                            };

                            _scheduledRecordings.Add(remoteId, rec);
                        }
                    }
                }
            }
        }

        private void LoadEpgInfo()
        {
            Logger.LogInformation("  Loading EPG info for subscriptions");

            if (_scheduledRecordings.Any())
            {
                // Get EPG database file names since it appears like there could be multiple
                var databaseFilePath = Path.GetDirectoryName(_libraryDatabaseFileName);
                var tvEpgDatabaseFileNames = new List<string>();
                var sqlEpgProviders = new StringBuilder()
                    .AppendLine("select")
                    .AppendLine("  epg.identifier,")
                    .AppendLine("  dvr.uuid")
                    .AppendLine("from media_provider_resources as epg")
                    .AppendLine("inner join media_provider_resources as dvr on dvr.id = epg.parent_id")
                    .AppendLine("where epg.identifier like 'tv.plex.providers.epg.%'");

                using (var conn = new SQLiteConnection($"Data Source={_libraryDatabaseFileName};Version=3;Read Only=True;"))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = sqlEpgProviders.ToString();
                        var reader = cmd.ExecuteReader();

                        while (reader.Read())
                        {
                            var identifier = reader.GetString(0);
                            var uuid = reader.GetString(1);
                            var tvEpgDatabaseFileName = Path.Combine(databaseFilePath, $"{identifier}-{uuid}.db");
                            tvEpgDatabaseFileNames.Add(tvEpgDatabaseFileName);
                        }
                    }
                }

                // Get scheduled show start times from EPG
                var sql = new StringBuilder()
                    .AppendLine("drop table if exists temp.remote_ids;")
                    .AppendLine("create temp table 'remote_ids' ('id' varchar(255) not null primary key);")
                    .AppendLine()
                    .AppendLine("insert into temp.remote_ids (id) values")
                    .AppendLine("  " + string.Join(",", _scheduledRecordings.Keys.Select(id => "(?)")) + ";")
                    .AppendLine()
                    .AppendLine("select")
                    .AppendLine("  episode.guid as remote_id,")
                    .AppendLine("  season.\"index\" as season_number,")
                    .AppendLine("  episode.\"index\" as episode_number,")
                    .AppendLine("  show.title as show_title,")
                    .AppendLine("  season.title as season_title,")
                    .AppendLine("  episode.title as episode_title,")
                    .AppendLine("  min(media_items.begins_at) as begins_at,")
                    .AppendLine("  min(media_items.ends_at) as ends_at,")
                    .AppendLine("  episode.year")
                    .AppendLine("from temp.remote_ids")
                    .AppendLine("inner join metadata_items as episode on episode.guid = temp.remote_ids.id")
                    .AppendLine("left join metadata_items as season on season.id = episode.parent_id")
                    .AppendLine("left join metadata_items as show on show.id = season.parent_id")
                    .AppendLine("inner join media_items on media_items.metadata_item_id = episode.id")
                    .AppendLine($"where episode.metadata_type in ({(int)MetadataType.Movie}, {(int)MetadataType.Episode})")
                    .AppendLine("group by remote_id, season_number, episode_number, show_title, season_title, episode_title, episode.year;")
                    .AppendLine()
                    .AppendLine("drop table if exists temp.remote_ids;");

                foreach (var tvEpgDatabaseFileName in tvEpgDatabaseFileNames)
                {
                    using (var conn = new SQLiteConnection($"Data Source={tvEpgDatabaseFileName};Version=3;Read Only=True;"))
                    {
                        conn.Open();
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = sql.ToString();
                            cmd.Parameters.AddRange(
                                _scheduledRecordings.Keys
                                    .Select(id => new SQLiteParameter(DbType.String, 255) { Value = id })
                                    .ToArray()
                            );
                            var reader = cmd.ExecuteReader();

                            while (reader.Read())
                            {
                                var remoteId = reader.GetString(0);

                                if (_scheduledRecordings.TryGetValue(remoteId, out var rec))
                                {
                                    rec.SeasonNumber = !reader.IsDBNull(1) ? reader.GetInt32(1) : default;
                                    rec.EpisodeNumber = !reader.IsDBNull(2) ? reader.GetInt32(2) : default;

                                    if (string.IsNullOrWhiteSpace(rec.ShowTitle))
                                    {
                                        rec.ShowTitle = !reader.IsDBNull(3) ? reader.GetString(3) : string.Empty;
                                    }

                                    if (string.IsNullOrWhiteSpace(rec.SeasonTitle))
                                    {
                                        rec.SeasonTitle = !reader.IsDBNull(4) ? reader.GetString(4) : string.Empty;
                                    }

                                    if (string.IsNullOrWhiteSpace(rec.EpisodeTitle))
                                    {
                                        rec.EpisodeTitle = !reader.IsDBNull(5) ? reader.GetString(5) : string.Empty;
                                    }

                                    rec.StartTime = reader.GetDateTime(6).ToLocalTime();
                                    rec.EndTime = reader.GetDateTime(7).ToLocalTime();
                                    rec.YearOriginallyAvailable = !reader.IsDBNull(8) ? reader.GetInt32(8) : default;

                                    // Clean up some bad Epg data
                                    // Indicates Epg may not have full information for some reason
                                    if (rec.SeasonNumber >= 1900)
                                    {
                                        rec.SeasonNumber = 0;
                                        rec.SeasonTitle = string.Empty;
                                    }
                                    if (rec.EpisodeNumber < 0)
                                    {
                                        rec.EpisodeNumber = 0;
                                        rec.EpisodeTitle = string.Empty;
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }

        private void RemoveUnschedulableItems()
        {
            Logger.LogInformation("  Removing unschedulable items");

            if (_scheduledRecordings.Any())
            {
                // Remove items that don't have a start time
                var idsToRemove = _scheduledRecordings.Values
                    .Where(rec => !rec.StartTime.HasValue)
                    .Select(rec => rec.RemoteId);

                RemoveScheduledRecordings(idsToRemove);
            }
        }

        private void RemoveExistingTvShows()
        {
            Logger.LogInformation("  Removing previously recorded TV shows");

            if (_scheduledRecordings.Any())
            {
                const string SUBSCRIPTION_ID_PARAM = "@subscriptionId";
                const string SEASON_NUMBER_PARAM = "@seasonNumber";
                const string EPISODE_NUMBER_PARAM = "@episodeNumber";

                var sqlShow = new StringBuilder()
                    .AppendLine("select 1")
                    .AppendLine("from media_subscriptions")
                    .AppendLine("inner join metadata_items as seasons on seasons.parent_id = media_subscriptions.target_metadata_item_id")
                    .AppendLine("inner join metadata_items as episodes on episodes.parent_id = seasons.id")
                    .AppendLine($"where media_subscriptions.id = {SUBSCRIPTION_ID_PARAM}")
                    .AppendLine($"and media_subscriptions.metadata_type = {(int)MetadataType.Show}")
                    .AppendLine($"and seasons.\"index\" = {SEASON_NUMBER_PARAM}")
                    .AppendLine($"and episodes.\"index\" = {EPISODE_NUMBER_PARAM}");

                var sqlEpisode = new StringBuilder()
                    .AppendLine("select 1")
                    .AppendLine("from media_subscriptions")
                    .AppendLine("inner join metadata_items as episodes on episodes.id = media_subscriptions.target_metadata_item_id")
                    .AppendLine("inner join metadata_items as seasons on seasons.id = episodes.parent_id")
                    .AppendLine($"where media_subscriptions.id = {SUBSCRIPTION_ID_PARAM}")
                    .AppendLine($"and media_subscriptions.metadata_type = {(int)MetadataType.Episode}")
                    .AppendLine($"and seasons.\"index\" = {SEASON_NUMBER_PARAM}")
                    .AppendLine($"and episodes.\"index\" = {EPISODE_NUMBER_PARAM}");

                var sqlParams = new[]
                {
                    new SQLiteParameter(SUBSCRIPTION_ID_PARAM, DbType.Int32),
                    new SQLiteParameter(SEASON_NUMBER_PARAM, DbType.Int32),
                    new SQLiteParameter(EPISODE_NUMBER_PARAM, DbType.Int32),
                };

                var idsToRemove = new List<string>();

                using (var conn = new SQLiteConnection($"Data Source={_libraryDatabaseFileName};Version=3;Read Only=True;"))
                {
                    conn.Open();
                    using (var cmdShow = conn.CreateCommand())
                    using (var cmdEpisode = conn.CreateCommand())
                    {
                        cmdShow.CommandText = sqlShow.ToString();
                        cmdShow.Parameters.AddRange(sqlParams);

                        cmdEpisode.CommandText = sqlEpisode.ToString();
                        cmdEpisode.Parameters.AddRange(sqlParams);

                        foreach (var rec in _scheduledRecordings.Values)
                        {
                            Object result;

                            switch (rec.SubscriptionMetadataType)
                            {
                                case MetadataType.Show:
                                    cmdShow.Parameters[SUBSCRIPTION_ID_PARAM].Value = rec.SubscriptionId;
                                    cmdShow.Parameters[SEASON_NUMBER_PARAM].Value = rec.SeasonNumber;
                                    cmdShow.Parameters[EPISODE_NUMBER_PARAM].Value = rec.EpisodeNumber;
                                    result = cmdShow.ExecuteScalar();
                                    break;

                                case MetadataType.Episode:
                                    cmdEpisode.Parameters[SUBSCRIPTION_ID_PARAM].Value = rec.SubscriptionId;
                                    cmdEpisode.Parameters[SEASON_NUMBER_PARAM].Value = rec.SeasonNumber;
                                    cmdEpisode.Parameters[EPISODE_NUMBER_PARAM].Value = rec.EpisodeNumber;
                                    result = cmdEpisode.ExecuteScalar();
                                    break;

                                default:
                                    // Skip this record
                                    continue;
                            }

                            if (result != null)
                            {
                                // Episode already exists in library, so lets remove it from recording schedule
                                idsToRemove.Add(rec.RemoteId);
                            }
                        }
                    }
                }

                RemoveScheduledRecordings(idsToRemove);
            }
        }

        private void RemoveExistingMovies()
        {
            Logger.LogInformation("  Removing previously recorded movies");

            if (_scheduledRecordings.Any())
            {
                const string YEAR_PARAM = "@year";
                const string TITLE_PARAM = "@title";

                var sql = new StringBuilder()
                    .AppendLine("select 1")
                    .AppendLine("from metadata_items as movies")
                    .AppendLine($"where movies.metadata_type = {(int)MetadataType.Movie}")
                    .AppendLine($"and movies.year = {YEAR_PARAM}")
                    .AppendLine($"and movies.title = {TITLE_PARAM}");

                var sqlParams = new[]
                {
                    new SQLiteParameter(YEAR_PARAM, DbType.Int32),
                    new SQLiteParameter(TITLE_PARAM, DbType.String, 255)
                };

                var idsToRemove = new List<string>();

                using (var conn = new SQLiteConnection($"Data Source={_libraryDatabaseFileName};Version=3;Read Only=True;"))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = sql.ToString();
                        cmd.Parameters.AddRange(sqlParams);

                        foreach (var rec in _scheduledRecordings.Values)
                        {
                            cmd.Parameters[YEAR_PARAM].Value = rec.YearOriginallyAvailable;
                            cmd.Parameters[TITLE_PARAM].Value = rec.EpisodeTitle;
                            var result = cmd.ExecuteScalar();

                            if (result != null)
                            {
                                // Movie already exists in library, so lets remove it from recording schedule
                                idsToRemove.Add(rec.RemoteId);
                            }
                        }
                    }
                }

                RemoveScheduledRecordings(idsToRemove);
            }
        }

        private void RemoveScheduledRecordings(IEnumerable<string> idsToRemove)
        {
            if (idsToRemove != null && idsToRemove.Any())
            {
                foreach (var id in idsToRemove.ToArray())
                {
                    if (_scheduledRecordings.ContainsKey(id))
                    {
                        _scheduledRecordings.Remove(id);
                    }
                }
            }
        }

    }
}