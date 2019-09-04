# Plex DVR Waker
Are you fed up with Plex's lack of functionality to wakeup your computer to record a show?  Even a VCR from the '80s could do that.  Have no fear, Plex DVR Waker is here!!

Plex DVR Waker is a simple command-line tool for waking the computer before the next scheduled recording.  It works by creating a Windows Task Scheduler task that can sync with and/or monitor the Plex library database and then schedule another task to wakeup the computer before the next scheduled recording.

*Disclaimer:*
_I reverse engineered the Plex library and EPG databases in order to piece together enough functionality to be able to recognize scheduled recordings and previously recorded TV shows and movies (so that it doesn't wake the computer for no reason).  I just used whatever simple data that I could find in the databases to identify TV shows and movies.  So, this tool clearly does not support all the advanced features built-in to Plex._

## Requirements
- Windows 7/8/10
- Windows Task Scheduler
- .NET Core 2.2+ Runtime
  - Download from here
- "Run as administrator" rights
  - Administrator rights are needed in order to create the sync and monitor tasks so that they run hidden without popping up a console window every time the task is triggered.

## Supported Features
- Recording start time offset
- Recognizes previously recorded TV shows and movies (as best as possible)

_NOTE: It does *not* support any other "advanced record options" (ie: Prefer HD, Replace lower resolution items, etc)._

# Command-line Arguments


## Heirarchy
```
add-task
    --wakeup
    --sync [--interval=MINUTES]
    --monitor [--debounce=SECONDS]
    [--plexdata=PATH]
    [--verbose]
list
    [--plexdata=PATH]
    [--verbose]
monitor
    [--debounce=SECONDS]
    [--plexdata=PATH]
    [--verbose]
```



```
  add-task    Add/update Windows Task Scheduler tasks for waking the computer for the next scheduled recording, syncing
              the next wakeup, or monitoring the Plex library database for changes.

  list        Prints upcoming scheduled recordings to standard output.

  monitor     Monitors the Plex library database for changes and updates the 'wakeup' task based on the next scheduled
              recording time.

```