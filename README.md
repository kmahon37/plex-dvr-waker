# Plex DVR Waker
Are you fed up with Plex's lack of functionality to wakeup your computer to record a show?  C'mon, even a VCR from the '80s could do that.

Plex DVR Waker is a simple command-line tool for waking the computer before the next scheduled recording.  It works by creating a Windows Task Scheduler task that can sync with and/or monitor the Plex library database and then schedule another task to wakeup the computer before the next scheduled recording.

> *DISCLAIMER:*
> _I reverse engineered the Plex library and EPG databases in order to piece together enough functionality to be able to recognize scheduled recordings and previously recorded TV shows and movies (so that it doesn't wake the computer for no reason).  I just used whatever data that I could find and interpret in the databases to identify TV shows and movies.  So, while this tools works fairly well, it clearly does not support all the advanced features built-in to Plex._

## Supported Features
- Syncs with and/or monitors the Plex library database and schedules a wakeup task
- Wakes up the computer 15 seconds before the next scheduled recording
- Recording start time offset is taken into account
- Recognizes previously recorded TV shows and movies (as best as possible) so that it doesn't inadvertently wake the computer for something you already have recorded.
- Prints out upcoming scheduled recordings
- Support for custom Plex application data path

> _NOTE: It does *not* support any other Plex "advanced record options" (ie: Prefer HD, Replace lower resolution items, etc)._

## Requirements
- Windows 7/8/10
- Windows Task Scheduler
- Windows .NET Core 2.2+ Runtime ([download from Microsoft](https://dotnet.microsoft.com/download))
- "Run as administrator" rights
  - Administrator rights are needed in order to create the sync and monitor tasks so that they run hidden without popping up a console window every time the task is triggered.  This is a Windows Task Scheduler limitation.

## Installation
TODO

## Quick Start
If you want a quick, hassle-free way to get started, simply run the following `sync` command.  It will synchronize with the Plex library database every 15 minutes and create/update a Windows Task Scheduler task to wakeup the computer before your next scheduled recording.
```
dotnet PlexDvrWaker.dll add-task --sync
```

## Command-line Arguments

- [Display help](#cmdline-help)
- [Adding a wakeup, sync, or monitor task](#cmdline-add-task)
- [Display upcoming scheduled recordings](#cmdline-list)
- [Monitor Plex library database (interactive mode)](#cmdline-monitor)

### Displaying help <a name="cmdline-help"></a>
The main help screen displays the top-level help for the available commands.  You can also view specific detailed help for each command by using the syntax: `dotnet PlexDvrWaker.dll help <command_name>`.

*Usage:*
```
dotnet PlexDvrWaker.dll
dotnet PlexDvrWaker.dll help
dotnet PlexDvrWaker.dll help add-task
dotnet PlexDvrWaker.dll help list
dotnet PlexDvrWaker.dll help monitor
```

*Example output:*
```
  add-task    Add/update Windows Task Scheduler tasks for waking the computer for the next scheduled
              recording, syncing the next wakeup, or monitoring the Plex library database for changes.

  list        Prints upcoming scheduled recordings to standard output.

  monitor     Monitors the Plex library database for changes and updates the 'wakeup' task based on the
              next scheduled recording time.

  help        Display more information on a specific command.

  version     Display version information.
```

### Adding a wakeup, sync, or monitor task <a name="cmdline-add-task"></a>
Plex DVR Waker works primarily by using Windows Task Scheduler tasks to keep up-to-date with the Plex library database and keep a wakeup task scheduled for your next recording time.  You can use either the `sync` or `monitor` task, or both at the same time - the choice is yours based on your needs.

The `sync` task will poll the Plex library database at a specified interval (default every 15 minutes) and create/update the `wakeup` task for you automatically.  This is a nice lightweight solution, however it could theoretically miss something if, for example, you schedule a recording to start in 8 minutes and then immediately put the computer to sleep before the next `sync` task can run.

The `monitor` task will watch the Plex library database files for changes and also create/update the `wakeup` task for you automatically.  Plex can sometimes cause a lot of changes to its library database files in a short period of time which will cause the `wakeup` task to be updated frequently.  It is a fairly quick process to update the `wakeup` task, but just note that it may run a good bit more often.  You can use the `--debouce=SECONDS` option to adjust the frequency of runs.  The plus side is that you are basically guaranteed your `wakeup` task will always be set appropriately.

*Usage:*
```
dotnet PlexDvrWaker.dll add-task --wakeup [--plexdata=PATH] [--verbose]
dotnet PlexDvrWaker.dll add-task --sync [--interval=MINUTES] [--plexdata=PATH] [--verbose]
dotnet PlexDvrWaker.dll add-task --monitor [--debounce=SECONDS] [--plexdata=PATH] [--verbose]
```

*Arguments:*
```
  --wakeup              Creates or updates a Windows Task Scheduler 'wakeup' task that will wakeup the
                        computer 15 seconds before the next scheduled recording time.

  --sync                Creates or updates a Windows Task Scheduler 'sync' task to run at the specified
                        interval and sync the 'wakeup' task with the next scheduled recording time.

  --interval=MINUTES    (Default: 15) The interval to sync the 'wakeup' task with the next scheduled
                        recording time.

  --monitor             Creates or updates a Windows Task Scheduler 'monitor' task to run in the background
                        when the computer starts up that will monitor the Plex library database file for
                        changes and update the 'wakeup' task based on the next scheduled recording time.

  --debounce=SECONDS    (Default: 5) Since the Plex library database can change multiple times within a
                        short time, upon the first change it will wait the specified number of seconds
                        before it updates the Task Scheduler 'wakeup' task with the next scheduled recording
                        time.

  --plexdata=PATH       (Default: %LOCALAPPDATA%) The path where Plex stores its local application data.

  --verbose             Prints all messages to standard output.
```

*Example output (--wakeup):*
```
Wakeup task scheduled for 9/14/2019 12:59:45 PM
```

*Example output (--sync):*
```
DVR sync task scheduled for every 15 minutes
```

*Example output (--monitor):*
```
DVR monitor task scheduled to run at log on
DVR monitor task has been started
```

### Display upcoming scheduled recordings <a name="cmdline-list"></a>
You can display the upcoming scheduled recordings that are recognized by Plex DVR Waker.  This is useful to compare to your actual recording schedule in Plex.  In theory, the two schedules should match (_see disclaimer above_).

*Usage:*
```
dotnet PlexDvrWaker.dll list [--plexdata=PATH] [--verbose]
```

*Arguments:*
```
  --plexdata=PATH    (Default: %LOCALAPPDATA%) The path where Plex stores its local application data.

  --verbose          Prints all messages to standard output.
```

*Example output:*
```
Start Time              End Time                Title
--------------------    --------------------    --------------------------------------------------
9/14/2019 1:00:00 PM    9/14/2019 2:00:00 PM    MacGyver (2016) - S01E22 - The Assassin
9/16/2019 6:30:00 PM    9/16/2019 7:00:00 PM    The Big Bang Theory - S09E22 - The Fermentation Bifurcation
9/16/2019 7:00:00 PM    9/16/2019 7:30:00 PM    The Big Bang Theory - S04E16 - The Cohabitation Formulation
```

### Monitor Plex library database (interactive mode) <a name="cmdline-monitor"></a>
You can also monitor the Plex library database in a foreground/interactive console window.  When run with the `verbose` option, this allows you to see how frequently your Plex library database is changing.  Based on the frequency, you may want to adjust the `debouce` setting accordingly.

*Usage:*
```
dotnet PlexDvrWaker.dll monitor [--debounce=SECONDS] [--plexdata=PATH] [--verbose]
```

*Arguments:*
```
  --debounce=SECONDS    (Default: 5) Since the database can change multiple times within a short time,
                        upon the first change it will wait the specified number of seconds before it
                        updates the Task Scheduler 'wakeup' task with the next scheduled recording time.
                        (Minimum: 1 second)

  --plexdata=PATH       (Default: %LOCALAPPDATA%) The path where Plex stores its local application data.

  --verbose             Prints all messages to standard output.
```

*Example output:*
```
Started monitoring the Plex library database
Press any key to stop monitoring
```

## Troubleshooting
If you are having issues, you can check the Plex DVR Waker log files located in the same directory.  There are up to 4 rolling log files that can reach 1MB each.  Look for files named like:
```
PlexDvrWaker.log
PlexDvrWaker.0.log
PlexDvrWaker.1.log
PlexDvrWaker.2.log
```