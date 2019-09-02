using System;
using System.Collections.Generic;
using CommandLine;
using CommandLine.Text;

namespace PlexDvrWaker.CmdLine
{
    [Verb("add-task", HelpText = "Add/update Windows Task Scheduler tasks for waking the computer for the next scheduled recording, syncing the next wakeup, or monitoring the Plex library database for changes.")]
    internal class AddTaskOptions : ProgramOptions
    {
        [Option("wakeup",
            HelpText = "Creates or updates a Windows Task Scheduler 'wakeup' task that will wakeup the computer 15 seconds before the next scheduled recording time.")]
        public bool Wakeup { get; set; }

        [Option("sync",
            HelpText = "Creates or updates a Windows Task Scheduler 'sync' task to run at the specified interval and sync the 'wakeup' task with the next scheduled recording time.")]
        public bool Sync { get; set; }

        private int? _syncIntervalMinutes;
        [Option("interval",
            MetaValue = "MINUTES",
            Default = 15,
            HelpText = "The interval to sync the 'wakeup' task with the next scheduled recording time.")]
        public int? SyncIntervalMinutes
        {
            get
            {
                return _syncIntervalMinutes;
            }
            set
            {
                if (value.HasValue && value.Value < 1)
                {
                    throw new ArgumentOutOfRangeException("sync", value.Value, "The value must be greater than or equal to 1.");
                }
                _syncIntervalMinutes = value;
            }
        }

        [Option("monitor",
            HelpText = "Creates or updates a Windows Task Scheduler 'monitor' task to run in the background when the computer starts up that will monitor the Plex library database file for changes and update the 'wakeup' task based on the next scheduled recording time.")]
        public bool Monitor { get; set; }

        private int? _debounceSeconds;

        [Option("debounce",
            MetaValue = "SECONDS",
            Default = 5,
            HelpText = "Since the Plex library database can change multiple times within a short time, upon the first change it will wait the specified number of seconds before it updates the Task Scheduler 'wakeup' task with the next scheduled recording time.")]
        public int? DebounceSeconds
        {
            get
            {
                return _debounceSeconds;
            }
            set
            {
                if (value.HasValue && value.Value < 1)
                {
                    throw new ArgumentOutOfRangeException("debounce", value.Value, "The value must be greater than or equal to 1.");
                }
                _debounceSeconds = value;
            }
        }


        [Usage(ApplicationAlias = "dotnet PlexDvrWaker.dll")]
        public static IEnumerable<Example> Examples
        {
            get
            {
                return new List<Example>() {
                    new Example("Create the 'wakeup' task", new[] { UnParserSettings.WithUseEqualTokenOnly() }, new AddTaskOptions { Wakeup = true }),
                    new Example("Create the 'sync' task", new[] { UnParserSettings.WithUseEqualTokenOnly() }, new AddTaskOptions { Sync = true, SyncIntervalMinutes = 15 }),
                    new Example("Create the 'monitor' task", new[] { UnParserSettings.WithUseEqualTokenOnly() }, new AddTaskOptions { Monitor = true, DebounceSeconds = 5 })
                };
            }
        }

    }
}