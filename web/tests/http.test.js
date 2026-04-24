import test from "node:test";
import assert from "node:assert/strict";

async function loadHttpModule () {
  const moduleUrl = new URL("../assets/modules/http.js", import.meta.url);
  return import(`${moduleUrl.href}?case=${Date.now()}-${Math.random()}`);
}

test("api client aborts stalled requests with a readable timeout error", async (t) => {
  const originalFetch = globalThis.fetch;
  t.after(() => {
    globalThis.fetch = originalFetch;
  });

  const { createApiClient } = await loadHttpModule();
  let abortObserved = false;
  globalThis.fetch = (_url, options = {}) => new Promise((_resolve, reject) => {
    options.signal.addEventListener("abort", () => {
      abortObserved = true;
      const error = new Error("aborted");
      error.name = "AbortError";
      reject(error);
    });
  });

  const client = createApiClient(() => "session-id");
  try {
    await client.post("/auth/login", { user: "20260001" }, { timeoutMs: 5 });
    assert.fail("expected request timeout");
  } catch (error) {
    assert.equal(error.status, 0);
    assert.equal(error.reason, "timeout");
    assert.match(error.message, /请求超时/);
    assert.equal(abortObserved, true);
  }
});

test("api client keeps session headers when custom timeout options are used", async (t) => {
  const originalFetch = globalThis.fetch;
  t.after(() => {
    globalThis.fetch = originalFetch;
  });

  const { SESSION_HEADER_NAME, createApiClient } = await loadHttpModule();
  let capturedUrl = "";
  let capturedHeaders = {};
  globalThis.fetch = async (url, options = {}) => {
    capturedUrl = String(url);
    capturedHeaders = options.headers || {};
    return {
      ok: true,
      json: async () => ({ ok: true, msg: "ok", data: {} }),
    };
  };

  const client = createApiClient(() => "fallback-session");
  const result = await client.getWithSession("/auth/session", "explicit-session", {
    timeoutMs: 50,
  });

  assert.equal(result.ok, true);
  assert.equal(capturedUrl, "/api/auth/session");
  assert.equal(capturedHeaders[SESSION_HEADER_NAME], "explicit-session");
});