using System;
using System.Collections.Generic;
using CommandLine;
using CommandLine.Text;

namespace PlexDvrWaker.CmdLine
{
    [Verb("monitor",
        HelpText = "Monitors the Plex library database for changes and updates the 'wakeup' task based on the next scheduled recording time or Plex maintenance time.")]
    internal class MonitorOptions : PlexOptions
    {
        private int? _debounceSeconds;

        private int? _offsetSeconds;
        [Option("offset",
            MetaValue = "SECONDS",
            Default = AddTaskOptions.WAKEUP_OFFSET_SECONDS_DEFAULT,
            HelpText = "The number of seconds to wakeup the computer before the next scheduled recording time or Plex maintenance time.")]
        public int? OffsetSeconds
        {
            get
            {
                return _offsetSeconds;
            }
            set
            {
                VerifyMinimumValue("offset", value, 0);
                _offsetSeconds = value;
            }
        }

        [Option("debounce",
            MetaValue = "SECONDS",
            Default = 5,
            HelpText = "Since the database can change multiple times within a short time, upon the first change it will wait the specified number of seconds before it updates the Task Scheduler 'wakeup' task with the next scheduled recording time or Plex maintenance time.  (Minimum: 1 second)")]
        public int? DebounceSeconds
        {
            get
            {
                return _debounceSeconds;
            }
            set
            {
                VerifyMinimumValue("debounce", value, 1);
                _debounceSeconds = value;
            }
        }

        [Option("non-interactive",
            Hidden = true,
            HelpText = "Determines whether to run the monitor in non-interactive mode when running from the Windows Task Scheduler.")]
        public bool NonInteractive { get; set; }

        [Usage(ApplicationAlias = Program.APP_EXE)]
        public static IEnumerable<Example> Examples
        {
            get
            {
                return new List<Example>() {
                    new Example("Monitors the Plex library database for changes", new MonitorOptions { }),
                    new Example("Monitors with custom settings", new[] { UnParserSettings.WithUseEqualTokenOnly() }, new MonitorOptions { DebounceSeconds = 30, OffsetSeconds = 60 }),
                    new Example("Monitors and prints messages to standard output", new MonitorOptions { Verbose = true })
                };
            }
        }
    }
}