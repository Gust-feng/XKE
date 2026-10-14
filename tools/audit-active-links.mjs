#!/usr/bin/env node
import { readdir, readFile, stat } from "node:fs/promises";
import { resolve, relative } from "node:path";
import { fileURLToPath } from "node:url";

const repoRoot = resolve(fileURLToPath(new URL("..", import.meta.url)));
const backendRoots = [
  "src",
  "tests",
  "tools",
];
const frontendRoots = ["frontend/src", "frontend/tests", "frontend/build"];
const includeFrontend = process.argv.includes("--include-frontend");
const roots = includeFrontend ? [...backendRoots, ...frontendRoots] : backendRoots;
const ignoredDirectories = new Set([
  ".git",
  ".runtime",
  "bin",
  "obj",
  "node_modules",
  "dist",
  "dist-preview",
  "preview",
  "coverage",
]);
const sourceExtensions = /\.(cs|ts|tsx|js|mjs|ps1|html|css|json|md)$/i;
const deferredPattern =
  /deferred|暂未|后置|后续|占位|placeholder|NotImplemented|Status501|501|验证码|OCR|云上传|抢课/i;
const legacyPattern =
  /XKE_DATA_DIR|legacy data|Python legacy|真实 \\.env|\\.env|legacy runtime|core\/notice|core\/remote_sensing/i;
const sensitivePatterns = [
  { name: "student-id-like", re: /20\d{10}/g },
  { name: "quoted-at-secret-like", re: /['"]@[A-Za-z][A-Za-z0-9_!#$%^&*.-]{7,}['"]/g },
  { name: "token-like", re: /(password|passwd|token|secret)\\s*[:=]\\s*['"][^'"]{8,}/gi },
];

async function walk(root, files = []) {
  let entries = [];
  try {
    entries = await readdir(root, { withFileTypes: true });
  } catch {
    return files;
  }

  for (const entry of entries) {
    const path = resolve(root, entry.name);
    if (entry.isDirectory()) {
      if (!ignoredDirectories.has(entry.name)) await walk(path, files);
      continue;
    }
    if (sourceExtensions.test(entry.name)) files.push(path);
  }
  return files;
}

function normalizePath(path) {
  return relative(repoRoot, path).replace(/\\/g, "/");
}

function pushHit(bucket, file, line, text, max = 120) {
  bucket.push({
    file: normalizePath(file),
    line,
    text: text.trim().slice(0, max),
  });
}

function byFileCount(hits) {
  const counts = new Map();
  for (const hit of hits) counts.set(hit.file, (counts.get(hit.file) || 0) + 1);
  return [...counts.entries()]
    .map(([file, count]) => ({ file, count }))
    .sort((a, b) => b.count - a.count || a.file.localeCompare(b.file));
}

const files = [];
for (const root of roots) {
  await walk(resolve(repoRoot, root), files);
}

const metrics = [];
const deferredHits = [];
const legacyHits = [];
const sensitiveHits = [];
const duplicateTerms = new Map([
  ["TemporaryWebRoot", []],
  ["TemporaryDirectory", []],
  ["XkeWebHostService(new XkeWebHostOptions", []],
  ["MissingNoticeRemoteSource", []],
  ["DefaultFakeJwScheduleClient", []],
]);

for (const file of files) {
  const content = await readFile(file, "utf8");
  const lines = content.split(/\r\n|\n|\r/);
  const fileStat = await stat(file);
  metrics.push({
    file: normalizePath(file),
    lines: lines.length,
    bytes: fileStat.size,
  });

  for (let index = 0; index < lines.length; index += 1) {
    const line = lines[index];
    const lineNo = index + 1;
    if (deferredPattern.test(line)) pushHit(deferredHits, file, lineNo, line);
    if (legacyPattern.test(line)) pushHit(legacyHits, file, lineNo, line);
    for (const [term, bucket] of duplicateTerms.entries()) {
      if (line.includes(term)) pushHit(bucket, file, lineNo, line);
    }
    for (const pattern of sensitivePatterns) {
      if (pattern.re.test(line)) {
        sensitiveHits.push({
          file: normalizePath(file),
          line: lineNo,
          pattern: pattern.name,
        });
      }
      pattern.re.lastIndex = 0;
    }
  }
}

metrics.sort((a, b) => b.lines - a.lines || a.file.localeCompare(b.file));
const duplicateSummary = {};
for (const [term, hits] of duplicateTerms.entries()) {
  duplicateSummary[term] = {
    count: hits.length,
    top_files: byFileCount(hits).slice(0, 8),
  };
}

const report = {
  ok: sensitiveHits.length === 0,
  scanned_at: new Date().toISOString(),
  mode: includeFrontend ? "backend-plus-frontend" : "backend",
  scope: roots,
  totals: {
    files: files.length,
    deferred_hits: deferredHits.length,
    legacy_reference_hits: legacyHits.length,
    sensitive_hits: sensitiveHits.length,
  },
  largest_files: metrics.slice(0, 15),
  remaining_deferred_top_files: byFileCount(deferredHits).slice(0, 15),
  duplicate_test_support: duplicateSummary,
  allowed_legacy_boundaries: [
    "Retired mobile authentication and schedule paths remain test-only seams",
    "Production Auth/Schedule use the JW form protocol; old cloud and Updater modules are retired",
    "Core/Releases and DesktopHost/Updates implement the currently authorized GitHub updates",
  ],
  legacy_reference_samples: legacyHits.slice(0, 20),
  sensitive_hits: sensitiveHits,
};

console.log(JSON.stringify(report, null, 2));
if (sensitiveHits.length) process.exitCode = 2;
