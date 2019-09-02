using System;

namespace PlexDvrWaker.Common
{
    internal static class Logger
    {
        private static readonly ConsoleColor _defaultForegroundColor = ConsoleColor.Gray;

        public static bool Verbose { get; set; }

        public static void LogInformation(string message)
        {
            LogInformation(message, false);
        }

        public static void LogInformation(string message, bool showMessageToUser)
        {
            if (Verbose)
            {
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.WriteLine($"{DateTime.Now}\t{message}");
                Console.ForegroundColor = _defaultForegroundColor;
            }

            if (showMessageToUser)
            {
                Console.WriteLine(message);
            }
        }

        public static void LogError(string message)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.Error.WriteLine($"ERROR: {message}");
            Console.ForegroundColor = _defaultForegroundColor;
        }
    }
}