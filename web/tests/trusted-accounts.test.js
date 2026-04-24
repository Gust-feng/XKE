import test from "node:test";
import assert from "node:assert/strict";

function createLocalStorageMock () {
  const store = new Map();
  return {
    get length () {
      return store.size;
    },
    getItem (key) {
      return store.has(key) ? store.get(key) : null;
    },
    setItem (key, value) {
      store.set(String(key), String(value));
    },
    removeItem (key) {
      store.delete(String(key));
    },
    key (index) {
      return Array.from(store.keys())[index] || null;
    },
    clear () {
      store.clear();
    },
  };
}

async function loadStorageModule () {
  globalThis.localStorage = createLocalStorageMock();
  const moduleUrl = new URL("../assets/modules/storage.js", import.meta.url);
  return import(`${moduleUrl.href}?case=${Date.now()}-${Math.random()}`);
}

async function loadTrustedModule () {
  const moduleUrl = new URL("../assets/modules/trusted-accounts.js", import.meta.url);
  return import(`${moduleUrl.href}?case=${Date.now()}-${Math.random()}`);
}

test("legacy remembered login migrates once into trusted account list", async () => {
  const storage = await loadStorageModule();
  localStorage.setItem(
    "xke-remembered-login",
    JSON.stringify({
      user: "20260001",
      prelogin_mode: "coded",
      trusted_login_token: "trusted-token",
    }),
  );

  const result = storage.migrateRememberedLoginToTrustedAccounts();

  assert.equal(result.migrated, true);
  assert.equal(localStorage.getItem("xke-remembered-login"), null);
  assert.equal(storage.getLastAccount(), "20260001");
  assert.equal(storage.getTrustedAccounts().length, 1);
  assert.equal(storage.getTrustedAccounts()[0].trusted_login_token, "trusted-token");
});

test("trusted accounts upsert, touch, and forget by user", async () => {
  const storage = await loadStorageModule();

  storage.upsertTrustedAccount({
    user: "20260001",
    trusted_login_token: "first-token",
    prelogin_mode: "capture",
  });
  storage.touchTrustedAccount("20260001", {
    trusted_login_token: "next-token",
    prelogin_mode: "coded",
  });

  const account = storage.findTrustedAccount("20260001");
  assert.equal(account.trusted_login_token, "next-token");
  assert.equal(account.prelogin_mode, "coded");

  storage.saveLastAccount("20260001");
  storage.removeTrustedAccount("20260001");
  assert.equal(storage.getTrustedAccounts().length, 0);
  assert.equal(storage.getLastAccount(), "");
});

test("trusted bootstrap chooses session, auto, chooser, or manual entry", async () => {
  const trusted = await loadTrustedModule();
  const account = {
    user: "20260001",
    trusted_login_token: "token",
    prelogin_mode: "capture",
  };

  assert.equal(
    trusted.resolveTrustedBootstrap({ sessionId: "sid", accounts: [account] }).mode,
    "session",
  );
  assert.equal(
    trusted.resolveTrustedBootstrap({ sessionId: "", accounts: [account] }).mode,
    "auto",
  );
  assert.equal(
    trusted.resolveTrustedBootstrap({
      sessionId: "",
      accounts: [account, { ...account, user: "20260002" }],
    }).mode,
    "chooser",
  );
  assert.equal(
    trusted.resolveTrustedBootstrap({ sessionId: "", accounts: [] }).mode,
    "manual",
  );
});

test("boot fallback resolves one final ui state after auth failures", async () => {
  const trusted = await loadTrustedModule();
  const account = {
    user: "20260001",
    trusted_login_token: "token",
    prelogin_mode: "capture",
  };

  assert.deepEqual(
    trusted.resolveAuthBootFallbackState({
      restored: true,
      accounts: [account],
    }).mode,
    "authenticated",
  );
  assert.deepEqual(
    trusted.resolveAuthBootFallbackState({
      blocked: true,
      accounts: [account],
    }).mode,
    "blocked",
  );
  const repair = trusted.resolveAuthBootFallbackState({
    localInitializationIncomplete: true,
    repairUser: "20260001",
    accounts: [account, { ...account, user: "20260002" }],
  });
  assert.equal(repair.mode, "repair");
  assert.equal(repair.prefillUser, "20260001");
  assert.equal(
    trusted.resolveAuthBootFallbackState({
      accounts: [account, { ...account, user: "20260002" }],
    }).mode,
    "chooser",
  );
  assert.equal(
    trusted.resolveAuthBootFallbackState({
      lastAccount: "20260003",
      accounts: [],
    }).mode,
    "manual",
  );
});

test("trust prompt is skipped when the same trusted token already exists", async () => {
  const trusted = await loadTrustedModule();
  const account = {
    user: "20260001",
    trusted_login_token: "token",
    prelogin_mode: "capture",
  };

  assert.equal(
    trusted.shouldPromptToTrustAccount({
      user: "20260001",
      trustedLoginToken: "token",
      accounts: [account],
    }),
    false,
  );
  assert.equal(
    trusted.shouldPromptToTrustAccount({
      user: "20260001",
      trustedLoginToken: "new-token",
      accounts: [account],
    }),
    true,
  );
});
