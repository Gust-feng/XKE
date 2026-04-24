import test from "node:test";
import assert from "node:assert/strict";

import {
  createRemoteFailureCooldown,
  createScheduleFetchGate,
  shouldMarkRemoteFailureForAuthError,
} from "../assets/modules/schedule-fetch.js";

test("schedule fetch gate reuses concurrent outer refreshes", async () => {
  let calls = 0;
  let release;
  const gate = createScheduleFetchGate();
  const first = gate.run(
    () =>
      new Promise((resolve) => {
        calls += 1;
        release = () => resolve("ok");
      }),
  );
  const second = gate.run(() => {
    calls += 1;
    return "unexpected";
  });

  release();

  assert.equal(await first, "ok");
  assert.equal(await second, "ok");
  assert.equal(calls, 1);
});

test("schedule fetch gate allows explicit bypass for same-flow follow-up refreshes", async () => {
  const gate = createScheduleFetchGate();
  const events = [];

  const result = await gate.run(async () => {
    events.push("outer");
    const nested = await gate.run(async () => {
      events.push("nested");
      return "nested-ok";
    }, { bypass: true });
    return `outer:${nested}`;
  });

  assert.equal(result, "outer:nested-ok");
  assert.deepEqual(events, ["outer", "nested"]);
});

test("remote failure cooldown suppresses repeated mobile requests for a short window", () => {
  let now = 1000;
  const cooldown = createRemoteFailureCooldown({
    durationMs: 30000,
    now: () => now,
  });

  assert.equal(cooldown.snapshot().active, false);

  cooldown.mark("验证过频");
  assert.equal(cooldown.snapshot().active, true);
  assert.equal(cooldown.snapshot().reason, "验证过频");
  assert.equal(cooldown.snapshot().remaining_ms, 30000);

  now += 30001;
  assert.equal(cooldown.snapshot().active, false);

  cooldown.mark("上游不可用");
  cooldown.clear();
  assert.equal(cooldown.snapshot().active, false);
  assert.equal(cooldown.snapshot().reason, "");
});

test("auth cooldown classification ignores local-only failures", () => {
  assert.equal(
    shouldMarkRemoteFailureForAuthError({
      status: 401,
      message: "本地账号或密码不正确",
    }),
    false,
  );
  assert.equal(
    shouldMarkRemoteFailureForAuthError({
      status: 409,
      message: "本地初始化数据不完整：本地课表缓存为空，请重新初始化或修复本地数据",
    }),
    false,
  );
  assert.equal(
    shouldMarkRemoteFailureForAuthError({
      status: 401,
      message: "移动教务认证失败：账号或密码错误",
    }),
    true,
  );
});
