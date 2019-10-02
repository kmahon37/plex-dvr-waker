using CommandLine;

namespace PlexDvrWaker.CmdLine
{
    internal class ProgramOptions
    {
        [Option("verbose",
            HelpText = "Prints all messages to standard output.")]
        public bool Verbose { get; set; }
    }
}