using CommandLine;
using Microsoft.Win32.TaskScheduler;
using PlexDvrWaker.CmdLine;
using PlexDvrWaker.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace PlexDvrWaker.Plex
{
    /// <summary>
    /// Class for creating Windows Task Scheduler tasks
    /// </summary>
    internal class TaskScheduler
    {
        private const string TASK_SCHEDULER_FOLDER = Program.APP_FRIENDLY_NAME;
        private const string TASK_NAME_DVR_WAKE = TASK_SCHEDULER_FOLDER + "\\DVR wake";
        private const string TASK_NAME_DVR_SYNC = TASK_SCHEDULER_FOLDER + "\\DVR sync";
        private const string TASK_NAME_DVR_MONITOR = TASK_SCHEDULER_FOLDER + "\\DVR monitor";
        private const string TASK_NAME_VERSION_CHECK = TASK_SCHEDULER_FOLDER + "\\Version check";

        private readonly string _libraryDatabaseFileName;

        public TaskScheduler()
        {
            _libraryDatabaseFileName = Settings.LibraryDatabaseIsOverridden ? Settings.LibraryDatabaseFileName : null;
        }

        public bool CreateOrUpdateWakeUpTask(DateTime startTime, int wakeupOffsetSeconds, IEnumerable<string> wakeupActions)
        {
            return CreateOrUpdateWakeUpTask(startTime, wakeupOffsetSeconds, wakeupActions, true);
        }

        internal bool CreateOrUpdateWakeUpTask(DateTime startTime, int wakeupOffsetSeconds, IEnumerable<string> wakeupActions, bool showMessageToUser)
        {
            Logger.LogInformation($"Creating/updating wakeup task: {TASK_NAME_DVR_WAKE}");

            var td = TaskService.Instance.NewTask();
            td.RegistrationInfo.Description = "This task will wake the computer for the next Plex DVR recording or maintenance time.";
            td.Principal.LogonType = TaskLogonType.S4U;
            td.Principal.RunLevel = TaskRunLevel.Highest;
            td.Settings.Hidden = true;
            td.Settings.AllowDemandStart = true;
            td.Settings.DisallowStartIfOnBatteries = false;
            td.Settings.StopIfGoingOnBatteries = false;
            td.Settings.WakeToRun = true;

            var trigger = new TimeTrigger()
            {
                // Trigger a few seconds earlier to give computer time to wakeup
                StartBoundary = startTime.AddSeconds(-1 * wakeupOffsetSeconds)
            };
            td.Triggers.Add(trigger);

            // Add custom wakeup actions first
            if (wakeupActions != null && wakeupActions.Any())
            {
                td.Actions.AddRange(wakeupActions.Select(wakeupAction => new ExecAction { Path = wakeupAction }));
            }

            // Add the recreate/update wakeup action last because it will delay when the wakeup task is being refreshed
            td.Actions.Add(new ExecAction()
            {
                Path = Program.APP_EXE,
                WorkingDirectory = Program.APP_WORKING_DIRECTORY,
                Arguments = Parser.Default.FormatCommandLine(
                    new AddTaskOptions
                    {
                        // Recreate/update the wakeup task 15 seconds "after" the current recording has started.
                        Wakeup = true,
                        WakeupOffsetSeconds = wakeupOffsetSeconds,
                        WakeupRefreshDelaySeconds = wakeupOffsetSeconds + 15,
                        WakeupActions = wakeupActions,
                        LibraryDatabaseFileName = _libraryDatabaseFileName,
                        TaskName = TASK_NAME_DVR_WAKE
                    },
                    Program.ConfigureUnParserSettings
                )
            });

            var successMessage = $"Wakeup task scheduled for {trigger.StartBoundary}";
            if (!TryCreateTask(TASK_NAME_DVR_WAKE, td, successMessage, showMessageToUser))
            {
                return false;
            }

            return true;
        }

        public bool CreateOrUpdateDVRSyncTask(int intervalMinutes, int wakeupOffsetSeconds, IEnumerable<string> wakeupActions)
        {
            Logger.LogInformation($"Creating/updating DVR sync task: {TASK_NAME_DVR_SYNC}");

            var td = TaskService.Instance.NewTask();
            td.RegistrationInfo.Description = "This task will sync with the Plex database and ensure the 'DVR wake' task is updated appropriately.";
            td.Principal.LogonType = TaskLogonType.S4U;
            td.Principal.RunLevel = TaskRunLevel.Highest;
            td.Settings.Hidden = true;
            td.Settings.AllowDemandStart = true;
            td.Settings.DisallowStartIfOnBatteries = false;
            td.Settings.StopIfGoingOnBatteries = false;
            td.Settings.WakeToRun = false;

            var tt = new TimeTrigger(DateTime.Today)
            {
                Repetition = new RepetitionPattern(TimeSpan.FromMinutes(intervalMinutes), TimeSpan.Zero)
            };
            td.Triggers.Add(tt);

            td.Actions.Add(new ExecAction()
            {
                Path = Program.APP_EXE,
                WorkingDirectory = Program.APP_WORKING_DIRECTORY,
                Arguments = Parser.Default.FormatCommandLine(
                    new AddTaskOptions
                    {
                        Wakeup = true,
                        WakeupOffsetSeconds = wakeupOffsetSeconds,
                        WakeupActions = wakeupActions,
                        LibraryDatabaseFileName = _libraryDatabaseFileName,
                        TaskName = TASK_NAME_DVR_SYNC
                    },
                    Program.ConfigureUnParserSettings
                )
            });

            var successMessage = $"DVR sync task scheduled for every {intervalMinutes} minute{(intervalMinutes > 1 ? "s" : "")}";
            if (!TryCreateTask(TASK_NAME_DVR_SYNC, td, successMessage, true))
            {
                return false;
            }

            return true;
        }

        public bool CreateOrUpdateDVRMonitorTask(int debounceSeconds, int offsetSeconds, IEnumerable<string> wakeupActions)
        {
            Logger.LogInformation($"Creating/updating DVR monitor task: {TASK_NAME_DVR_MONITOR}");

            var td = TaskService.Instance.NewTask();
            td.RegistrationInfo.Description = "This task will monitor the Plex database and ensure the 'DVR wake' task is updated appropriately.";
            td.Principal.LogonType = TaskLogonType.S4U;
            td.Principal.RunLevel = TaskRunLevel.Highest;
            td.Settings.Hidden = true;
            td.Settings.AllowDemandStart = true;
            td.Settings.DisallowStartIfOnBatteries = false;
            td.Settings.StopIfGoingOnBatteries = false;
            td.Settings.WakeToRun = false;
            td.Settings.ExecutionTimeLimit = TimeSpan.Zero;
            td.Settings.RestartInterval = TimeSpan.FromMinutes(1);
            td.Settings.RestartCount = 3;

            td.Triggers.Add(new BootTrigger
            {
                // Delay after logon to give Plex time to startup and update things
                Delay = TimeSpan.FromMinutes(5)
            });

            td.Actions.Add(new ExecAction()
            {
                Path = Program.APP_EXE,
                WorkingDirectory = Program.APP_WORKING_DIRECTORY,
                Arguments = Parser.Default.FormatCommandLine(
                    new MonitorOptions
                    {
                        OffsetSeconds = offsetSeconds,
                        DebounceSeconds = debounceSeconds,
                        WakeupActions = wakeupActions,
                        NonInteractive = true,
                        LibraryDatabaseFileName = _libraryDatabaseFileName,
                        TaskName = TASK_NAME_DVR_MONITOR
                    },
                    Program.ConfigureUnParserSettings
                )
            });

            var successMessage = $"DVR monitor task scheduled to run at startup";
            if (!TryCreateTask(TASK_NAME_DVR_MONITOR, td, successMessage, true))
            {
                return false;
            }

            Logger.LogInformation($"Starting DVR monitor task");
            TaskService.Instance.GetTask(TASK_NAME_DVR_MONITOR).Run();
            Logger.LogInformation($"DVR monitor task has been started", true);

            return true;
        }

#pragma warning disable CA1822
        public bool CreateOrUpdateVersionCheckTask(int intervalDays)
#pragma warning restore CA1822
        {
            Logger.LogInformation($"Creating/updating version check task: {TASK_NAME_VERSION_CHECK}");

            var td = TaskService.Instance.NewTask();
            td.RegistrationInfo.Description = $"This task will check for a newer version of {Program.APP_FRIENDLY_NAME}.";
            td.Principal.LogonType = TaskLogonType.InteractiveToken;
            td.Settings.AllowDemandStart = true;
            td.Settings.DisallowStartIfOnBatteries = false;
            td.Settings.StopIfGoingOnBatteries = false;
            td.Settings.WakeToRun = false;
            td.Settings.RunOnlyIfNetworkAvailable = true;
            td.Settings.StartWhenAvailable = true;

            var tt = new TimeTrigger(DateTime.Now)
            {
                Repetition = new RepetitionPattern(TimeSpan.FromDays(intervalDays), TimeSpan.Zero)
            };
            td.Triggers.Add(tt);

            td.Actions.Add(new ExecAction()
            {
                Path = Program.APP_EXE,
                WorkingDirectory = Program.APP_WORKING_DIRECTORY,
                Arguments = Parser.Default.FormatCommandLine(
                    new VersionCheckOptions
                    {
                        NonInteractive = true,
                        TaskName = TASK_NAME_VERSION_CHECK
                    },
                    Program.ConfigureUnParserSettings
                )
            });

            var successMessage = $"Version check task scheduled for every {intervalDays} day{(intervalDays > 1 ? "s" : "")}";
            if (!TryCreateTask(TASK_NAME_VERSION_CHECK, td, successMessage, true))
            {
                return false;
            }

            return true;
        }

        private static bool TryCreateTask(string taskPathAndName, TaskDefinition td, string successMessage, bool showMessageToUser)
        {
            try
            {
                Logger.LogInformation("  Creating/updating the task");
                TaskService.Instance.RootFolder.RegisterTaskDefinition(taskPathAndName, td);
                Logger.LogInformation(successMessage, showMessageToUser);
            }
            catch (UnauthorizedAccessException)
            {
                Logger.LogError($"Access is denied.  Try running as an Administrator.  Administrator rights are needed in order to create the '{taskPathAndName}' task so that it runs hidden without flashing a console window every time the task is triggered.");
                return false;
            }

            return true;
        }
    }
}
