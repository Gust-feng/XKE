import test from "node:test";
import assert from "node:assert/strict";
import { readFile } from "node:fs/promises";

const html = await readFile(new URL("../index.html", import.meta.url), "utf8");

function collectButtonTags() {
  return Array.from(html.matchAll(/<button\b[\s\S]*?>/gi)).map((match) => ({
    tag: match[0].replace(/\s+/g, " ").trim(),
    line: html.slice(0, match.index).split(/\r?\n/).length,
  }));
}

test("login fields are inside a real form with a submit button", () => {
  assert.match(html, /<form\b[^>]*id="loginForm"[\s\S]*id="inputUser"[\s\S]*id="inputPassword"[\s\S]*id="btnLogin"[\s\S]*type="submit"[\s\S]*<\/form>/);
});

test("non-submit buttons declare type to avoid accidental form submit", () => {
  const missingTypes = collectButtonTags().filter(
    (button) => !/\stype\s*=/.test(button.tag),
  );
  assert.deepEqual(missingTypes, []);
});

test("settings markup does not contain visible broken label/input seams", () => {
  assert.doesNotMatch(html, /^\s*><input\b/m);
  assert.doesNotMatch(html, /<\/label\s*\r?\n\s*><input\b/);
  assert.doesNotMatch(html, /<\/(?:label|span|a|button|strong|p)\s*\r?\n\s*>/);
});
