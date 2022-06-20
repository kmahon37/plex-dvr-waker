using PlexDvrWaker.Common;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PlexDvrWaker.Plex
{
    /// <summary>
    /// Class for monitoring the Plex library databases for changes
    /// </summary>
    internal class LibraryMonitor: IDisposable
    {
        public const string PRESS_ANY_KEY_TO_STOP = "Press any key to stop monitoring";

        private readonly DataAdapter _plexAdapter;
        private readonly TaskScheduler _taskScheduler;
        private readonly TimeSpan _bundledChangesTimeSpan;
        private readonly int _wakeupOffsetSeconds;
        private readonly IEnumerable<string> _wakeupActions;
        private readonly FileSystemWatcher _libraryDatabaseFileWatcher;
        private readonly object _libraryChangedLock = new();
        private DateTime? _libraryChangedDate;
        private DateTime? _startDate;
        private ulong _numTimesTriggered;

        public LibraryMonitor(DataAdapter plexAdapter, TaskScheduler taskScheduler, int debounceSeconds, int offsetSeconds, IEnumerable<string> wakeupActions)
        {
            _plexAdapter = plexAdapter;
            _taskScheduler = taskScheduler;
            _bundledChangesTimeSpan = TimeSpan.FromSeconds(debounceSeconds);
            _wakeupOffsetSeconds = offsetSeconds;
            _wakeupActions = wakeupActions;

            var libraryDatabaseFileName = Path.GetFileName(Settings.LibraryDatabaseFileName);
            _libraryDatabaseFileWatcher = new FileSystemWatcher(Path.GetDirectoryName(Settings.LibraryDatabaseFileName))
            {
                NotifyFilter = NotifyFilters.LastWrite,
                IncludeSubdirectories = false,
                InternalBufferSize = 4096 * 8
            };
            _libraryDatabaseFileWatcher.Filters.Add(libraryDatabaseFileName);
            _libraryDatabaseFileWatcher.Filters.Add(libraryDatabaseFileName + "-wal");
            _libraryDatabaseFileWatcher.Filters.Add(libraryDatabaseFileName + "-shm");
            _libraryDatabaseFileWatcher.Changed += OnLibraryDatabaseChanged;
            _libraryDatabaseFileWatcher.Error += OnLibraryDatabaseError;
        }

        public bool Enabled
        {
            get
            {
                return _libraryDatabaseFileWatcher.EnableRaisingEvents;
            }
            set
            {
                _libraryDatabaseFileWatcher.EnableRaisingEvents = value;
                _startDate = value ? DateTime.Now : (DateTime?)null;
                _numTimesTriggered = 0;
            }
        }

        private void OnLibraryDatabaseChanged(object source, FileSystemEventArgs e)
        {
            //Double lock to prevent overlapping issues
            if (_libraryChangedDate == null)
            {
                lock (_libraryChangedLock)
                {
                    if (_libraryChangedDate == null)
                    {
                        //Bundle changes so we're not running this a ton
                        _libraryChangedDate = DateTime.Now;
                        _numTimesTriggered += 1;

                        Logger.LogInformation("--------------------------------------------------------------");
                        Logger.LogInformation($"Monitor detected a Plex library file changed: {e.Name}");
                        Logger.LogInformation($"Monitor start date: {_startDate.Value:s} ({new TimeSpan((_libraryChangedDate.Value - _startDate.Value).Ticks)})");
                        Logger.LogInformation($"Monitor triggered: {_numTimesTriggered} time{(_numTimesTriggered > 1 ? "s" : "")}");
                        if (_bundledChangesTimeSpan.TotalSeconds > 0)
                        {
                            Logger.LogInformation($"Bundling changes, waiting {_bundledChangesTimeSpan.TotalSeconds} second{(_bundledChangesTimeSpan.TotalSeconds > 1 ? "s" : "")} until next refresh");
                        }

                        //Since we are waiting and bundling changes, we need to asynchronously refresh the next wakeup time
                        //so we don't block this thread.  We want to keep processing/ignoring other changes until we are
                        //done with this current refresh.
                        Task.Run(() =>
                        {
                            //Sleep this async thread for the timespan to bundle multiple library changes
                            Task.Delay(_bundledChangesTimeSpan).Wait();
                            RefreshNextWakeupTime();
                            _libraryChangedDate = null;
                        });
                    }
                }
            }
        }

        private void OnLibraryDatabaseError(object source, ErrorEventArgs e)
        {
            if (e.GetException() is not InternalBufferOverflowException)
            {
                Logger.LogError(e.GetException().ToString());
            }
        }

        private void RefreshNextWakeupTime()
        {
            Logger.LogInformation("Refreshing next wakeup time");

            var wakeupTime = _plexAdapter.GetNextWakeupTime();
            _taskScheduler.CreateOrUpdateWakeUpTask(wakeupTime, _wakeupOffsetSeconds, _wakeupActions, false);
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    if (_libraryDatabaseFileWatcher != null)
                    {
                        _libraryDatabaseFileWatcher.Dispose();
                    }
                }

                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }
}
