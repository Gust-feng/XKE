import test from "node:test";
import assert from "node:assert/strict";

import {
  COURSE_DEFERRED_MESSAGE,
  COURSE_DEFERRED_REASON,
  COURSE_GRAB_DEFERRED_ACTION_MESSAGE,
  COURSE_GRAB_DEFERRED_MESSAGE,
  COURSE_GRAB_DEFERRED_REASON,
  canUseCourseCenter,
  createDefaultCourseDeferredState,
  normalizeActiveScheduleSourceValue,
  normalizeCourseConnectionState,
  resolveCourseConnectionMessage,
  resolveScheduleSourceSelection,
} from "../assets/modules/capability-policy.js";
import { SCHEDULE_SOURCE_OPTIONS } from "../assets/modules/constants.js";

test("cookie schedule selection targets the traditional jw cookie flow", () => {
  const selection = resolveScheduleSourceSelection("cookie");
  assert.equal(selection.requestedSource, "cookie");
  assert.equal(selection.source, "cookie");
  assert.equal(selection.endpoint, "cookie");
  assert.equal(selection.deferred, false);
  assert.match(selection.message, /验证码/);
});

test("active schedule source normalization keeps preferred, mobile, and cookie", () => {
  assert.equal(normalizeActiveScheduleSourceValue("preferred"), "preferred");
  assert.equal(normalizeActiveScheduleSourceValue("mobile"), "mobile");
  assert.equal(normalizeActiveScheduleSourceValue("cookie"), "cookie");
  assert.equal(normalizeActiveScheduleSourceValue("jw"), "cookie");
});

test("course connection defaults to deferred until traditional jw slice exists", () => {
  const connection = normalizeCourseConnectionState({});
  assert.equal(connection.connected, false);
  assert.equal(connection.status, "deferred");
  assert.equal(connection.reason, COURSE_DEFERRED_REASON);
  assert.equal(connection.deferred, true);
  assert.equal(canUseCourseCenter(connection), false);
});

test("course connection message stays in deferred posture with a session", () => {
  const message = resolveCourseConnectionMessage(
    {
      connected: false,
      status: "deferred",
      reason: COURSE_DEFERRED_REASON,
    },
    true,
  );
  assert.equal(message, COURSE_DEFERRED_MESSAGE);
});

test("course deferred constants produce one default frontend state", () => {
  const state = createDefaultCourseDeferredState();

  assert.equal(state.course_connection.reason, COURSE_DEFERRED_REASON);
  assert.equal(state.course_connection.message, COURSE_DEFERRED_MESSAGE);
  assert.equal(state.features.grab, false);
  assert.equal(state.features.grab_reason, COURSE_GRAB_DEFERRED_REASON);
  assert.equal(state.grab_state.reason, COURSE_GRAB_DEFERRED_REASON);
  assert.equal(state.grab_state.message, COURSE_GRAB_DEFERRED_MESSAGE);
  assert.match(COURSE_GRAB_DEFERRED_ACTION_MESSAGE, /不会启动线程/);
});

test("schedule source constants expose cookie option for development", () => {
  const cookie = SCHEDULE_SOURCE_OPTIONS.find((item) => item.value === "cookie");
  assert.ok(cookie);
  assert.notEqual(cookie.disabled, true);
  assert.match(cookie.description, /开发态可用/);
});
