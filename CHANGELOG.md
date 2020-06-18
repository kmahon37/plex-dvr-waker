## 2.1.1 (2020-06-17)
### Fixed bugs:
- Fixed an issue when running the `monitor` task where it would not properly release the database connections which could prevent Plex from properly refreshing the TV Guide. [#27](https://github.com/kmahon37/plex-dvr-waker/issues/27)

## 2.1.0 (2020-06-08)
### New Features:
- Added a new `add-task` option of `--offset=SECONDS` for specifying the number of seconds to wakeup the computer before the next scheduled recording.  It defaults to 15 seconds which is the offset that was previously hard-coded.  Applies to the `wakeup`, `sync`, and `monitor` tasks. [#25](https://github.com/kmahon37/plex-dvr-waker/issues/25)

## 2.0.0 (2020-05-25)
### Breaking Changes _(Action Required)_:
- Upgraded to use .NET Core 3.1 (since .NET Core 2.2 has reached "end of life" and is no longer supported by Microsoft) [#11](https://github.com/kmahon37/plex-dvr-waker/issues/11)
  - You will need to download and install the latest Windows .NET Core Runtime 3.1
    - [Download from Microsoft](https://dotnet.microsoft.com/download/dotnet-core/3.1)
    - You only need the ".NET Core Runtime" installer _(not the "SDK", "ASP.NET Core Runtime", or "Desktop Runtime")_.
  - You should also uninstall .NET Core 2.2 (assuming nothing else is using it).
- Plex DVR Waker is now a `.exe` application.  You can now run it via `PlexDvrWaker.exe` (you no longer need to run it via `dotnet PlexDvrWaker.dll`).
- Renamed some of the `add-task` options to make it clearer as to what they control (`--interval` is now `--sync-interval`, and `--debounce` is now `--monitor-debounce`).
  - It is recommended that you open an "Administrator" Command Prompt and rerun the `add-task` commands that you are using.  Doing this will update the existing tasks in Windows Task Scheduler with the necessary changes for this new version.

### New Features:
- Added command-line option for `add-task --version-check` that will create a Windows Task Scheduler task to check for a newer version of Plex DVR Waker every so many days (default every 30 days) and notify you if a newer version is available for download.
- Added command-line verb for `version-check` that will allow you to manually check for a newer version.

## 1.1.9 (2020-04-26)
### Fixed bugs:
- Fixed issue caused in v1.1.8 with running the `monitor` task in Windows Task Scheduler where the task would quit with an error code of 0xE0434352. [#13](https://github.com/kmahon37/plex-dvr-waker/issues/13)

### Other:
- Added some logging improvements that should help with tracking down future issues.

## 1.1.8 (2020-04-19) - _DO NOT USE_
_UPDATE: (2020-04-26) This fix did not work as expected and the release has been removed._
### Fixed bugs:
- Fixed issue when Windows Task Scheduler cannot automatically find `dotnet.exe` on some systems like Windows 7 (and maybe others).  It will now check for `dotnet.exe` at the default install location of `%ProgramFiles%\dotnet\dotnet.exe`, and if not found then it will also check all locations in the `PATH` environment variable.  If `dotnet.exe` cannot be found, it will error out when running one of the `add-task` commands. [#13](https://github.com/kmahon37/plex-dvr-waker/issues/13)

### Upgrade notes:
If you previously installed v1.1.7 and Windows is preventing you from deleting/overwriting files in your PlexDvrWaker folder, then you may need to perform the following steps in order to upgrade.
1. Open Windows Task Scheduler and find the "Plex DVR Waker" folder, and then delete all tasks in the folder.
2. Restart your computer so that Windows releases the locks on the files.
3. Delete your PlexDvrWaker v1.1.7 folder.
4. Install a new version of PlexDvrWaker.

## 1.1.7 (2020-04-15) - _DO NOT USE_
_UPDATE: (2020-04-19) This fix did not work as expected and the release has been removed._
### Fixed bugs:
- Fixed issue when running on Win7 and the Windows Task Scheduler cannot find the `dotnet.exe` [#13](https://github.com/kmahon37/plex-dvr-waker/issues/13)

## 1.1.6 (2020-04-14)
### Fixed bugs:
- Fixed `NullReferenceException` application crash that was introduced in v1.1.5 when running any of the `add-task` commands [#13](https://github.com/kmahon37/plex-dvr-waker/issues/13)

## 1.1.5 (2020-04-13)
### Fixed bugs:
- Fixed application crash when all scheduled recordings have a start time in the past [#13](https://github.com/kmahon37/plex-dvr-waker/issues/13)

## 1.1.4 (2020-04-12)
### New Features:
- Added command-line option for `--database=FILE` to support for custom Plex installations [#10](https://github.com/kmahon37/plex-dvr-waker/issues/10)

## 1.1.3 (2020-02-08)
### Fixed bugs:
- Fixed loading EPG info to support xmltv providers [#8](https://github.com/kmahon37/plex-dvr-waker/issues/8)

## 1.1.2 (2020-01-18)
### Fixed bugs:
- Fixed error when loading EPG info from the Plex library database [#6](https://github.com/kmahon37/plex-dvr-waker/issues/6)

## 1.1.1 (2019-10-27)
### Fixed bugs:
- Fixed error when reading advanced recording information with an unexpected format from the Plex library database [#4](https://github.com/kmahon37/plex-dvr-waker/issues/4)

## 1.1.0 (2019-09-29)
### New Features:
- Now reading Plex settings from the Windows Registry
- Removed command-line option for `--plexdata` since it can be read from the registry.
- Plex maintenance time support
  - Added new command-line option (`list --maintenance`) for displaying the next Plex maintenance time.
  - All wakeup features now consider the Plex maintence time in addition to the next scheduled recording time and use whichever comes first to wakeup the computer.
### Fixed bugs:
- Fixed Plex library database file validation for all commands [#1](https://github.com/kmahon37/plex-dvr-waker/issues/1)
- Fixed error when the recording schedule contains duplicate show Ids [#1](https://github.com/kmahon37/plex-dvr-waker/issues/1)
- Fixed error fetching scheduled recordings when Plex contains no scheduled recordings [#2](https://github.com/kmahon37/plex-dvr-waker/issues/2)

## 1.0.1 (2019-09-15)
- Fixed issue with incorrect wakeup times when an episode has repeat airings.  It was incorrectly using the last airing time, but this fix now uses the earliest airing time.

## 1.0.0 (2019-09-14)
- Initial release