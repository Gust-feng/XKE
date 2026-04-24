#!/usr/bin/env node
import { spawn } from "node:child_process";
import { createInterface } from "node:readline/promises";
import { stdin as input, stdout as output } from "node:process";
import { mkdir, mkdtemp, writeFile } from "node:fs/promises";
import { fileURLToPath } from "node:url";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";

const repoRoot = resolve(fileURLToPath(new URL("..", import.meta.url)));
const projectPath = join(repoRoot, "src", "Xke.WebHost", "Xke.WebHost.csproj");

function readArg(name) {
  const prefix = `--${name}=`;
  const item = process.argv.slice(2).find((value) => value.startsWith(prefix));
  return item ? item.slice(prefix.length) : "";
}

function hasArg(name) {
  const prefix = `--${name}=`;
  return process.argv.slice(2).some((value) => value === `--${name}` || value.startsWith(prefix));
}

async function promptVisible(label, fallback = "") {
  const rl = createInterface({ input, output });
  try {
    const answer = await rl.question(`${label}${fallback ? ` (${fallback})` : ""}: `);
    return (answer || fallback).trim();
  } finally {
    rl.close();
  }
}

async function promptHidden(label) {
  if (!input.isTTY) {
    return promptVisible(label);
  }

  return new Promise((resolvePrompt) => {
    const chars = [];
    output.write(`${label}: `);
    input.setRawMode(true);
    input.resume();
    const onData = (buffer) => {
      const value = buffer.toString("utf8");
      if (value === "\r" || value === "\n") {
        input.setRawMode(false);
        input.pause();
        input.off("data", onData);
        output.write("\n");
        resolvePrompt(chars.join(""));
        return;
      }
      if (value === "\u0003") {
        input.setRawMode(false);
        input.pause();
        process.exit(130);
      }
      if (value === "\b" || value === "\u007f") {
        chars.pop();
        return;
      }
      chars.push(value);
      output.write("*");
    };
    input.on("data", onData);
  });
}

function pickPort() {
  return 55200 + Math.floor(Math.random() * 800);
}

async function delay(ms) {
  await new Promise((resolveDelay) => setTimeout(resolveDelay, ms));
}

async function fetchJson(url, options = {}) {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), options.timeoutMs || 120000);
  try {
    const response = await fetch(url, {
      ...options,
      signal: controller.signal,
      headers: {
        "content-type": "application/json",
        ...(options.headers || {}),
      },
    });
    const text = await response.text();
    let body = {};
    try {
      body = text ? JSON.parse(text) : {};
    } catch {
      body = { detail: text };
    }
    if (!response.ok || body.ok === false) {
      const error = new Error(body.msg || body.detail || `HTTP ${response.status}`);
      error.status = response.status;
      error.body = body;
      throw error;
    }
    return body;
  } finally {
    clearTimeout(timer);
  }
}

async function waitForHealth(baseUrl, host = null) {
  const deadline = Date.now() + 60000;
  let lastError = null;
  while (Date.now() < deadline) {
    if (host?.spawnError) {
      throw host.spawnError;
    }
    if (host?.child && host.child.exitCode !== null) {
      throw new Error(`WebHost exited before health check completed: ${host.child.exitCode}`);
    }
    try {
      return await fetchJson(`${baseUrl}/api/health`, { timeoutMs: 5000 });
    } catch (error) {
      lastError = error;
      await delay(1000);
    }
  }
  throw lastError || new Error("WebHost did not become healthy");
}

