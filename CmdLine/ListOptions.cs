using System.Collections.Generic;
using CommandLine;
using CommandLine.Text;

namespace PlexDvrWaker.CmdLine
{
    [Verb("list", HelpText = "Prints upcoming scheduled recordings to standard output.")]
    internal class ListOptions : ProgramOptions
    {
        [Option("maintenance",
            HelpText = "Prints the next Plex maintenance time to standard output.")]
        public bool ShowMaintenance { get; set; }

        [Usage(ApplicationAlias = Program.APPLICATION_ALIAS)]
        public static IEnumerable<Example> Examples
        {
            get
            {
                return new List<Example>() {
                    new Example("Print upcoming scheduled recordings", new ListOptions { })
                };
            }
        }
    }
}