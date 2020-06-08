using System;
using CommandLine;

namespace PlexDvrWaker.CmdLine
{
    internal class PlexOptions : ProgramOptions
    {
        [Option("database",
            MetaValue = "FILE",
            HelpText = @"(Default: <Plex local application data path or %LOCALAPPDATA%>\Plex Media Server\Plug-in Support\Databases\com.plexapp.plugins.library.db) The Plex library database file to use for custom Plex installations.")]
        public string LibraryDatabaseFileName { get; set; }

        protected static void VerifyMinimumValue(string paramName, int? value, int minimumValue)
        {
            if (value.HasValue && value.Value < minimumValue)
            {
                throw new ArgumentOutOfRangeException(paramName, value, $"The value must be greater than or equal to {minimumValue}.");
            }
        }

    }
}