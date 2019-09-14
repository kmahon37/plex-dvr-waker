using CommandLine;
using PlexDvrWaker.CmdLine;
using PlexDvrWaker.Common;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace PlexDvrWaker
{
    public static class Program
    {
        private enum ExitCode : int
        {
            Success = 0,
            ArgumentParseError = 1,
            AccessDeniedDuringTaskCreation = 2
        }

        internal const string APPLICATION_ALIAS = "dotnet PlexDvrWaker.dll";

        public static int Main(string[] args)
        {
            // If no args, show help by default
            if (args == null || !args.Any())
            {
                args = new string[] { "help" };
            }

            return Parser.Default.ParseArguments<AddTaskOptions, ListOptions, MonitorOptions, ProgramOptions>(args)
                .MapResult(
                    (AddTaskOptions opts) => RunAddTask(opts),
                    (ListOptions opts) => RunList(opts),
                    (MonitorOptions opts) => RunMonitor(opts),
                    errs => (int)ExitCode.ArgumentParseError
                );
        }

        private static int RunAddTask(AddTaskOptions options)
        {
            SetupLogger(options);

            var taskScheduler = new Plex.TaskScheduler(options.PlexDataPath);

            if (options.Wakeup)
            {
                if (options.WakeupRefreshDelaySeconds.HasValue && options.WakeupRefreshDelaySeconds.Value > 0)
                {
                    // Used to wait until after the current recording has started before we refresh the next scheduled recording time
                    Logger.LogInformation($"Waiting {options.WakeupRefreshDelaySeconds.Value} seconds before refreshing the next scheduled recording time.");
                    Task.Delay(TimeSpan.FromSeconds(options.WakeupRefreshDelaySeconds.Value)).Wait();
                }

                var plexDataAdapter = new Plex.DataAdapter(options.PlexDataPath);
                var wakeupTime = plexDataAdapter.GetNextScheduledRecordingTime();

                if (wakeupTime.HasValue)
                {
                    var created = taskScheduler.CreateOrUpdateWakeUpTask(wakeupTime.Value);
                    if (!created)
                    {
                        return (int)ExitCode.AccessDeniedDuringTaskCreation;
                    }
                }
                else
                {
                    // No shows to record, remove wakeup task, if exists
                    taskScheduler.DeleteWakeUpTask();
                    Console.WriteLine("Wakeup task cannot be created because there are no upcoming scheduled recordings.");
                }
            }

            if (options.Sync)
            {
                var created = taskScheduler.CreateOrUpdateDVRSyncTask(options.SyncIntervalMinutes.Value);
                if (!created)
                {
                    return (int)ExitCode.AccessDeniedDuringTaskCreation;
                }
            }

            if (options.Monitor)
            {
                var created = taskScheduler.CreateOrUpdateDVRMonitorTask(options.MonitorDebounceSeconds.Value);
                if (!created)
                {
                    return (int)ExitCode.AccessDeniedDuringTaskCreation;
                }
            }

            return (int)ExitCode.Success;
        }

        private static int RunList(ListOptions options)
        {
            SetupLogger(options);

            var plexDataAdapter = new Plex.DataAdapter(options.PlexDataPath);
            plexDataAdapter.PrintScheduledRecordings();

            return (int)ExitCode.Success;
        }

        private static int RunMonitor(MonitorOptions options)
        {
            SetupLogger(options);

            var plexDataAdapter = new Plex.DataAdapter(options.PlexDataPath);
            var taskScheduler = new Plex.TaskScheduler(options.PlexDataPath);
            using (var libraryMonitor = new Plex.LibraryMonitor(plexDataAdapter, taskScheduler, options.DebounceSeconds.Value))
            {
                libraryMonitor.Enabled = true;

                Console.WriteLine("Started monitoring the Plex library database");
                Console.WriteLine(Plex.LibraryMonitor.PRESS_ANY_KEY_TO_STOP);
                Console.ReadKey(true);
            }

            return (int)ExitCode.Success;
        }

        private static void SetupLogger<T>(T options) where T : ProgramOptions
        {
            Logger.ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;
            Logger.Verbose = options.Verbose;
            Logger.InteractiveMonitor = (typeof(T) == typeof(MonitorOptions));
            Logger.LogToFile(string.Concat(
                DateTime.Now.ToString("s"),
                "\t",
                Logger.ProcessId.ToString().PadLeft(10),
                "\t",
                APPLICATION_ALIAS,
                " ",
                Parser.Default.FormatCommandLine(options, s => {
                    s.UseEqualToken = true;
                    s.ShowHidden = true;
                })
            ));
        }
    }
}
