/**
 * FuelStock Agent Pipeline
 *
 * Runs 4 agents in sequence with feedback loops:
 *
 *   Research → Developer ⇄ Research (clarification, max 5 loops)
 *                    ↕
 *                   QA  (if issues → back to Developer, default 5 loops, then asks you)
 *                    ↓
 *                PR Check
 *
 * Usage: npx tsx pipeline.ts "your feature idea"
 */

import { runResearch, log } from "./research.ts";
import { runDeveloper } from "./developer.ts";
import { runQA } from "./qa.ts";
import { runPRCheck } from "./pr-check.ts";
import * as fs from "fs";
import * as readline from "readline";

const DEFAULT_QA_LOOPS = 5;
const MEMORY_FILE = new URL("./pipeline-memory.json", import.meta.url).pathname;

// ── Types ──────────────────────────────────────────────────────────────────

interface PipelineRun {
  runId: string;
  date: string;
  idea: string;
  intent: string;
  branch: string;
  prUrl: string;
  filesChanged: string[];
  qaLoops: number;
  testsPassed: number;
  testsFailed: number;
  qaIssuesRemaining: string[];
  ciPassed: boolean;
  allGood: boolean;
  durationSeconds: number;
}

interface PipelineMemory {
  lastUpdated: string;
  runs: PipelineRun[];
}

// ── Memory helpers ─────────────────────────────────────────────────────────

function readMemory(): PipelineMemory {
  if (!fs.existsSync(MEMORY_FILE)) {
    return { lastUpdated: new Date().toISOString(), runs: [] };
  }
  try {
    return JSON.parse(fs.readFileSync(MEMORY_FILE, "utf-8")) as PipelineMemory;
  } catch {
    return { lastUpdated: new Date().toISOString(), runs: [] };
  }
}

function writeMemory(memory: PipelineMemory, run: PipelineRun) {
  memory.runs.unshift(run);             // most recent first
  if (memory.runs.length > 20) memory.runs = memory.runs.slice(0, 20); // keep last 20
  memory.lastUpdated = new Date().toISOString();
  fs.writeFileSync(MEMORY_FILE, JSON.stringify(memory, null, 2), "utf-8");
  log("PIPELINE", "💾", `Memory updated — ${memory.runs.length} run(s) on record`);
}

function printMemorySummary(memory: PipelineMemory) {
  if (memory.runs.length === 0) {
    log("PIPELINE", "📂", "No previous runs in memory — starting fresh");
    return;
  }

  log("PIPELINE", "📂", `Memory loaded — ${memory.runs.length} previous run(s) found`);
  console.log();

  const last = memory.runs[0];
  console.log(`  \x1b[1mLast run:\x1b[0m`);
  console.log(`    Date:        ${new Date(last.date).toLocaleString("en-AU")}`);
  console.log(`    Feature:     ${last.idea}`);
  console.log(`    Intent:      ${last.intent}`);
  console.log(`    Branch/PR:   ${last.branch} — ${last.prUrl || "no PR"}`);
  console.log(`    Files:       ${last.filesChanged.join(", ") || "none"}`);
  console.log(`    Tests:       ${last.testsPassed} passed, ${last.testsFailed} failed`);
  console.log(`    CI:          ${last.ciPassed ? "✓ passed" : "✗ failed"}`);
  console.log(`    Result:      ${last.allGood ? "✅ all good" : `⚠️  ${last.qaIssuesRemaining.length} issue(s) remaining`}`);
  if (last.qaIssuesRemaining.length > 0) {
    last.qaIssuesRemaining.forEach(i => console.log(`      - ${i}`));
  }
  console.log();

  if (memory.runs.length > 1) {
    console.log(`  \x1b[90mPrevious features built:\x1b[0m`);
    memory.runs.slice(1, 5).forEach(r =>
      console.log(`  \x1b[90m  • ${r.idea} (${new Date(r.date).toLocaleDateString("en-AU")}) — ${r.allGood ? "✓" : "⚠️ "}\x1b[0m`)
    );
    console.log();
  }
}

