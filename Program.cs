using CommandLine;
using PlexDvrWaker.CmdLine;
using PlexDvrWaker.Common;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace PlexDvrWaker
{
    public static class Program
    {
        private enum ExitCode : int
        {
            Success = 0,
            ArgumentParseError = 1,
            AccessDeniedDuringTaskCreation = 2,
            PlexLibraryDatabaseNotFound = 3,
            Unknown = 99
        }

        internal const string EXE_NAME = "PlexDvrWaker.exe";

        public static int Main(string[] args)
        {
            // If no args, show help by default
            if (args == null || !args.Any())
            {
                args = new string[] { "help" };
            }

            return Parser.Default.ParseArguments<AddTaskOptions, ListOptions, MonitorOptions, ProgramOptions>(args)
                .MapResult(
                    (AddTaskOptions opts) => RunVerb(opts, RunAddTask),
                    (ListOptions opts) => RunVerb(opts, RunList),
                    (MonitorOptions opts) => RunVerb(opts, RunMonitor),
                    errs => (int)ExitCode.ArgumentParseError
                );
        }

        private static int RunVerb<T>(T options, Func<T, int> verbFunc) where T: ProgramOptions
        {
            int exitCode = (int)ExitCode.Unknown;

            try
            {
                if (!TryInitializeVerb(options, out exitCode))
                {
                    return exitCode;
                }

                exitCode = verbFunc(options);
            }
            catch (Exception ex)
            {
                // Global error handler
                Logger.InteractiveMonitor = false;
                Logger.LogError(ex.ToString());
                exitCode = (int)ExitCode.Unknown;
            }
            finally
            {
                Logger.InteractiveMonitor = false;
                var exitCodeName = Enum.GetName(typeof(ExitCode), exitCode);
                Logger.LogInformation($"Exit code: {exitCode} ({exitCodeName ?? "???"})");
            }

            return exitCode;
        }

        private static int RunAddTask(AddTaskOptions options)
        {
            var taskScheduler = new Plex.TaskScheduler();

            if (options.Wakeup)
            {
                if (options.WakeupRefreshDelaySeconds.HasValue && options.WakeupRefreshDelaySeconds.Value > 0)
                {
                    // Used to wait until after the current recording has started before we refresh the next scheduled recording time
                    Logger.LogInformation($"Waiting {options.WakeupRefreshDelaySeconds.Value} seconds before refreshing the next scheduled recording time.");
                    Task.Delay(TimeSpan.FromSeconds(options.WakeupRefreshDelaySeconds.Value)).Wait();
                }

                var plexDataAdapter = new Plex.DataAdapter();
                var wakeupTime = plexDataAdapter.GetNextWakeupTime();
                var created = taskScheduler.CreateOrUpdateWakeUpTask(wakeupTime);
                if (!created)
                {
                    return (int)ExitCode.AccessDeniedDuringTaskCreation;
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
            var plexDataAdapter = new Plex.DataAdapter();
            plexDataAdapter.PrintScheduledRecordings();

            if (options.ShowMaintenance)
            {
                if (!options.Verbose)
                {
                    Console.WriteLine();
                }
                plexDataAdapter.PrintNextMaintenanceTime();
            }

            return (int)ExitCode.Success;
        }

        private static int RunMonitor(MonitorOptions options)
        {
            var plexDataAdapter = new Plex.DataAdapter();
            var taskScheduler = new Plex.TaskScheduler();

            using (var libraryMonitor = new Plex.LibraryMonitor(plexDataAdapter, taskScheduler, options.DebounceSeconds.Value))
            {
                libraryMonitor.Enabled = true;

                Console.WriteLine("Started monitoring the Plex library database");
                Console.WriteLine(Plex.LibraryMonitor.PRESS_ANY_KEY_TO_STOP);

                Logger.InteractiveMonitor = !options.NonInteractive;
                Logger.LogToFile($"InteractiveMonitor: {Logger.InteractiveMonitor}");

                Console.ReadKey(true);
            }

            return (int)ExitCode.Success;
        }

        private static bool TryInitializeVerb(ProgramOptions options, out int exitCode)
        {
            // Setup the logger first so we capture any errors
            SetupLogger(options);

            // Setup and check if the database file actually exists
            if (!SetupPlexLibraryDatabase(options))
            {
                exitCode = (int)ExitCode.PlexLibraryDatabaseNotFound;
                return false;
            }

            exitCode = (int)ExitCode.Success;
            return true;
        }

        private static void SetupLogger(ProgramOptions options)
        {
            // Configure the logger
            Logger.ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;
            Logger.Verbose = options.Verbose;

            // Log the command line and arguments that started this process
            Logger.LogToFile("--------------------------------------------------------------");
            Logger.LogToFile(string.Concat(
                EXE_NAME,
                " ",
                Parser.Default.FormatCommandLine(options, ConfigureUnParserSettings)
            ));
            Logger.LogToFile("Version: " + Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion);
        }

        public static void ConfigureUnParserSettings(UnParserSettings settings)
        {
            settings.UseEqualToken = true;
            settings.ShowHidden = true;
            settings.SkipDefault = true;
        }

        private static bool SetupPlexLibraryDatabase(ProgramOptions options)
        {
            // Override the default Plex library database file name, if specified
            if (!string.IsNullOrWhiteSpace(options.LibraryDatabaseFileName))
            {
                Plex.Settings.LibraryDatabaseFileName = options.LibraryDatabaseFileName;
            }

            // Make sure the database file exists
            if (!File.Exists(Plex.Settings.LibraryDatabaseFileName))
            {
                Logger.LogError("Unable to find the Plex library database file: " + Plex.Settings.LibraryDatabaseFileName);
                return false;
            }

            // Log the database file name we are using
            Logger.LogInformation($"Using Plex library database file:  {Plex.Settings.LibraryDatabaseFileName}");

            return true;
        }
    }
}
