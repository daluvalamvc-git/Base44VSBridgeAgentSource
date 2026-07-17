using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace VsixBootstrapper
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            try
            {
                string vsixPath = args.Length > 0 ? args[0] : FindVsixInWorkingTree();
                if (string.IsNullOrEmpty(vsixPath) || !File.Exists(vsixPath))
                {
                    Console.Error.WriteLine("Error: .vsix not specified and none found in expected locations.");
                    Console.Error.WriteLine("Usage: VsixBootstrapper.exe path\\to\\extension.vsix");
                    return 2;
                }

                string installer = FindVsixInstaller();
                if (string.IsNullOrEmpty(installer) || !File.Exists(installer))
                {
                    Console.Error.WriteLine("Error: Could not find VSIXInstaller.exe. Ensure Visual Studio is installed.");
                    return 3;
                }

                Console.WriteLine($"Using VSIX installer: {installer}");
                Console.WriteLine($"Installing VSIX: {vsixPath}");

                var psi = new ProcessStartInfo
                {
                    FileName = installer,
                    Arguments = $"\"{vsixPath}\" /quiet",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };

                using (var proc = Process.Start(psi))
                {
                    proc.WaitForExit();
                    Console.WriteLine($"VSIX installer exited with code {proc.ExitCode}");
                    return proc.ExitCode;
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine("Unhandled error: " + ex.Message);
                return 1;
            }
        }

        private static string FindVsixInWorkingTree()
        {
            var dir = new DirectoryInfo(Environment.CurrentDirectory);
            for (int depth = 0; dir != null && depth < 6; depth++)
            {
                // exact filename preferred
                var exact = Path.Combine(dir.FullName, "Base44AIPilot.vsix");
                if (File.Exists(exact))
                    return exact;

                // any vsix in this directory
                var any = Directory.EnumerateFiles(dir.FullName, "*.vsix").FirstOrDefault();
                if (any != null)
                    return any;

                dir = dir.Parent;
            }

            // fallback to previously used relative candidates
            var cwd = Environment.CurrentDirectory;
            var candidates = new[]
            {
                Path.Combine(cwd, "Base44AIPilot.vsix"),
                Path.Combine(cwd, "bin", "Release", "Base44AIPilot.vsix"),
                Path.Combine(cwd, "bin", "Debug", "Base44AIPilot.vsix"),
                Path.Combine(cwd, "..", "Base44AIPilot", "bin", "Release", "Base44AIPilot.vsix"),
                Path.Combine(cwd, "..", "Base44AIPilot", "bin", "Debug", "Base44AIPilot.vsix")
            };

            return candidates.Select(Path.GetFullPath).FirstOrDefault(File.Exists);
        }

        private static string FindVsixInstaller()
        {
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            string[] vsYears = { "2022", "2019", "2017", "2015" };
            string[] editions = { "Enterprise", "Professional", "Community", "BuildTools" };

            foreach (var year in vsYears)
            foreach (var edition in editions)
            {
                string path = Path.Combine(programFilesX86, "Microsoft Visual Studio", year, edition, "Common7", "IDE", "VSIXInstaller.exe");
                if (File.Exists(path))
                    return path;
            }

            var legacy = Path.Combine(programFilesX86, "Common Files", "Microsoft Shared", "VSIX", "VSIXInstaller.exe");
            if (File.Exists(legacy))
                return legacy;

            string fromPath = TryFindInPath("VSIXInstaller.exe");
            if (!string.IsNullOrEmpty(fromPath))
                return fromPath;

            return null;
        }

        private static string TryFindInPath(string fileName)
        {
            var paths = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator);
            foreach (var p in paths)
            {
                try
                {
                    var candidate = Path.Combine(p.Trim('"'), fileName);
                    if (File.Exists(candidate))
                        return candidate;
                }
                catch { }
            }
            return null;
        }
    }
}