// ── Stdin helper ───────────────────────────────────────────────────────────

async function askUser(question: string): Promise<string> {
  const rl = readline.createInterface({ input: process.stdin, output: process.stdout });
  return new Promise(resolve => {
    rl.question(question, answer => {
      rl.close();
      resolve(answer.trim());
    });
  });
}

async function promptContinueLoops(loopsDone: number): Promise<number> {
  console.log();
  log("PIPELINE", "🤔", `Reached ${loopsDone} QA loop(s). QA still not passing.`);
  console.log();

  const answer = await askUser(
    `  How many more QA→Developer loops should we run? (default 5, 0 = stop): `
  );

  const parsed = parseInt(answer, 10);
  if (isNaN(parsed) || answer === "") return 5;
  return Math.max(0, parsed);
}

// ── Helpers ────────────────────────────────────────────────────────────────

function banner(title: string, color = "\x1b[34m") {
  const line = "═".repeat(58);
  console.log(`\n${color}${line}\x1b[0m`);
  console.log(`${color}  ${title}\x1b[0m`);
  console.log(`${color}${line}\x1b[0m\n`);
}

function section(phase: number, title: string) {
  console.log(`\n\x1b[1m── Phase ${phase}: ${title} ${"─".repeat(Math.max(0, 46 - title.length))}\x1b[0m\n`);
}

function summary(label: string, value: string, ok: boolean) {
  const icon = ok ? "\x1b[32m✓\x1b[0m" : "\x1b[31m✗\x1b[0m";
  console.log(`  ${icon}  ${label.padEnd(22)} ${value}`);
}

// ── Pipeline ───────────────────────────────────────────────────────────────

const idea = process.argv[2];

if (!idea) {
  console.error('\nUsage: npx tsx pipeline.ts "your feature idea"');
  console.error('Example: npx tsx pipeline.ts "show fuel prices on station cards"\n');
  process.exit(1);
}

const runId = Date.now().toString();
const startTime = Date.now();

banner(`🚀  FuelStock Agent Pipeline`, "\x1b[34m");
log("PIPELINE", "💡", `Feature: "${idea}"`);
log("PIPELINE", "🔑", `Run ID: ${runId}`);
log("PIPELINE", "⚙️ ", `Default QA loops: ${DEFAULT_QA_LOOPS} then human check-in`);

// ── Phase 0: Read memory ───────────────────────────────────────────────────

section(0, "Memory — Familiarity with project history");

const memory = readMemory();
printMemorySummary(memory);

// ── Phase 1: Research ──────────────────────────────────────────────────────

section(1, "Research");

const research = await runResearch(idea, "initial");

log("PIPELINE", "📋", `Research complete — ${research.affected_files.length} file(s) in scope`);
log("PIPELINE", "🧠", `Intent: ${research.intent} — ${research.intentReason}`);

// ── Phase 2: Developer ─────────────────────────────────────────────────────

section(2, "Implementation");

let devOutput = await runDeveloper({ idea, runId, research });

if (devOutput.status === "failed") {
  banner("❌  Pipeline aborted — developer failed", "\x1b[31m");
  process.exit(1);
}

log("PIPELINE", "🔗", `PR created: ${devOutput.prUrl || "(none yet)"}`);

// ── Phase 3: QA → Developer feedback loop (with human check-in) ───────────

section(3, "QA + Developer feedback loop");

let qaLoops = 0;
let totalLoopBudget = DEFAULT_QA_LOOPS;
let qaOutput = await runQA({ idea, branch: devOutput.branch, prUrl: devOutput.prUrl, research });

