<a href="https://github.com/kmahon37/plex-dvr-waker/releases/latest"><img alt="Latest release" src="https://img.shields.io/github/v/release/kmahon37/plex-dvr-waker"></a>
<a href="https://github.com/kmahon37/plex-dvr-waker/releases/latest"><img alt="Downloads" src="https://img.shields.io/github/downloads/kmahon37/plex-dvr-waker/total"></a>
<a href="https://github.com/kmahon37/plex-dvr-waker/actions?query=workflow%3A%22.NET+Core+Build%22+branch%3Amaster"><img alt="Build status" src="https://img.shields.io/github/workflow/status/kmahon37/plex-dvr-waker/.NET%20Core%20Build/master"></a>
<a href="https://github.com/kmahon37/plex-dvr-waker/blob/master/LICENSE"><img alt="License" src="https://img.shields.io/github/license/kmahon37/plex-dvr-waker"></a>

# Plex DVR Waker
Are you fed up with Plex's lack of functionality to wakeup your computer to record a show?  C'mon, even a VCR from the '80s could do that.

Plex DVR Waker is a simple command-line tool for waking the computer before the next scheduled recording.  It works by creating a Windows Task Scheduler task that can sync with and/or monitor the Plex library database and then schedule another task to wakeup the computer before the next scheduled recording.

