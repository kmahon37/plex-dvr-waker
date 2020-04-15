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