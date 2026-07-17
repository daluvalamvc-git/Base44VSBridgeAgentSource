import { createClientFromRequest } from 'npm:@base44/sdk@0.8.31';

const AGENT_ID = "6a57fda9caabceffcbd70384";
const BASE44_API_BASE = "https://app.base44.com/api/agents";

const corsHeaders = {
  "Content-Type": "application/json",
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
  "Access-Control-Allow-Headers": "Content-Type",
};

const intentInstructions: Record<string, string> = {
  analyze_solution: `Perform a full solution analysis. Detect the architecture pattern (MVC, repository, DI, Unit of Work, etc.), inventory files by type, map dependencies between projects. Flag code smells, anti-patterns, missing test coverage, and any security concerns. Return a structured report with specific file-path references and at least 3 actionable recommendations.`,
  trace_bug: `Trace the described bug across the entire solution. Follow the data flow from the entry point (Controller action or route) through Service, Repository, Model, and View layers. Identify the exact root cause with file and line references. Propose a fix as a unified diff (--- / +++ / @@ format). If relevant files are missing from context, explicitly list exactly which files are needed.`,
  generate_feature: `Generate a complete end-to-end feature following the EXACT conventions of this solution. Produce: Model/ViewModel (matching existing namespace), Controller with constructor DI matching existing pattern, Razor Views (Index/Create/Edit/Details/Delete as applicable), EF Core migration class (matching existing migration style), and any DI registration updates in Startup/Program. Return new files as [NEW FILE: path] with full content, modified files as unified diffs.`,
  refactor_code: `Perform the requested refactoring across the solution. Enumerate all affected files. Generate refactored code that preserves behavior. Return each modified file as a unified diff (--- / +++ / @@ format). Flag any ⚠️ Breaking Changes with explicit migration guidance and list of affected call sites.`,
  explain_flow: `Trace the complete data/request flow through the MVC pipeline. Cover: routing → Controller action → Service layer → Repository → EF Core → database query → result mapping → ViewModel → Razor View. Return a numbered step-by-step explanation with exact file paths and method names at each step. Include a text/ASCII diagram of the flow.`,
  answer_code_qa: `Answer the developer's question using the provided solution context. Reference specific files, classes, methods, and line numbers where possible. Suggest improvements or alternative approaches if applicable. Be direct and technical.`,
};

function buildContextMessage(body: any): string {
  const fileTree = body.files.map((f: any) => {
    const status = f.is_included_full ? '[FULL]' : f.content ? '[SUMMARY]' : '[TREE-ONLY]';
    return `  ${status} ${f.file_path} (${f.file_type})`;
  }).join('\n');

  const fileContents = body.files
    .filter((f: any) => f.content)
    .map((f: any) => {
      const label = f.is_included_full ? 'FULL' : 'SUMMARY';
      const ext = f.file_path.endsWith('.cshtml') ? 'html' : 'csharp';
      return `### [${label}] ${f.file_path}\n\`\`\`${ext}\n${f.content}\n\`\`\``;
    }).join('\n\n');

  return `You are dotnet_vs_pilot — a deeply experienced .NET MVC architect embedded in a Visual Studio 2022 extension.

━━━ SOLUTION CONTEXT ━━━
Solution: ${body.solution_name}
Path: ${body.sln_path || 'unknown'}
Active File: ${body.active_file_path || 'none'}
Intent: ${body.intent}

━━━ SOLUTION FILE TREE ━━━
${fileTree}

${fileContents ? `━━━ FILE CONTENTS ━━━\n${fileContents}` : ''}

━━━ TASK ━━━
${intentInstructions[body.intent] || 'Answer the developer\'s question about this .NET MVC solution.'}

━━━ OPERATING RULES ━━━
- Always match existing conventions (DI style, repository pattern, naming, namespaces, folder structure).
- Return file changes as unified diffs (--- a/path +++ b/path @@ hunk format) or [NEW FILE: path] blocks with full content.
- Flag breaking changes with ⚠️ and provide migration guidance.
- If you need files not in the context, say exactly: "I need to see [file path] to confirm."
- Lead with the answer. Be specific and technical. Reference exact file paths and method names.
- Never suggest new NuGet packages unless the solution already references them or the user explicitly requests it.

━━━ USER PROMPT ━━━
${body.prompt}`;
}

Deno.serve(async (req: Request) => {
  if (req.method === "OPTIONS") return new Response(null, { status: 204, headers: corsHeaders });

  try {
    const body = await req.json();

    if (!body.api_key) {
      return new Response(JSON.stringify({ error: "api_key is required" }), { status: 401, headers: corsHeaders });
    }
    if (!body.intent || !body.prompt || !body.solution_name || !body.files) {
      return new Response(JSON.stringify({ error: "intent, prompt, solution_name, and files are required" }), { status: 400, headers: corsHeaders });
    }

    // Step 1: Get or create a Base44 conversation
    let conversationId = body.conversation_id;
    if (!conversationId) {
      const convResp = await fetch(`${BASE44_API_BASE}/${AGENT_ID}/conversations`, {
        method: "POST",
        headers: { "Content-Type": "application/json", "api_key": body.api_key },
        body: JSON.stringify({ title: `VS2022: ${body.solution_name}` }),
      });

      if (!convResp.ok) {
        const err = await convResp.text();
        return new Response(JSON.stringify({ error: `Failed to create conversation: ${err}` }), { status: convResp.status, headers: corsHeaders });
      }

      const convData = await convResp.json();
      conversationId = convData.id || convData.conversation_id || convData._id;
    }

    // Step 2: Build and send the message
    const fullMessage = buildContextMessage(body);

    const msgResp = await fetch(`${BASE44_API_BASE}/${AGENT_ID}/conversations/${conversationId}/messages`, {
      method: "POST",
      headers: { "Content-Type": "application/json", "api_key": body.api_key },
      body: JSON.stringify({ content: fullMessage, role: "user" }),
    });

    if (!msgResp.ok) {
      const err = await msgResp.text();
      return new Response(JSON.stringify({ error: `Agent API error: ${err}` }), { status: msgResp.status, headers: corsHeaders });
    }

    const msgData = await msgResp.json();
    const responseText = msgData.content ?? msgData.message ?? JSON.stringify(msgData);

    return new Response(JSON.stringify({
      conversation_id: conversationId,
      message_id: msgData.id,
      response: responseText,
      intent: body.intent,
      solution_name: body.solution_name,
      files_sent_full: body.files.filter((f: any) => f.is_included_full).length,
      files_sent_summary: body.files.filter((f: any) => !f.is_included_full && f.content).length,
      files_tree_only: body.files.filter((f: any) => !f.content).length,
    }), { status: 200, headers: corsHeaders });

  } catch (err: any) {
    return new Response(JSON.stringify({ error: err.message }), { status: 500, headers: corsHeaders });
  }
});
