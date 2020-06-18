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
            VersionCheckError = 4,
            Unknown = 99
        }

        internal const string APP_FRIENDLY_NAME = "Plex DVR Waker";
        internal const string APP_EXE = "PlexDvrWaker.exe";
        internal static readonly string APP_PATH_AND_EXE = typeof(Program).Assembly.Location;
        internal static readonly string APP_WORKING_DIRECTORY = Path.GetDirectoryName(APP_PATH_AND_EXE);

        internal static readonly bool RunInDevEnv = bool.Parse(Environment.GetEnvironmentVariable("RunInDevEnv") ?? bool.FalseString);

        public static int Main(string[] args)
        {
            // If no args, show help by default
            if (args == null || !args.Any())
            {
                args = new string[] { "help" };
            }

            return Parser.Default.ParseArguments<AddTaskOptions, ListOptions, MonitorOptions, VersionCheckOptions, ProgramOptions>(args)
                .MapResult(
                    (AddTaskOptions opts) => RunVerb(opts, RunAddTask),
                    (ListOptions opts) => RunVerb(opts, RunList),
                    (MonitorOptions opts) => RunVerb(opts, RunMonitor),
                    (VersionCheckOptions opts) => RunVerb(opts, RunVersionCheck),
                    errs => (int)ExitCode.ArgumentParseError
                );
        }

        private static int RunVerb<T>(T options, Func<T, int> verbFunc) where T: ProgramOptions
        {
            int exitCode = (int)ExitCode.Unknown;

            try
            {
                // Setup the logger first so we capture any errors
                SetupLogger(options);

                // Setup and check if the database file actually exists
                if (options is PlexOptions plexOpts &&
                    !(options is AddTaskOptions addTaskOpts && addTaskOpts.VersionCheck) &&
                    !SetupPlexLibraryDatabase(plexOpts))
                {
                    exitCode = (int)ExitCode.PlexLibraryDatabaseNotFound;
                }
                else
                {
                    exitCode = verbFunc(options);
                }
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
                var created = taskScheduler.CreateOrUpdateWakeUpTask(wakeupTime, options.WakeupOffsetSeconds.Value);
                if (!created)
                {
                    return (int)ExitCode.AccessDeniedDuringTaskCreation;
                }
            }

            if (options.Sync)
            {
                var created = taskScheduler.CreateOrUpdateDVRSyncTask(options.SyncIntervalMinutes.Value, options.WakeupOffsetSeconds.Value);
                if (!created)
                {
                    return (int)ExitCode.AccessDeniedDuringTaskCreation;
                }
            }

            if (options.Monitor)
            {
                var created = taskScheduler.CreateOrUpdateDVRMonitorTask(options.MonitorDebounceSeconds.Value, options.WakeupOffsetSeconds.Value);
                if (!created)
                {
                    return (int)ExitCode.AccessDeniedDuringTaskCreation;
                }
            }

            if (options.VersionCheck)
            {
                var created = taskScheduler.CreateOrUpdateVersionCheckTask(options.VersionCheckDays.Value);
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

            using (var libraryMonitor = new Plex.LibraryMonitor(plexDataAdapter, taskScheduler, options.DebounceSeconds.Value, options.OffsetSeconds.Value))
            {
                libraryMonitor.Enabled = true;

                Console.WriteLine("Started monitoring the Plex library database");
                Console.WriteLine(Plex.LibraryMonitor.PRESS_ANY_KEY_TO_STOP);

                Logger.InteractiveMonitor = !options.NonInteractive;
                Logger.LogToFile($"InteractiveMonitor: {Logger.InteractiveMonitor}");

                if (Logger.InteractiveMonitor && !RunInDevEnv)
                {
                    Console.ReadKey(true);
                }
                else
                {
                    // Running as a scheduled task, so wait indefinitely
                    Task.Delay(-1).Wait();
                }
            }

            return (int)ExitCode.Success;
        }

        private static int RunVersionCheck(VersionCheckOptions options)
        {
            ExitCode exitCode;

            void waitBeforeClosing()
            {
                if (options.NonInteractive)
                {
                    Console.WriteLine();

                    // Wait a couple seconds for user to see message before automatically closing
                    for (int i = 5; i > 0; i--)
                    {
                        Console.WriteLine($"Closing in {i} seconds...");
                        Console.CursorTop -= 1;
                        Task.Delay(TimeSpan.FromSeconds(1)).Wait();
                    }
                }
            }

            if (options.NonInteractive)
            {
                var banner = $"**  {APP_FRIENDLY_NAME} - Version Check  **";
                var border = new string('*', banner.Length);
                Console.WriteLine(border);
                Console.WriteLine(banner);
                Console.WriteLine(border);
            }

            Console.WriteLine("Fetching latest version information...");

            if (VersionUtils.TryGetLatestVersion(out var latestVersion))
            {
                var assemblyVersion = VersionUtils.GetAssemblyVersion();
                if (latestVersion > assemblyVersion)
                {
                    Console.WriteLine($"Current version: {assemblyVersion}");
                    Console.WriteLine($"Latest version:  {latestVersion}");
                    Console.WriteLine($"A newer version of {APP_FRIENDLY_NAME} is available for download from:");
                    Console.WriteLine("https://github.com/kmahon37/plex-dvr-waker/releases/latest");

                    if (options.NonInteractive)
                    {
                        Console.WriteLine();
                        Console.WriteLine("Press any key to exit");
                        Console.ReadKey(true);
                    }
                }
                else
                {
                    Console.WriteLine("You already have the latest version.");
                    waitBeforeClosing();
                }

                exitCode = ExitCode.Success;
            }
            else
            {
                Logger.LogError("Unable to retrieve latest version information at this time.  Please try again later.");
                waitBeforeClosing();
                exitCode = ExitCode.VersionCheckError;
            }

            return (int)exitCode;
        }

        private static void SetupLogger(ProgramOptions options)
        {
            // Configure the logger
            Logger.ProcessId = System.Diagnostics.Process.GetCurrentProcess().Id;
            Logger.Verbose = options.Verbose;

            // Log the command line and arguments that started this process
            Logger.LogToFile("--------------------------------------------------------------");
            Logger.LogToFile(string.Concat(
                APP_EXE,
                " ",
                Parser.Default.FormatCommandLine(options, ConfigureUnParserSettings)
            ));
            Logger.LogToFile($"{APP_FRIENDLY_NAME} version: " + VersionUtils.GetAssemblyVersion());
            Logger.LogToFile(".Net Core version: " + Environment.Version);
        }

        public static void ConfigureUnParserSettings(UnParserSettings settings)
        {
            settings.UseEqualToken = true;
            settings.ShowHidden = true;
            settings.SkipDefault = true;
        }

        private static bool SetupPlexLibraryDatabase(PlexOptions options)
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
