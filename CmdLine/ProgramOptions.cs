using CommandLine;

namespace PlexDvrWaker.CmdLine
{
    internal class ProgramOptions
    {
        [Option("task",
            MetaValue = "NAME",
            Hidden = true,
            HelpText = @"The name of the Windows Task Scheduler task that is running to use for logging.")]
        public string TaskName { get; set; }

        [Option("verbose",
            HelpText = "Prints all messages to standard output.")]
        public bool Verbose { get; set; }
    }
}
