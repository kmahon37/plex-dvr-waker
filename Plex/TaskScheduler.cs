using CommandLine;
using Microsoft.Win32.TaskScheduler;
using PlexDvrWaker.CmdLine;
using PlexDvrWaker.Common;
using System;
using System.IO;

namespace PlexDvrWaker.Plex
{
    internal static class TaskScheduler
    {
        private const string TASK_SCHEDULER_FOLDER = "Plex DVR Waker";
        private const string TASK_NAME_DVR_WAKE = TASK_SCHEDULER_FOLDER + "\\DVR wake";
        private const string TASK_NAME_DVR_SYNC = TASK_SCHEDULER_FOLDER + "\\DVR sync";
        private const string TASK_NAME_DVR_MONITOR = TASK_SCHEDULER_FOLDER + "\\DVR monitor";

        public static string PlexDataPath { get; set; }

        public static void CreateOrUpdateWakeUpTask(DateTime startTime)
        {
            CreateOrUpdateWakeUpTask(startTime, true);
        }

        public static void CreateOrUpdateWakeUpTask(DateTime startTime, bool showMessageToUser)
        {
            Logger.LogInformation($"Creating/updating wakeup task: {TASK_NAME_DVR_WAKE}");

            var td = TaskService.Instance.NewTask();
            td.RegistrationInfo.Description = "This task will wake the computer for the next Plex DVR recording.";
            td.Principal.LogonType = TaskLogonType.InteractiveToken;
            td.Settings.Hidden = true;
            td.Settings.AllowDemandStart = true;
            td.Settings.DisallowStartIfOnBatteries = false;
            td.Settings.StopIfGoingOnBatteries = false;
            td.Settings.WakeToRun = true;
            td.Settings.DeleteExpiredTaskAfter = TimeSpan.FromSeconds(1);

            var trigger = new TimeTrigger()
            {
                // Trigger a few seconds earlier to give computer time to wakeup
                StartBoundary = startTime.AddSeconds(-15),
                EndBoundary = startTime.AddSeconds(5)
            };
            td.Triggers.Add(trigger);

            td.Actions.Add(new ExecAction()
            {
                //rundll32.exe without arguments is a no-op
                Path = "rundll32.exe"
            });

            TaskService.Instance.RootFolder.RegisterTaskDefinition(TASK_NAME_DVR_WAKE, td);

            Logger.LogInformation($"Wakeup task scheduled for {trigger.StartBoundary}", showMessageToUser);
        }

        public static void DeleteWakeUpTask()
        {
            Logger.LogInformation($"Deleting wakeup task (if exists): {TASK_NAME_DVR_WAKE}");

            TaskService.Instance.RootFolder.DeleteTask(TASK_NAME_DVR_WAKE, false);

            Logger.LogInformation("Wakeup task deleted");
        }

        public static bool CreateOrUpdateDVRSyncTask(int intervalMinutes)
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

            var fullPath = typeof(TaskScheduler).Assembly.Location;
            var workingDirectory = Path.GetDirectoryName(fullPath);
            var dllName = Path.GetFileName(fullPath);
            td.Actions.Add(new ExecAction()
            {
                Path = "dotnet.exe",
                WorkingDirectory = workingDirectory,
                Arguments = dllName + " " + Parser.Default.FormatCommandLine(new AddTaskOptions
                {
                    Wakeup = true,
                    PlexDataPath = PlexDataPath
                },
                settings =>
                {
                    settings.UseEqualToken = true;
                })
            });

            try
            {
                TaskService.Instance.RootFolder.RegisterTaskDefinition(TASK_NAME_DVR_SYNC, td);
                Logger.LogInformation($"DVR sync task scheduled for every {intervalMinutes} minute{(intervalMinutes > 1 ? "s" : "")}", true);
            }
            catch (UnauthorizedAccessException)
            {
                LogAccessDenied(TASK_NAME_DVR_SYNC);
                return false;
            }

            return true;
        }

        public static bool CreateOrUpdateDVRMonitorTask(int debounceSeconds)
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

            td.Triggers.Add(new BootTrigger
            {
                // Delay after logon to give Plex time to startup and update things
                Delay = TimeSpan.FromMinutes(5)
            });

            var fullPath = typeof(TaskScheduler).Assembly.Location;
            var workingDirectory = Path.GetDirectoryName(fullPath);
            var dllName = Path.GetFileName(fullPath);
            td.Actions.Add(new ExecAction()
            {
                Path = "dotnet.exe",
                WorkingDirectory = workingDirectory,
                Arguments = dllName + " " + Parser.Default.FormatCommandLine(
                    new MonitorOptions
                    {
                        DebounceSeconds = debounceSeconds,
                        PlexDataPath = PlexDataPath
                    },
                    settings =>
                    {
                        settings.UseEqualToken = true;
                    }
                )
            });

            try
            {
                TaskService.Instance.RootFolder.RegisterTaskDefinition(TASK_NAME_DVR_MONITOR, td);
                Logger.LogInformation($"DVR monitor task scheduled to run at log on", true);
            }
            catch (UnauthorizedAccessException)
            {
                LogAccessDenied(TASK_NAME_DVR_MONITOR);
                return false;
            }

            Logger.LogInformation($"Starting DVR monitor task");
            TaskService.Instance.GetTask(TASK_NAME_DVR_MONITOR).Run();
            Logger.LogInformation($"DVR monitor task has been started", true);

            return true;
        }

        private static void LogAccessDenied(string taskName)
        {
            Logger.LogError($"Access is denied.  Try running as an Administrator.  Administrator rights are needed in order to create the '{taskName}' task so that it runs hidden without flashing a console window every time the task is triggered.");
        }

    }
}