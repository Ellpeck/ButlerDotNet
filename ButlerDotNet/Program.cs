using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;

namespace ButlerDotNet {
    public static class Program {

        private static async Task<int> Main(string[] args) {
            var dll = new FileInfo(Assembly.GetExecutingAssembly().Location);
            var butlerDir = dll.Directory.CreateSubdirectory("Butler");

            var channel = GetButlerChannel();
            Console.WriteLine($"Channel: {channel}");

            // check which butler version we have
            var version = new Version();
            var versionFile = new FileInfo(Path.Combine(dll.Directory.FullName, "ButlerVersion.txt"));
            if (versionFile.Exists) {
                using var reader = versionFile.OpenText();
                Version.TryParse(await reader.ReadToEndAsync(), out version);
            }
            Console.WriteLine($"Installed version: {version}");

            // get latest version of butler
            using var client = new WebClient();
            var link = $"https://broth.itch.ovh/butler/{channel}/LATEST";
            var latest = await client.DownloadStringTaskAsync(link);
            Console.WriteLine($"Latest version: {latest}");

            // update butler if necessary
            if (!Version.TryParse(latest, out var latestVersion) || latestVersion > version) {
                Console.WriteLine("Updating butler to " + latestVersion);
                var zipFile = Path.Combine(dll.Directory.FullName, "butler.zip");
                await client.DownloadFileTaskAsync($"{link}/archive/default", zipFile);
                ZipFile.ExtractToDirectory(zipFile, butlerDir.FullName, true);
                File.Delete(zipFile);
                Console.WriteLine("Finished updating butler to " + latestVersion);

                // update latest version file
                await using var writer = versionFile.CreateText();
                writer.Write(latestVersion);
            }

            // run butler with our arguments
            Console.WriteLine("Running butler");
            using var process = Process.Start(new ProcessStartInfo(Path.Combine(butlerDir.FullName, "butler")) {
                Arguments = string.Join(' ', args),
                CreateNoWindow = false
            });
            process.WaitForExit();
            return process.ExitCode;
        }

        private static string GetButlerChannel() {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return "darwin-amd64";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return "linux-amd64";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "windows-amd64";
            throw new ArgumentException("Unable to determine current operating system platform");
        }

    }
}