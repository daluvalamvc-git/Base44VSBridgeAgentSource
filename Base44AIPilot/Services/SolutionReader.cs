using System;
using System.Collections.Generic;
using System.IO;
using Base44AIPilot.Models;
using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;

namespace Base44AIPilot.Services
{
    public class SolutionReader
    {
        private readonly IServiceProvider _serviceProvider;

        private static readonly HashSet<string> CodeExtensions =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".cs", ".cshtml", ".json", ".xml", ".config", ".csproj" };

        private static readonly HashSet<string> IgnoredFolders =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { "bin", "obj", "packages", ".vs", "node_modules", "migrations" };

        public SolutionReader(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null) throw new ArgumentNullException("serviceProvider");
            _serviceProvider = serviceProvider;
        }

        public SolutionInfo GetSolutionInfo()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dte = _serviceProvider.GetService(typeof(DTE)) as DTE2;
            if (dte == null || dte.Solution == null || string.IsNullOrEmpty(dte.Solution.FullName))
                return new SolutionInfo { SolutionName = "No solution open" };

            var slnPath = dte.Solution.FullName;
            var info = new SolutionInfo();
            info.SolutionName = Path.GetFileNameWithoutExtension(slnPath);
            info.SlnPath      = slnPath;
            info.ProjectCount = dte.Solution.Projects.Count;
            return info;
        }

        public List<SolutionFile> GetSolutionFiles()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dte = _serviceProvider.GetService(typeof(DTE)) as DTE2;
            if (dte == null || dte.Solution == null || string.IsNullOrEmpty(dte.Solution.FullName))
                return new List<SolutionFile>();

            var solutionDir = Path.GetDirectoryName(dte.Solution.FullName);
            if (string.IsNullOrEmpty(solutionDir)) return new List<SolutionFile>();

            var files = new List<SolutionFile>();
            CollectFiles(solutionDir, solutionDir, files);
            return files;
        }

        public string GetActiveFilePath()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dte = _serviceProvider.GetService(typeof(DTE)) as DTE2;
            if (dte == null) return null;
            try   { return dte.ActiveDocument != null ? dte.ActiveDocument.FullName : null; }
            catch { return null; }
        }

        // ------------------------------------------------------------------ //

        private void CollectFiles(string solutionDir, string dir, List<SolutionFile> files)
        {
            if (!Directory.Exists(dir)) return;
            if (IgnoredFolders.Contains(Path.GetFileName(dir))) return;

            foreach (var filePath in Directory.GetFiles(dir))
            {
                var ext = Path.GetExtension(filePath);
                if (!CodeExtensions.Contains(ext)) continue;
                if (!ShouldInclude(filePath))      continue;

                try
                {
                    var content  = File.ReadAllText(filePath);
                    var relative = MakeRelative(solutionDir, filePath);
                    var sf = new SolutionFile();
                    sf.FilePath = relative;
                    sf.FileType = ClassifyFile(filePath, content);
                    sf.Language = ExtToLanguage(ext);
                    sf.Content  = content;
                    files.Add(sf);
                }
                catch { /* skip unreadable */ }
            }

            foreach (var subDir in Directory.GetDirectories(dir))
                CollectFiles(solutionDir, subDir, files);
        }

        private static bool ShouldInclude(string path)
        {
            var lower = path.ToLowerInvariant();
            if (lower.EndsWith(".designer.cs")) return false;
            if (lower.EndsWith(".g.cs"))        return false;
            if (lower.EndsWith(".g.i.cs"))      return false;
            return true;
        }

        private static string MakeRelative(string basePath, string fullPath)
        {
            if (!basePath.EndsWith(Path.DirectorySeparatorChar.ToString()))
                basePath += Path.DirectorySeparatorChar;
            return fullPath.StartsWith(basePath, StringComparison.OrdinalIgnoreCase)
                ? fullPath.Substring(basePath.Length)
                : fullPath;
        }

        private static string ClassifyFile(string path, string content)
        {
            var lower = path.ToLowerInvariant();
            var name  = Path.GetFileNameWithoutExtension(lower);
            var ext   = Path.GetExtension(lower);

            if (ext == ".cshtml")                                       return "View";
            if (ext == ".config" || name == "appsettings")              return "Config";
            if (name.EndsWith("controller"))                            return "Controller";
            if (name.EndsWith("service"))                               return "Service";
            if (name.EndsWith("repository") || name.EndsWith("repo"))   return "Repository";
            if (name.EndsWith("context") && ext == ".cs")               return "DbContext";
            if (name.EndsWith("viewmodel") || name.EndsWith("vm"))      return "ViewModel";
            if (name.StartsWith("i") && content.Contains("interface ")) return "Interface";
            if (lower.Contains("migration"))                            return "Migration";
            if (ext == ".cs" && content.Contains("class "))            return "Model";
            return "Other";
        }

        private static string ExtToLanguage(string ext)
        {
            switch (ext.ToLowerInvariant())
            {
                case ".cs":     return "csharp";
                case ".cshtml": return "html";
                case ".json":   return "json";
                case ".xml":    return "xml";
                case ".config": return "xml";
                case ".csproj": return "xml";
                default:        return "text";
            }
        }
    }
}