> _DISCLAIMER:_<br>
> _I reverse engineered the Plex library and EPG databases in order to piece together enough functionality to be able to recognize scheduled recordings and previously recorded TV shows and movies (so that it doesn't wake the computer for no reason).  I just used whatever data that I could find and interpret in the databases to identify TV shows and movies.  So, while this tools works fairly well, it clearly does not support all the advanced features built-in to Plex._

## Supported Features
- Syncs with and/or monitors the Plex library database and schedules a wakeup task
- Wakes up the computer 15 seconds before the next scheduled recording or Plex maintenance time
- Recording start time offset is taken into account
- Recognizes previously recorded TV shows and movies (as best as possible) so that it doesn't inadvertently wake the computer for something you already have recorded.
- Prints out upcoming scheduled recordings and Plex maintenance time
- Support for custom Plex application data path

> _NOTE: At this time, it does **not** support any other Plex "advanced record options" (ie: Prefer HD, Replace lower resolution items, etc).  If you think there is something it should support, please let me know and I can try to investigate it._

## Requirements
- Plex Media Server for Windows ([download from Plex](https://www.plex.tv/media-server-downloads/))
- Windows 7/8/10
- Windows Task Scheduler
- Windows .NET Core Runtime 3.1 ([download from Microsoft](https://dotnet.microsoft.com/download/dotnet-core/3.1))
  - You only need the ".NET Core Runtime" installer _(not the "SDK", "ASP.NET Core Runtime", or "Desktop Runtime")_.
- "Run as administrator" rights
  - Administrator rights are needed in order to create the sync and/or monitor tasks so that they run hidden without popping up a console window every time the task is triggered.  This is a Windows Task Scheduler limitation for console applications.

## Installation
1. Download and install the latest Windows .NET Core Runtime 3.1
    - [Download from Microsoft](https://dotnet.microsoft.com/download/dotnet-core/3.1)
    - You only need the ".NET Core Runtime" installer _(not the "SDK", "ASP.NET Core Runtime", or "Desktop Runtime")_.
2. Download the latest version of Plex DVR Waker
    - [Download Plex DVR Waker](https://github.com/kmahon37/plex-dvr-waker/releases/latest)
    - Unzip the contents to a folder.

## Upgrades
If you are upgrading from a previous version of Plex DVR Waker, please follow these steps.
1. If you are using the `monitor` task, then you must first stop it before you can delete/overwrite the `PlexDvrWaker.exe` file.
    1. Open the Windows Task Scheduler.
    2. Go to the "Plex DVR Waker" folder.
    3. Right-click on the "DVR monitor" task, and click "End".
2. Delete the folder containing your previous version of Plex DVR Waker.
3. Unzip the contents of the new version.
4. If you are upgrading to a newer major version (ie: 1.x.x -> 2.x.x), then you may need to open an "Administrator" Command Prompt and rerun the `add-task` commands that you are using.  Doing this will update the existing tasks in Windows Task Scheduler with any potential breaking changes for the new Plex DVR Waker version.

## Quick Start
If you want a quick way to get started, simply run the following `sync` or `monitor` commands from an "Administrator" Command Prompt.  The `sync` command will create a Windows Task Scheduler task that will synchronize with the Plex library database every 15 minutes.  The `monitor` command will watch the Plex library database for changes.  Both will create/update a different Windows Task Scheduler task to wakeup the computer before your next scheduled recording or Plex maintenance time.  [See below for more information](#cmdline-add-task) and pros/cons of each command.
```
PlexDvrWaker.exe add-task --sync
```
or
```
PlexDvrWaker.exe add-task --monitor
```

## Command-line Arguments

- [Display help](#cmdline-help)
- [Create scheduled tasks](#cmdline-add-task)
- [Display upcoming scheduled recordings](#cmdline-list)
- [Monitor Plex library database (interactive mode)](#cmdline-monitor)
- [Check for new version](#cmdline-version-check)
- [Custom Plex Installations](#cmdline-custom)

_NOTE: The documentation below contains command line "usage" syntax that describes the available options for each command.  Items in `[ ]` indicate optional arguments, and items separated by `|` indicate the available options for that argument._

### Display help <a name="cmdline-help"></a>
The main help screen displays the top-level help for the available commands.  You can also view specific detailed help for each command by using the syntax: `PlexDvrWaker.exe help <command_name>` or `PlexDvrWaker.exe <command_name> --help`.

_Usage:_
```
PlexDvrWaker.exe help [add-task|list|monitor|version-check]
```
or
```
PlexDvrWaker.exe [add-task|list|monitor|version-check] --help
```

_Example output:_
```
  add-task         Add/update Windows Task Scheduler tasks for waking the computer for the next scheduled
                   recording or Plex maintenance time, syncing the next wakeup, monitoring the Plex
                   library database for changes, or checking for a newer version of this application.

  list             Prints upcoming scheduled recordings to standard output.

  monitor          Monitors the Plex library database for changes and updates the 'wakeup' task based
                   on the next scheduled recording time or Plex maintenance time.

  version-check    Checks for a newer version of this application.

  help             Display more information on a specific command.

  version          Display version information.
```

### Create scheduled tasks <a name="cmdline-add-task"></a>
Plex DVR Waker works primarily by using Windows Task Scheduler tasks to keep up-to-date with the Plex library database and keep a wakeup task scheduled for your next recording time or Plex maintenance time.  You can use either the `sync` or `monitor` task, or both at the same time - the choice is yours based on your needs.

The `wakeup` task will wakeup the computer from sleep 15 seconds before the next scheduled recording time or Plex maintenance time.  You can add/run this task manually, but typically you would let either the `sync` or `monitor` task create/update the `wakeup` task for you.  The `wakeup` task will either not be created or be deleted if there are no shows to record.

The `sync` task will poll the Plex library database at a specified interval (default every 15 minutes) and create/update the `wakeup` task for you automatically.  This is a nice lightweight solution, however it could theoretically miss something if, for example, you schedule a recording to start in 8 minutes and then immediately put the computer to sleep before the next `sync` task can run.

The `monitor` task will watch the Plex library database files for changes and also create/update the `wakeup` task for you automatically.  Plex can sometimes cause a lot of changes to its library database files in a short period of time which will cause the `wakeup` task to be updated frequently.  It is a fairly quick process to update the `wakeup` task, but just note that it may run a good bit more often.  You can use the `--debouce=SECONDS` option to adjust the frequency of runs.  The plus side is that you are basically guaranteed your `wakeup` task will always be set appropriately.

The `version-check` task will check for a newer version of Plex DVR Waker every so many days (default every 30 days) and notify you if a newer version is available for download.  If you currently have the latest version, then you will not see any notifications (except for the console window popping up and closing real quick when it runs the check).  The version numbers respect the [Semantic Versioning 2.0.0](https://semver.org/) specification.

_Usage:_
```
PlexDvrWaker.exe add-task --wakeup [--database=FILE] [--verbose]
PlexDvrWaker.exe add-task --sync [--interval=MINUTES] [--database=FILE] [--verbose]
PlexDvrWaker.exe add-task --monitor [--debounce=SECONDS] [--database=FILE] [--verbose]
PlexDvrWaker.exe add-task --version-check [--version-check-interval=DAYS] [--verbose]
```

_Arguments:_
```
  --wakeup                         Creates or updates a Windows Task Scheduler 'wakeup' task that will wakeup the
                                   computer 15 seconds before the next scheduled recording time or Plex maintenance
                                   time.

  --sync                           Creates or updates a Windows Task Scheduler 'sync' task to run at the specified
                                   interval and sync the 'wakeup' task with the next scheduled recording time or
                                   Plex maintenance time.

  --sync-interval=MINUTES          (Default: 15) The interval to sync the 'wakeup' task with the next scheduled
                                   recording time or Plex maintenance time.

  --monitor                        Creates or updates a Windows Task Scheduler 'monitor' task to run in the
                                   background when the computer starts up that will monitor the Plex library
                                   database file for changes and update the 'wakeup' task based on the next
                                   scheduled recording time or Plex maintenance time.

  --monitor-debounce=SECONDS       (Default: 5) Since the Plex library database can change multiple times within a
                                   short time, upon the first change it will wait the specified number of seconds
                                   before it updates the Task Scheduler 'wakeup' task with the next scheduled
                                   recordingtime or Plex maintenance time.

  --version-check                  Creates or updates a Windows Task Scheduler 'version-check' task to run at the
                                   specified interval and check for a newer version of this application.

  --version-check-interval=DAYS    (Default: 30) The number of days between checking for a newer version of this
                                   application.

  --database=FILE                  (Default: <Plex local application data path or %LOCALAPPDATA%>\Plex Media Server
                                   \Plug-in Support\Databases\com.plexapp.plugins.library.db) The Plex library
                                   database file to use for custom Plex installations.

  --verbose                        Prints all messages to standard output.
```

_Example output (`--wakeup`):_
```
Wakeup task scheduled for 9/14/2019 12:59:45 PM
```

_Example output (`--sync`):_
```
DVR sync task scheduled for every 15 minutes
```

_Example output (`--monitor`):_
```
DVR monitor task scheduled to run at startup
DVR monitor task has been started
```

_Example output (`version-check`):_
```
Version check task scheduled for every 30 days
```

### Display upcoming scheduled recordings <a name="cmdline-list"></a>
You can display the upcoming scheduled recordings and Plex maintenance that is recognized by Plex DVR Waker.  This is useful to compare to your actual recording schedule in Plex.  In theory, the two schedules should match (_see disclaimer above_).  The start/end times displayed here will include any offsets configured in Plex.

_Usage:_
```
PlexDvrWaker.exe list [--maintenance] [--database=FILE] [--verbose]
```

_Arguments:_
```
  --maintenance      Prints the next Plex maintenance time to standard output.

  --database=FILE    (Default: <Plex local application data path or %LOCALAPPDATA%>\Plex Media Server\Plug-in
                     Support\Databases\com.plexapp.plugins.library.db) The Plex library database file to use for
                     custom Plex installations.

  --verbose          Prints all messages to standard output.
```

_Example output (`--maintenance`):_
```
Start Time              End Time                Title
--------------------    --------------------    --------------------------------------------------
9/14/2019 1:00:00 PM    9/14/2019 2:00:00 PM    MacGyver (2016) - S01E22 - The Assassin
9/16/2019 6:30:00 PM    9/16/2019 7:00:00 PM    The Big Bang Theory - S09E22 - The Fermentation Bifurcation
9/16/2019 7:00:00 PM    9/16/2019 7:30:00 PM    The Big Bang Theory - S04E16 - The Cohabitation Formulation

Plex maintenance is 2am to 3am every day
Next scheduled maintenance time is 9/15/2019 2:00:00 AM to 9/15/2019 3:00:00 AM
```

### Monitor Plex library database (interactive mode) <a name="cmdline-monitor"></a>
You can also monitor the Plex library database for changes and automatically refresh the next wakeup time.  This is what is run hidden in the background when you run the `add-task --monitor` command.  If you want to, you can also run it in a foreground/interactive console window.  When run with the `--verbose` option, this allows you to see how frequently your Plex library database is changing.  Based on the frequency, you may want to adjust the `--debouce` setting accordingly.

_Usage:_
```
PlexDvrWaker.exe monitor [--debounce=SECONDS] [--database=FILE] [--verbose]
```

_Arguments:_
```
  --debounce=SECONDS    (Default: 5) Since the database can change multiple times within a short time,
                        upon the first change it will wait the specified number of seconds before it
                        updates the Task Scheduler 'wakeup' task with the next scheduled recording time
                        or Plex maintenance time. (Minimum: 1 second)

  --database=FILE       (Default: <Plex local application data path or %LOCALAPPDATA%>\Plex Media Server\Plug-in
                        Support\Databases\com.plexapp.plugins.library.db) The Plex library database file to use for
                        custom Plex installations.

  --verbose             Prints all messages to standard output.
```

_Example output:_
```
Started monitoring the Plex library database
Press any key to stop monitoring
```

### Check for new version <a name="cmdline-version-check"></a>
You can check if a newer version of Plex DVR Waker is available for download.  See the [upgrades](#upgrades) section for how to successfully upgrade the application.  The version numbers respect the [Semantic Versioning 2.0.0](https://semver.org/) specification.

_Usage:_
```
PlexDvrWaker.exe version-check [--verbose]
```

_Arguments:_
```
  --verbose    Prints all messages to standard output.
```

_Example output:_
```
You already have the latest version.
```
or
```
Current version: 1.1.9
Latest version:  2.0.0
A newer version of Plex DVR Waker is available for download from:
https://github.com/kmahon37/plex-dvr-waker/releases/latest
```

### Custom Plex Installations <a name="cmdline-custom"></a>
If you have a custom Plex installation, such as if you installed Plex under a different user/service account, then you may need to specify the `--database` option when running commands.  With this option, you will need to specify the full path and file name to your database file in the custom location (ie: `--database="C:\My Custom Folder\custom2\com.plexapp.plugins.library.db"`).

By default, Plex DVR Waker tries to load Plex's local application data storage path from the registry (`Computer\HKEY_CURRENT_USER\Software\Plex, Inc.\Plex Media Server\LocalAppDataPath`) if it exists (Note: This is controlled via an Advanced setting in Plex under "Settings > General").  Otherwise, Plex DVR Waker will fallback onto Plex's default local application data storage path (`%LOCALAPPDATA%` which usually corresponds to `C:\Users\<USER_NAME>\AppData\Local\`).  Once the storage path is identified, it then tries to find the Plex library database file under that folder (`...\Plex Media Server\Plug-in Support\Databases\com.plexapp.plugins.library.db`).

## Troubleshooting

### Windows Task Scheduler
Plex DVR Waker works simply by using scheduled tasks in the Windows built-in Task Scheduler program.  To see the scheduled tasks, open the Task Scheduler and go to the "Plex DVR Waker" folder.  You should see up to 4 tasks named: DVR monitor, DVR sync, DVR wakeup, Version check.  You may edit/delete these tasks, but your changes may be lost if you run any of the `add-task` commands as they will overwrite/recreate these tasks.

### Log files
If you are having issues, you can check the Plex DVR Waker log files located in the same directory.  There are up to 4 rolling log files that can reach 1MB each.  If needed, these files can safely be deleted, but they will regenerate over time.
```
PlexDvrWaker.log
PlexDvrWaker.0.log
PlexDvrWaker.1.log
PlexDvrWaker.2.log
```

### Uninstall
If you wish to stop using Plex DVR Waker and uninstall it, just follow these steps:
1. Delete any Windows Task Scheduler tasks created by Plex DVR Waker.
    1. Open Windows Task Scheduler and find the "Plex DVR Waker" folder.
    2. Delete all tasks in the folder.
    3. Delete the "Plex DVR Waker" folder.
2. Delete the folder where you unzipped Plex DVR Waker.
3. If you installed the .NET Core Runtime, then you may optionally choose to uninstall it also (assuming nothing else is using it).
