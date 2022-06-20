using System;
using System.IO;
using System.Linq;

namespace PlexDvrWaker.Common
{
    /// <summary>
    /// Static class for logging
    /// </summary>
    internal static class Logger
    {
        private static readonly ConsoleColor DEFAULT_FOREGROUND_COLOR = ConsoleColor.Gray;
        private const int MAX_ROLLED_LOG_COUNT = 3;
        private const int MAX_LOG_SIZE = 1 * 1024 * 1024;  //1MB
        private static readonly object LOG_FILE_LOCK = new();

        private static string _logFileName;
        private static string LogFileName
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_logFileName))
                {
                    var assembly = typeof(Logger).Assembly;
                    var fullPath = assembly.Location;
                    var workingDirectory = Path.GetDirectoryName(fullPath);
                    _logFileName = Path.Combine(workingDirectory, assembly.GetName().Name + ".log");
                }
                return _logFileName;
            }
        }

        public static bool InteractiveMonitor { get; set; }
        public static bool Verbose { get; set; }
        public static int ProcessId { get; set; }

        public static void LogInformation(string message)
        {
            LogInformation(message, false);
        }

        public static void LogInformation(string message, bool showMessageToUser)
        {
            var dateNow = DateTime.Now;

            LogToFile(message, dateNow);
            LogToConsole($"{dateNow:s}\t{message}", (msg) =>
            {
                if (Verbose)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.WriteLine(msg);
                    Console.ForegroundColor = DEFAULT_FOREGROUND_COLOR;
                }

                if (showMessageToUser)
                {
                    Console.WriteLine(message);
                }
            });
        }

        public static void LogError(string message)
        {
            LogErrorToFile(message);
            LogToConsole($"ERROR: {message}", (msg) =>
            {
                LogErrorToConsole(msg);
            });
        }

        public static void LogErrorToFile(string message)
        {
            LogToFile($"ERROR: {message}");
        }

        private static void LogErrorToConsole(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(message);
            Console.ForegroundColor = DEFAULT_FOREGROUND_COLOR;
        }

        private static void LogToConsole(string message, Action<string> logAction)
        {
            if (InteractiveMonitor && !Program.RunInDevEnv)
            {
                // Keeps the "Press any key..." message at the bottom of the console
                Console.CursorTop -= 1;
            }

            logAction(message);

            if (InteractiveMonitor && !Program.RunInDevEnv)
            {
                Console.WriteLine(Plex.LibraryMonitor.PRESS_ANY_KEY_TO_STOP);
            }
        }

        public static void LogToFile(string message)
        {
            LogToFile(message, null);
        }

        private static void LogToFile(string message, DateTime? dateTime = null)
        {
            var logFileMsg = $"{dateTime ?? DateTime.Now:s}\t{ProcessId,10}\t{message}";

            lock (LOG_FILE_LOCK)
            {
                RollLogFileIfNeeded();
                File.AppendAllLines(LogFileName, new[] { logFileMsg });
            }
        }

        private static void RollLogFileIfNeeded()
        {
            if (File.Exists(LogFileName))
            {
                // Rolling log file logic taken from:  https://stackoverflow.com/a/33264202
                try
                {
                    var length = new FileInfo(LogFileName).Length;

                    if (length > MAX_LOG_SIZE)
                    {
                        var path = Path.GetDirectoryName(LogFileName);
                        var fileNameNoExt = Path.GetFileNameWithoutExtension(LogFileName);
                        var wildLogName = fileNameNoExt + "*" + Path.GetExtension(LogFileName);
                        var logFileList = Directory.GetFiles(path, wildLogName, SearchOption.TopDirectoryOnly);

                        if (logFileList.Length > 0)
                        {
                            // Only take files like logfilename.log and logfilename.0.log, so there also can be a maximum of 10 additional rolled files (0..9)
                            var rolledLogFileList = logFileList.Where(fileName => fileName.Length == (LogFileName.Length + 2)).ToArray();
                            Array.Sort(rolledLogFileList, 0, rolledLogFileList.Length);
                            if (rolledLogFileList.Length >= MAX_ROLLED_LOG_COUNT)
                            {
                                // Delete the last/oldest log file
                                File.Delete(rolledLogFileList[MAX_ROLLED_LOG_COUNT - 1]);
                                var list = rolledLogFileList.ToList();
                                list.RemoveAt(MAX_ROLLED_LOG_COUNT - 1);
                                rolledLogFileList = list.ToArray();
                            }

                            // Rename the remaining rolled files
                            var bareLogFilePath = Path.Combine(path, fileNameNoExt);
                            for (int i = rolledLogFileList.Length; i > 0; --i)
                            {
                                File.Move(rolledLogFileList[i - 1], bareLogFilePath + "." + i + Path.GetExtension(LogFileName));
                            }

                            // Rename the original file to a rolled file
                            var targetPath = bareLogFilePath + ".0" + Path.GetExtension(LogFileName);
                            File.Move(LogFileName, targetPath);
                        }
                    }
                }
                catch (Exception ex)
                {
                    // Log error directly to console to prevent possible infinite error loop
                    LogErrorToConsole(ex.ToString());
                }
            }
        }
    }
}
