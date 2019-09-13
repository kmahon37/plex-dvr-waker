using CommandLine;
using PlexDvrWaker.CmdLine;
using PlexDvrWaker.Common;
using System;
using System.Linq;

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

            Plex.TaskScheduler.PlexDataPath = options.PlexDataPathIsDefault ? null : options.PlexDataPath;

            if (options.Wakeup)
            {
                var da = GetPlexDataAdapter(options.PlexDataPath);
                var wakeupTime = da.GetNextScheduledRecordingTime();

                if (wakeupTime.HasValue)
                {
                    Plex.TaskScheduler.CreateOrUpdateWakeUpTask(wakeupTime.Value);
                }
                else
                {
                    // No shows to record, remove wakeup task, if exists
                    Plex.TaskScheduler.DeleteWakeUpTask();
                    Console.WriteLine("Wakeup task cannot be created because there are no upcoming scheduled recordings.");
                }
            }

            if (options.Sync)
            {
                var created = Plex.TaskScheduler.CreateOrUpdateDVRSyncTask(options.SyncIntervalMinutes.Value);
                if (!created)
                {
                    return (int)ExitCode.AccessDeniedDuringTaskCreation;
                }
            }

            if (options.Monitor)
            {
                var created = Plex.TaskScheduler.CreateOrUpdateDVRMonitorTask(options.DebounceSeconds.Value);
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

            var da = GetPlexDataAdapter(options.PlexDataPath);
            da.PrintScheduledRecordings();

            return (int)ExitCode.Success;
        }

        private static int RunMonitor(MonitorOptions options)
        {
            SetupLogger(options);

            var da = GetPlexDataAdapter(options.PlexDataPath);
            using (var pm = new Plex.LibraryMonitor(da, TimeSpan.FromSeconds(options.DebounceSeconds.Value)))
            {
                pm.Enabled = true;

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
            Logger.LogToFile(DateTime.Now.ToString("s") + "\t" + Logger.ProcessId.ToString().PadLeft(10) + "\t" + APPLICATION_ALIAS + " " + Parser.Default.FormatCommandLine(options, s => s.UseEqualToken = true));
        }

        private static Plex.DataAdapter GetPlexDataAdapter(string plexDataPath)
        {
            return new Plex.DataAdapter(plexDataPath);
        }

    }
}
