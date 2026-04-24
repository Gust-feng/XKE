import test from "node:test";
import assert from "node:assert/strict";

import {
  collectProfileHydrationMismatches,
  mergeProfileSnapshots,
  renderStudentProfile,
  updateConnectionUI,
} from "../assets/modules/profile.js";

function createTextRef() {
  return {
    textContent: "",
    disabled: false,
  };
}

test("connection ui uses mobile jw wording for disconnected state", () => {
  const chipStatus = createTextRef();
  const btnConnectJw = createTextRef();

  updateConnectionUI({ chipStatus, btnConnectJw }, false);

  assert.equal(chipStatus.textContent, "未验证移动教务");
  assert.equal(btnConnectJw.textContent, "验证移动教务");
  assert.equal(btnConnectJw.disabled, false);
});

test("connection ui uses mobile jw wording for connected state", () => {
  const chipStatus = createTextRef();
  const btnConnectJw = createTextRef();

  updateConnectionUI({ chipStatus, btnConnectJw }, true);

  assert.equal(chipStatus.textContent, "已验证移动教务");
  assert.equal(btnConnectJw.textContent, "已验证移动教务");
  assert.equal(btnConnectJw.disabled, true);
});

test("profile renderer uses major_name for the major row before falling back to class_name", () => {
  globalThis.document = {
    createElement() {
      let text = "";
      return {
        set textContent(value) {
          text = String(value ?? "");
        },
        get innerHTML() {
          return text
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;");
        },
      };
    },
  };
  const refs = {
    studentName: {
      textContent: "",
      classList: { add() {}, remove() {} },
      setAttribute() {},
      removeAttribute() {},
    },
    studentMeta: { textContent: "", hidden: false },
    profileAcademy: { innerHTML: "", textContent: "" },
    profileMajorClass: { innerHTML: "", textContent: "" },
    profileName: { textContent: "" },
    profileLevel: { innerHTML: "", textContent: "" },
  };
  const avatarController = {
    applyAvatar() {},
    getCurrentAvatarUrl() {
      return "";
    },
  };

  renderStudentProfile({
    refs,
    avatarController,
    profile: {
      student_id: "20260001",
      name: "测试同学",
      major_name: "软件技术",
      class_name: "软件一班",
      avatar_url: "/avatar.png",
    },
  });

  assert.match(refs.profileMajorClass.innerHTML, /软件技术/);
  assert.doesNotMatch(refs.profileMajorClass.innerHTML, /软件一班/);
});

test("profile renderer does not show missing placeholders when backend payload has full fields", () => {
  globalThis.localStorage = {
    getItem() {
      return null;
    },
    setItem() {},
    removeItem() {},
  };
  globalThis.document = {
    createElement() {
      let text = "";
      return {
        set textContent(value) {
          text = String(value ?? "");
        },
        get innerHTML() {
          return text
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;");
        },
      };
    },
  };
  const refs = {
    studentName: {
      textContent: "",
      classList: { add() {}, remove() {} },
      setAttribute() {},
      removeAttribute() {},
    },
    studentMeta: { textContent: "", hidden: false },
    profileAcademy: { innerHTML: "", textContent: "" },
    profileMajorClass: { innerHTML: "", textContent: "" },
    profileName: { textContent: "" },
    profileLevel: { innerHTML: "", textContent: "" },
  };
  const avatarController = {
    applyAvatar() {},
    getCurrentAvatarUrl() {
      return "";
    },
  };

  renderStudentProfile({
    refs,
    avatarController,
    profile: {
      student_id: "20260001",
      name: "测试同学",
      academy_name: "信息工程学院",
      major_name: "信息安全与管理（本）",
      training_level: "普通本科",
    },
  });

  const rendered = [
    refs.profileAcademy.innerHTML,
    refs.profileMajorClass.innerHTML,
    refs.profileLevel.innerHTML,
  ].join(" ");
  assert.doesNotMatch(rendered, /未提供/);
  assert.match(rendered, /信息工程学院/);
  assert.match(rendered, /信息安全与管理/);
  assert.match(rendered, /普通本科/);
});

test("profile hydration diagnostic only reports fields present in payload", () => {
  const partialProfile = {
    student_id: "20260001",
    name: "测试同学",
    academy_name: "信息工程学院",
  };

  assert.deepEqual(
    collectProfileHydrationMismatches(partialProfile, {
      academy: "院系 信息工程学院",
      major_class: "专业 未提供",
      training_level: "培养层次 未提供",
    }),
    [],
  );

  assert.deepEqual(
    collectProfileHydrationMismatches(partialProfile, {
      academy: "院系 未提供",
      major_class: "专业 未提供",
      training_level: "培养层次 未提供",
    }),
    [{ field: "academy_name", rendered_text: "院系 未提供" }],
  );
});

test("profile merge keeps full auth fields when schedule payload is partial", () => {
  const currentProfile = {
    student_id: "20260001",
    name: "测试同学",
    academy_name: "信息工程学院",
    major_name: "软件技术",
    training_level: "专科",
  };

  const merged = mergeProfileSnapshots(currentProfile, {
    student_id: "20260001",
    name: "测试同学",
  });

  assert.equal(merged.academy_name, "信息工程学院");
  assert.equal(merged.major_name, "软件技术");
  assert.equal(merged.training_level, "专科");
});
