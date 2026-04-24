export const DEFERRED_SCHEDULE_SOURCE_MESSAGE =
  "教务系统验证码链路需要人工输入，Cookie 仅作为登录成功后的派生状态。";

export const COURSE_DEFERRED_REASON = "traditional-jw-captcha-deferred";
export const COURSE_DEFERRED_MESSAGE =
  "选课中心框架已接入，传统教务验证码连接后置。";
export const COURSE_GRAB_DEFERRED_REASON = "course-grab-deferred";
export const COURSE_GRAB_DEFERRED_MESSAGE =
  "当前只交付选课中心连接、轮次和课程预览，快速抢课后续单独开发。";
export const COURSE_GRAB_DEFERRED_ACTION_MESSAGE =
  "快速抢课后续单独开发，本轮不会启动线程、轮询或写入抢课状态。";

export function isDeferredScheduleSource(value) {
  void value;
  return false;
}

export function normalizeActiveScheduleSourceValue(value) {
  const cleaned = String(value || "")
    .trim()
    .toLowerCase();
  if (["cookie", "jw", "jw_schedule"].includes(cleaned)) return "cookie";
  return cleaned === "mobile" ? "mobile" : "preferred";
}

export function resolveScheduleSourceSelection(value) {
  const requestedSource = String(value || "")
    .trim()
    .toLowerCase();
  const source = normalizeActiveScheduleSourceValue(requestedSource);
  return {
    requestedSource,
    source,
    endpoint: source === "cookie" ? "cookie" : "preferred",
    deferred: false,
    message: source === "cookie" ? DEFERRED_SCHEDULE_SOURCE_MESSAGE : "",
  };
}

export function normalizeCourseConnectionState(raw = {}) {
  const connection = raw && typeof raw === "object" ? raw : {};
  const connected = Boolean(connection.connected);
  const reason = String(connection.reason || "").trim();
  const status = String(
    connection.status || (connected ? "connected" : "deferred"),
  ).trim();
  const deferred =
    !connected && (status === "deferred" || reason === COURSE_DEFERRED_REASON);

  return {
    connected,
    status: deferred ? "deferred" : status,
    reason: reason || (deferred ? COURSE_DEFERRED_REASON : ""),
    message: String(
      connection.message || (deferred ? COURSE_DEFERRED_MESSAGE : ""),
    ).trim(),
    updated_at: String(connection.updated_at || "").trim(),
    deferred,
  };
}

export function canUseCourseCenter(connection = {}) {
  return Boolean(connection.connected) && !connection.deferred;
}

export function resolveCourseConnectionMessage(connection = {}, hasSession = false) {
  if (!hasSession) return "登录后可查看选课中心状态";
  const normalized = normalizeCourseConnectionState(connection);
  if (canUseCourseCenter(normalized)) {
    return normalized.message || "选课中心已连接";
  }

  return normalized.message || COURSE_DEFERRED_MESSAGE;
}

export function createDefaultCourseDeferredState() {
  return {
    course_connection: {
      connected: false,
      status: "deferred",
      reason: COURSE_DEFERRED_REASON,
      message: COURSE_DEFERRED_MESSAGE,
      updated_at: "",
      deferred: true,
    },
    features: {
      rounds: true,
      preview: true,
      grab: false,
      grab_reason: COURSE_GRAB_DEFERRED_REASON,
    },
    grab_state: {
      status: "deferred",
      running: false,
      deferred: true,
      reason: COURSE_GRAB_DEFERRED_REASON,
      message: COURSE_GRAB_DEFERRED_MESSAGE,
    },
  };
}
