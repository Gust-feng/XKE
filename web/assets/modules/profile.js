import { esc } from "./format.js";
import { getCachedAvatarUrl } from "./storage.js";

/**
 * Profile area rendering is isolated from the auth/session flow.
 *
 * The caller decides when profile data is authoritative; this module only
 * reflects the chosen snapshot into the sidebar UI.
 */
export function renderStudentProfile({ profile, refs, avatarController }) {
  const {
    studentName,
    studentMeta,
    profileAcademy,
    profileMajorClass,
    profileName,
    profileLevel,
  } = refs;

  if (!profile || (!profile.name && !profile.student_id)) {
    avatarController.applyAvatar(getCachedAvatarUrl());
    if (studentName) {
      studentName.textContent = "未识别姓名";
      studentName.classList.remove("is-editable");
      studentName.removeAttribute("title");
    }
    if (studentMeta) {
      studentMeta.hidden = false;
      studentMeta.textContent = "登录后自动读取学生资料并按学号归档";
    }
    profileAcademy.textContent = "学院未提供";
    profileMajorClass.textContent = "专业班级未提供";
    profileName.textContent = "姓名未提供";
    profileLevel.textContent = "培养层次未提供";
    return;
  }

  const hasNickname = Boolean(profile.use_nickname && profile.nickname);
  const displayName = hasNickname
    ? profile.nickname
    : profile.name || profile.student_id || "未识别姓名";
  const currentAvatarUrl =
    avatarController &&
    typeof avatarController.getCurrentAvatarUrl === "function"
      ? avatarController.getCurrentAvatarUrl()
      : "";
  const resolvedAvatarUrl =
    profile.avatar_url || getCachedAvatarUrl() || currentAvatarUrl || "";
  if (resolvedAvatarUrl || !currentAvatarUrl) {
    avatarController.applyAvatar(resolvedAvatarUrl);
  }

  if (studentName) {
    studentName.textContent = displayName;
    studentName.classList.add("is-editable");
    studentName.setAttribute(
      "title",
      hasNickname ? `当前昵称：${displayName}，点击修改` : "点击自定义昵称",
    );
  }
  if (studentMeta) {
    studentMeta.textContent = "";
    studentMeta.hidden = true;
  }

  profileAcademy.innerHTML =
    '<span class="detail-label">院系</span><span class="detail-val">' +
    esc(profile.academy_name || "未提供") +
    "</span>";
  profileMajorClass.innerHTML =
    '<span class="detail-label">专业</span><span class="detail-val">' +
    esc(profile.major_name || profile.class_name || "未提供") +
    "</span>";
  profileName.textContent = profile.name || displayName;
  profileLevel.innerHTML =
    '<span class="detail-label">培养层次</span><span class="detail-val">' +
    esc(profile.training_level || "未提供") +
    "</span>";
}

function normalizeDiagnosticText(value) {
  return String(value || "").replace(/\s+/g, " ").trim();
}

function hasProfileValue(value) {
  return normalizeDiagnosticText(value).length > 0;
}

export function collectProfileHydrationMismatches(profile = {}, renderedFields = {}) {
  if (!profile || typeof profile !== "object") return [];

  const checks = [
    {
      field: "academy_name",
      expected: profile.academy_name,
      rendered: renderedFields.academy,
    },
    {
      field: profile.major_name ? "major_name" : "class_name",
      expected: profile.major_name || profile.class_name,
      rendered: renderedFields.major_class,
    },
    {
      field: "training_level",
      expected: profile.training_level,
      rendered: renderedFields.training_level,
    },
  ];

  return checks
    .filter((item) => hasProfileValue(item.expected))
    .map((item) => ({
      field: item.field,
      rendered_text: normalizeDiagnosticText(item.rendered),
    }))
    .filter((item) => item.rendered_text.includes("未提供"));
}

export function mergeProfileSnapshots(currentProfile = null, incomingProfile = null) {
  if (!incomingProfile || typeof incomingProfile !== "object") {
    return currentProfile ? { ...currentProfile } : null;
  }

  const merged =
    currentProfile && typeof currentProfile === "object"
      ? { ...currentProfile }
      : {};
  for (const [key, value] of Object.entries(incomingProfile)) {
    if (hasProfileValue(value)) {
      merged[key] = value;
    }
  }

  return Object.keys(merged).length ? merged : null;
}

export function updateConnectionUI({ chipStatus, btnConnectJw }, connected) {
  const ok = Boolean(connected);
  if (chipStatus) chipStatus.textContent = ok ? "已验证移动教务" : "未验证移动教务";
  if (!btnConnectJw) return;
  btnConnectJw.textContent = ok ? "已验证移动教务" : "验证移动教务";
  btnConnectJw.disabled = ok;
}
