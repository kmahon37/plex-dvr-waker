using CommandLine;

namespace PlexDvrWaker.CmdLine
{
    internal class ProgramOptions
    {
        private const string DEFAULT_PLEX_DATA_PATH = "%LOCALAPPDATA%";

        private string _plexDataPath;
        [Option("plexdata",
            MetaValue = "PATH",
            Default = DEFAULT_PLEX_DATA_PATH,
            HelpText = "The path where Plex stores its local application data.")]
        public string PlexDataPath
        {
            get
            {
                return _plexDataPath;
            }
            set
            {
                _plexDataPath = value;
                PlexDataPathIsDefault = string.Equals(value, DEFAULT_PLEX_DATA_PATH, System.StringComparison.InvariantCultureIgnoreCase);
            }
        }

        [Option("verbose",
            HelpText = "Prints all messages to standard output.")]
        public bool Verbose { get; set; }


        internal bool PlexDataPathIsDefault { get; private set; }
    }
}