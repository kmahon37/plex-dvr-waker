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
                LoadSubscriptions();

                if (_scheduledRecordings.Any())
                {
                    LoadEpgInfo();
                    RemoveUnschedulableAndPastItems();
                    RemoveExistingTvShows();
                    RemoveExistingMovies();
                    DetermineScheduleConflicts();
                }

                var count = _scheduledRecordings.Count();
                var msg = $"Found {count} upcoming scheduled recordings";
                if (count > 0)
                {
                    var nextRec = GetNextScheduledRecording(_scheduledRecordings.Values);
                    if (nextRec?.StartTimeWithOffset != null)
                    {
                        msg += $" starting at {nextRec.StartTimeWithOffset}";
                    }
                }
                Logger.LogInformation(msg);

                return new ReadOnlyCollection<ScheduledRecording>(_scheduledRecordings.Values.ToArray());
            }
        }

        public ScheduledRecording GetNextScheduledRecording()
        {
            var recs = GetScheduledRecordings();
            return GetNextScheduledRecording(recs);
        }

        private ScheduledRecording GetNextScheduledRecording(IEnumerable<ScheduledRecording> scheduledRecordings)
        {
            return scheduledRecordings
                .Where(r => !r.HasConflict)
                .OrderBy(r => r.StartTimeWithOffset)
                .FirstOrDefault();
        }

        public DateTime? GetNextScheduledRecordingTime()
        {
            var nextRec = GetNextScheduledRecording();
            return nextRec?.StartTimeWithOffset;
        }

        public ScheduledMaintenance GetNextScheduledMaintenance(bool showMessageToUser)
        {
            Logger.LogInformation("Getting next Plex maintenance time");

            var scheduledMaintenance = new ScheduledMaintenance
            {
                StartHour = Settings.ButlerStartHour,
                EndHour = Settings.ButlerEndHour
            };

            Logger.LogInformation($"Plex maintenance is {scheduledMaintenance.StartHourString} to {scheduledMaintenance.EndHourString} every day", showMessageToUser);
            Logger.LogInformation($"Next scheduled maintenance time is {FormatDateTime(scheduledMaintenance.StartTime)} to {FormatDateTime(scheduledMaintenance.EndTime)}", showMessageToUser);

            return scheduledMaintenance;
        }

        public DateTime GetNextScheduledMaintenanceTime()
        {
            var scheduledMaintenance = GetNextScheduledMaintenance(false);
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
                var startDateColLength = recs.Max(r => FormatDateTime(r.StartTimeWithOffset).Length);
                var endDateColLength = recs.Max(r => FormatDateTime(r.EndTimeWithOffset).Length);
                var hasConflicts = recs.Any(r => r.HasConflict);

                static string getDateColHeader(string headerName, int dateColLength)
                {
                    return headerName + new string(' ', dateColLength - headerName.Length);
                }

                static string getHeaderDivider(int length)
                {
                    return new string('-', length);
                }

                Console.WriteLine($"{getDateColHeader("Start Time", startDateColLength)}  {getDateColHeader("End Time", endDateColLength)}  Title  {(hasConflicts ? "(▲ = conflict)" : "")}");
                Console.WriteLine($"{getHeaderDivider(startDateColLength)}  {getHeaderDivider(endDateColLength)}  {getHeaderDivider(50)}");

                foreach (var rec in recs.OrderBy(r => r.StartTimeWithOffset))
                {
                    Console.WriteLine($"{FormatDateTime(rec.StartTimeWithOffset).PadRight(startDateColLength)}  {FormatDateTime(rec.EndTimeWithOffset).PadRight(endDateColLength)}  {FormatRecordingTitle(rec)}");
                }
            }
            else
            {
                Console.WriteLine("No upcoming scheduled recordings.");
            }
        }

        public void PrintNextMaintenanceTime()
        {
            GetNextScheduledMaintenance(true);
        }

        private string FormatRecordingTitle(ScheduledRecording rec)
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
            var title = (rec.HasConflict ? "▲ " : "") + string.Join(" - ", parts.Where(p => !string.IsNullOrWhiteSpace(p)));

            return title;
        }

        private string FormatDateTime(DateTime? date)
        {
            return date.HasValue ? date.Value.ToString("g", DateTimeFormatInfo.CurrentInfo) : string.Empty;
        }

        private void LoadSubscriptions()
        {
            Logger.LogInformation("  Loading subscriptions");

            _scheduledRecordings.Clear();

            // Get scheduled show/episode ids from library
            var sql = new StringBuilder()
                .AppendLine("select distinct")
                .AppendLine("  media_subscriptions.id as sub_id,")
                .AppendLine("  media_subscriptions.metadata_type,")
                .AppendLine($"  coalesce((case media_subscriptions.metadata_type when {(int)MetadataType.Show} then metadata_items.title when {(int)MetadataType.Episode} then null end), '') as show_title,")
                .AppendLine($"  coalesce((case media_subscriptions.metadata_type when {(int)MetadataType.Show} then null when {(int)MetadataType.Episode} then metadata_items.title end), '') as episode_title,")
                .AppendLine("  metadata_subscription_desired_items.remote_id,")
                .AppendLine("  media_subscriptions.extra_data,")
                .AppendLine("  media_subscriptions.\"order\"")
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
                    using (var reader = cmd.ExecuteReader())
                    {
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
                                        if (TryGetExtraDataValue(extraDataArray, "pr:startOffsetMinutes", out string startOffsetMinutesString))
                                        {
                                            int.TryParse(startOffsetMinutesString, out startOffsetMinutes);
                                        }

                                        if (TryGetExtraDataValue(extraDataArray, "pr:endOffsetMinutes", out string endOffsetMinutesString))
                                        {
                                            int.TryParse(endOffsetMinutesString, out endOffsetMinutes);
                                        }
                                    }
                                }

                                var rec = new ScheduledRecording()
                                {
                                    SubscriptionId = reader.GetInt32(0),
                                    SubscriptionMetadataType = (MetadataType)Enum.Parse(typeof(MetadataType), reader.GetInt32(1).ToString()),
                                    SubscriptionPriorityOrder = reader.GetFloat(6),
                                    ShowTitle = !reader.IsDBNull(2) ? reader.GetString(2) : string.Empty,
                                    EpisodeTitle = !reader.IsDBNull(3) ? reader.GetString(3) : string.Empty,
                                    RemoteId = remoteId,
                                    StartOffsetMinutes = startOffsetMinutes,
                                    EndOffsetMinutes = endOffsetMinutes
                                };

                                Logger.LogInformation($"    {remoteId}\t{rec.SubscriptionMetadataType}\t{string.Join(" - ", (new[] {rec.ShowTitle, rec.EpisodeTitle}).Where(p => !string.IsNullOrWhiteSpace(p)))}");

                                _scheduledRecordings.Add(remoteId, rec);
                            }
                        }
                    }
                }
            }

            Logger.LogInformation($"    Subscriptions loaded: {_scheduledRecordings.Count()}");
        }

        private void LoadEpgInfo()
        {
            if (_scheduledRecordings.Any())
            {
                var epgInfoCount = 0;

                Logger.LogInformation("  Loading EPG info for subscriptions");

                // Get EPG database file names since it appears like there could be multiple
                var databaseFilePath = Path.GetDirectoryName(_libraryDatabaseFileName);
                var tvEpgDatabases = new List<Tuple<string, int>>();
                var sqlEpgProviders = new StringBuilder()
                    .AppendLine("select")
                    .AppendLine("  epg.identifier,")
                    .AppendLine("  dvr.uuid,")
                    .AppendLine("  dvr.id")
                    .AppendLine("from media_provider_resources as epg")
                    .AppendLine("inner join media_provider_resources as dvr on dvr.id = epg.parent_id")
                    .AppendLine("where epg.identifier like 'tv.plex.providers.epg.%'");

                using (var conn = new SQLiteConnection($"Data Source={_libraryDatabaseFileName};Version=3;Read Only=True;"))
                {
                    conn.Open();
                    using (var cmd = conn.CreateCommand())
                    {
                        cmd.CommandText = sqlEpgProviders.ToString();
                        using (var reader = cmd.ExecuteReader())
                        {
                            while (reader.Read())
                            {
                                var identifier = reader.GetString(0);
                                var uuid = reader.GetString(1);
                                var dvrId = reader.GetInt32(2);
                                var tvEpgDatabaseFileName = Path.Combine(databaseFilePath, $"{identifier}-{uuid}.db");

                                tvEpgDatabases.Add(new Tuple<string, int>(tvEpgDatabaseFileName, dvrId));
                            }
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

                foreach (var tvEpgDatabase in tvEpgDatabases)
                {
                    var (tvEpgDatabaseFileName, dvrId) = tvEpgDatabase;

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
                            using (var reader = cmd.ExecuteReader())
                            {
                                while (reader.Read())
                                {
                                    var remoteId = reader.GetString(0);

                                    if (_scheduledRecordings.TryGetValue(remoteId, out var rec))
                                    {
                                        epgInfoCount++;

                                        rec.DvrId = dvrId;

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

                                        Logger.LogInformation($"    {remoteId}\t{FormatDateTime(rec.StartTimeWithOffset)}\t{FormatDateTime(rec.EndTimeWithOffset)}\t{FormatRecordingTitle(rec)}");
                                    }
                                }
                            }
                        }
                    }
                }

                Logger.LogInformation($"    EPG info found: {epgInfoCount}");
            }
        }

        private void RemoveUnschedulableAndPastItems()
        {
            if (_scheduledRecordings.Any())
            {
                Logger.LogInformation("  Removing unschedulable and past items");

                // Remove items that don't have a start time, or that start in the past
                var idsToRemove = _scheduledRecordings.Values
                    .Where(rec => !rec.StartTimeWithOffset.HasValue || !rec.EndTimeWithOffset.HasValue || rec.StartTimeWithOffset < DateTime.Now)
                    .Select(rec => rec.RemoteId).ToArray();

                RemoveScheduledRecordings(idsToRemove);

                Logger.LogInformation($"    Removed items: {idsToRemove.Count()}");
            }
        }

        private void RemoveExistingTvShows()
        {
            if (_scheduledRecordings.Any())
            {
                Logger.LogInformation("  Removing previously recorded TV shows");

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

                Logger.LogInformation($"    Removed TV shows: {idsToRemove.Count()}");
            }
        }

        private void RemoveExistingMovies()
        {
            if (_scheduledRecordings.Any())
            {
                Logger.LogInformation("  Removing previously recorded movies");

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

                Logger.LogInformation($"    Removed movies: {idsToRemove.Count()}");
            }
        }

        private void DetermineScheduleConflicts()
        {
            if (_scheduledRecordings.Any())
            {
                Logger.LogInformation("  Determining recording conflicts based on priority");

                static bool hasConflict(ScheduledRecording rec1, ScheduledRecording rec2)
                {
                    var start1 = rec1.StartTimeWithOffset.Value;
                    var end1 = rec1.EndTimeWithOffset.Value;
                    var start2 = rec2.StartTimeWithOffset.Value;
                    var end2 = rec2.EndTimeWithOffset.Value;

                    return start1 < end2 && end1 > start2;
                }

                // Identify all potential conflicts
                foreach (var rec1 in _scheduledRecordings.Values)
                {
                    foreach (var rec2 in _scheduledRecordings.Values.Where(r => r != rec1))
                    {
                        if (hasConflict(rec1, rec2))
                        {
                            if (!rec1.Conflicts.Contains(rec2))
                            {
                                rec1.Conflicts.Add(rec2);
                            }

                            if (!rec2.Conflicts.Contains(rec1))
                            {
                                rec2.Conflicts.Add(rec1);
                            }
                        }
                    }
                }

                // Reduce and mark actual conflicts based on number of tuners available
                var tunerCounts = GetDvrTunerCounts();
                var scheduledRecordingsOrdered = _scheduledRecordings.Values.OrderBy(r => r.SubscriptionPriorityOrder).ToArray();
                foreach (var rec in scheduledRecordingsOrdered.Where(r => !r.HasConflict))
                {
                    if (!tunerCounts.TryGetValue(rec.DvrId, out var tunerCount))
                    {
                        Logger.LogInformation($"    WARNING: Scheduled recording has an unrecognized DVR id '{rec.DvrId}', assuming only 1 tuner");
                        tunerCount = 1;
                    }

                    var conflicts = rec.Conflicts.Where(r => !r.HasConflict).ToArray();
                    if (conflicts.Length + 1 > tunerCount)
                    {
                        var potentialConflicts = new List<ScheduledRecording>();

                        // Only consider this show's conflicts that conflict with each other
                        foreach (var rec1 in conflicts)
                        {
                            foreach (var rec2 in conflicts.Where(r => r != rec1))
                            {
                                if (hasConflict(rec1, rec2))
                                {
                                    if (!potentialConflicts.Contains(rec1))
                                    {
                                        potentialConflicts.Add(rec1);
                                    }
                                    
                                    if (!potentialConflicts.Contains(rec2))
                                    {
                                        potentialConflicts.Add(rec2);
                                    }
                                }
                            }
                        }

                        if (potentialConflicts.Any())
                        {
                            potentialConflicts.Add(rec);

                            // Mark the actual conflicts based on priority and number of tuners
                            var actualConflicts = potentialConflicts
                                .OrderBy(r => r.SubscriptionPriorityOrder)
                                .Skip(tunerCount);

                            foreach (var actualConflict in actualConflicts)
                            {
                                actualConflict.HasConflict = true;
                            }

                            var i = 0;
                            Logger.LogInformation($"    Potential conflicts ({rec.RemoteId}):");
                            foreach (var conflict in potentialConflicts.OrderBy(r => r.SubscriptionPriorityOrder))
                            {
                                Logger.LogInformation($"      {++i}:{conflict.SubscriptionPriorityOrder}\t{conflict.RemoteId}\t{FormatDateTime(conflict.StartTimeWithOffset)}\t{FormatDateTime(conflict.EndTimeWithOffset)}\t{FormatRecordingTitle(conflict)}");
                            }
                        }
                    }
                }

                Logger.LogInformation($"    Conflicts found: {_scheduledRecordings.Values.Count(r => r.HasConflict)}");
            }
        }

        private void RemoveScheduledRecordings(IEnumerable<string> idsToRemove)
        {
            if (idsToRemove != null && idsToRemove.Any())
            {
                foreach (var id in idsToRemove.ToArray())
                {
                    if (_scheduledRecordings.TryGetValue(id, out var rec))
                    {
                        _scheduledRecordings.Remove(id);

                        Logger.LogInformation($"    {id}\t{FormatDateTime(rec.StartTimeWithOffset)}\t{FormatDateTime(rec.EndTimeWithOffset)}\t{FormatRecordingTitle(rec)}");
                    }
                }
            }
        }

        private bool TryGetExtraDataValue(string[] extraDataArray, string key, out string value)
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

        private Dictionary<int, int> GetDvrTunerCounts()
        {
            var tunerCounts = new Dictionary<int, int>();

            // I'm not entirely sure about what all the filters on this query actually mean, but this seems to indicate a DVR tuner that is enabled.
            // I figure it is better to be more restrictive until I have a better understanding of all the fields.
            var sqlDvrTuners = new StringBuilder()
                .AppendLine("select")
                .AppendLine("  parent_id,")
                .AppendLine("  extra_data")
                .AppendLine("from media_provider_resources")
                .AppendLine("where identifier = 'tv.plex.grabbers.tunerservice'")
                .AppendLine("and protocol = 'livetv'")
                .AppendLine("and parent_id is not null")
                .AppendLine("and type = 4")
                .AppendLine("and status = 1")
                .AppendLine("and state = 1");

            using (var conn = new SQLiteConnection($"Data Source={_libraryDatabaseFileName};Version=3;Read Only=True;"))
            {
                conn.Open();
                using (var cmd = conn.CreateCommand())
                {
                    cmd.CommandText = sqlDvrTuners.ToString();

                    using (var reader = cmd.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var dvrId = reader.GetInt32(0);
                            var tunerCount = 0;

                            // Parse "extra_data" field for some advanced recording info.  We need to be
                            // careful of unexpected data structures, so carefully grab what we need from it.
                            var extraDataString = !reader.IsDBNull(1) ? reader.GetString(1) : string.Empty;
                            if (!string.IsNullOrWhiteSpace(extraDataString))
                            {
                                var extraDataArray = Uri.UnescapeDataString(extraDataString).Split('&');
                                if (extraDataArray.Any())
                                {
                                    if (TryGetExtraDataValue(extraDataArray, "at:tuners", out string tunersString))
                                    {
                                        int.TryParse(tunersString, out tunerCount);
                                    }
                                }
                            }

                            if (tunerCount == 0)
                            {
                                Logger.LogInformation($"    WARNING: Unable to fetch number of tuners for DVR id '{dvrId}', assuming only 1 tuner");
                                tunerCount = 1;
                            }

                            tunerCounts.Add(dvrId, tunerCount);
                        }
                    }
                }
            }

            Logger.LogInformation("    DVR tuners found:");
            foreach (var dvr in tunerCounts)
            {
                Logger.LogInformation($"      DVR Id: {dvr.Key}\tTuner count: {dvr.Value}");
            }

            return tunerCounts;
        }

    }
}