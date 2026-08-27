using System;
using System.Collections;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Harpoon.Core;
using UnityEngine;
using UnityEngine.Networking;

namespace Harpoon.Runtime
{
    [Serializable]
    public sealed class GitHubReleaseAsset
    {
        public string name;
        public string browser_download_url;
        public string digest;
        public long size;
    }

    [Serializable]
    public sealed class GitHubRelease
    {
        public string tag_name;
        public string name;
        public string body;
        public string html_url;
        public bool draft;
        public bool prerelease;
        public GitHubReleaseAsset[] assets;
    }

    public sealed class UpdateCheckResult
    {
        public bool Succeeded { get; }
        public bool UpdateAvailable { get; }
        public string Message { get; }
        public GitHubRelease Release { get; }
        public GitHubReleaseAsset Asset { get; }

        public UpdateCheckResult(bool succeeded, bool updateAvailable, string message,
            GitHubRelease release = null, GitHubReleaseAsset asset = null)
        {
            Succeeded = succeeded;
            UpdateAvailable = updateAvailable;
            Message = message ?? string.Empty;
            Release = release;
            Asset = asset;
        }
    }

    public static class GitHubUpdateService
    {
        public const string RepositoryOwner = "murdockpeter";
        public const string RepositoryName = "harpoon_captains_first_island_chain";
        public const string WindowsAssetName = "Harpoon-Captains-Edition-Windows.zip";
        private const string ApiVersion = "2022-11-28";
        private static string LatestReleaseUrl =>
            $"https://api.github.com/repos/{RepositoryOwner}/{RepositoryName}/releases/latest";