function startHost({ dataRoot, port }) {
  const env = {
    ...process.env,
    XKE_DOTNET_DATA_DIR: dataRoot,
    ASPNETCORE_URLS: `http://127.0.0.1:${port}`,
  };
  const child = spawn("dotnet", ["run", "--project", projectPath, "--no-launch-profile"], {
    cwd: repoRoot,
    env,
    windowsHide: true,
    stdio: ["ignore", "pipe", "pipe"],
  });
  const logs = [];
  let spawnError = null;
  child.stdout.on("data", (chunk) => logs.push(chunk.toString("utf8")));
  child.stderr.on("data", (chunk) => logs.push(chunk.toString("utf8")));
  child.on("error", (error) => {
    spawnError = error;
    logs.push(`HOST_SPAWN_ERROR: ${error.message}`);
  });
  return {
    child,
    logs,
    get spawnError() {
      return spawnError;
    },
  };
}

async function stopHost(host) {
  if (!host || host.child.exitCode !== null) return;
  host.child.kill();
  const exited = await new Promise((resolveStop) => {
    const timer = setTimeout(() => resolveStop(false), 5000);
    host.child.once("exit", () => {
      clearTimeout(timer);
      resolveStop(true);
    });
  });
  if (exited || host.child.exitCode !== null) return;
  host.child.kill("SIGKILL");
  await new Promise((resolveStop) => {
    const timer = setTimeout(resolveStop, 3000);
    host.child.once("exit", () => {
      clearTimeout(timer);
      resolveStop();
    });
  });
}

function assertPayload(condition, message, detail = {}) {
  if (condition) return;
  const error = new Error(message);
  error.detail = detail;
  throw error;
}

function summarizeStudent(student = {}) {
  return {
    student_id: student.student_id || "",
    has_name: Boolean(student.name),
    has_academy_name: Boolean(student.academy_name),
    has_major_name: Boolean(student.major_name),
    has_training_level: Boolean(student.training_level),
  };
}

