using System;
using System.Collections.Generic;
using CommandLine;
using CommandLine.Text;

namespace PlexDvrWaker.CmdLine
{
    [Verb("monitor", HelpText = "Monitors the Plex library database for changes and updates the 'wakeup' task based on the next scheduled recording time or Plex maintenance time.")]
    internal class MonitorOptions : ProgramOptions
    {
        private int? _debounceSeconds;

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
                if (value.HasValue && value.Value < 1)
                {
                    throw new ArgumentOutOfRangeException("debounce", value.Value, "The value must be greater than or equal to 1.");
                }
                _debounceSeconds = value;
            }
        }

        [Option("non-interactive",
            Hidden = true,
            HelpText = "Determines whether to run the monitor in non-interactive mode when running from the Windows Task Scheduler.")]
        public bool NonInteractive { get; set; }

        [Usage(ApplicationAlias = Program.EXE_NAME)]
        public static IEnumerable<Example> Examples
        {
            get
            {
                return new List<Example>() {
                    new Example("Monitors the Plex library database for changes", new MonitorOptions { }),
                    new Example("Monitors with a custom debounce", new[] { UnParserSettings.WithUseEqualTokenOnly() }, new MonitorOptions { DebounceSeconds = 30 }),
                    new Example("Monitors and prints messages to standard output", new MonitorOptions { Verbose = true })
                };
            }
        }
    }
}