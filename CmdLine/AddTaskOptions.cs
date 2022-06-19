using System;
using System.Collections.Generic;
using System.Linq;
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
            HelpText = "Creates or updates a Windows Task Scheduler 'wakeup' task that will wakeup the computer before the next scheduled recording time or Plex maintenance time.")]
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
                VerifyMinimumValue("wakeup-delay", value, 0);
                VerifyWakeupDelayAndOffset("wakeup-delay", value, WakeupOffsetSeconds);
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
                VerifyMinimumValue("sync-interval", value, 1);
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
                VerifyMinimumValue("monitor-debounce", value, 1);
                _monitorDebounceSeconds = value;
            }
        }

        #endregion Monitor

        #region Version

        private bool _versionCheck;
        [Option("version-check",
            SetName = "version-check",
            HelpText = "Creates or updates a Windows Task Scheduler 'version-check' task to run at the specified interval and check for a newer version of this application.")]
        public bool VersionCheck
        {
            get
            {
                return _versionCheck;
            }
            set
            {
                _versionCheck = value;
            }
        }

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
                VerifyMinimumValue("version-check-interval", value, 1);
                _versionCheckDays = value;
            }
        }

        #endregion Version

        internal const int WAKEUP_OFFSET_SECONDS_DEFAULT = 15;
        private int? _wakeupOffsetSeconds;
        [Option("offset",
            MetaValue = "SECONDS",
            Default = WAKEUP_OFFSET_SECONDS_DEFAULT,
            HelpText = "The number of seconds to wakeup the computer before the next scheduled recording time or Plex maintenance time.  Applies to the 'wakeup', 'sync', and 'monitor' tasks.")]
        public int? WakeupOffsetSeconds
        {
            get
            {
                return _wakeupOffsetSeconds;
            }
            set
            {
                VerifyMinimumValue("offset", value, 0);
                VerifyWakeupDelayAndOffset("offset", WakeupRefreshDelaySeconds, value);
                _wakeupOffsetSeconds = value;
            }
        }

        private IEnumerable<string> _wakeupActions;
        [Option("actions",
            MetaValue = "FILE1;FILE2",
            HelpText = "A list of actions separated by ';' to run when the 'wakeup' task is triggered.  This can be a path to any file(s) that Windows Task Scheduler can execute (ie: .bat, .exe, etc).",
            Separator = ';'
        )]
        public IEnumerable<string> WakeupActions
        {
            get
            {
                return _wakeupActions;
            }
            set
            {
                _wakeupActions = value.Select(v => $"\"{v.Trim('"')}\"").ToList();
            }
        }

        [Usage(ApplicationAlias = Program.APP_EXE)]
        public static IEnumerable<Example> Examples
        {
            get
            {
                var formatStyles = new[] { UnParserSettings.WithUseEqualTokenOnly() };

                return new List<Example>() {
                    new Example("Create the 'wakeup' task with custom settings", formatStyles, new AddTaskOptions { Wakeup = true, WakeupOffsetSeconds = 60, WakeupActions = new[] { "\"C:\\dir 1\\script.bat\"" } }),
                    new Example("Create the 'sync' task with custom settings", formatStyles, new AddTaskOptions { Sync = true, SyncIntervalMinutes = 5, WakeupOffsetSeconds = 60, WakeupActions = new[] { "\"C:\\dir 1\\script.bat\"" } }),
                    new Example("Create the 'monitor' task with custom settings", formatStyles, new AddTaskOptions { Monitor = true, MonitorDebounceSeconds = 10, WakeupOffsetSeconds = 60, WakeupActions = new[] { "\"C:\\dir 1\\script.bat\"" } }),
                    new Example("Create the 'version-check' task to check every 90 days", formatStyles, new AddTaskOptions { VersionCheck = true, VersionCheckDays = 90 })
                };
            }
        }

        private static void VerifyWakeupDelayAndOffset(string paramName, int? delaySeconds, int? offsetSeconds)
        {
            if (delaySeconds.HasValue && offsetSeconds.HasValue && delaySeconds.Value <= offsetSeconds.Value)
            {
                throw new ArgumentException($"The 'wakeup-delay' value '{delaySeconds.Value}' must be greater than the 'offset' value '{offsetSeconds.Value}' so that the Windows Task Scheduler 'wakeup' task will get updated after the current recording has started.", paramName);
            }
        }

    }
}