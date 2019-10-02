using System;
using System.IO;
using Microsoft.Win32;

namespace PlexDvrWaker.Plex
{
    /// <summary>
    /// Static class for reading Plex settings from the Windows Registry
    /// </summary>
    /// <remarks>
    /// The defaults are defined by Plex, but we need them here because the values may not be 
    /// written in the registry yet if the user has never changed the settings.
    /// </remarks>
    internal static class Settings
    {
        public static string LocalAppDataPath { get; } = GetRegistryValue("LocalAppDataPath", Environment.ExpandEnvironmentVariables("%LOCALAPPDATA%"));
        public static int ButlerStartHour { get; } = GetRegistryValue("ButlerStartHour", 2);
        public static int ButlerEndHour { get; } = GetRegistryValue("ButlerEndHour", 5);

        private static string _libraryDatabaseFileName;
        public static string LibraryDatabaseFileName {
            get
            {
                if (string.IsNullOrWhiteSpace(_libraryDatabaseFileName))
                {
                    //Initialize database file name
                    _libraryDatabaseFileName = Path.GetFullPath(
                        Path.Join(
                            LocalAppDataPath,
                            @"Plex Media Server\Plug-in Support\Databases\com.plexapp.plugins.library.db"
                        )
                    );
                }

                return _libraryDatabaseFileName;
            }
        }

        private static T GetRegistryValue<T>(string keyName, T defaultValue)
        {
            // Computer\HKEY_CURRENT_USER\Software\Plex, Inc.\Plex Media Server
            using (var key = Registry.CurrentUser.OpenSubKey(@"Software\Plex, Inc.\Plex Media Server"))
            {
                var value = key?.GetValue(keyName) is T keyValue ? keyValue : defaultValue;
                return value;
            }
        }
    }
}