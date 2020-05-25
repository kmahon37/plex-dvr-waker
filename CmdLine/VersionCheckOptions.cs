using System;
using System.Collections.Generic;
using CommandLine;
using CommandLine.Text;

namespace PlexDvrWaker.CmdLine
{
    [Verb("version-check",
        HelpText = "Checks for a newer version of this application.")]
    internal class VersionCheckOptions : ProgramOptions
    {
        [Option("non-interactive",
            Hidden = true,
            HelpText = "Determines whether to run the version check in non-interactive mode when running from the Windows Task Scheduler.")]
        public bool NonInteractive { get; set; }

        [Usage(ApplicationAlias = Program.APP_EXE)]
        public static IEnumerable<Example> Examples
        {
            get
            {
                return new List<Example>() {
                    new Example("Check for a new version", new VersionCheckOptions { })
                };
            }
        }
    }
}