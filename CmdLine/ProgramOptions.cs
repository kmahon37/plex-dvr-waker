using CommandLine;

namespace PlexDvrWaker.CmdLine
{
    internal class ProgramOptions
    {
        [Option("database",
            MetaValue = "FILE",
            HelpText = @"(Default: <Plex local application data path or %LOCALAPPDATA%>\Plex Media Server\Plug-in Support\Databases\com.plexapp.plugins.library.db) The Plex library database file to use for custom Plex installations.")]
        public string LibraryDatabaseFileName { get; set; }

        [Option("verbose",
            HelpText = "Prints all messages to standard output.")]
        public bool Verbose { get; set; }
    }
}