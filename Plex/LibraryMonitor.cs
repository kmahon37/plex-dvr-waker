using PlexDvrWaker.Common;
using System;
using System.IO;
using System.Threading.Tasks;

namespace PlexDvrWaker.Plex
{
    internal class LibraryMonitor: IDisposable
    {
        private readonly DataAdapter _plexAdapter;
        private readonly TimeSpan _bundledChangesTimeSpan;
        private readonly FileSystemWatcher _libraryDatabaseFileWatcher;
        private bool _libraryChanged = false;
        private DateTime? _libraryChangedDate;

        public LibraryMonitor(DataAdapter plexAdapter, TimeSpan bundledChangesTimeSpan)
        {
            _plexAdapter = plexAdapter;
            _bundledChangesTimeSpan = bundledChangesTimeSpan;

            _libraryDatabaseFileWatcher = new FileSystemWatcher(Path.GetDirectoryName(_plexAdapter.LibraryDatabaseFileName), Path.GetFileName(_plexAdapter.LibraryDatabaseFileName) + "*")
            {
                NotifyFilter = NotifyFilters.LastWrite,
                IncludeSubdirectories = false
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

        public void WaitIndefinitely()
        {
            Task.Delay(-1).Wait();
        }

        private void OnLibraryDatabaseChanged(object source, FileSystemEventArgs e)
        {
            Logger.LogInformation($"Plex library changed: {e.Name}");

            //Bundle changes so we're not running this a ton
            if (!_libraryChanged)
            {
                if (_bundledChangesTimeSpan.TotalSeconds > 0)
                {
                    Logger.LogInformation($"Bundling changes, waiting {_bundledChangesTimeSpan.TotalSeconds} second{(_bundledChangesTimeSpan.TotalSeconds > 1 ? "s" : "")} until next refresh");
                }

                _libraryChanged = true;
                _libraryChangedDate = DateTime.Now;

                //Asynchronously refresh the next wakeup time
                Task.Run(() =>
                {
                    //Sleep this async thread for a few seconds to bundle multiple library changes
                    Task.Delay(_bundledChangesTimeSpan).Wait();

                    RefreshNextWakeupTime();

                    _libraryChanged = false;
                    _libraryChangedDate = null;
                });
            }
            else
            {
                if (_bundledChangesTimeSpan.TotalSeconds > 0)
                {
                    var secondsRemaining = _bundledChangesTimeSpan.TotalSeconds - (DateTime.Now - _libraryChangedDate.Value).TotalSeconds;
                    Logger.LogInformation($"Bundling changes, waiting {Math.Round(secondsRemaining, 2)} seconds until next refresh");
                }
            }
        }

        private void OnLibraryDatabaseError(object source, ErrorEventArgs e)
        {
            //TODO handle better
            Logger.LogError(e.GetException().ToString());
        }

        private void RefreshNextWakeupTime()
        {
            Logger.LogInformation("Refreshing next wakeup time");

            var wakeupTime = _plexAdapter.GetNextScheduledRecordingTime();
            if (wakeupTime.HasValue)
            {
                TaskScheduler.CreateOrUpdateWakeUpTask(wakeupTime.Value);
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