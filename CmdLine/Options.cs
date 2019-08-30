using CommandLine;
using CommandLine.Text;
using PlexDvrWaker.Common;
using System;

namespace PlexDvrWaker.CmdLine
{
    internal class Options
    {
        //TODO add verbs and usage
        //add
        //  --wakeup
        //  --sync=MINUTES
        //  --monitor=SECONDS
        //list
        //monitor
        //  --bundle=SECONDS
        //--plexdata
        //--verbose
        //--help

        [Option("list",
            HelpText = "Prints upcoming scheduled recordings to standard output.")]
        public bool PrintRecordingSchedule { get; set; }

        [Option("wakeup",
            HelpText = "Creates or updates a Windows Task Scheduler 'wakeup' task that will wakeup the computer 15 seconds before the next scheduled recording time.")]
        public bool SetNextWakeup { get; set; }

        private uint? _syncIntervalMinutes;

        [Option("sync",
            MetaValue = "MINUTES",
            HelpText = "Creates or updates a Windows Task Scheduler 'sync' task to run at the specified interval and sync the 'wakeup' task with the next scheduled recording time.  (Minimum: 1 minute)")]
        public uint? SyncIntervalMinutes
        {
            get
            {
                return _syncIntervalMinutes;
            }
            set
            {
                if (value.HasValue && value.Value < 1)
                {
                    throw new ArgumentOutOfRangeException("The value must be greater than or equal to 1.", (Exception)null);
                }
                _syncIntervalMinutes = value;
            }
        }

        private uint? _monitorBundleSeconds;

        [Option("monitor",
            MetaValue = "SECONDS",
            HelpText = "Enables file monitoring for the Plex library database.  Since the database can change multiple times within a short time, upon the first change it will wait the specified number of seconds before it updates the Task Scheduler 'wakeup' task with the next scheduled recording time.  (Minimum: 1 second)")]
        public uint? MonitorBundleSeconds
        {
            get
            {
                return _monitorBundleSeconds;
            }
            set
            {
                if (value.HasValue && value.Value < 1)
                {
                    throw new ArgumentOutOfRangeException("The value must be greater than or equal to 1.", (Exception)null);
                }
                _monitorBundleSeconds = value;
            }
        }

        [Option("plexdata",
            MetaValue = "PATH",
            Default = @"%LOCALAPPDATA%",
            HelpText = "The path where Plex stores its local application data.")]
        public string PlexDataPath { get; set; }

        [Option("verbose",
            HelpText = "Prints all messages to standard output.")]
        public bool Verbose { get; set; }

        // //TODO alias depends on how I decide to build/distribute the executable
        // [Usage(ApplicationAlias = "dotnet PlexDvrWaker.dll")]
        // public static IEnumerable<Example> Examples
        // {
        //     get
        //     {
        //         return new List<Example>() {
        //             new Example("Print upcoming scheduled recordings.", new Options { PrintRecordingSchedule = true }),
        //             new Example("Foo", new Options { SetNextWakeup = true }),
        //             new Example("Bar", new Options { SyncIntervalSeconds = 300 }),
        //             new Example("Baz", new Options { MonitorBundleSeconds = 5 })
        //         };
        //     }
        // }
    }
}