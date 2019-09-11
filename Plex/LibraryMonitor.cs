using PlexDvrWaker.Common;
using System;
using System.IO;
using System.Threading.Tasks;

namespace PlexDvrWaker.Plex
{
    internal class LibraryMonitor: IDisposable
    {
        public const string PRESS_ANY_KEY_TO_STOP = "Press any key to stop monitoring";

        private readonly DataAdapter _plexAdapter;
        private readonly TimeSpan _bundledChangesTimeSpan;
        private readonly FileSystemWatcher _libraryDatabaseFileWatcher;
        private readonly Object _libraryChangedLock = new Object();
        private DateTime? _libraryChangedDate;

        public LibraryMonitor(DataAdapter plexAdapter, TimeSpan bundledChangesTimeSpan)
        {
            _plexAdapter = plexAdapter;
            _bundledChangesTimeSpan = bundledChangesTimeSpan;

            _libraryDatabaseFileWatcher = new FileSystemWatcher(Path.GetDirectoryName(_plexAdapter.LibraryDatabaseFileName), Path.GetFileName(_plexAdapter.LibraryDatabaseFileName) + "*")
            {
                NotifyFilter = NotifyFilters.LastWrite,
                IncludeSubdirectories = false,
                InternalBufferSize = 4096 * 8
            };
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

                        Logger.LogInformation($"Plex library changed: {e.Name}");
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
            if (!(e.GetException() is InternalBufferOverflowException))
            {
                Logger.LogError(e.GetException().ToString());
            }
        }

        private void RefreshNextWakeupTime()
        {
            Logger.LogInformation("Refreshing next wakeup time");

            var wakeupTime = _plexAdapter.GetNextScheduledRecordingTime();
            if (wakeupTime.HasValue)
            {
                TaskScheduler.CreateOrUpdateWakeUpTask(wakeupTime.Value, false);
            }
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

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~PlexAdapter()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}