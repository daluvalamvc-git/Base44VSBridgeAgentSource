import { createClientFromRequest } from 'npm:@base44/sdk@0.8.31';

const MAX_TOKENS_PER_FILE = 2000;
const CHARS_PER_TOKEN = 4;

function estimateTokens(text: string): number {
  return Math.ceil(text.length / CHARS_PER_TOKEN);
}

function summarizeFile(content: string, maxLines = 30): string {
  const lines = content.split('\n');
  if (lines.length <= maxLines) return content;
  return lines.slice(0, maxLines).join('\n') + `\n// ... [${lines.length - maxLines} more lines truncated for context]`;
}

const PRIORITY: Record<string, number> = {
  Controller: 1, Service: 1, Repository: 2, DbContext: 2,
  Model: 3, ViewModel: 3, Interface: 3, Migration: 4,
  View: 5, Config: 6, Other: 7,
};

Deno.serve(async (req: Request) => {
  const corsHeaders = {
    "Content-Type": "application/json",
    "Access-Control-Allow-Origin": "*",
    "Access-Control-Allow-Methods": "POST, OPTIONS",
    "Access-Control-Allow-Headers": "Content-Type",
  };

  if (req.method === "OPTIONS") return new Response(null, { status: 204, headers: corsHeaders });

  try {
    const body = await req.json();
    const { files, active_file_path, token_budget = 12000 } = body;

    if (!files || !Array.isArray(files)) {
      return new Response(JSON.stringify({ error: "files array is required" }), { status: 400, headers: corsHeaders });
    }

    const sorted = [...files].sort((a: any, b: any) => {
      if (a.file_path === active_file_path) return -1;
      if (b.file_path === active_file_path) return 1;
      return (PRIORITY[a.file_type] || 7) - (PRIORITY[b.file_type] || 7);
    });

    let tokensUsed = 0;
    const chunked: any[] = [];

    for (const file of sorted) {
      const content = file.content || '';
      const fileTokens = estimateTokens(content);
      const isActive = file.file_path === active_file_path;
      const priority = PRIORITY[file.file_type] || 7;

      if (tokensUsed >= token_budget) {
        chunked.push({ file_path: file.file_path, file_type: file.file_type, is_included_full: false, content: null, content_summary: `[tree-only: ${fileTokens} tokens]`, token_estimate: 0 });
        continue;
      }

      const remaining = token_budget - tokensUsed;

      if (isActive || priority <= 2) {
        const maxChars = Math.min(remaining, MAX_TOKENS_PER_FILE) * CHARS_PER_TOKEN;
        const truncated = content.length > maxChars ? content.substring(0, maxChars) + '\n// [truncated]' : content;
        const used = estimateTokens(truncated);
        tokensUsed += used;
        chunked.push({ file_path: file.file_path, file_type: file.file_type, is_included_full: truncated === content, content: truncated, content_summary: null, token_estimate: used });
      } else if (priority <= 4) {
        const summary = summarizeFile(content, 30);
        const used = estimateTokens(summary);
        tokensUsed += used;
        chunked.push({ file_path: file.file_path, file_type: file.file_type, is_included_full: false, content: summary, content_summary: `[summarized: ${fileTokens} tokens in full]`, token_estimate: used });
      } else {
        chunked.push({ file_path: file.file_path, file_type: file.file_type, is_included_full: false, content: null, content_summary: `[tree-only: ${fileTokens} tokens]`, token_estimate: 0 });
      }
    }

    return new Response(JSON.stringify({
      total_files: files.length,
      included_full: chunked.filter((f: any) => f.is_included_full).length,
      included_summary: chunked.filter((f: any) => !f.is_included_full && f.content).length,
      tree_only: chunked.filter((f: any) => !f.content).length,
      tokens_used: tokensUsed,
      token_budget,
      chunked_files: chunked,
    }), { status: 200, headers: corsHeaders });
  } catch (err: any) {
    return new Response(JSON.stringify({ error: err.message }), { status: 500, headers: corsHeaders });
  }
});
