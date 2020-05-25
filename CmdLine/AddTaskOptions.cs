using System;
using System.Collections.Generic;
using CommandLine;
using CommandLine.Text;

namespace PlexDvrWaker.CmdLine
{
    [Verb("add-task",
        HelpText = "Add/update Windows Task Scheduler tasks for waking the computer for the next scheduled recording or Plex maintenance time, syncing the next wakeup, monitoring the Plex library database for changes, or checking for a newer version of this application.")]
    internal class AddTaskOptions : PlexOptions
    {

        #region Wakeup

        [Option("wakeup",
            SetName = "wakeup",
            HelpText = "Creates or updates a Windows Task Scheduler 'wakeup' task that will wakeup the computer 15 seconds before the next scheduled recording time or Plex maintenance time.")]
        public bool Wakeup { get; set; }

        private int? _wakeupRefreshDelaySeconds;
        [Option("wakeup-delay",
            SetName = "wakeup",
            MetaValue = "SECONDS",
            Hidden = true,
            HelpText = "The number of seconds to wait before updating the Windows Task Scheduler 'wakeup' task.  This is used when the 'wakeup' task is triggered so that it waits until the current recording has started before updating the 'wakeup' task with the next scheduled recording time or Plex maintenance time.")]
        public int? WakeupRefreshDelaySeconds
        {
            get
            {
                return _wakeupRefreshDelaySeconds;
            }
            set
            {
                if (value.HasValue && value.Value < 0)
                {
                    throw new ArgumentOutOfRangeException("wakeup-delay", value.Value, "The value must be greater than or equal to 0.");
                }
                _wakeupRefreshDelaySeconds = value;
            }
        }

        #endregion Wakeup

        #region Sync

        [Option("sync",
            SetName = "sync",
            HelpText = "Creates or updates a Windows Task Scheduler 'sync' task to run at the specified interval and sync the 'wakeup' task with the next scheduled recording time or Plex maintenance time.")]
        public bool Sync { get; set; }

        private int? _syncIntervalMinutes;
        [Option("sync-interval",
            SetName = "sync",
            MetaValue = "MINUTES",
            Default = 15,
            HelpText = "The number of minutes between syncing the 'wakeup' task with the next scheduled recording time or Plex maintenance time.")]
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
                    throw new ArgumentOutOfRangeException("sync-interval", value.Value, "The value must be greater than or equal to 1.");
                }
                _syncIntervalMinutes = value;
            }
        }

        #endregion Sync

        #region Monitor

        [Option("monitor",
            SetName = "monitor",
            HelpText = "Creates or updates a Windows Task Scheduler 'monitor' task to run in the background when the computer starts up that will monitor the Plex library database file for changes and update the 'wakeup' task based on the next scheduled recording time or Plex maintenance time.")]
        public bool Monitor { get; set; }

        private int? _monitorDebounceSeconds;

        [Option("monitor-debounce",
            SetName = "monitor",
            MetaValue = "SECONDS",
            Default = 5,
            HelpText = "Since the Plex library database can change multiple times within a short time, upon the first change it will wait the specified number of seconds before it updates the Task Scheduler 'wakeup' task with the next scheduled recording time or Plex maintenance time.")]
        public int? MonitorDebounceSeconds
        {
            get
            {
                return _monitorDebounceSeconds;
            }
            set
            {
                if (value.HasValue && value.Value < 1)
                {
                    throw new ArgumentOutOfRangeException("monitor-debounce", value.Value, "The value must be greater than or equal to 1.");
                }
                _monitorDebounceSeconds = value;
            }
        }

        #endregion Monitor

        #region Version

        [Option("version-check",
            SetName = "version-check",
            HelpText = "Creates or updates a Windows Task Scheduler 'version-check' task to run at the specified interval and check for a newer version of this application.")]
        public bool VersionCheck { get; set; }

        private int? _versionCheckDays;

        [Option("version-check-interval",
            SetName = "version-check",
            MetaValue = "DAYS",
            Default = 30,
            HelpText = "The number of days between checking for a newer version of this application.")]
        public int? VersionCheckDays
        {
            get
            {
                return _versionCheckDays;
            }
            set
            {
                if (value.HasValue && value.Value < 1)
                {
                    throw new ArgumentOutOfRangeException("version-check-interval", value.Value, "The value must be greater than or equal to 1.");
                }
                _versionCheckDays = value;
            }
        }

        #endregion Version

        [Usage(ApplicationAlias = Program.APP_EXE)]
        public static IEnumerable<Example> Examples
        {
            get
            {
                var formatStyles = new[] { UnParserSettings.WithUseEqualTokenOnly() };

                return new List<Example>() {
                    new Example("Create the 'wakeup' task", formatStyles, new AddTaskOptions { Wakeup = true }),
                    new Example("Create the 'sync' task", formatStyles, new AddTaskOptions { Sync = true, SyncIntervalMinutes = 15 }),
                    new Example("Create the 'monitor' task", formatStyles, new AddTaskOptions { Monitor = true, MonitorDebounceSeconds = 5 }),
                    new Example("Create the 'version-check' task", formatStyles, new AddTaskOptions { VersionCheck = true, VersionCheckDays = 30 })
                };
            }
        }

    }
}