using CommandLine;
using CommandLine.Text;
using PlexDvrWaker.CmdLine;
using PlexDvrWaker.Common;
using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Security.Permissions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlexDvrWaker
{
    public static class Program
    {
        private static Options _options;
        private static Plex.DataAdapter _plexDataAdapter;
        private static Plex.DataAdapter PlexDataAdapter
        {
            get
            {
                if (_plexDataAdapter == null)
                {
                    if (_options == null)
                    {
                        throw new InvalidOperationException("Options must be initialized first.");
                    }
                    _plexDataAdapter = new Plex.DataAdapter(_options.PlexDataPath);
                }
                return _plexDataAdapter;
            }
        }

        public static void Main(string[] args)
        {
            // If no args, show help by default
            if (args == null || !args.Any())
            {
                args = new string[] { "--help" };
            }

            Parser.Default.ParseArguments<Options>(args)
                .WithParsed(opts => Run(opts));
        }

        private static void Run(Options opts)
        {
            _options = opts;
            Logger.Verbose = opts.Verbose;

            if (opts.PrintRecordingSchedule)
            {
                PlexDataAdapter.PrintScheduledRecordings();
            }

            if (opts.SetNextWakeup)
            {
                var wakeupTime = PlexDataAdapter.GetNextScheduledRecordingTime();
                if (wakeupTime.HasValue)
                {
                    Plex.TaskScheduler.CreateOrUpdateWakeUpTask(wakeupTime.Value);
                }
            }

            if (opts.SyncIntervalMinutes.HasValue)
            {
                Plex.TaskScheduler.CreateOrUpdateDVRSyncTask(opts.SyncIntervalMinutes.Value);
            }

            // Since multipe options could be specified at once, run the "monitor" last since it will run indefinitely
            if (opts.MonitorBundleSeconds.HasValue)
            {
                using (var pm = new Plex.LibraryMonitor(PlexDataAdapter, TimeSpan.FromSeconds(opts.MonitorBundleSeconds.Value)))
                {
                    pm.Enabled = true;
                    pm.WaitIndefinitely();
                }
            }
        }

    }
}
