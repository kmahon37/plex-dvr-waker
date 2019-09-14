using CommandLine;

namespace PlexDvrWaker.CmdLine
{
    internal class ProgramOptions
    {
        internal const string DEFAULT_PLEX_DATA_PATH = "%LOCALAPPDATA%";

        [Option("plexdata",
            MetaValue = "PATH",
            Default = DEFAULT_PLEX_DATA_PATH,
            HelpText = "The path where Plex stores its local application data.")]
        public string PlexDataPath { get; set; }

        [Option("verbose",
            HelpText = "Prints all messages to standard output.")]
        public bool Verbose { get; set; }
    }
}