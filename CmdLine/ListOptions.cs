using System.Collections.Generic;
using CommandLine;
using CommandLine.Text;

namespace PlexDvrWaker.CmdLine
{
    [Verb("list", HelpText = "Prints upcoming scheduled recordings to standard output.")]
    internal class ListOptions : ProgramOptions
    {
        [Usage(ApplicationAlias = "dotnet PlexDvrWaker.dll")]
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