async function main() {
  if (hasArg("password")) {
    throw new Error("mobile-real-smoke 不接受 --password 参数；请使用交互隐藏输入，或显式设置 XKE_REAL_PASSWORD_UNSAFE 环境变量。");
  }

  const user = readArg("user") || process.env.XKE_REAL_USER || await promptVisible("移动教务账号");
  const password = process.env.XKE_REAL_PASSWORD_UNSAFE || await promptHidden("移动教务密码");
  const dataRoot = readArg("data-dir")
    || await mkdtemp(join(tmpdir(), "xke-mobile-real-smoke-"));
  const port = Number(readArg("port")) || pickPort();
  const baseUrl = `http://127.0.0.1:${port}`;
  const diagnosticsDir = join(dataRoot, "diagnostics");
  await mkdir(diagnosticsDir, { recursive: true });

  const summary = {
    data_root: dataRoot,
    base_url: baseUrl,
    started_at: new Date().toISOString(),
    keep_data_root: true,
    steps: [],
  };

  let host = null;
  try {
    host = startHost({ dataRoot, port });
    const health = await waitForHealth(baseUrl, host);
    summary.health = health;
    summary.steps.push({ name: "health", ok: true });

    const login = await fetchJson(`${baseUrl}/api/auth/login`, {
      method: "POST",
      body: JSON.stringify({ user, password, prelogin_mode: "capture" }),
    });
    const loginData = login.data || {};
    const loginRows = Array.isArray(loginData.cached_schedule?.rows)
      ? loginData.cached_schedule.rows.length
      : 0;
    assertPayload(loginData.session_id, "首次初始化未返回 session_id", loginData);
    assertPayload(loginData.student?.student_id && loginData.student?.name, "首次初始化未拿到完整身份", loginData.student);
    assertPayload(loginRows > 0, "首次初始化未拿到可用课表", loginData.cached_schedule);
    summary.initial_login = {
      connected: Boolean(loginData.connected),
      student: summarizeStudent(loginData.student),
      cached_schedule_rows: loginRows,
      week_label: loginData.cached_schedule?.week_label || "",
      remote_initialized: true,
    };
    summary.steps.push({ name: "initial-login", ok: true });

    await stopHost(host);
    host = startHost({ dataRoot, port });
    await waitForHealth(baseUrl, host);
    summary.steps.push({ name: "restart-before-local-credential-login", ok: true });

    const localLogin = await fetchJson(`${baseUrl}/api/auth/login`, {
      method: "POST",
      body: JSON.stringify({ user, password, prelogin_mode: "capture" }),
    });
    const localData = localLogin.data || {};
    assertPayload(localData.session_id, "本地凭据登录未返回 session_id", localData);
    assertPayload(localData.connected === false, "重启后的本地凭据登录不应复用远端连接状态", localData);
    summary.local_login = {
      after_host_restart: true,
      connected: Boolean(localData.connected),
      cached_schedule_rows: Array.isArray(localData.cached_schedule?.rows)
        ? localData.cached_schedule.rows.length
        : 0,
    };
    summary.steps.push({ name: "local-credential-login", ok: true });

    const localSchedule = await fetchJson(`${baseUrl}/api/schedule/local?session_id=${encodeURIComponent(localData.session_id)}`);
    summary.local_schedule = {
      rows: Array.isArray(localSchedule.data?.rows) ? localSchedule.data.rows.length : 0,
      week_label: localSchedule.data?.week_label || "",
      cached: Boolean(localSchedule.data?.cached),
    };
    assertPayload(summary.local_schedule.rows > 0, "本地课表读取为空", localSchedule.data);
    summary.steps.push({ name: "local-schedule", ok: true });

    await stopHost(host);
    host = startHost({ dataRoot, port });
    await waitForHealth(baseUrl, host);
    summary.steps.push({ name: "restart-before-session-restore", ok: true });
    const restored = await fetchJson(`${baseUrl}/api/auth/session?session_id=${encodeURIComponent(localData.session_id)}`);
    summary.restored_session = {
      connected: Boolean(restored.data?.connected),
      student: summarizeStudent(restored.data?.student),
      cached_schedule_rows: Array.isArray(restored.data?.cached_schedule?.rows)
        ? restored.data.cached_schedule.rows.length
        : 0,
    };
    assertPayload(summary.restored_session.connected === false, "重启后的 session 不应标记为已远端连接", restored.data);
    assertPayload(summary.restored_session.cached_schedule_rows > 0, "重启后的 session 未恢复本地课表", restored.data);
    summary.steps.push({ name: "restart-session-restore", ok: true });

    try {
      const connect = await fetchJson(`${baseUrl}/api/auth/connect?session_id=${encodeURIComponent(localData.session_id)}`, {
        method: "POST",
        body: JSON.stringify({ prelogin_mode: "capture" }),
      });
      summary.connect = { ok: true, connected: Boolean(connect.data?.connected) };
    } catch (error) {
      summary.connect = {
        ok: false,
        message: error.message,
        status: error.status || 0,
      };
      const stillLocal = await fetchJson(`${baseUrl}/api/schedule/local?session_id=${encodeURIComponent(localData.session_id)}`);
      assertPayload(Array.isArray(stillLocal.data?.rows) && stillLocal.data.rows.length > 0, "connect 失败后本地课表不可用", stillLocal.data);
      summary.connect.local_state_preserved = true;
    }
    summary.steps.push({ name: "connect-or-preserve-local-state", ok: true });
  } catch (error) {
    summary.failed = {
      message: error.message,
      status: error.status || 0,
      detail: error.detail || error.body || {},
    };
    throw error;
  } finally {
    await stopHost(host);
    summary.finished_at = new Date().toISOString();
    summary.host_logs_tail = host && Array.isArray(host.logs)
      ? host.logs.join("").slice(-8000)
      : "";
    const summaryPath = join(diagnosticsDir, "mobile-real-smoke-summary.json");
    await writeFile(summaryPath, `${JSON.stringify(summary, null, 2)}\n`, "utf8");
    console.log(JSON.stringify({ ok: !summary.failed, summary_path: summaryPath, data_root: dataRoot }, null, 2));
  }
}

main().catch((error) => {
  console.error(error.message);
  process.exitCode = 1;
});