        public static IEnumerator Check(string installedVersion, Action<UpdateCheckResult> completed)
        {
            using (var request = UnityWebRequest.Get(LatestReleaseUrl))
            {
                request.SetRequestHeader("Accept", "application/vnd.github+json");
                request.SetRequestHeader("X-GitHub-Api-Version", ApiVersion);
                request.SetRequestHeader("User-Agent", $"Harpoon-Captains-Edition/{installedVersion}");
                yield return request.SendWebRequest();
                if (request.result != UnityWebRequest.Result.Success)
                {
                    var noRelease = request.responseCode == 404;
                    completed?.Invoke(new UpdateCheckResult(noRelease, false,
                        noRelease ? "No published GitHub release is available yet." :
                        $"Update check failed ({request.responseCode}): {request.error}"));
                    yield break;
                }

                GitHubRelease release;
                try { release = JsonUtility.FromJson<GitHubRelease>(request.downloadHandler.text); }
                catch (Exception exception)
                {
                    completed?.Invoke(new UpdateCheckResult(false, false,
                        "GitHub returned an unreadable release: " + exception.Message));
                    yield break;
                }
                if (release == null || release.draft || release.prerelease ||
                    string.IsNullOrWhiteSpace(release.tag_name))
                {
                    completed?.Invoke(new UpdateCheckResult(false, false,
                        "The latest GitHub release is not installable."));
                    yield break;
                }
                var asset = (release.assets ?? Array.Empty<GitHubReleaseAsset>()).FirstOrDefault(item =>
                    string.Equals(item.name, WindowsAssetName, StringComparison.OrdinalIgnoreCase));
                var newer = ReleaseVersion.IsNewer(release.tag_name, installedVersion);
                if (!newer)
                {
                    completed?.Invoke(new UpdateCheckResult(true, false,
                        $"Harpoon Captain's Edition {installedVersion} is current.", release));
                    yield break;
                }
                if (asset == null || !IsTrustedDownload(asset.browser_download_url))
                {
                    completed?.Invoke(new UpdateCheckResult(false, false,
                        $"{release.tag_name} exists, but its Windows package is missing or untrusted.", release));
                    yield break;
                }
                if (string.IsNullOrWhiteSpace(asset.digest) ||
                    !asset.digest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
                {
                    completed?.Invoke(new UpdateCheckResult(false, false,
                        $"{release.tag_name} has no GitHub SHA-256 asset digest; automatic installation is blocked.", release));
                    yield break;
                }
                completed?.Invoke(new UpdateCheckResult(true, true,
                    $"Harpoon Captain's Edition {release.tag_name.TrimStart('v', 'V')} is available.", release, asset));
            }
        }

        public static IEnumerator DownloadAndVerify(GitHubRelease release, GitHubReleaseAsset asset,
            Action<float> progress, Action<string, string> completed)
        {
            if (release == null || asset == null || !IsTrustedDownload(asset.browser_download_url))
            {
                completed?.Invoke(null, "The update package is missing or untrusted.");
                yield break;
            }
            var version = SafeName(release.tag_name);
            var directory = Path.Combine(Application.persistentDataPath, "Updates", version);
            Directory.CreateDirectory(directory);
            var packagePath = Path.Combine(directory, WindowsAssetName);
            using (var request = new UnityWebRequest(asset.browser_download_url, UnityWebRequest.kHttpVerbGET))
            {
                request.downloadHandler = new DownloadHandlerFile(packagePath) { removeFileOnAbort = true };
                request.SetRequestHeader("User-Agent", $"Harpoon-Captains-Edition/{Application.version}");
                var operation = request.SendWebRequest();
                while (!operation.isDone)
                {
                    progress?.Invoke(request.downloadProgress);
                    yield return null;
                }
                if (request.result != UnityWebRequest.Result.Success)
                {
                    completed?.Invoke(null, "Update download failed: " + request.error);
                    yield break;
                }
            }
            progress?.Invoke(1f);
            var expected = asset.digest.Substring("sha256:".Length).Trim();
            string actual;
            try { actual = Sha256(packagePath); }
            catch (Exception exception)
            {
                completed?.Invoke(null, "Could not verify update: " + exception.Message);
                yield break;
            }
            if (!string.Equals(actual, expected, StringComparison.OrdinalIgnoreCase))
            {
                try { File.Delete(packagePath); } catch { /* Retain the primary verification error. */ }
                completed?.Invoke(null, "Update verification failed. The downloaded package was deleted.");
                yield break;
            }
            completed?.Invoke(packagePath, null);
        }

        public static bool LaunchInstaller(string packagePath, string version, out string error)
        {
            error = null;
            try
            {
                if (Application.platform != RuntimePlatform.WindowsPlayer)
                    throw new PlatformNotSupportedException("Automatic installation is currently available for Windows builds only.");
                if (string.IsNullOrWhiteSpace(packagePath) || !File.Exists(packagePath))
                    throw new FileNotFoundException("The verified update package is missing.", packagePath);
                var sourceScript = Path.Combine(Application.streamingAssetsPath, "HarpoonUpdater.ps1");
                if (!File.Exists(sourceScript)) throw new FileNotFoundException("The updater helper is missing.", sourceScript);
                var temporaryScript = Path.Combine(Path.GetTempPath(), $"HarpoonUpdater-{Guid.NewGuid():N}.ps1");
                File.Copy(sourceScript, temporaryScript, true);
                var installDirectory = Path.GetDirectoryName(Application.dataPath);
                var executableName = Path.GetFileName(Process.GetCurrentProcess().MainModule?.FileName);
                if (string.IsNullOrWhiteSpace(installDirectory) || string.IsNullOrWhiteSpace(executableName))
                    throw new InvalidOperationException("The running installation could not be located.");
                var backupDirectory = Path.Combine(Application.persistentDataPath, "UpdateBackups",
                    SafeName(Application.version));
                var arguments = $"-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File {Quote(temporaryScript)} " +
                    $"-GamePid {Process.GetCurrentProcess().Id} -PackagePath {Quote(packagePath)} " +
                    $"-InstallDirectory {Quote(installDirectory)} -ExecutableName {Quote(executableName)} " +
                    $"-BackupDirectory {Quote(backupDirectory)} -TargetVersion {Quote(version)}";
                Process.Start(new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments = arguments,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    WorkingDirectory = installDirectory
                });
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static bool IsTrustedDownload(string address) => Uri.TryCreate(address, UriKind.Absolute, out var uri) &&
            uri.Scheme == Uri.UriSchemeHttps &&
            (string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
             uri.Host.EndsWith(".githubusercontent.com", StringComparison.OrdinalIgnoreCase));

        private static string Sha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var algorithm = SHA256.Create())
                return string.Concat(algorithm.ComputeHash(stream).Select(value => value.ToString("x2")));
        }

        private static string Quote(string value) => "\"" + (value ?? string.Empty).Replace("\"", "\\\"") + "\"";
        private static string SafeName(string value) => string.Concat((value ?? "update").Where(character =>
            char.IsLetterOrDigit(character) || character == '.' || character == '-' || character == '_'));
    }
}
