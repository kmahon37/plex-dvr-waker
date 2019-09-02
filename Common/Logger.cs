using System;
using System.IO;

namespace PlexDvrWaker.Common
{
    internal static class Logger
    {
        private static readonly ConsoleColor _defaultForegroundColor = ConsoleColor.Gray;

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

        public static bool Verbose { get; set; }

        public static void LogInformation(string message)
        {
            LogInformation(message, false);
        }

        public static void LogInformation(string message, bool showMessageToUser)
        {
            var logMsg = $"{DateTime.Now}\t{message}";

            LogToFile(logMsg);

            if (Verbose)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine(logMsg);
                Console.ForegroundColor = _defaultForegroundColor;
            }

            if (showMessageToUser)
            {
                Console.WriteLine(message);
            }
        }

        public static void LogError(string message)
        {
            var logMsg = $"ERROR: {message}";

            LogToFile(logMsg);

            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine(logMsg);
            Console.ForegroundColor = _defaultForegroundColor;
        }

        public static void LogToFile(string message)
        {
            File.AppendAllLines(LogFileName, new[] { message });
        }
    }
}