while (!qaOutput.passed) {
  qaLoops++;
  log("PIPELINE", "🔁", `QA failed — ${qaOutput.issues.length} issue(s), loop ${qaLoops}/${totalLoopBudget}`);
  console.log();

  if (qaLoops >= totalLoopBudget) {
    // Ask the human whether to keep going
    const extraLoops = await promptContinueLoops(qaLoops);

    if (extraLoops === 0) {
      log("PIPELINE", "🛑", `Human chose to stop — moving on with ${qaOutput.issues.length} remaining issue(s)`);
      break;
    }

    totalLoopBudget = qaLoops + extraLoops;
    log("PIPELINE", "▶️ ", `Continuing for ${extraLoops} more loop(s) (budget now ${totalLoopBudget})`);
    console.log();
  }

  devOutput = await runDeveloper({
    idea,
    runId,
    research,
    qaFeedback: qaOutput.issues,
    iteration: qaLoops + 1,
  });

  if (devOutput.status === "failed") {
    log("PIPELINE", "⚠️ ", "Developer failed during QA fix loop — moving on");
    break;
  }

  qaOutput = await runQA({ idea, branch: devOutput.branch, prUrl: devOutput.prUrl, research });
}

// ── Phase 4: PR Check ──────────────────────────────────────────────────────

section(4, "PR Check — CI + Merge Conflicts");

let prCheckOutput = { ciPassed: false, hasConflicts: false, ciStatus: "unknown" as const, issues: [] as string[] };

if (devOutput.prUrl) {
  prCheckOutput = await runPRCheck(devOutput.prUrl, devOutput.branch);
} else {
  log("PIPELINE", "⚠️ ", "No PR URL available — skipping PR check");
}

// ── Final Summary ─────────────────────────────────────────────────────────

const elapsed = ((Date.now() - startTime) / 1000).toFixed(1);

const allGood = devOutput.status === "success"
  && qaOutput.passed
  && prCheckOutput.ciPassed
  && !prCheckOutput.hasConflicts;

banner("📊  Pipeline Summary", "\x1b[34m");

console.log(`  Feature:   ${idea}`);
console.log(`  Run ID:    ${runId}`);
console.log(`  Duration:  ${elapsed}s`);
console.log(`  QA loops:  ${qaLoops} (budget was ${totalLoopBudget})`);
console.log();

summary("Research",        `${research.affected_files.length} file(s) in scope`,  true);
summary("Implementation",  devOutput.status,                                        devOutput.status === "success");
summary("Files changed",   `${devOutput.filesChanged.length} file(s)`,             devOutput.filesChanged.length > 0);
summary("PR",              devOutput.prUrl || "none",                               !!devOutput.prUrl);
summary("Tests",           `${qaOutput.testsPassed} passed, ${qaOutput.testsFailed} failed`, qaOutput.testsFailed === 0);
summary("API health",      qaOutput.apiHealthy ? "healthy" : "unreachable",         qaOutput.apiHealthy);
summary("QA",              qaOutput.passed ? "passed" : `${qaOutput.issues.length} issue(s)`, qaOutput.passed);
summary("CI",              prCheckOutput.ciStatus,                                  prCheckOutput.ciPassed);
summary("Merge conflicts", prCheckOutput.hasConflicts ? "YES — fix needed" : "none", !prCheckOutput.hasConflicts);

const remainingIssues = [
  ...(qaOutput.passed ? [] : qaOutput.issues),
  ...prCheckOutput.issues,
];

console.log();
if (allGood) {
  banner("✅  All checks passed — PR is ready to merge", "\x1b[32m");
} else {
  banner("⚠️   Pipeline finished with issues — see summary above", "\x1b[33m");
  if (remainingIssues.length) {
    console.log("  Remaining issues:");
    remainingIssues.forEach((issue, i) => console.log(`    ${i + 1}. ${issue}`));
    console.log();
  }
}

// ── Phase 5: Write memory ──────────────────────────────────────────────────

section(5, "Memory — Recording this run");

const thisRun: PipelineRun = {
  runId,
  date: new Date().toISOString(),
  idea,
  intent: research.intent,
  branch: devOutput.branch,
  prUrl: devOutput.prUrl || "",
  filesChanged: devOutput.filesChanged,
  qaLoops,
  testsPassed: qaOutput.testsPassed,
  testsFailed: qaOutput.testsFailed,
  qaIssuesRemaining: remainingIssues,
  ciPassed: prCheckOutput.ciPassed,
  allGood,
  durationSeconds: parseFloat(elapsed),
};

writeMemory(memory, thisRun);
