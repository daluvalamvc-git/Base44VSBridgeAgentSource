import { createClientFromRequest } from 'npm:@base44/sdk@0.8.31';

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
    const { file_path, original_content, modified_content } = body;

    if (!file_path || original_content === undefined || modified_content === undefined) {
      return new Response(JSON.stringify({ error: "file_path, original_content, and modified_content are required" }), { status: 400, headers: corsHeaders });
    }

    const diff = generateUnifiedDiff(file_path, original_content, modified_content);
    const linesAdded = (diff.match(/^\+[^+]/mg) || []).length;
    const linesRemoved = (diff.match(/^-[^-]/mg) || []).length;

    return new Response(JSON.stringify({
      file_path,
      unified_diff: diff,
      lines_added: linesAdded,
      lines_removed: linesRemoved,
      has_changes: original_content !== modified_content,
    }), { status: 200, headers: corsHeaders });
  } catch (err: any) {
    return new Response(JSON.stringify({ error: err.message }), { status: 500, headers: corsHeaders });
  }
});

function generateUnifiedDiff(filePath: string, original: string, modified: string): string {
  const header = `--- a/${filePath}\n+++ b/${filePath}\n`;
  if (original === modified) return header + '// No changes detected.';

  const origLines = original.split('\n');
  const modLines = modified.split('\n');
  const CONTEXT = 3;

  type Change = { type: 'eq' | 'del' | 'ins' | 'rep'; orig?: string; mod?: string; origIdx: number; modIdx: number };
  const changes: Change[] = [];
  let oi = 0, mi = 0;

  while (oi < origLines.length || mi < modLines.length) {
    if (oi < origLines.length && mi < modLines.length && origLines[oi] === modLines[mi]) {
      changes.push({ type: 'eq', orig: origLines[oi], mod: modLines[mi], origIdx: oi, modIdx: mi });
      oi++; mi++;
    } else if (oi < origLines.length && mi < modLines.length) {
      let found = false;
      for (let la = 1; la <= 5; la++) {
        if (mi + la < modLines.length && origLines[oi] === modLines[mi + la]) {
          for (let k = 0; k < la; k++) { changes.push({ type: 'ins', mod: modLines[mi + k], origIdx: oi, modIdx: mi + k }); mi++; }
          found = true; break;
        }
        if (oi + la < origLines.length && modLines[mi] === origLines[oi + la]) {
          for (let k = 0; k < la; k++) { changes.push({ type: 'del', orig: origLines[oi + k], origIdx: oi + k, modIdx: mi }); oi++; }
          found = true; break;
        }
      }
      if (!found) {
        changes.push({ type: 'rep', orig: origLines[oi], mod: modLines[mi], origIdx: oi, modIdx: mi });
        oi++; mi++;
      }
    } else if (oi < origLines.length) {
      changes.push({ type: 'del', orig: origLines[oi], origIdx: oi, modIdx: mi }); oi++;
    } else {
      changes.push({ type: 'ins', mod: modLines[mi], origIdx: oi, modIdx: mi }); mi++;
    }
  }

  let hunkOutput = '';
  let pendingContext: string[] = [];
  let activeHunk: string[] = [];
  let hunkOrigStart = 1, hunkModStart = 1;
  let hunkOrigCount = 0, hunkModCount = 0;
  let inActiveHunk = false;
  let origLine = 1, modLine = 1;

  for (let ci = 0; ci < changes.length; ci++) {
    const c = changes[ci];
    const isChanged = c.type !== 'eq';

    if (isChanged) {
      if (!inActiveHunk) {
        const ctxLines = pendingContext.slice(-CONTEXT);
        hunkOrigStart = origLine - ctxLines.length;
        hunkModStart = modLine - ctxLines.length;
        hunkOrigCount = ctxLines.length;
        hunkModCount = ctxLines.length;
        activeHunk = ctxLines.map(l => ' ' + l);
        inActiveHunk = true;
        pendingContext = [];
      }
      if (c.type === 'del' || c.type === 'rep') { activeHunk.push('-' + (c.orig ?? '')); hunkOrigCount++; origLine++; }
      if (c.type === 'ins' || c.type === 'rep') { activeHunk.push('+' + (c.mod ?? '')); hunkModCount++; modLine++; }
      if (c.type === 'eq') { activeHunk.push(' ' + (c.orig ?? '')); hunkOrigCount++; hunkModCount++; origLine++; modLine++; }
    } else {
      if (inActiveHunk) {
        activeHunk.push(' ' + (c.orig ?? '')); hunkOrigCount++; hunkModCount++; origLine++; modLine++;
        const nextChange = changes.slice(ci + 1).find(ch => ch.type !== 'eq');
        const distToNext = nextChange ? nextChange.origIdx - c.origIdx : Infinity;
        if (distToNext > CONTEXT * 2) {
          hunkOutput += `@@ -${hunkOrigStart},${hunkOrigCount} +${hunkModStart},${hunkModCount} @@\n`;
          hunkOutput += activeHunk.join('\n') + '\n';
          activeHunk = []; inActiveHunk = false; pendingContext = [];
        }
      } else {
        pendingContext.push(c.orig ?? '');
        if (pendingContext.length > CONTEXT) pendingContext.shift();
        origLine++; modLine++;
      }
    }
  }

  if (inActiveHunk && activeHunk.length > 0) {
    hunkOutput += `@@ -${hunkOrigStart},${hunkOrigCount} +${hunkModStart},${hunkModCount} @@\n`;
    hunkOutput += activeHunk.join('\n') + '\n';
  }

  return header + (hunkOutput || '// No structural diff produced.');
}
