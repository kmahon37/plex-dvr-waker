using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using Semver;

namespace PlexDvrWaker.Common
{
    internal static class VersionUtils
    {

        public static SemVersion GetAssemblyVersion()
        {
            var versionString = Assembly.GetEntryAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>().InformationalVersion;
            return SemVersion.Parse(versionString);
        }

        public static bool TryGetLatestVersion(out SemVersion latestVersion)
        {
            latestVersion = null;

            Logger.LogInformation($"Fetching latest {Program.APP_FRIENDLY_NAME} version");

            try
            {
                // Fetch the latest version
                var httpClient = new HttpClient();
                var response = httpClient.GetAsync("https://raw.githubusercontent.com/kmahon37/plex-dvr-waker/master/VERSION").Result;
                if (response.StatusCode == HttpStatusCode.OK)
                {
                    var latestVersionString = response.Content.ReadAsStringAsync().Result.Trim();
                    latestVersion = SemVersion.Parse(latestVersionString);
                    Logger.LogInformation($"Latest version: {latestVersion}");
                    return true;
                }
                else
                {
                    // Unexpected response status code, so log an error. Try to read the response content, if possible.
                    string content = null;
                    try
                    {
                        content = response.Content?.ReadAsStringAsync().Result;
                    }
                    catch
                    {
                        // Ignore exception
                    }

                    Logger.LogErrorToFile($"Unable to retrieve latest version information at this time because of an unexpected response.\nStatus code: {response.StatusCode}\nContent: {content}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Logger.LogErrorToFile($"Unable to retrieve latest version information at this time because of an exception.");
                Logger.LogErrorToFile(ex.ToString());
                return false;
            }
        }

    }
}