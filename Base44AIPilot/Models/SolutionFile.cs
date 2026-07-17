using System;
using System.Collections.Generic;

namespace Base44AIPilot.Models
{
    public class SolutionFile
    {
        public string FilePath { get; set; }
        public string FileType { get; set; }
        public string Language { get; set; }
        public string Content  { get; set; }
        public int TokenEstimate { get { return (int)Math.Ceiling(Content.Length / 4.0); } }

        public SolutionFile()
        {
            FilePath = string.Empty;
            FileType = "Other";
            Language = "csharp";
            Content  = string.Empty;
        }
    }

    public class ChunkedFile
    {
        public string FilePath       { get; set; }
        public string FileType       { get; set; }
        public bool   IsIncludedFull { get; set; }
        public string Content        { get; set; }
        public string ContentSummary { get; set; }
        public int    TokenEstimate  { get; set; }

        public ChunkedFile()
        {
            FilePath = string.Empty;
            FileType = "Other";
        }
    }

    public class SolutionInfo
    {
        public string SolutionName { get; set; }
        public string SlnPath      { get; set; }
        public int    ProjectCount { get; set; }
        public int    FileCount    { get; set; }

        public SolutionInfo()
        {
            SolutionName = string.Empty;
            SlnPath      = string.Empty;
        }
    }

    public class ChunkRequest
    {
        public List<SolutionFile> Files          { get; set; }
        public string             ActiveFilePath { get; set; }
        public int                TokenBudget    { get; set; }

        public ChunkRequest()
        {
            Files       = new List<SolutionFile>();
            TokenBudget = 12000;
        }
    }

    public class ChunkResponse
    {
        public int               TotalFiles      { get; set; }
        public int               IncludedFull    { get; set; }
        public int               IncludedSummary { get; set; }
        public int               TreeOnly        { get; set; }
        public int               TokensUsed      { get; set; }
        public List<ChunkedFile> ChunkedFiles    { get; set; }

        public ChunkResponse()
        {
            ChunkedFiles = new List<ChunkedFile>();
        }
    }

    public class ChatRequest
    {
        public string            ApiKey         { get; set; }
        public string            ConversationId { get; set; }
        public string            Intent         { get; set; }
        public string            Prompt         { get; set; }
        public string            SolutionName   { get; set; }
        public string            SlnPath        { get; set; }
        public string            ActiveFilePath { get; set; }
        public List<ChunkedFile> Files          { get; set; }

        public ChatRequest()
        {
            ApiKey       = string.Empty;
            Intent       = "answer_code_qa";
            Prompt       = string.Empty;
            SolutionName = string.Empty;
            Files        = new List<ChunkedFile>();
        }
    }

    public class ChatResponse
    {
        public string ConversationId  { get; set; }
        public string MessageId       { get; set; }
        public string Response        { get; set; }
        public int    FilesSentFull    { get; set; }
        public int    FilesSentSummary { get; set; }
        public int    FilesTreeOnly    { get; set; }
        public string Error           { get; set; }

        public ChatResponse()
        {
            Response = string.Empty;
        }
    }

    public class ParsedResponse
    {
        public string        PlainText { get; set; }
        public List<NewFile> NewFiles  { get; set; }
        public List<FileDiff> Diffs   { get; set; }
        public bool HasChanges { get { return NewFiles.Count > 0 || Diffs.Count > 0; } }

        public ParsedResponse()
        {
            PlainText = string.Empty;
            NewFiles  = new List<NewFile>();
            Diffs     = new List<FileDiff>();
        }
    }

    public class NewFile
    {
        public string FilePath { get; set; }
        public string Content  { get; set; }

        public NewFile()
        {
            FilePath = string.Empty;
            Content  = string.Empty;
        }
    }

    public class FileDiff
    {
        public string FilePath    { get; set; }
        public string UnifiedDiff { get; set; }
        public int    LinesAdded   { get; set; }
        public int    LinesRemoved { get; set; }

        public FileDiff()
        {
            FilePath    = string.Empty;
            UnifiedDiff = string.Empty;
        }
    }
}
