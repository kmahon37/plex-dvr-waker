using Microsoft.Win32.TaskScheduler;
using PlexDvrWaker.Common;
using System;
using System.IO;

namespace PlexDvrWaker.Plex
{
    internal static class TaskScheduler
    {
        private const string TASK_SCHEDULER_FOLDER = "Plex DVR Waker";

        public static void CreateOrUpdateWakeUpTask(DateTime startTime)
        {
            var taskName = $@"{TASK_SCHEDULER_FOLDER}\DVR wake";

            Logger.LogInformation($"Creating/updating wakeup task: {taskName}");

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

            TaskService.Instance.RootFolder.RegisterTaskDefinition(taskName, td);

            Logger.LogInformation($"Wakeup task scheduled for {trigger.StartBoundary}");
        }

        public static void CreateOrUpdateDVRSyncTask(uint intervalMinutes)
        {
            var taskName = $@"{TASK_SCHEDULER_FOLDER}\DVR sync";

            Logger.LogInformation($"Creating/updating DVR sync task: {taskName}");

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
                Arguments = $"{dllName} --wakeup"
            });

            try
            {
                TaskService.Instance.RootFolder.RegisterTaskDefinition(taskName, td);
                Logger.LogInformation($"DVR sync task scheduled for every {intervalMinutes} minute{(intervalMinutes > 1 ? "s" : "")}");
            }
            catch (UnauthorizedAccessException)
            {
                Logger.LogError($"Access is denied.  Try running as an Administrator.  Administrator rights are needed in order to create the DVR sync task so that it runs hidden without flashing a console window every time the task is triggered.");
            }
        }

    }
}