using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Base44AIPilot.Models;

namespace Base44AIPilot.Services
{
    public static class DiffParser
    {
        private static readonly Regex NewFileHeader = new Regex(
            @"\[NEW FILE:\s*([^\]]+)\]",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private static readonly Regex DiffHeaderA = new Regex(
            @"^---\s+(?:a/)?(.+)$",
            RegexOptions.Multiline | RegexOptions.Compiled);

        public static ParsedResponse Parse(string aiResponse)
        {
            if (string.IsNullOrWhiteSpace(aiResponse))
            {
                var empty = new ParsedResponse();
                empty.PlainText = string.Empty;
                return empty;
            }

            var result    = new ParsedResponse();
            var lines     = aiResponse.Split('\n');
            var plainText = new StringBuilder();
            int i         = 0;

            while (i < lines.Length)
            {
                var line = lines[i];

                // [NEW FILE: path] marker
                var newFileMatch = NewFileHeader.Match(line);
                if (newFileMatch.Success)
                {
                    var filePath = newFileMatch.Groups[1].Value.Trim();
                    i++;
                    var contentBuilder = new StringBuilder();

                    if (i < lines.Length && lines[i].TrimStart().StartsWith("```"))
                        i++;

                    while (i < lines.Length)
                    {
                        if (lines[i].TrimStart().StartsWith("```") ||
                            NewFileHeader.IsMatch(lines[i]) ||
                            (lines[i].StartsWith("---") && i + 1 < lines.Length && lines[i + 1].StartsWith("+++")))
                            break;
                        contentBuilder.AppendLine(lines[i]);
                        i++;
                    }

                    if (i < lines.Length && lines[i].TrimStart().StartsWith("```"))
                        i++;

                    if (!string.IsNullOrWhiteSpace(filePath))
                    {
                        var nf = new NewFile();
                        nf.FilePath = filePath.Replace('/', System.IO.Path.DirectorySeparatorChar);
                        nf.Content  = contentBuilder.ToString().TrimEnd();
                        result.NewFiles.Add(nf);
                    }
                    continue;
                }

                // Unified diff: --- a/path / --- path
                if ((line.StartsWith("--- a/") || line.StartsWith("--- ")) &&
                    i + 1 < lines.Length && lines[i + 1].StartsWith("+++"))
                {
                    var pathMatch = DiffHeaderA.Match(line);
                    var filePath  = pathMatch.Success ? pathMatch.Groups[1].Value.Trim() : "unknown";

                    var diffBuilder = new StringBuilder();
                    diffBuilder.AppendLine(line);
                    i++;

                    while (i < lines.Length)
                    {
                        var dline = lines[i];
                        if (NewFileHeader.IsMatch(dline)) break;
                        if ((dline.StartsWith("--- ") || dline.StartsWith("--- a/")) &&
                            i + 1 < lines.Length && lines[i + 1].StartsWith("+++")) break;
                        diffBuilder.AppendLine(dline);
                        i++;
                    }

                    var diffText = diffBuilder.ToString();
                    var added    = CountMatches(diffText, @"^\+[^+]", RegexOptions.Multiline);
                    var removed  = CountMatches(diffText, @"^-[^-]",  RegexOptions.Multiline);

                    var fd = new FileDiff();
                    fd.FilePath    = filePath.Replace('/', System.IO.Path.DirectorySeparatorChar);
                    fd.UnifiedDiff = diffText.TrimEnd();
                    fd.LinesAdded   = added;
                    fd.LinesRemoved = removed;
                    result.Diffs.Add(fd);
                    continue;
                }

                plainText.AppendLine(line);
                i++;
            }

            result.PlainText = plainText.ToString().Trim();
            return result;
        }

        private static int CountMatches(string text, string pattern, RegexOptions opts)
        {
            return Regex.Matches(text, pattern, opts).Count;
        }
    }
}
