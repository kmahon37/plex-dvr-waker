using CommandLine;
using Microsoft.Win32.TaskScheduler;
using PlexDvrWaker.CmdLine;
using PlexDvrWaker.Common;
using System;
using System.IO;

namespace PlexDvrWaker.Plex
{
    /// <summary>
    /// Class for creating Windows Task Scheduler tasks
    /// </summary>
    internal class TaskScheduler
    {
        private const string TASK_SCHEDULER_FOLDER = "Plex DVR Waker";
        private const string TASK_NAME_DVR_WAKE = TASK_SCHEDULER_FOLDER + "\\DVR wake";
        private const string TASK_NAME_DVR_SYNC = TASK_SCHEDULER_FOLDER + "\\DVR sync";
        private const string TASK_NAME_DVR_MONITOR = TASK_SCHEDULER_FOLDER + "\\DVR monitor";
        private const string CMD_LINE_EXE = "cmd.exe";

        private readonly string _workingDirectory;
        private readonly string _cmdLineArgsPrefix;
        private readonly string _libraryDatabaseFileName;

        public TaskScheduler()
        {
            var fullPath = typeof(TaskScheduler).Assembly.Location;
            _workingDirectory = Path.GetDirectoryName(fullPath);
            _libraryDatabaseFileName = Settings.LibraryDatabaseIsOverridden ? Settings.LibraryDatabaseFileName : null;

            // Win7 Task Scheduler cannot find dotnet.exe by itself even though it's in the PATH environment variable,
            // but running as "cmd.exe /k start dotnet.exe ..." seems to work on Win7/10.
            var dllName = Path.GetFileName(fullPath);
            _cmdLineArgsPrefix = "/k start dotnet.exe " + dllName + " ";
        }

        public bool CreateOrUpdateWakeUpTask(DateTime startTime)
        {
            return CreateOrUpdateWakeUpTask(startTime, true);
        }

        internal bool CreateOrUpdateWakeUpTask(DateTime startTime, bool showMessageToUser)
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
                StartBoundary = startTime.AddSeconds(-15)
            };
            td.Triggers.Add(trigger);

            td.Actions.Add(new ExecAction()
            {
                Path = CMD_LINE_EXE,
                WorkingDirectory = _workingDirectory,
                Arguments = _cmdLineArgsPrefix + Parser.Default.FormatCommandLine(
                    new AddTaskOptions
                    {
                        // Recreate/update the wakeup task "after" the current recording has started.
                        Wakeup = true,
                        WakeupRefreshDelaySeconds = 30,
                        LibraryDatabaseFileName = _libraryDatabaseFileName
                    },
                    settings =>
                    {
                        settings.UseEqualToken = true;
                        settings.ShowHidden = true;
                    }
                )
            });

            var successMessage = $"Wakeup task scheduled for {trigger.StartBoundary}";
            if (!TryCreateAdminTask(TASK_NAME_DVR_WAKE, td, successMessage, showMessageToUser))
            {
                return false;
            }

            return true;
        }

        public bool CreateOrUpdateDVRSyncTask(int intervalMinutes)
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

            var tt = new TimeTrigger(new DateTime(2019, 1, 1))
            {
                Repetition = new RepetitionPattern(TimeSpan.FromMinutes(intervalMinutes), TimeSpan.Zero)
            };
            td.Triggers.Add(tt);

            td.Actions.Add(new ExecAction()
            {
                Path = CMD_LINE_EXE,
                WorkingDirectory = _workingDirectory,
                Arguments = _cmdLineArgsPrefix + Parser.Default.FormatCommandLine(
                    new AddTaskOptions
                    {
                        Wakeup = true,
                        LibraryDatabaseFileName = _libraryDatabaseFileName
                    },
                    settings =>
                    {
                        settings.UseEqualToken = true;
                    }
                )
            });

            var successMessage = $"DVR sync task scheduled for every {intervalMinutes} minute{(intervalMinutes > 1 ? "s" : "")}";
            if (!TryCreateAdminTask(TASK_NAME_DVR_SYNC, td, successMessage, true))
            {
                return false;
            }

            return true;
        }

        public bool CreateOrUpdateDVRMonitorTask(int debounceSeconds)
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
                Path = CMD_LINE_EXE,
                WorkingDirectory = _workingDirectory,
                Arguments = _cmdLineArgsPrefix + Parser.Default.FormatCommandLine(
                    new MonitorOptions
                    {
                        DebounceSeconds = debounceSeconds,
                        LibraryDatabaseFileName = _libraryDatabaseFileName
                    },
                    settings =>
                    {
                        settings.UseEqualToken = true;
                    }
                )
            });

            var successMessage = $"DVR monitor task scheduled to run at startup";
            if (!TryCreateAdminTask(TASK_NAME_DVR_MONITOR, td, successMessage, true))
            {
                return false;
            }

            Logger.LogInformation($"Starting DVR monitor task");
            TaskService.Instance.GetTask(TASK_NAME_DVR_MONITOR).Run();
            Logger.LogInformation($"DVR monitor task has been started", true);

            return true;
        }

        private bool TryCreateAdminTask(string taskPathAndName, TaskDefinition td, string successMessage, bool showMessageToUser)
        {
            // Stop the task first so that we can overwrite it
            var task = TaskService.Instance.GetTask(taskPathAndName);
            if (task != null)
            {
                task.Stop();
            }

            try
            {
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