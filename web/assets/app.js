import {
  API,
  DEFAULT_SCHEDULE_BLOCKS,
  DEV_MOCK_DAY_OPTIONS,
  NETWORK_HEARTBEAT_INTERVAL_OPTIONS,
  NETWORK_PROVIDER_OPTIONS,
  PRELOGIN_MODE_OPTIONS,
  SCHEDULE_REMINDER_MINUTE_OPTIONS,
  SCHEDULE_AUTO_REFRESH_INTERVAL_OPTIONS,
  SCHEDULE_AUTO_REFRESH_MODE_OPTIONS,
  SCHEDULE_DIFF_ANIMATION_MS,
  SCHEDULE_ROW_HEIGHT,
  SCHEDULE_SOURCE_OPTIONS,
  SCHEDULE_WEEKDAYS,
  SCHEDULE_WEEK_MODE_OPTIONS,
  WEEKEND_IGNORE_DAY_OPTIONS,
} from "./modules/constants.js";
import { createDesktopChromeController } from "./modules/desktop-chrome.js";
import { $, $$, refs } from "./modules/dom.js";
import {
  esc,
  formatMinutesCompact,
  formatSecondsCompact,
  formatWeekLabel,
  parseClockToMinutes,
  parseWeekNumberFromLabel,
  resolveCurrentWeekLabelFromAvailableWeeks,
  resolveWeekdayIndex,
} from "./modules/format.js";
import { API_TIMEOUTS, buildSessionHeaders, createApiClient } from "./modules/http.js";
import {
  collectProfileHydrationMismatches,
  mergeProfileSnapshots,
  renderStudentProfile,
  updateConnectionUI,
} from "./modules/profile.js";
import {
  buildMobileFetchOverridesForMissingWeek,
  createRemoteFailureCooldown,
  createScheduleFetchGate,
  shouldMarkRemoteFailureForAuthError,
} from "./modules/schedule-fetch.js";
import { createSelectMenuController } from "./modules/select-menu.js";
import {
  clearFrontendClientState,
  clearSessionId,
  findTrustedAccount,
  getDevToolsState,
  getLastAccount,
  getNoticeState,
  getTrustedAccounts,
  getSessionId as loadSessionId,
  getThemeForStudent,
  migrateRememberedLoginToTrustedAccounts,
  removeTrustedAccount,
  saveDevToolsState,
  saveLastAccount,
  saveNoticeState,
  saveSessionId,
  saveThemeForStudent,
  touchTrustedAccount,
  upsertTrustedAccount,
} from "./modules/storage.js";
import { createTabsController } from "./modules/tabs.js";
import { createThemeController, resolveThemeId } from "./modules/theme.js";
import {
  resolveAuthBootFallbackState,
  resolveTrustedBootstrap,
  shouldPromptToTrustAccount,
} from "./modules/trusted-accounts.js";
import { createLogger, hideMsg, setLoading, showMsg } from "./modules/ui.js";
import {
  formatNoticeTimestamp,
  looksLikeMarkdownContent,
  normalizeNoticeItem,
  renderMarkdownContent,
  renderPlainTextContent,
  renderUpdatesDetail,
  renderUpdatesList,
  resolveNoticeTone,
  sortNoticeItems,
} from "./modules/updates.js";
import { createDesktopRuntime } from "./modules/desktop.js";
import {
  COURSE_DEFERRED_MESSAGE,
  COURSE_DEFERRED_REASON,
  COURSE_GRAB_DEFERRED_ACTION_MESSAGE,
  COURSE_GRAB_DEFERRED_MESSAGE,
  COURSE_GRAB_DEFERRED_REASON,
  canUseCourseCenter,
  createDefaultCourseDeferredState,
  normalizeCourseConnectionState,
  normalizeActiveScheduleSourceValue,
  resolveCourseConnectionMessage as resolveDeferredCourseMessage,
  resolveScheduleSourceSelection,
} from "./modules/capability-policy.js";

/**
 * XKE Frontend — SPA controller
 * Communicates with the FastAPI backend at /api/*
 */
(function () {
    const FRONTEND_BUILD = "20260425-jw-cookie1";
  const FRONTEND_VERSION = "0.9.9";

  try {
    let sessionId = null;
    let isJwConnected = false;
    let developerOptionsUnlocked = false;
    let developerOptionsEnabled = false;
    let activeWeekdays = [
      "星期一",
      "星期二",
      "星期三",
      "星期四",
      "星期五",
      "星期六",
    ];
    const {
      desktopChromeShell,
      desktopChrome,
      desktopChromeHandle,
      desktopChromeStatus,
      desktopChromePanel,
      btnDesktopMinimize,
      btnDesktopMaximize,
      btnDesktopClose,
      loginCard,
      loginHero,
      loginScene,
      dashboard,
      connDot,
      connText,
      inputUser,
      inputPassword,
      btnPasswordVisibility,
      loginForm,
      selectMode,
      trustedAccountPanel,
      trustedAccountList,
      trustedAccountHint,
      btnTrustedAccountUsePassword,
      btnLogin,
      btnReinitializeAuth,
      loginMsg,
      loginBetaBanner,
      chipSession,
      chipMode,
      chipStatus,
      btnConnectJw,
      btnLogout,
      studentName,
      studentMeta,
      profileAcademy,
      profileMajorClass,
      profileName,
      profileLevel,
      profileAvatar,
      profileAvatarImg,
      profileAvatarIcon,
      btnAvatarUpload,
      inputAvatar,
      hitokotoCard,
      hitokotoContent,
      autoRqText,
      scheduleSettings,
      scheduleSettingsBody,
      btnToggleScheduleSettings,
      schedSourceQuick,
      schedRefreshQuick,
      schedWeekendQuick,
      schedSettingsState,
      schedSource,
      schedSourceCombobox,
      btnSchedSourceSelect,
      schedSourceMenu,
      devOnlyFields,
      schedAutoRefreshEnabled,
      schedAutoRefreshMode,
      schedAutoRefreshModeCombobox,
      btnSchedAutoRefreshModeSelect,
      schedAutoRefreshModeMenu,
      schedAutoRefreshDailyField,
      schedAutoRefreshDailyTime,
      schedAutoRefreshDailyHourCombobox,
      btnSchedAutoRefreshDailyHourSelect,
      schedAutoRefreshDailyHourMenu,
      schedAutoRefreshIntervalField,
      schedAutoRefreshIntervalHours,
      schedAutoRefreshIntervalCombobox,
      btnSchedAutoRefreshIntervalSelect,
      schedAutoRefreshIntervalMenu,
      schedWeekendAutoSwitchEnabled,
      schedWeekendIgnoreGrid,
      schedWeekMode,
      schedWeekModeCombobox,
      btnSchedWeekModeSelect,
      schedWeekModeMenu,
      schedManualWeek,
      schedCookieRq,
      schedTermLabel,
      devMockTime,
      schedAcademicTerm,
      btnSchedFetch,
      loginModeCombobox,
      btnLoginModeSelect,
      loginModeMenu,
      loginModeName,
      loginModeHint,
      loginModeDescription,
      loginModeSettingsBody,
      appearanceSettingsBody,
      appearanceThemeGrid,
      desktopSettingsBody,
      desktopLaunchAtStartup,
      desktopSilentStartup,
      desktopCloseToTray,
      desktopStartupSummary,
      desktopSilentSummary,
      desktopTraySummary,
      devToolsCard,
      developerSettingsCard,
      developerSettingsBody,
      developerSettingsBuildChip,
      developerSettingsHint,
      developerSettingsRuntime,
      developerSettingsChannel,
      developerSettingsBaseUrl,
      developerSettingsWebRoot,
      developerSettingsAssetHash,
      developerSettingsToggle,
      devToolsRuntimeLabel,
      devToolsBuildChip,
      devMuteNoticeReminder,
      devMuteNoticeSummary,
      btnDevReloadUpdates,
      btnDevPreviewReminder,
      btnDevClearFrontendCache,
      networkProvider,
      networkProviderCombobox,
      btnNetworkProviderSelect,
      networkProviderMenu,
      networkHeartbeatEnabled,
      networkHeartbeatInterval,
      networkHeartbeatIntervalCombobox,
      btnNetworkHeartbeatIntervalSelect,
      networkHeartbeatIntervalMenu,
      networkCampusFields,
      networkCampusIp,
      networkCampusSubnetPrefix,
      networkCampusGateway,
      networkCampusDns,
      networkOperatorFields,
      networkAccountLabel,
      networkPasswordLabel,
      networkAccount,
      networkPassword,
      btnNetworkSaveConfig,
      btnNetworkRefresh,
      btnNetworkHeartbeat,
      networkStatusSummary,
      networkCampusStatus,
      networkInternetStatus,
      networkHeartbeatState,
      networkPortalHost,
      networkLocalIp,
      networkAuthIp,
      networkInterfaceName,
      networkSubnetMask,
      networkGateway,
      networkDnsServers,
      networkActiveAccount,
      networkOnlineDuration,
      networkUsedFlow,
      networkCheckedAt,
      networkAttempts,
      schedBoardWrap,
      schedLoading,
      schedLoadingLabel,
      schedBoard,
      schedEmpty,
      schedWarnings,
      schedStatusBar,
      schedInfo,
      schedDataMeta,
      devMockDay,
      devMockDayCombobox,
      btnDevMockDaySelect,
      devMockDayMenu,
      selectRound,
      roundCombobox,
      btnRoundSelect,
      roundMenu,
      btnCourseConnect,
      btnLoadRounds,
      inputZbid,
      btnPreview,
      courseBody,
      courseEmpty,
      courseInfo,
      courseConnectionStatus,
      selectedCourse,
      grabKcid,
      grabJx0404id,
      grabInterval,
      btnGrabStart,
      btnGrabStop,
      grabMsg,
      logArea,
      btnClearLog,
      updatesTab,
      aboutTab,
      updatesTabDot,
      updatesPanelEyebrow,
      updatesPanelTitle,
      updatesPanelSubtitle,
      updatesBlockBanner,
      updatesBlockTitle,
      updatesBlockMessage,
      updatesContentGrid,
      updatesFeedbackCard,
      btnUpdatesToggleFeedback,
      btnUpdatesManualRefresh,
      updatesFeedbackCategoryCombobox,
      btnUpdatesFeedbackCategorySelect,
      updatesFeedbackCategoryMenu,
      updatesFeedbackCategory,
      updatesFeedbackContact,
      updatesFeedbackContent,
      updatesFeedbackStatus,
      btnUpdatesSubmitFeedback,
      updatesFeedStatus,
      updatesMetaTotal,
      updatesMetaUnread,
      updatesMetaSource,
      updatesTimeline,
      updatesEmpty,
      updatesDetail,
      aboutHighlightBuild,
      aboutHighlightUpdateTile,
      aboutHighlightUpdate,
      aboutHighlightUpdateMeta,
      aboutHighlightRuntimeTile,
      aboutHighlightRuntime,
      aboutHighlightRuntimeMeta,
      aboutUpdateBadge,
      aboutBuild,
      aboutUpdateEyebrow,
      aboutUpdateTitle,
      aboutUpdateMessage,
      aboutUpdateAction,
      aboutLifecycleBadge,
      aboutLifecycleStage,
      aboutAppControlState,
      aboutLifecycleMessage,
      btnAboutUploadTelemetry,
      aboutUploadTelemetryStatus,
      aboutLatestVersion,
      aboutMinSupportedVersion,
      aboutDownloadUrl,
      aboutFeatureList,
      devConfigChannel,
      devNoticeSource,
      devConfigUpdatedAt,
      devSoftwarePolicyPath,
      devVersionPolicyPath,
      devUpdateInfoPath,
      devNoticePath,
      noticeReminderToast,
      noticeReminderBadge,
      noticeReminderTitle,
      noticeReminderBody,
      btnNoticeReminderOpen,
      btnNoticeReminderDismiss,
      noticeModalOverlay,
      noticeModalBadge,
      noticeModalTitle,
      noticeModalBody,
      btnNoticeModalDismiss,
    } = refs;

    if (btnNetworkApplyIp) {
      // 未来开发：当前版本暂不开放本机网卡 IP 直接修改入口，统一保持禁用。
      btnNetworkApplyIp.disabled = true;
      btnNetworkApplyIp.title = "暂未开放，未来开发";
      btnNetworkApplyIp.textContent = "暂未开放";
    }

    let scheduleBlocks = DEFAULT_SCHEDULE_BLOCKS.map((item) => ({ ...item }));
    let latestScheduleRows = [];
    let latestScheduleWeekLabel = "";
    let currentScheduleWeekNumber = null;
    let viewedScheduleWeekNumber = null;
    let scheduleNeedsMobileBootstrap = false;
    let scheduleAvailableWeeks = [];
    let scheduleWeekDates = {};
    let schedulePayloadMemoryCache = new Map();
    let schedulePrefetchInFlight = new Map();
    let scheduleLiveTimer = null;
    let scheduleAutoRefreshTimer = null;
    let scheduleWarmupPrefetchTimer = null;
    let lessonReminderLastDateKey = "";
    let lessonReminderPermissionPrimed = false;
    let sentLessonReminderKeys = new Set();
    let networkHeartbeatTimer = null;
    let telemetryTimer = null;
    let remoteConfigTimer = null;
    let courseGrabRuntimeTimer = null;
    let postAuthWarmupTimer = null;
    let scheduleRenderVersion = 0;
    let scheduleFetchVersion = 0;
    let scheduleRenderTimeout = null;
    let resolveScheduleRenderWait = null;
    let scheduleWeekShiftContext = null;
    let scheduleAutoWeekAttemptKey = "";
    let scheduleManualWeekViewLocked = false;
    let currentStudentId = "";
    let currentStudentProfile = null;
    const SETTINGS_DEV_UNLOCK_CLICKS = 7;
    const DEFAULT_AUTO_REFRESH_TIME = "20:00";
    const DEFAULT_AUTO_REFRESH_INTERVAL_HOURS = 4;
    const DEFAULT_COURSE_GRAB_MIN_INTERVAL_SEC = 0.5;
    const DEFAULT_COURSE_GRAB_MAX_RUNTIME_SEC = 5;
    const DEFAULT_COURSE_GRAB_DAILY_COOLDOWN_SEC = 24 * 60 * 60;
    const DEFAULT_NETWORK_PROVIDER = "campus";
    const DEFAULT_NETWORK_HEARTBEAT_INTERVAL_MINUTES = 5;
    const DEFAULT_REMOTE_CONFIG_PULL_INTERVAL_SEC = 21600;
    const DEFAULT_REMOTE_SUMMARY_REPORT_INTERVAL_SEC = 3600;
    const HEALTH_CHECK_INTERVAL_MS = 60 * 1000;
    const HEALTH_CHECK_BACKGROUND_INTERVAL_MS = 5 * 60 * 1000;
    const HEALTH_CHECK_PROTECTION_INTERVAL_MS = 10 * 60 * 1000;
    const MOBILE_REMOTE_FAILURE_COOLDOWN_MS = 30 * 1000;
    const SCHEDULE_LIVE_REFRESH_INTERVAL_MS = 15 * 1000;
    const DEFAULT_POST_AUTH_WARMUP_DELAY_MS = 80;
    const LOW_PERFORMANCE_POST_AUTH_WARMUP_DELAY_MS = 260;
    const RUNTIME_PROTECTION_WATCHDOG_INTERVAL_MS = 5 * 1000;
    const RUNTIME_PROTECTION_LAG_THRESHOLD_MS = 2500;
    const RUNTIME_PROTECTION_LAG_STRIKE_LIMIT = 3;
    const RUNTIME_PROTECTION_SLEEP_RESET_MS = 60 * 1000;
    const TELEMETRY_FEATURE_MODULE_IDS = [
      "auth",
      "schedule",
      "course",
      "network",
      "updates",
      "settings",
      "about",
    ];
    const TELEMETRY_TAB_MODULE_MAP = {
      schedule: "schedule",
      course: "course",
      network: "network",
      settings: "settings",
      updates: "updates",
      about: "about",
    };
    let networkConfigLoaded = false;
    let lastNetworkStatus = null;
    let currentDesktopSettings = createDefaultDesktopSettings();
    let currentBetaStatus = createDefaultBetaStatus();
    let currentRemoteConfig = createDefaultRemoteConfig();
    let currentDevelopmentConfig = createDefaultDevelopmentConfig();
    let currentDevToolsState = createDefaultDevToolsState();
    let settingsDevUnlockProgress = 0;
    let isApplyingDesktopSettings = false;
    let latestUpdatesFeed = [];
    let activeUpdatesItemId = "";
    let latestUpdatesConfig = null;
    let latestReminderItemId = "";
    let telemetrySessionStartedAt = new Date().toISOString();
    let telemetryAccumulatedForegroundMs = 0;
    let telemetryAccumulatedBackgroundMs = 0;
    let telemetryLastTickAt = Date.now();
    let telemetryVisibleState = !document.hidden && document.hasFocus();
    let telemetryCurrentModuleId = "schedule";
    let telemetryFeatureUsageDeltas = createEmptyTelemetryFeatureUsageDeltas();
    let manualTelemetryUploadInFlight = false;
    let remoteMaintenanceInFlight = false;
    let feedbackSubmissionInFlight = false;
    let updatesFeedbackPanelOpen = false;
    let healthCheckInFlight = null;
    let healthCheckTimer = null;
    let lastManualRemoteMaintenanceAt = 0;
    let manualUpdatesRefreshCooldownTimer = null;
    let currentNetworkCredentials = createEmptyNetworkCredentialBundle();
    let currentNetworkCampusDraft = createEmptyNetworkCampusDraft();
    let activeNetworkDraftProvider = DEFAULT_NETWORK_PROVIDER;
    const bootstrapUrlParams = new URLSearchParams(
      window.location.search || "",
    );
    let runtimeProtectionMode =
      bootstrapUrlParams.get("xke_safe_mode") === "1";
    let runtimeProtectionReason = runtimeProtectionMode
      ? "startup-safe-mode"
      : "";
    let runtimeWorkloadSuspended = false;
    let runtimeProtectionWatchdogTimer = null;
    let runtimeProtectionWatchdogLastTickAt = Date.now();
    let runtimeProtectionLagStrikeCount = 0;
    let desktopWindowRuntimeState = {
      visible: true,
      minimized: false,
      state: "normal",
    };
    let tabsController = null;
    const WEEKEND_DAY_INDEX_MAP = {
      saturday: 6,
      sunday: 7,
    };
    let currentPreferences = {
      theme: resolveThemeId(document.documentElement.dataset.theme || ""),
    };
    let currentSettings = createDefaultSettings();
    let trustedAccountUiMode = "manual";
    let lastIncompleteAuthUser = "";
    let bootAuthFlowCompleted = false;
    let connectRequestInFlight = false;
    let profileHydrationDiagnosticLogged = false;
    const performanceLiteMode = detectPerformanceLiteMode();
    let settingsSaveTimer = null;
    let isApplyingStoredSettings = false;
    const nicknameEditState = {
      active: false,
      saving: false,
      initialNickname: "",
      composing: false,
      inputEl: null,
      measureEl: null,
    };
    const NICKNAME_WIDE_CHAR_WEIGHT = 12;
    const NICKNAME_NARROW_CHAR_WEIGHT = 7;
    const NICKNAME_MAX_WEIGHT = 84;
    const TIME_HOUR_OPTIONS = Array.from({ length: 16 }, (_item, index) => {
      const value = String(index + 8).padStart(2, "0");
      return {
        value,
        label: `${value} 时`,
      };
    });

    // Stored courses for selection
    let coursesData = [];
    let courseRounds = [];
    let currentCourseState = createDefaultCourseFrameworkState();
    let isCourseGrabRunning = false;
    let courseGrabRuntimeState = createDefaultCourseGrabRuntimeState();
    const log = createLogger(logArea);
    const apiClient = createApiClient(() => sessionId);
    const { post, get, postWithSession, getWithSession } = apiClient;
    const scheduleFetchGate = createScheduleFetchGate({
      onReuse: () => log("课表刷新正在进行，已复用当前请求以避免重复访问移动教务"),
    });
    const mobileRemoteCooldown = createRemoteFailureCooldown({
      durationMs: MOBILE_REMOTE_FAILURE_COOLDOWN_MS,
    });
    const desktopRuntime = createDesktopRuntime({ log });
    const themeController = createThemeController({
      root: document.documentElement,
      grid: appearanceThemeGrid,
    });
    const desktopChromeController = createDesktopChromeController({
      shell: desktopChromeShell,
      chrome: desktopChrome,
      handle: desktopChromeHandle,
      status: desktopChromeStatus,
      panel: desktopChromePanel,
      btnMinimize: btnDesktopMinimize,
      btnMaximize: btnDesktopMaximize,
      btnClose: btnDesktopClose,
    });
    let avatarController = null;
    let avatarControllerPromise = null;
    let avatarControllerBound = false;
    let hitokotoController = null;
    let hitokotoControllerPromise = null;
    let updatesFeedbackCategoryController = null;
    let hitokotoControllerBound = false;
    let loginHeroMotionPromise = null;
    let loginRequestInFlight = false;
    let reinitializeRequestInFlight = false;

    async function ensureAvatarController() {
      if (avatarController) return avatarController;
      if (avatarControllerPromise) return avatarControllerPromise;
      avatarControllerPromise = import("./modules/avatar.js")
        .then(({ createAvatarController }) => {
          avatarController = createAvatarController({
            elements: {
              profileAvatar,
              profileAvatarImg,
              profileAvatarIcon,
              btnAvatarUpload,
              inputAvatar,
            },
            getSessionId: () => sessionId,
            log,
          });
          return avatarController;
        })
        .finally(() => {
          avatarControllerPromise = null;
        });
      return avatarControllerPromise;
    }

    async function ensureHitokotoController() {
      if (hitokotoController) return hitokotoController;
      if (hitokotoControllerPromise) return hitokotoControllerPromise;
      hitokotoControllerPromise = import("./modules/hitokoto.js")
        .then(({ createHitokotoController }) => {
          hitokotoController = createHitokotoController({
            card: hitokotoCard,
            content: hitokotoContent,
          });
          return hitokotoController;
        })
        .finally(() => {
          hitokotoControllerPromise = null;
        });
      return hitokotoControllerPromise;
    }

    function bindAvatarController() {
      if (avatarControllerBound) return;
      avatarControllerBound = true;
      void ensureAvatarController()
        .then((controller) => {
          controller.bind();
        })
        .catch(() => {
          avatarControllerBound = false;
        });
    }

    function restoreCachedAvatar() {
      void ensureAvatarController().then((controller) => {
        controller.restoreCachedAvatar();
      });
    }

    function bindHitokotoController() {
      if (hitokotoControllerBound) return;
      hitokotoControllerBound = true;
      void ensureHitokotoController()
        .then((controller) => {
          controller.bind();
        })
        .catch(() => {
          hitokotoControllerBound = false;
        });
    }

    function fetchHitokoto(...args) {
      void ensureHitokotoController().then((controller) =>
        controller.fetch(...args),
      );
    }

    function prefetchHitokoto(...args) {
      void ensureHitokotoController().then((controller) =>
        controller.prefetch(...args),
      );
    }

    function initLoginHeroMotionDeferred() {
      if (loginHeroMotionPromise) return;
      loginHeroMotionPromise = import("./modules/login-hero.js")
        .then(({ initLoginHeroMotion }) => {
          initLoginHeroMotion({
            loginCard,
            loginHero,
            loginScene,
            inputUser,
            inputPassword,
            btnPasswordVisibility,
            resolveRememberState: () => getTrustedAccounts().length > 0,
            onSubmit: () => btnLogin.click(),
            reducedEffects:
              runtimeProtectionMode || runtimeWorkloadSuspended || prefersReducedMotion(),
          });
        })
        .catch(() => {})
        .finally(() => {
          loginHeroMotionPromise = null;
        });
    }

    function createDefaultSettings() {
      return {
        appearance: {
          theme: resolveThemeId(document.documentElement.dataset.theme || ""),
        },
        schedule: {
          source: "preferred",
          week_mode: "current",
          manual_week: "",
          cookie_rq: "",
          term_label: "",
          academic_term: "",
          mock_day: "",
          mock_time: "",
          auto_refresh_enabled: false,
          auto_refresh_mode: "daily",
          auto_refresh_daily_time: DEFAULT_AUTO_REFRESH_TIME,
          auto_refresh_interval_hours: DEFAULT_AUTO_REFRESH_INTERVAL_HOURS,
          lesson_reminder_enabled: false,
          lesson_reminder_minutes: 15,
          weekend_auto_switch_enabled: false,
          ignored_weekend_blocks: {
            saturday: [],
            sunday: [],
          },
        },
        auth: {
          prelogin_mode: "capture",
        },
        network: {
          provider: DEFAULT_NETWORK_PROVIDER,
          heartbeat_enabled: false,
          heartbeat_interval_minutes:
            DEFAULT_NETWORK_HEARTBEAT_INTERVAL_MINUTES,
          campus_ip: "",
          campus_subnet_prefix: 24,
          campus_gateway: "",
          campus_dns: "",
        },
        ui: {
          panels: {
            schedule: false,
            login_mode: false,
            appearance: false,
          },
        },
      };
    }

    function createDefaultDesktopSettings() {
      return {
        launch_at_startup: false,
        silent_startup: false,
        close_to_tray: true,
      };
    }

    function createDefaultBetaStatus() {
      return {
        enabled: false,
        expired: false,
        expires_at: "",
        expires_at_text: "",
        message: "",
      };
    }

    function createDefaultDevelopmentConfig() {
      return {
        development: false,
        frozen: false,
        channel: "release",
        runtime: "source-workspace",
        base_url: "",
        web_root: "",
        web_root_found: false,
        frontend_build: "",
        static_asset_hashes: {},
        health_checked_at: "",
      };
    }

    function normalizeDevelopmentConfigPayload(raw = {}) {
      const payload = raw && typeof raw === "object" ? raw : {};
      const defaults = createDefaultDevelopmentConfig();
      return {
        development: Boolean(payload.development),
        frozen: Boolean(payload.frozen),
        channel:
          String(payload.channel || defaults.channel).trim() ||
          defaults.channel,
        runtime:
          String(payload.runtime || defaults.runtime).trim() ||
          defaults.runtime,
        base_url: String(payload.base_url || defaults.base_url).trim(),
        web_root: String(payload.web_root || defaults.web_root).trim(),
        web_root_found: Boolean(payload.web_root_found),
        frontend_build: String(
          payload.frontend_build || defaults.frontend_build,
        ).trim(),
        static_asset_hashes:
          payload.static_asset_hashes &&
          typeof payload.static_asset_hashes === "object"
            ? { ...payload.static_asset_hashes }
            : {},
        health_checked_at: String(
          payload.health_checked_at || defaults.health_checked_at,
        ).trim(),
      };
    }

    function applyHealthDiagnosticsPayload(raw = {}) {
      if (!raw || typeof raw !== "object") return;
      currentDevelopmentConfig = normalizeDevelopmentConfigPayload({
        ...currentDevelopmentConfig,
        base_url: raw.base_url,
        web_root: raw.web_root,
        web_root_found: raw.web_root_found,
        frontend_build: raw.frontend_build,
        static_asset_hashes: raw.static_asset_hashes,
        health_checked_at: new Date().toISOString(),
      });
      syncDeveloperSettingsInfo();
      applyDevToolsVisibility();
    }

    function createDefaultDevToolsState() {
      return {
        muteNoticeReminder: false,
      };
    }

    function createDefaultRemoteLifecycle() {
      return {
        stage: "",
        effective_at: "",
      };
    }

    function createDefaultRemoteAppControl() {
      return {
        state: "",
        reason: "",
      };
    }

    function createDefaultRemoteStatusNotice() {
      return {
        message: "",
      };
    }

    function createDefaultRemoteRelease() {
      return {
        latest_version: "",
        min_supported_version: "",
        download_url: "",
      };
    }

    function createDefaultRemoteNotice() {
      return {
        title: "",
        subtitle: "",
        notice_text: "",
        bundles: [],
      };
    }

    function createDefaultRemoteFeatureFlags() {
      return {};
    }

    function createDefaultRemoteFeatureLimits() {
      return {
        course_grab: {
          min_interval_sec: DEFAULT_COURSE_GRAB_MIN_INTERVAL_SEC,
          max_runtime_sec: DEFAULT_COURSE_GRAB_MAX_RUNTIME_SEC,
          cooldown_sec: DEFAULT_COURSE_GRAB_DAILY_COOLDOWN_SEC,
        },
      };
    }

    function createDefaultRemoteAccessPolicy() {
      return {
        default_message: "",
        message: "",
        blocked_students: [],
        blocked_student_hashes: [],
      };
    }

    function createDefaultRemoteAccessDecision() {
      return {
        blocked: false,
        message: "",
        student_id_hash: "",
        reason: "",
      };
    }

    function createDefaultRemoteConfig() {
      return {
        source: "",
        warnings: [],
        schema_version: "",
        remote_version: "",
        published_at: "",
        config_path: "",
        sha256: "",
        latest_version: "",
        min_supported_version: "",
        download_url: "",
        notice_text: "",
        notice_version: "",
        notice_path: "",
        notice_sha256: "",
        notice_published_at: "",
        app_policy_version: "",
        app_policy_path: "",
        app_policy_sha256: "",
        app_policy_published_at: "",
        release_policy_version: "",
        release_policy_path: "",
        release_policy_sha256: "",
        release_policy_published_at: "",
        feature_flags: createDefaultRemoteFeatureFlags(),
        feature_limits: createDefaultRemoteFeatureLimits(),
        access_policy: createDefaultRemoteAccessPolicy(),
        access_decision: createDefaultRemoteAccessDecision(),
        software_policy_version: "",
        software_policy_path: "",
        software_policy_sha256: "",
        software_policy_published_at: "",
        version_policy_version: "",
        version_policy_path: "",
        version_policy_sha256: "",
        version_policy_published_at: "",
        update_info_path: "",
        update_info_updated_at: "",
        update_info_sha256: "",
        telemetry: {},
        runtime_policy: {},
        lifecycle: createDefaultRemoteLifecycle(),
        app_control: createDefaultRemoteAppControl(),
        status_notice: createDefaultRemoteStatusNotice(),
        release: createDefaultRemoteRelease(),
        notice: createDefaultRemoteNotice(),
        software_policy_match: {},
        software_policy: {},
        version_policy: {},
        app_policy: {
          lifecycle: createDefaultRemoteLifecycle(),
          app_control: createDefaultRemoteAppControl(),
          status_notice: createDefaultRemoteStatusNotice(),
          feature_flags: createDefaultRemoteFeatureFlags(),
          feature_limits: createDefaultRemoteFeatureLimits(),
          runtime_policy: {},
        },
        release_policy: {},
      };
    }

    function normalizeDevToolsStatePayload(raw = {}) {
      const payload = raw && typeof raw === "object" ? raw : {};
      return {
        muteNoticeReminder: Boolean(payload.muteNoticeReminder),
      };
    }

    function normalizeRemoteLifecyclePayload(raw = {}) {
      const payload = raw && typeof raw === "object" ? raw : {};
      const stage = String(payload.stage || "")
        .trim()
        .toLowerCase();
      return {
        stage: ["maintenance", "deprecated"].includes(stage) ? stage : "",
        effective_at: String(payload.effective_at || "").trim(),
      };
    }

    function normalizeRemoteAppControlPayload(raw = {}) {
      const payload = raw && typeof raw === "object" ? raw : {};
      const state = String(payload.state || payload.mode || "")
        .trim()
        .toLowerCase();
      return {
        state: ["normal", "readonly", "blocked"].includes(state) ? state : "",
        reason: String(payload.reason || "").trim(),
      };
    }

    function normalizeRemoteStatusNoticePayload(
      raw = {},
      lifecyclePayload = {},
      appControlPayload = {},
    ) {
      const payload = raw && typeof raw === "object" ? raw : {};
      const lifecycle =
        lifecyclePayload && typeof lifecyclePayload === "object"
          ? lifecyclePayload
          : {};
      const appControl =
        appControlPayload && typeof appControlPayload === "object"
          ? appControlPayload
          : {};
      return {
        message: String(
          payload.message ||
            payload.title ||
            appControl.message ||
            appControl.title ||
            lifecycle.message ||
            lifecycle.title ||
            "",
        ).trim(),
      };
    }

    function normalizeRemoteReleasePayload(raw = {}) {
      const payload = raw && typeof raw === "object" ? raw : {};
      return {
        latest_version: String(payload.latest_version || "").trim(),
        min_supported_version: String(
          payload.min_supported_version || "",
        ).trim(),
        download_url: String(payload.download_url || "").trim(),
      };
    }

    function normalizeRemoteFeatureFlagsPayload(raw = {}) {
      const payload = raw && typeof raw === "object" ? raw : {};
      const normalized = {};
      [
        "schedule_view",
        "public_course_preview",
        "course_grab",
        "campus_login",
        "keepalive",
      ].forEach((key) => {
        if (typeof payload[key] === "boolean") normalized[key] = payload[key];
      });
      return normalized;
    }

    function normalizeRemoteFeatureLimitsPayload(raw = {}) {
      const payload = raw && typeof raw === "object" ? raw : {};
      const courseGrabPayload =
        payload.course_grab && typeof payload.course_grab === "object"
          ? payload.course_grab
          : {};
      const minInterval = Number.parseFloat(
        String(courseGrabPayload.min_interval_sec ?? ""),
      );
      const maxRuntimeSec = Number.parseInt(
        String(courseGrabPayload.max_runtime_sec ?? ""),
        10,
      );
      const cooldownSec = Number.parseInt(
        String(courseGrabPayload.cooldown_sec ?? ""),
        10,
      );
      return {
        course_grab: {
          min_interval_sec:
            Number.isFinite(minInterval) && minInterval >= 0.1
              ? Math.round(minInterval * 1000) / 1000
              : DEFAULT_COURSE_GRAB_MIN_INTERVAL_SEC,
          max_runtime_sec:
            Number.isInteger(maxRuntimeSec) && maxRuntimeSec >= 1
              ? maxRuntimeSec
              : DEFAULT_COURSE_GRAB_MAX_RUNTIME_SEC,
          cooldown_sec:
            Number.isInteger(cooldownSec) && cooldownSec >= 0
              ? cooldownSec
              : DEFAULT_COURSE_GRAB_DAILY_COOLDOWN_SEC,
        },
      };
    }

    function normalizeRemoteAccessPolicyPayload(raw = {}) {
      const payload = raw && typeof raw === "object" ? raw : {};
      const blockedStudents = Array.isArray(payload.blocked_students)
        ? payload.blocked_students
            .map((item) => {
              const entry = item && typeof item === "object" ? item : {};
              const studentIdHash = String(
                entry.student_id_hash || entry.hash || "",
              ).trim();
              if (!studentIdHash) return null;
              return {
                student_id_hash: studentIdHash,
                message: String(entry.message || "").trim(),
              };
            })
            .filter(Boolean)
        : [];
      const legacyHashes = Array.isArray(payload.blocked_student_hashes)
        ? payload.blocked_student_hashes
            .map((item) => String(item || "").trim())
            .filter(Boolean)
        : [];
      const mergedStudents = [
        ...blockedStudents,
        ...legacyHashes
          .filter(
            (studentIdHash) =>
              !blockedStudents.some(
                (item) => item && item.student_id_hash === studentIdHash,
              ),
          )
          .map((student_id_hash) => ({
            student_id_hash,
            message: "",
          })),
      ];
      return {
        default_message: String(
          payload.default_message || payload.message || "",
        ).trim(),
        message: String(payload.message || payload.default_message || "").trim(),
        blocked_students: mergedStudents,
        blocked_student_hashes: mergedStudents.map(
          (item) => item.student_id_hash,
        ),
      };
    }

    function normalizeRemoteAccessDecisionPayload(raw = {}) {
      const payload = raw && typeof raw === "object" ? raw : {};
      return {
        blocked: Boolean(payload.blocked),
        message: String(payload.message || "").trim(),
        student_id_hash: String(payload.student_id_hash || "").trim(),
        reason: String(payload.reason || "").trim(),
      };
    }

    function formatAboutValue(value) {
      const text = String(value ?? "").trim();
      return text || "--";
    }

    function formatLifecycleStageLabel(stage) {
      if (stage === "supported") return "支持中";
      if (stage === "maintenance") return "维护中";
      if (stage === "deprecated") return "停止维护";
      if (stage === "blocked") return "已停用";
      return "--";
    }

    function formatAppControlStateLabel(state) {
      if (state === "normal") return "正常";
      if (state === "readonly") return "只读";
      if (state === "blocked") return "阻断";
      return "--";
    }

    function formatBooleanStateLabel(value) {
      if (value === true) return "已开放";
      if (value === false) return "已关闭";
      return "--";
    }

    function setAboutText(node, value) {
      if (!node) return;
      node.textContent = formatAboutValue(value);
      node.removeAttribute("title");
    }

    function setAboutLink(node, value) {
      if (!node) return;
      const text = String(value ?? "").trim();
      node.textContent = formatAboutValue(text);
      if (text) {
        node.href = text;
        node.title = text;
        node.classList.remove("is-empty");
      } else {
        node.removeAttribute("href");
        node.removeAttribute("title");
        node.classList.add("is-empty");
      }
    }

    function setAboutTone(node, tone = "") {
      if (!node) return;
      if (tone) node.dataset.tone = tone;
      else delete node.dataset.tone;
    }

    function setAboutTelemetryUploadStatus(text, tone = "") {
      if (!aboutUploadTelemetryStatus) return;
      aboutUploadTelemetryStatus.textContent = text || "";
      if (tone) aboutUploadTelemetryStatus.dataset.tone = tone;
      else delete aboutUploadTelemetryStatus.dataset.tone;
    }

    function syncAboutTelemetryUploadActionState({ resetStatus = false } = {}) {
      const canUpload = Boolean(sessionId);
      if (btnAboutUploadTelemetry) {
        btnAboutUploadTelemetry.disabled =
          manualTelemetryUploadInFlight || !canUpload;
        btnAboutUploadTelemetry.title = canUpload ? "" : "请先登录后再同步";
      }
      if (!resetStatus) return;
      if (!canUpload) {
        setAboutTelemetryUploadStatus("请先登录账号，然后再手动同步当前状态。");
        return;
      }
      setAboutTelemetryUploadStatus("点击按钮后会立即同步当前状态。");
    }

    function resolveAboutUpdateSummaryMeta(
      release = {},
      releaseBannerState = {},
    ) {
      if (
        releaseBannerState.tone === "critical" &&
        release.min_supported_version
      ) {
        return `最低支持 ${release.min_supported_version}`;
      }
      if (release.latest_version) return `最新 ${release.latest_version}`;
      return releaseBannerState.title || "未检测到更新信息";
    }

    function resolveAboutRuntimeSummary(appControl = {}, lifecycle = {}) {
      const appStateLabel = formatAppControlStateLabel(appControl.state);
      const lifecycleLabel = formatLifecycleStageLabel(lifecycle.stage);

      if (appControl.state === "blocked" || lifecycle.stage === "blocked") {
        return {
          label: "已阻断",
          meta: "当前版本已被限制使用",
        };
      }
      if (appControl.state === "readonly") {
        return {
          label: "只读运行",
          meta: lifecycleLabel !== "--" ? lifecycleLabel : "部分操作受限",
        };
      }
      if (lifecycle.stage === "maintenance") {
        return {
          label: "维护中",
          meta: appStateLabel !== "--" ? `应用 ${appStateLabel}` : "支持中",
        };
      }
      if (lifecycle.stage === "deprecated") {
        return {
          label: "停止维护",
          meta:
            appStateLabel !== "--" ? `应用 ${appStateLabel}` : "建议尽快升级",
        };
      }

      return {
        label: "运行正常",
        meta:
          [lifecycleLabel, appStateLabel]
            .filter((item) => item !== "--")
            .join(" · ") || "支持中 · 正常",
      };
    }

    function isOfflineFallbackReadonlyRuntime(config = currentRemoteConfig) {
      const lifecycle =
        config && typeof config.lifecycle === "object" ? config.lifecycle : {};
      const appControl =
        config && typeof config.app_control === "object"
          ? config.app_control
          : {};
      const source = String(config?.source || "")
        .trim()
        .toLowerCase();
      const reason = String(appControl.reason || "")
        .trim()
        .toLowerCase();
      return (
        appControl.state === "readonly" &&
        reason === "notice-integrity-check-failed" &&
        ["", "local-cache", "cos-fallback"].includes(source) &&
        lifecycle.stage !== "blocked"
      );
    }

    function resolveAboutDisplayRuntime(config = currentRemoteConfig) {
      const lifecycle =
        config && typeof config.lifecycle === "object" ? config.lifecycle : {};
      const appControl =
        config && typeof config.app_control === "object"
          ? config.app_control
          : {};
      const isOfflineFallbackReadonly =
        isOfflineFallbackReadonlyRuntime(config);

      if (!isOfflineFallbackReadonly) {
        return { lifecycle, appControl };
      }

      return {
        lifecycle: {
          ...lifecycle,
          stage: "",
        },
        appControl: {
          ...appControl,
          state: "normal",
          reason: "",
        },
      };
    }

    function buildAboutFeatureEntries(config = currentRemoteConfig) {
      const featureFlags =
        config && typeof config.feature_flags === "object"
          ? config.feature_flags
          : {};
      const featureLimits =
        config && typeof config.feature_limits === "object"
          ? config.feature_limits
          : {};
      const featureCatalog = [
        {
          key: "schedule_view",
          name: "课表",
          disabledDetail: "当前版本暂不开放课表查看。",
        },
        {
          key: "campus_login",
          name: "校园网登录",
          disabledDetail: "当前版本暂不开放校园网登录。",
        },
        {
          key: "keepalive",
          name: "保活心跳",
          disabledDetail: "当前版本暂不开放保活心跳。",
        },
        {
          key: "public_course_preview",
          name: "选课预览",
          disabledDetail: "当前版本暂不开放选课预览。",
        },
        {
          key: "course_grab",
          name: "快速抢课",
          disabledDetail: "当前版本暂不开放快速抢课。",
        },
      ];
      const entries = featureCatalog
        .filter((feature) => featureFlags[feature.key] === false)
        .map((feature) => ({
          name: feature.name,
          summary: "已关闭",
          detail: feature.disabledDetail,
          tone: "critical",
        }));

      const courseGrabLimits =
        featureLimits.course_grab &&
        typeof featureLimits.course_grab === "object"
          ? featureLimits.course_grab
          : {};
      const minInterval = Number.parseFloat(
        String(courseGrabLimits.min_interval_sec ?? ""),
      );
      if (
        featureFlags.course_grab !== false &&
        Number.isFinite(minInterval) &&
        minInterval > DEFAULT_COURSE_GRAB_MIN_INTERVAL_SEC
      ) {
        entries.push({
          name: "快速抢课",
          summary: "受限",
          detail: `最低频率 ${minInterval} 秒`,
          tone: "important",
        });
      }

      return entries;
    }

    function renderAboutFeatureList(config = currentRemoteConfig) {
      if (!aboutFeatureList) return;
      const entries = buildAboutFeatureEntries(config);
      aboutFeatureList.innerHTML = "";
      if (!entries.length) {
        const empty = document.createElement("div");
        empty.className = "about-feature-empty";
        empty.textContent = "当前功能均正常开放。";
        aboutFeatureList.append(empty);
        return;
      }
      entries.forEach((entry) => {
        const item = document.createElement("div");
        item.className = "about-feature-item";
        const title = document.createElement("strong");
        title.textContent = entry.name;
        const summary = document.createElement("span");
        summary.className = "about-feature-summary";
        summary.textContent = entry.summary;
        if (entry.tone) summary.dataset.tone = entry.tone;
        item.append(title, summary);
        if (entry.detail) {
          const detail = document.createElement("p");
          detail.className = "about-feature-detail";
          detail.textContent = entry.detail;
          item.append(detail);
        }
        aboutFeatureList.append(item);
      });
    }

    function resolveAboutRuntimeTone(appControl = {}, lifecycle = {}) {
      if (
        appControl.state === "blocked" ||
        lifecycle.stage === "blocked" ||
        lifecycle.stage === "deprecated"
      )
        return "critical";
      if (appControl.state === "readonly" || lifecycle.stage === "maintenance")
        return "important";
      return "neutral";
    }

    function resolveAboutRuntimeMessage(config = currentRemoteConfig) {
      const { lifecycle, appControl } = resolveAboutDisplayRuntime(config);
      const statusNotice =
        config && typeof config.status_notice === "object"
          ? config.status_notice
          : {};
      const directMessage = String(
        statusNotice.message || appControl.reason || lifecycle.message || "",
      ).trim();
      if (directMessage) return directMessage;
      if (appControl.state === "readonly")
        return "当前为只读模式，部分操作会受到限制。";
      if (appControl.state === "blocked" || lifecycle.stage === "blocked")
        return "当前版本已被限制使用，请先处理版本更新。";
      if (lifecycle.stage === "maintenance")
        return "当前版本进入维护期，可继续使用但可能收到维护提示。";
      if (lifecycle.stage === "deprecated")
        return "当前版本已停止维护，建议尽快升级。";
      return "当前运行正常，没有维护或限制提示。";
    }

    function renderAboutDevDiagnostics(payload = {}) {
      setAboutText(devConfigChannel, payload.channel);
      setAboutText(devNoticeSource, payload.source);
      setAboutText(
        devConfigUpdatedAt,
        payload.updatedAt ? formatNoticeTimestamp(payload.updatedAt) : "",
      );
      setAboutText(devSoftwarePolicyPath, payload.softwarePolicyPath);
      setAboutText(devVersionPolicyPath, payload.versionPolicyPath);
      setAboutText(devUpdateInfoPath, payload.updateInfoPath);
      setAboutText(devNoticePath, payload.noticePath);
    }

    function renderAboutPanel(config = currentRemoteConfig) {
      const { lifecycle, appControl } = resolveAboutDisplayRuntime(config);
      const release =
        config && typeof config.release === "object" ? config.release : {};
      const softwarePolicyPath = String(
        config.software_policy_path || config.software_policy_version || "",
      ).trim();
      const versionPolicyPath = String(
        config.version_policy_path || config.version_policy_version || "",
      ).trim();
      const updateInfoPath = String(config.update_info_path || "").trim();
      const noticePath = String(config.notice_path || "").trim();
      const source = String(config.source || "").trim();
      const updatedAt = String(
        config.updated_at ||
          config.update_info_updated_at ||
          config.software_policy_published_at ||
          "",
      ).trim();
      const channel = String(currentDevelopmentConfig.channel || "").trim();
      const releaseBannerState = resolveReleaseBannerState(config);
      const runtimeTone = resolveAboutRuntimeTone(appControl, lifecycle);
      const runtimeSummary = resolveAboutRuntimeSummary(appControl, lifecycle);
      const hasUpdateAction = Boolean(releaseBannerState.actionUrl);
      const downloadUrl = String(
        config.download_url || release.download_url || "",
      ).trim();

      setAboutText(aboutHighlightBuild, FRONTEND_VERSION);
      setAboutText(
        aboutHighlightUpdate,
        releaseBannerState.badgeLabel || releaseBannerState.title,
      );
      setAboutText(
        aboutHighlightUpdateMeta,
        resolveAboutUpdateSummaryMeta(release, releaseBannerState),
      );
      setAboutText(aboutHighlightRuntime, runtimeSummary.label);
      setAboutText(aboutHighlightRuntimeMeta, runtimeSummary.meta);
      setAboutText(aboutBuild, FRONTEND_VERSION);
      setAboutText(aboutUpdateBadge, releaseBannerState.badgeLabel);
      setAboutText(aboutUpdateEyebrow, releaseBannerState.eyebrow);
      setAboutText(aboutUpdateTitle, releaseBannerState.title);
      setAboutText(aboutUpdateMessage, releaseBannerState.body);
      setAboutText(
        aboutLifecycleStage,
        formatLifecycleStageLabel(lifecycle.stage),
      );
      setAboutText(
        aboutAppControlState,
        formatAppControlStateLabel(appControl.state),
      );
      setAboutText(aboutLifecycleMessage, resolveAboutRuntimeMessage(config));
      setAboutText(aboutLatestVersion, release.latest_version);
      setAboutText(aboutMinSupportedVersion, release.min_supported_version);
      setAboutLink(aboutDownloadUrl, downloadUrl);
      setAboutText(
        aboutLifecycleBadge,
        formatAppControlStateLabel(appControl.state) !== "--"
          ? formatAppControlStateLabel(appControl.state)
          : formatLifecycleStageLabel(lifecycle.stage),
      );
      setAboutTone(aboutHighlightUpdateTile, releaseBannerState.tone);
      setAboutTone(aboutHighlightRuntimeTile, runtimeTone);
      setAboutTone(aboutUpdateBadge, releaseBannerState.tone);
      setAboutTone(aboutLifecycleBadge, runtimeTone);
      if (aboutUpdateAction) {
        aboutUpdateAction.classList.toggle("hidden", !hasUpdateAction);
        aboutUpdateAction.textContent =
          releaseBannerState.actionLabel || "立即更新";
        aboutUpdateAction.href = hasUpdateAction
          ? releaseBannerState.actionUrl
          : "#";
      }
      renderAboutDevDiagnostics({
        channel,
        source,
        updatedAt,
        softwarePolicyPath,
        versionPolicyPath,
        updateInfoPath,
        noticePath,
      });
      renderAboutFeatureList(config);
      syncUpdatesNavigationState(latestUpdatesFeed, config);
    }

    function normalizeRemoteConfigPayload(raw = {}) {
      const payload = raw && typeof raw === "object" ? raw : {};
      const defaults = createDefaultRemoteConfig();
      const noticePayload =
        payload.notice && typeof payload.notice === "object"
          ? payload.notice
          : {};
      const softwarePolicyPayload =
        payload.software_policy && typeof payload.software_policy === "object"
          ? payload.software_policy
          : payload.software_policy_match &&
              typeof payload.software_policy_match === "object"
            ? payload.software_policy_match
            : payload.app_policy && typeof payload.app_policy === "object"
              ? payload.app_policy
              : {};
      const versionPolicyPayload =
        payload.version_policy && typeof payload.version_policy === "object"
          ? payload.version_policy
          : payload.version_policy_match &&
              typeof payload.version_policy_match === "object"
            ? payload.version_policy_match
            : payload.software_policy &&
                typeof payload.software_policy === "object"
              ? payload.software_policy
              : payload.app_policy && typeof payload.app_policy === "object"
                ? payload.app_policy
                : {};
      const updateInfoPayload =
        payload.update_info && typeof payload.update_info === "object"
          ? payload.update_info
          : payload.release_policy && typeof payload.release_policy === "object"
            ? payload.release_policy
            : {};
      const releasePayload = updateInfoPayload;
      const featureFlags =
        payload.feature_flags && typeof payload.feature_flags === "object"
          ? payload.feature_flags
          : softwarePolicyPayload.feature_flags &&
              typeof softwarePolicyPayload.feature_flags === "object"
            ? softwarePolicyPayload.feature_flags
            : {};
      const featureLimitsPayload =
        payload.feature_limits && typeof payload.feature_limits === "object"
          ? payload.feature_limits
          : softwarePolicyPayload.feature_limits &&
              typeof softwarePolicyPayload.feature_limits === "object"
            ? softwarePolicyPayload.feature_limits
            : {};
      const telemetry =
        payload.telemetry && typeof payload.telemetry === "object"
          ? payload.telemetry
          : {};
      const runtimePolicyPayload =
        payload.runtime_policy && typeof payload.runtime_policy === "object"
          ? payload.runtime_policy
          : softwarePolicyPayload.runtime_policy &&
              typeof softwarePolicyPayload.runtime_policy === "object"
            ? softwarePolicyPayload.runtime_policy
            : {};
      const configPullInterval = Number.parseInt(
        String(
          runtimePolicyPayload.config_pull_interval_sec ??
            telemetry.config_pull_interval_sec ??
            "",
        ),
        10,
      );
      const loginSyncCooldown = Number.parseInt(
        String(runtimePolicyPayload.login_sync_cooldown_sec ?? ""),
        10,
      );
      const summaryReportInterval = Number.parseInt(
        String(
          runtimePolicyPayload.summary_report_interval_sec ??
            telemetry.summary_report_interval_sec ??
            "",
        ),
        10,
      );
      const lifecycle = normalizeRemoteLifecyclePayload(
        payload.lifecycle && typeof payload.lifecycle === "object"
          ? payload.lifecycle
          : softwarePolicyPayload.lifecycle,
      );
      const appControl = normalizeRemoteAppControlPayload(
        payload.app_control && typeof payload.app_control === "object"
          ? payload.app_control
          : softwarePolicyPayload.app_control,
      );
      const statusNotice = normalizeRemoteStatusNoticePayload(
        payload.status_notice && typeof payload.status_notice === "object"
          ? payload.status_notice
          : softwarePolicyPayload.status_notice,
        payload.lifecycle && typeof payload.lifecycle === "object"
          ? payload.lifecycle
          : softwarePolicyPayload.lifecycle,
        payload.app_control && typeof payload.app_control === "object"
          ? payload.app_control
          : softwarePolicyPayload.app_control,
      );
      const release = normalizeRemoteReleasePayload({
        ...releasePayload,
        latest_version: payload.latest_version ?? releasePayload.latest_version,
        min_supported_version:
          payload.min_supported_version ?? releasePayload.min_supported_version,
        download_url: payload.download_url ?? releasePayload.download_url,
      });
      const normalizedFeatureFlags =
        normalizeRemoteFeatureFlagsPayload(featureFlags);
      const featureLimits =
        normalizeRemoteFeatureLimitsPayload(featureLimitsPayload);
      const accessPolicy = normalizeRemoteAccessPolicyPayload(
        payload.access_policy && typeof payload.access_policy === "object"
          ? payload.access_policy
          : {},
      );
      const accessDecision = normalizeRemoteAccessDecisionPayload(
        payload.access_decision && typeof payload.access_decision === "object"
          ? payload.access_decision
          : {},
      );
      const noticeText = String(
        payload.notice_text || noticePayload.notice_text || "",
      ).trim();
      const noticeBundles = Array.isArray(noticePayload.bundles)
        ? noticePayload.bundles
        : [];
      return {
        ...defaults,
        source: String(payload.source || defaults.source).trim(),
        warnings: Array.isArray(payload.warnings)
          ? payload.warnings.filter(Boolean).map((item) => String(item))
          : [],
        schema_version: String(
          payload.schema_version || defaults.schema_version,
        ).trim(),
        remote_version: String(
          payload.remote_version || defaults.remote_version,
        ).trim(),
        published_at: String(
          payload.published_at || defaults.published_at,
        ).trim(),
        config_path: String(payload.config_path || defaults.config_path).trim(),
        sha256: String(payload.sha256 || defaults.sha256).trim(),
        latest_version: release.latest_version,
        min_supported_version: release.min_supported_version,
        download_url: release.download_url,
        notice_text: noticeText,
        notice_version: String(
          payload.notice_version || defaults.notice_version,
        ).trim(),
        notice_path: String(payload.notice_path || defaults.notice_path).trim(),
        notice_sha256: String(
          payload.notice_sha256 || defaults.notice_sha256,
        ).trim(),
        notice_published_at: String(
          payload.notice_published_at || defaults.notice_published_at,
        ).trim(),
        software_policy_version: String(
          payload.software_policy_version || defaults.software_policy_version,
        ).trim(),
        software_policy_path: String(
          payload.software_policy_path || defaults.software_policy_path,
        ).trim(),
        software_policy_sha256: String(
          payload.software_policy_sha256 || defaults.software_policy_sha256,
        ).trim(),
        software_policy_published_at: String(
          payload.software_policy_published_at ||
            defaults.software_policy_published_at,
        ).trim(),
        version_policy_version: String(
          payload.version_policy_version || defaults.version_policy_version,
        ).trim(),
        version_policy_path: String(
          payload.version_policy_path || defaults.version_policy_path,
        ).trim(),
        version_policy_sha256: String(
          payload.version_policy_sha256 || defaults.version_policy_sha256,
        ).trim(),
        version_policy_published_at: String(
          payload.version_policy_published_at ||
            defaults.version_policy_published_at,
        ).trim(),
        update_info_path: String(
          payload.update_info_path || defaults.update_info_path,
        ).trim(),
        update_info_updated_at: String(
          payload.update_info_updated_at || defaults.update_info_updated_at,
        ).trim(),
        update_info_sha256: String(
          payload.update_info_sha256 || defaults.update_info_sha256,
        ).trim(),
        app_policy_version: String(
          payload.app_policy_version || defaults.app_policy_version,
        ).trim(),
        app_policy_path: String(
          payload.app_policy_path || defaults.app_policy_path,
        ).trim(),
        app_policy_sha256: String(
          payload.app_policy_sha256 || defaults.app_policy_sha256,
        ).trim(),
        app_policy_published_at: String(
          payload.app_policy_published_at || defaults.app_policy_published_at,
        ).trim(),
        release_policy_version: String(
          payload.release_policy_version || defaults.release_policy_version,
        ).trim(),
        release_policy_path: String(
          payload.release_policy_path || defaults.release_policy_path,
        ).trim(),
        release_policy_sha256: String(
          payload.release_policy_sha256 || defaults.release_policy_sha256,
        ).trim(),
        release_policy_published_at: String(
          payload.release_policy_published_at ||
            defaults.release_policy_published_at,
        ).trim(),
        feature_flags: normalizedFeatureFlags,
        feature_limits: featureLimits,
        access_policy: accessPolicy,
        access_decision: accessDecision,
        telemetry: {
          enabled: telemetry.enabled !== false,
          config_pull_interval_sec:
            Number.isInteger(configPullInterval) && configPullInterval >= 300
              ? configPullInterval
              : DEFAULT_REMOTE_CONFIG_PULL_INTERVAL_SEC,
          summary_report_interval_sec:
            Number.isInteger(summaryReportInterval) &&
            summaryReportInterval >= 300
              ? summaryReportInterval
              : DEFAULT_REMOTE_SUMMARY_REPORT_INTERVAL_SEC,
        },
        runtime_policy: {
          config_pull_interval_sec:
            Number.isInteger(configPullInterval) && configPullInterval >= 300
              ? configPullInterval
              : DEFAULT_REMOTE_CONFIG_PULL_INTERVAL_SEC,
          login_sync_cooldown_sec:
            Number.isInteger(loginSyncCooldown) && loginSyncCooldown >= 60
              ? loginSyncCooldown
              : 300,
          summary_report_interval_sec:
            Number.isInteger(summaryReportInterval) &&
            summaryReportInterval >= 300
              ? summaryReportInterval
              : DEFAULT_REMOTE_SUMMARY_REPORT_INTERVAL_SEC,
        },
        lifecycle,
        app_control: appControl,
        status_notice: statusNotice,
        release,
        notice: {
          ...createDefaultRemoteNotice(),
          ...noticePayload,
          title: String(noticePayload.title || payload.title || "").trim(),
          subtitle: String(
            noticePayload.subtitle || payload.subtitle || "",
          ).trim(),
          notice_text: noticeText,
          bundles: noticeBundles,
        },
        software_policy_match:
          payload.software_policy_match &&
          typeof payload.software_policy_match === "object"
            ? payload.software_policy_match
            : {},
        software_policy: softwarePolicyPayload,
        version_policy: versionPolicyPayload,
        update_info: updateInfoPayload,
        app_policy: {
          ...defaults.app_policy,
          ...softwarePolicyPayload,
          lifecycle,
          app_control: appControl,
          status_notice: statusNotice,
          feature_flags: normalizedFeatureFlags,
          feature_limits: featureLimits,
          runtime_policy: {
            config_pull_interval_sec:
              Number.isInteger(configPullInterval) && configPullInterval >= 300
                ? configPullInterval
                : defaults.runtime_policy.config_pull_interval_sec,
            login_sync_cooldown_sec:
              Number.isInteger(loginSyncCooldown) && loginSyncCooldown >= 60
                ? loginSyncCooldown
                : defaults.runtime_policy.login_sync_cooldown_sec,
            summary_report_interval_sec:
              Number.isInteger(summaryReportInterval) &&
              summaryReportInterval >= 300
                ? summaryReportInterval
                : defaults.runtime_policy.summary_report_interval_sec,
          },
        },
        release_policy: updateInfoPayload,
      };
    }

    function normalizeBetaStatusPayload(raw = {}) {
      const payload = raw && typeof raw === "object" ? raw : {};
      const defaults = createDefaultBetaStatus();
      return {
        enabled: Boolean(payload.enabled),
        expired: Boolean(payload.expired),
        expires_at: String(payload.expires_at || defaults.expires_at).trim(),
        expires_at_text: String(
          payload.expires_at_text || defaults.expires_at_text,
        ).trim(),
        message: String(payload.message || defaults.message).trim(),
        };
    }

    function syncLoginStatusBanner(text = "") {
      if (!loginBetaBanner) return;
      const normalized = String(text || "").trim();
      loginBetaBanner.textContent = normalized;
      loginBetaBanner.classList.toggle("hidden", !normalized);
    }

    function resolveBetaBannerText() {
      if (currentBetaStatus.expired) {
        return currentBetaStatus.message || "内测已结束";
      }
      return "内测版本";
    }

    function resolveLoginRuntimeBannerText(config = currentRemoteConfig) {
      if (isOfflineFallbackReadonlyRuntime(config)) return "";
      const appControl =
        config && typeof config.app_control === "object"
          ? config.app_control
          : {};
      const statusNotice =
        config && typeof config.status_notice === "object"
          ? config.status_notice
          : {};
      if (appControl.state === "readonly") {
        return String(statusNotice.message || "").trim() || "当前为只读模式";
      }
      return "";
    }

    function applyLoginAvailability(disabled) {
      [
        inputUser,
        inputPassword,
        btnPasswordVisibility,
        selectMode,
        btnLogin,
        btnLoginModeSelect,
        btnTrustedAccountUsePassword,
      ].forEach((element) => {
        if (element) element.disabled = disabled;
      });
    }

    function applyBetaStatusToUI(raw) {
      currentBetaStatus = normalizeBetaStatusPayload(raw);
      syncLoginStatusBanner(resolveBetaBannerText());
      applyLoginAvailability(currentBetaStatus.expired);
      if (currentBetaStatus.expired) {
        clearSessionId();
        if (loginMsg) loginMsg.dataset.betaLocked = "true";
        showMsg(
          loginMsg,
          "error",
          currentBetaStatus.message || "内测已结束，当前版本已停用。",
        );
        log(currentBetaStatus.message || "内测已结束，当前版本已停用。");
        return;
      }
      if (loginMsg && loginMsg.dataset.betaLocked === "true") {
        delete loginMsg.dataset.betaLocked;
        hideMsg(loginMsg);
      }
    }

    function compareVersionTokens(left, right) {
      const leftTokens = String(left || "").match(/\d+/g) || [];
      const rightTokens = String(right || "").match(/\d+/g) || [];
      const maxLength = Math.max(leftTokens.length, rightTokens.length);
      for (let index = 0; index < maxLength; index += 1) {
        const leftValue = Number.parseInt(leftTokens[index] || "0", 10);
        const rightValue = Number.parseInt(rightTokens[index] || "0", 10);
        if (leftValue > rightValue) return 1;
        if (leftValue < rightValue) return -1;
      }
      return 0;
    }

    function getRemoteReleaseTargetVersion() {
      return String(
        currentRemoteConfig.latest_version ||
          currentRemoteConfig.min_supported_version ||
          "",
      ).trim();
    }

    function isRemoteVersionExplicitlyBlocked() {
      return false;
    }

    function isRemoteForceUpdateRequired() {
      const minSupportedVersion = String(
        currentRemoteConfig.min_supported_version || "",
      ).trim();
      if (
        minSupportedVersion &&
        compareVersionTokens(FRONTEND_VERSION, minSupportedVersion) < 0
      )
        return true;
      return false;
    }

    function resolveRemoteBlockingState(config = currentRemoteConfig) {
      const accessDecision =
        config.access_decision && typeof config.access_decision === "object"
          ? config.access_decision
          : {};
      if (accessDecision.blocked) {
        return {
          blocked: true,
          title: "账号已停用",
          message: accessDecision.message || "",
        };
      }
      const appControl = config.app_control || {};
      const statusNotice = config.status_notice || {};
      if (appControl.state === "blocked") {
        return {
          blocked: true,
          title: "",
          message: statusNotice.message || "",
        };
      }
      const minSupportedVersion = String(
        config.min_supported_version || "",
      ).trim();
      if (
        minSupportedVersion &&
        compareVersionTokens(FRONTEND_VERSION, minSupportedVersion) < 0
      ) {
        return {
          blocked: true,
          title: "",
          message: "",
        };
      }
      return {
        blocked: false,
        title: "",
        message: "",
      };
    }

    function isAccessBlockedMode(config = currentRemoteConfig) {
      return Boolean(resolveRemoteBlockingState(config).blocked);
    }

    function clearRemoteConfigTimer() {
      if (remoteConfigTimer) {
        clearTimeout(remoteConfigTimer);
        remoteConfigTimer = null;
      }
    }

    function scheduleNextRemoteConfigPull() {
      clearRemoteConfigTimer();
      const intervalMs = Math.max(
        5 * 60 * 1000,
        Number(
          (currentRemoteConfig.telemetry &&
            currentRemoteConfig.telemetry.config_pull_interval_sec) ||
            DEFAULT_REMOTE_CONFIG_PULL_INTERVAL_SEC,
        ) * 1000,
      );
      remoteConfigTimer = window.setTimeout(() => {
        remoteConfigTimer = null;
        void loadConfig();
      }, intervalMs);
    }

    function setTabVisibility(tabId, visible) {
      const tab = $(`.tab[data-tab="${tabId}"]`);
      const panel = $(`#panel${tabId[0].toUpperCase()}${tabId.slice(1)}`);
      if (tab) tab.classList.toggle("hidden", !visible);
      if (panel) panel.classList.toggle("hidden", !visible);
    }

    function extractBlockedErrorDetail(error) {
      const detail =
        error && error.detail && typeof error.detail === "object"
          ? error.detail
          : null;
      if (!detail || !detail.blocked) return null;
      return {
        blocked: true,
        message: String(detail.message || "").trim(),
        student_id: String(detail.student_id || "").trim(),
        student_id_hash: String(detail.student_id_hash || "").trim(),
        reason: String(detail.reason || "").trim(),
      };
    }

    function applyRemoteFeatureFlags() {
      const flags = currentRemoteConfig.feature_flags || {};
      setTabVisibility("updates", true);
      setTabVisibility("about", true);
      setTabVisibility("settings", true);
      setTabVisibility("log", true);
      setTabVisibility("schedule", flags.schedule_view !== false);
      setTabVisibility(
        "network",
        flags.campus_login !== false || flags.keepalive !== false,
      );
      setTabVisibility(
        "course",
        flags.public_course_preview !== false || flags.course_grab !== false,
      );
      if (networkHeartbeatEnabled)
        networkHeartbeatEnabled.disabled = flags.keepalive === false;
      if (btnNetworkHeartbeatIntervalSelect)
        btnNetworkHeartbeatIntervalSelect.disabled = flags.keepalive === false;
      if (networkHeartbeatInterval)
        networkHeartbeatInterval.disabled = flags.keepalive === false;
      syncCourseActionState();
      syncNetworkActionState();
      if (tabsController) {
        const activeTab =
          document.querySelector(".tab.active:not(.hidden)") ||
          document.querySelector(".tab:not(.hidden)");
        if (activeTab) tabsController.activate(activeTab);
      }
    }

    function syncUpdatesBlockedBanner(blockingState = resolveRemoteBlockingState()) {
      const blocked = Boolean(blockingState && blockingState.blocked);
      if (updatesBlockBanner) updatesBlockBanner.classList.toggle("hidden", !blocked);
      if (updatesBlockTitle) {
        updatesBlockTitle.textContent =
          blocked && blockingState.title
            ? blockingState.title
            : "账号已停用";
      }
      if (updatesBlockMessage) {
        updatesBlockMessage.textContent =
          blocked && blockingState.message
            ? blockingState.message
            : "当前账号不能继续使用业务功能，现仅保留公告查看。";
      }
    }

    function syncUpdatesBlockedPresentation(blocked = isAccessBlockedMode()) {
      const updatesPanel = $("#panelUpdates");
      const updatesPanelHeader = $(".updates-panel-header");
      const updatesPanelMeta = $(".updates-panel-meta");
      const updatesPanelActions = $(".updates-panel-actions");
      const updatesPanelSide = $(".updates-panel-side");
      const updatesBlockActions = $("#updatesBlockActions");
      const workspaceCard = $("#updatesWorkspaceCard");
      if (updatesPanel) {
        updatesPanel.classList.toggle("is-access-blocked", Boolean(blocked));
      }
      if (updatesPanelHeader) {
        updatesPanelHeader.classList.toggle("hidden", Boolean(blocked));
      }
      if (updatesPanelMeta) {
        updatesPanelMeta.classList.toggle("hidden", Boolean(blocked));
      }
      if (updatesPanelActions) {
        updatesPanelActions.classList.toggle(
          "is-access-blocked",
          Boolean(blocked),
        );
        if (blocked && updatesBlockActions && updatesPanelActions.parentElement !== updatesBlockActions) {
          updatesBlockActions.appendChild(updatesPanelActions);
        } else if (
          !blocked &&
          updatesPanelSide &&
          updatesPanelActions.parentElement !== updatesPanelSide
        ) {
          updatesPanelSide.appendChild(updatesPanelActions);
        }
      }
      if (workspaceCard) {
        workspaceCard.classList.toggle("is-access-blocked", Boolean(blocked));
        workspaceCard.classList.toggle(
          "sidebar-settings-card",
          !Boolean(blocked),
        );
      }
      if (updatesContentGrid) {
        updatesContentGrid.classList.toggle(
          "is-access-blocked",
          Boolean(blocked),
        );
      }
      if (updatesTimeline) {
        updatesTimeline.classList.toggle("is-access-blocked", Boolean(blocked));
      }
      if (updatesDetail) {
        updatesDetail.classList.toggle("is-access-blocked", Boolean(blocked));
      }
    }

    function applyRemoteBlockingVisibility(blocked) {
      if (dashboard) dashboard.classList.toggle("is-access-blocked", Boolean(blocked));
      syncUpdatesBlockedBanner();
      syncUpdatesBlockedPresentation(blocked);
      if (!blocked) {
        if (btnUpdatesToggleFeedback) btnUpdatesToggleFeedback.classList.remove("hidden");
        syncCourseActionState({ refreshHint: false });
        syncNetworkActionState();
        if (btnConnectJw) btnConnectJw.disabled = false;
        return;
      }
      ["schedule", "network", "course", "settings", "about", "log"].forEach(
        (tabId) => setTabVisibility(tabId, false),
      );
      setTabVisibility("updates", true);
      if (btnUpdatesToggleFeedback) btnUpdatesToggleFeedback.classList.add("hidden");
      setUpdatesFeedbackPanelOpen(false);
      if (btnConnectJw) btnConnectJw.disabled = true;
      if (btnLoadRounds) btnLoadRounds.disabled = true;
      if (btnPreview) btnPreview.disabled = true;
      if (btnGrabStart) btnGrabStart.disabled = true;
      if (btnGrabStop) btnGrabStop.disabled = true;
      if (btnNetworkRefresh) btnNetworkRefresh.disabled = true;
      if (btnNetworkHeartbeat) btnNetworkHeartbeat.disabled = true;
      if (btnNetworkSaveConfig) btnNetworkSaveConfig.disabled = true;
      if (tabsController) {
        const activeTab =
          document.querySelector(".tab.active:not(.hidden)") ||
          document.querySelector(".tab:not(.hidden)");
        if (activeTab) tabsController.activate(activeTab);
      }
    }

    function applyRemoteConfigToUI() {
      currentRemoteConfig = normalizeRemoteConfigPayload(currentRemoteConfig);
      applyRemoteFeatureFlags();
      renderAboutPanel(currentRemoteConfig);
      const blockingState = resolveRemoteBlockingState();
      applyRemoteBlockingVisibility(blockingState.blocked);
      if (blockingState.blocked) {
        applyLoginAvailability(true);
        syncLoginStatusBanner(blockingState.title || "当前账号已被停止使用");
        if (loginMsg && loginCard && !loginCard.classList.contains("hidden")) {
          showMsg(
            loginMsg,
            "error",
            blockingState.message ||
              `当前版本过低，请先更新到 ${getRemoteReleaseTargetVersion()}`,
          );
        }
      } else if (!currentBetaStatus.expired) {
        applyLoginAvailability(false);
        syncLoginStatusBanner(
          resolveLoginRuntimeBannerText(currentRemoteConfig) ||
            resolveBetaBannerText(),
        );
      }
      syncUpdatesNavigationState();
      scheduleNextRemoteConfigPull();
      scheduleNextTelemetryReport();
      syncUpdatesManualRefreshActionState();
    }

    function isDevToolsVisible() {
      return Boolean(
        currentDevelopmentConfig && currentDevelopmentConfig.development,
      );
    }

    function syncDeveloperSettingsInfo() {
      const healthBuild = currentDevelopmentConfig.frontend_build || "";
      const assetHashes = currentDevelopmentConfig.static_asset_hashes || {};
      const appHash = String(assetHashes["assets/app.js"] || "").slice(0, 12);
      if (developerSettingsRuntime) {
        developerSettingsRuntime.textContent =
          currentDevelopmentConfig.runtime || "source-workspace";
      }
      if (developerSettingsChannel) {
        developerSettingsChannel.textContent =
          currentDevelopmentConfig.channel || "release";
      }
      if (developerSettingsBuildChip) {
        developerSettingsBuildChip.textContent = healthBuild
          ? `build ${FRONTEND_BUILD} / host ${healthBuild}`
          : `build ${FRONTEND_BUILD}`;
      }
      if (developerSettingsBaseUrl) {
        developerSettingsBaseUrl.textContent =
          currentDevelopmentConfig.base_url || "--";
      }
      if (developerSettingsWebRoot) {
        developerSettingsWebRoot.textContent =
          currentDevelopmentConfig.web_root || "--";
        developerSettingsWebRoot.title =
          currentDevelopmentConfig.web_root || "";
      }
      if (developerSettingsAssetHash) {
        developerSettingsAssetHash.textContent = appHash
          ? `app.js ${appHash}`
          : "--";
      }
    }

    function isDeveloperOptionsEnabled() {
      return Boolean(developerOptionsUnlocked && developerOptionsEnabled);
    }

    function applyDeveloperSettingsVisibility() {
      const unlocked = Boolean(developerOptionsUnlocked);
      const enabled = isDeveloperOptionsEnabled();
      if (developerSettingsCard) {
        developerSettingsCard.classList.toggle("hidden", !unlocked);
        developerSettingsCard.classList.toggle(
          "is-collapsed",
          unlocked && !enabled,
        );
      }
      if (developerSettingsBody) {
        developerSettingsBody.classList.toggle("hidden", !enabled);
      }
      if (devOnlyFields) devOnlyFields.classList.toggle("hidden", !enabled);
      if (developerSettingsToggle) {
        developerSettingsToggle.checked = enabled;
        developerSettingsToggle.disabled = !unlocked;
      }
      if (developerSettingsHint) {
        developerSettingsHint.textContent = enabled
          ? "调试参数与本机运行信息会在这里显示。"
          : "当前已关闭，可重新开启查看调试参数与本机运行信息。";
      }
      syncDeveloperSettingsInfo();
    }

    function setDeveloperOptionsEnabled(enabled) {
      developerOptionsEnabled = developerOptionsUnlocked && Boolean(enabled);
      applyDeveloperSettingsVisibility();
      triggerMockRefresh();
      updateScheduleSettingsSummary();
    }

    function unlockDeveloperOptions() {
      developerOptionsUnlocked = true;
      setDeveloperOptionsEnabled(true);
    }

    function updateDevToolsSummary() {
      if (devMuteNoticeSummary) {
        devMuteNoticeSummary.textContent =
          currentDevToolsState.muteNoticeReminder ? "已开启" : "已关闭";
      }
    }

    function applyDevToolsVisibility() {
      const visible = isDevToolsVisible();
      if (devToolsCard) devToolsCard.classList.toggle("hidden", !visible);
      if (devToolsRuntimeLabel) {
        const baseUrl = currentDevelopmentConfig.base_url
          ? ` · ${currentDevelopmentConfig.base_url}`
          : "";
        devToolsRuntimeLabel.textContent = visible
          ? `当前环境：${currentDevelopmentConfig.runtime} · ${currentDevelopmentConfig.channel}${baseUrl}，仅保存在本机浏览器。`
          : "仅在本地开发环境显示，不会写入账号目录。";
      }
      if (devToolsBuildChip)
        devToolsBuildChip.textContent = currentDevelopmentConfig.frontend_build
          ? `build ${FRONTEND_BUILD} / host ${currentDevelopmentConfig.frontend_build}`
          : `build ${FRONTEND_BUILD}`;
      syncDeveloperSettingsInfo();
      applyDeveloperSettingsVisibility();
    }

    function applyDevToolsStateToUI() {
      currentDevToolsState =
        normalizeDevToolsStatePayload(currentDevToolsState);
      if (devMuteNoticeReminder)
        devMuteNoticeReminder.checked = currentDevToolsState.muteNoticeReminder;
      updateDevToolsSummary();
    }

    function persistDevToolsState() {
      saveDevToolsState(currentDevToolsState);
      applyDevToolsStateToUI();
    }

    function resetDeveloperSettingsUnlockProgress() {
      settingsDevUnlockProgress = 0;
    }

    function registerDeveloperSettingsUnlockClick() {
      if (developerOptionsUnlocked) return;
      settingsDevUnlockProgress += 1;
      if (settingsDevUnlockProgress >= SETTINGS_DEV_UNLOCK_CLICKS) {
        unlockDeveloperOptions();
        resetDeveloperSettingsUnlockProgress();
      }
    }

    function normalizeDesktopSettingsPayload(raw = {}) {
      const payload = raw && typeof raw === "object" ? raw : {};
      const defaults = createDefaultDesktopSettings();
      const launchAtStartup = Boolean(payload.launch_at_startup);
      return {
        launch_at_startup: launchAtStartup,
        silent_startup: launchAtStartup && Boolean(payload.silent_startup),
        close_to_tray:
          payload.close_to_tray == null
            ? defaults.close_to_tray
            : Boolean(payload.close_to_tray),
      };
    }

    function normalizePreloginModeValue(value) {
      const cleaned = String(value || "")
        .trim()
        .toLowerCase();
      return PRELOGIN_MODE_OPTIONS.some((item) => item.value === cleaned)
        ? cleaned
        : "capture";
    }

    function normalizeScheduleSourceValue(value) {
      const cleaned = String(value || "")
        .trim()
        .toLowerCase();
      const known = SCHEDULE_SOURCE_OPTIONS.some((item) => item.value === cleaned)
        ? cleaned
        : "preferred";
      return normalizeActiveScheduleSourceValue(known);
    }

    function normalizeWeekModeValue(value) {
      const cleaned = String(value || "")
        .trim()
        .toLowerCase();
      return ["current", "next", "manual"].includes(cleaned)
        ? cleaned
        : "current";
    }

    function normalizeAutoRefreshModeValue(value) {
      const cleaned = String(value || "")
        .trim()
        .toLowerCase();
      return SCHEDULE_AUTO_REFRESH_MODE_OPTIONS.some(
        (item) => item.value === cleaned,
      )
        ? cleaned
        : "daily";
    }

    function normalizeAutoRefreshDailyTimeValue(value) {
      const cleaned = String(value || "").trim();
      const parts = cleaned.split(":");
      if (parts.length !== 2) return DEFAULT_AUTO_REFRESH_TIME;
      const hours = Number.parseInt(parts[0], 10);
      const minutes = Number.parseInt(parts[1], 10);
      if (!Number.isInteger(hours)) return DEFAULT_AUTO_REFRESH_TIME;
      if (!Number.isInteger(minutes)) return DEFAULT_AUTO_REFRESH_TIME;
      if (hours < 8 || hours > 23 || minutes < 0 || minutes > 59)
        return DEFAULT_AUTO_REFRESH_TIME;
      return `${String(hours).padStart(2, "0")}:${String(minutes).padStart(2, "0")}`;
    }

    function normalizeAutoRefreshIntervalHoursValue(value) {
      const normalized = Number.parseInt(String(value ?? ""), 10);
      return SCHEDULE_AUTO_REFRESH_INTERVAL_OPTIONS.includes(normalized)
        ? normalized
        : DEFAULT_AUTO_REFRESH_INTERVAL_HOURS;
    }

    function normalizeLessonReminderMinutesValue(value) {
      const normalized = Number.parseInt(String(value ?? ""), 10);
      return SCHEDULE_REMINDER_MINUTE_OPTIONS.includes(normalized)
        ? normalized
        : 15;
    }

    function normalizeNetworkProviderValue(value) {
      const cleaned = String(value || "")
        .trim()
        .toLowerCase();
      return NETWORK_PROVIDER_OPTIONS.some((item) => item.value === cleaned)
        ? cleaned
        : DEFAULT_NETWORK_PROVIDER;
    }

    function normalizeNetworkHeartbeatIntervalValue(value) {
      const normalized = Number.parseInt(String(value ?? ""), 10);
      return NETWORK_HEARTBEAT_INTERVAL_OPTIONS.includes(normalized)
        ? normalized
        : DEFAULT_NETWORK_HEARTBEAT_INTERVAL_MINUTES;
    }

    function normalizeCampusSubnetPrefixValue(value) {
      const normalized = Number.parseInt(String(value ?? ""), 10);
      if (!Number.isInteger(normalized)) return 24;
      return Math.min(32, Math.max(1, normalized));
    }

    function getTimeParts(value, fallback = DEFAULT_AUTO_REFRESH_TIME) {
      const normalized = normalizeAutoRefreshDailyTimeValue(value || fallback);
      const [hour = "20", minute = "00"] = normalized.split(":");
      return { hour, minute };
    }

    function createEmptyNetworkCredentialBundle() {
      return {
        telecom: { account: "", password: "" },
        unicom: { account: "", password: "" },
        mobile: { account: "", password: "" },
      };
    }

    function createEmptyNetworkCampusDraft() {
      return {
        campus_ip: "",
        campus_subnet_prefix: 24,
        campus_gateway: "",
        campus_dns: "",
      };
    }

    function normalizeNetworkCampusDraft(raw = {}) {
      return {
        campus_ip: String(raw.campus_ip || "").trim(),
        campus_subnet_prefix: normalizeCampusSubnetPrefixValue(
          raw.campus_subnet_prefix ?? 24,
        ),
        campus_gateway: String(raw.campus_gateway || "").trim(),
        campus_dns: String(raw.campus_dns || "").trim(),
      };
    }

    function syncNetworkCampusDraftFromSettings(
      settings = currentSettings.network,
    ) {
      currentNetworkCampusDraft = normalizeNetworkCampusDraft(settings || {});
    }

    function stashNetworkCampusDraft() {
      currentNetworkCampusDraft = normalizeNetworkCampusDraft({
        campus_ip: networkCampusIp
          ? networkCampusIp.value
          : currentNetworkCampusDraft.campus_ip,
        campus_subnet_prefix: networkCampusSubnetPrefix
          ? networkCampusSubnetPrefix.value
          : currentNetworkCampusDraft.campus_subnet_prefix,
        campus_gateway: networkCampusGateway
          ? networkCampusGateway.value
          : currentNetworkCampusDraft.campus_gateway,
        campus_dns: networkCampusDns
          ? networkCampusDns.value
          : currentNetworkCampusDraft.campus_dns,
      });
    }

    function normalizeNetworkCredentialBundle(raw = {}) {
      const next = createEmptyNetworkCredentialBundle();
      Object.keys(next).forEach((provider) => {
        const source =
          raw && typeof raw[provider] === "object" ? raw[provider] : {};
        next[provider] = {
          account: String(source.account || "").trim(),
          password: String(source.password || "").trim(),
        };
      });
      return next;
    }

    function getNetworkProviderMeta(provider) {
      return (
        NETWORK_PROVIDER_OPTIONS.find((item) => item.value === provider) ||
        NETWORK_PROVIDER_OPTIONS[0]
      );
    }

    function isCarrierNetworkProvider(provider) {
      return (
        normalizeNetworkProviderValue(provider) !== DEFAULT_NETWORK_PROVIDER
      );
    }

    function getAllScheduleBlockIndices() {
      return DEFAULT_SCHEDULE_BLOCKS.map((block) => block.index);
    }

    function isAllScheduleBlocksSelected(blocks = []) {
      const selected = Array.isArray(blocks) ? blocks : [];
      return getAllScheduleBlockIndices().every((blockIndex) =>
        selected.includes(blockIndex),
      );
    }

    function normalizeIgnoredWeekendBlocksValue(raw = {}) {
      const readBlocks = (day) => {
        const blocks = raw && Array.isArray(raw[day]) ? raw[day] : [];
        const next = [];
        blocks.forEach((item) => {
          const blockIndex = Number.parseInt(String(item), 10);
          if (!Number.isInteger(blockIndex)) return;
          if (blockIndex < 1 || blockIndex > DEFAULT_SCHEDULE_BLOCKS.length)
            return;
          if (!next.includes(blockIndex)) next.push(blockIndex);
        });
        return next.sort((left, right) => left - right);
      };
      return {
        saturday: readBlocks("saturday"),
        sunday: readBlocks("sunday"),
      };
    }

    function normalizePanelValue(value) {
      return value === true;
    }

    function normalizeSettingsPayload(raw = {}) {
      const defaults = createDefaultSettings();
      const appearance =
        raw && typeof raw.appearance === "object" ? raw.appearance : {};
      const schedule =
        raw && typeof raw.schedule === "object" ? raw.schedule : {};
      const auth = raw && typeof raw.auth === "object" ? raw.auth : {};
      const network = raw && typeof raw.network === "object" ? raw.network : {};
      const ui = raw && typeof raw.ui === "object" ? raw.ui : {};
      const panels = ui && typeof ui.panels === "object" ? ui.panels : {};
      const topLevelTheme =
        typeof raw.theme === "string" ? raw.theme.trim() : "";
      const topLevelMode =
        typeof raw.prelogin_mode === "string" ? raw.prelogin_mode.trim() : "";

      return {
        appearance: {
          theme: resolveThemeId(
            topLevelTheme || appearance.theme || defaults.appearance.theme,
          ),
        },
        schedule: {
          source: normalizeScheduleSourceValue(
            schedule.source || defaults.schedule.source,
          ),
          week_mode: normalizeWeekModeValue(
            schedule.week_mode || defaults.schedule.week_mode,
          ),
          manual_week: String(schedule.manual_week || "").trim(),
          // Keep the legacy-compatible field, but the mobile-only slice never
          // lets frontend state drive Cookie/JW requests.
          cookie_rq: "",
          term_label: String(schedule.term_label || "").trim(),
          academic_term: String(schedule.academic_term || "").trim(),
          mock_day: String(schedule.mock_day || "").trim(),
          mock_time: String(schedule.mock_time || "").trim(),
          auto_refresh_enabled: Boolean(schedule.auto_refresh_enabled),
          auto_refresh_mode: normalizeAutoRefreshModeValue(
            schedule.auto_refresh_mode || defaults.schedule.auto_refresh_mode,
          ),
          auto_refresh_daily_time: normalizeAutoRefreshDailyTimeValue(
            schedule.auto_refresh_daily_time ||
              defaults.schedule.auto_refresh_daily_time,
          ),
          auto_refresh_interval_hours: normalizeAutoRefreshIntervalHoursValue(
            schedule.auto_refresh_interval_hours ??
              defaults.schedule.auto_refresh_interval_hours,
          ),
          lesson_reminder_enabled: Boolean(schedule.lesson_reminder_enabled),
          lesson_reminder_minutes: normalizeLessonReminderMinutesValue(
            schedule.lesson_reminder_minutes ??
              defaults.schedule.lesson_reminder_minutes,
          ),
          weekend_auto_switch_enabled: Boolean(
            schedule.weekend_auto_switch_enabled,
          ),
          ignored_weekend_blocks: normalizeIgnoredWeekendBlocksValue(
            schedule.ignored_weekend_blocks ||
              defaults.schedule.ignored_weekend_blocks,
          ),
        },
        auth: {
          prelogin_mode: normalizePreloginModeValue(
            topLevelMode || auth.prelogin_mode || defaults.auth.prelogin_mode,
          ),
        },
        network: {
          provider: normalizeNetworkProviderValue(
            network.provider || defaults.network.provider,
          ),
          heartbeat_enabled: Boolean(network.heartbeat_enabled),
          heartbeat_interval_minutes: normalizeNetworkHeartbeatIntervalValue(
            network.heartbeat_interval_minutes ??
              defaults.network.heartbeat_interval_minutes,
          ),
          campus_ip: String(network.campus_ip || "").trim(),
          campus_subnet_prefix: normalizeCampusSubnetPrefixValue(
            network.campus_subnet_prefix ??
              defaults.network.campus_subnet_prefix,
          ),
          campus_gateway: String(network.campus_gateway || "").trim(),
          campus_dns: String(network.campus_dns || "").trim(),
        },
        ui: {
          panels: {
            schedule: normalizePanelValue(panels.schedule),
            login_mode: normalizePanelValue(panels.login_mode),
            appearance: normalizePanelValue(panels.appearance),
          },
        },
      };
    }

    function formatPreloginMode(mode) {
      const found = PRELOGIN_MODE_OPTIONS.find((item) => item.value === mode);
      return found ? found.label : mode || "--";
    }

    function getPreloginModeDescription(mode) {
      const found = PRELOGIN_MODE_OPTIONS.find((item) => item.value === mode);
      return found
        ? found.description
        : "登录方式已更新，后续联网认证会按当前流程执行。";
    }

    function syncLoginModeSettings() {
      const mode =
        selectMode && selectMode.value ? selectMode.value : "capture";
      if (loginModeName) loginModeName.textContent = formatPreloginMode(mode);
      if (loginModeDescription)
        loginModeDescription.textContent = getPreloginModeDescription(mode);
      if (loginModeHint)
        loginModeHint.textContent = sessionId
          ? "后续主动验证时会按当前选择执行；课表联网固定走移动教务链路。"
          : "首次无本地账号时会完成一次全量初始化；已有本地账号后，后续登录只校验本地凭据。";
    }

    function updateDesktopSettingState(inputEl, summaryEl, enabled) {
      if (summaryEl) {
        summaryEl.textContent = enabled ? "已开启" : "已关闭";
        summaryEl.dataset.state = enabled ? "on" : "off";
      }
      const tile =
        (inputEl && typeof inputEl.closest === "function"
          ? inputEl.closest(".desktop-setting-tile")
          : null) ||
        (summaryEl && typeof summaryEl.closest === "function"
          ? summaryEl.closest(".desktop-setting-tile")
          : null);
      if (tile) tile.classList.toggle("is-on", enabled);
    }

    function applyDesktopSettingsToUI(settings = currentDesktopSettings) {
      const normalized = normalizeDesktopSettingsPayload(settings);
      currentDesktopSettings = normalized;
      isApplyingDesktopSettings = true;
      if (desktopLaunchAtStartup)
        desktopLaunchAtStartup.checked = normalized.launch_at_startup;
      if (desktopSilentStartup) {
        desktopSilentStartup.checked = normalized.silent_startup;
        desktopSilentStartup.disabled = !normalized.launch_at_startup;
      }
      if (desktopCloseToTray)
        desktopCloseToTray.checked = normalized.close_to_tray;
      updateDesktopSettingState(
        desktopLaunchAtStartup,
        desktopStartupSummary,
        normalized.launch_at_startup,
      );
      updateDesktopSettingState(
        desktopSilentStartup,
        desktopSilentSummary,
        normalized.silent_startup,
      );
      updateDesktopSettingState(
        desktopCloseToTray,
        desktopTraySummary,
        normalized.close_to_tray,
      );
      if (desktopSilentStartup) {
        const silentTile = desktopSilentStartup.closest(
          ".desktop-setting-tile",
        );
        if (silentTile)
          silentTile.classList.toggle(
            "is-disabled",
            !normalized.launch_at_startup,
          );
      }
      isApplyingDesktopSettings = false;
    }

    function collectCurrentDesktopSettings() {
      const launchAtStartup = Boolean(
        desktopLaunchAtStartup && desktopLaunchAtStartup.checked,
      );
      return normalizeDesktopSettingsPayload({
        launch_at_startup: launchAtStartup,
        silent_startup:
          launchAtStartup &&
          Boolean(desktopSilentStartup && desktopSilentStartup.checked),
        close_to_tray: Boolean(
          desktopCloseToTray && desktopCloseToTray.checked,
        ),
      });
    }

    async function loadDesktopSettings(options = {}) {
      const { silent = true } = options;
      try {
        const res = await get("/desktop/settings");
        currentDesktopSettings = normalizeDesktopSettingsPayload(
          res.data && res.data.settings ? res.data.settings : {},
        );
        applyDesktopSettingsToUI(currentDesktopSettings);
        if (!silent) log(res.msg || "桌面设置已加载");
        return currentDesktopSettings;
      } catch (error) {
        if (!silent) log(`桌面设置加载失败: ${error.message}`);
        return null;
      }
    }

    async function saveDesktopSettings(options = {}) {
      const { silent = false, successMessage = "" } = options;
      const previous = { ...currentDesktopSettings };
      const next = collectCurrentDesktopSettings();
      currentDesktopSettings = next;
      applyDesktopSettingsToUI(next);
      try {
        const res = await post("/desktop/settings", next);
        currentDesktopSettings = normalizeDesktopSettingsPayload(
          res.data && res.data.settings ? res.data.settings : next,
        );
        applyDesktopSettingsToUI(currentDesktopSettings);
        if (!silent) log(successMessage || res.msg || "桌面设置已保存");
        return currentDesktopSettings;
      } catch (error) {
        currentDesktopSettings = previous;
        applyDesktopSettingsToUI(previous);
        if (!silent) log(`桌面设置保存失败: ${error.message}`);
        return null;
      }
    }

    function createHiddenSelectController({
      selectEl,
      combobox,
      trigger,
      menu,
      options,
      placeholder,
      renderOptionContent,
      isDisabled = () => Boolean(selectEl && selectEl.disabled),
    }) {
      return createSelectMenuController({
        combobox,
        trigger,
        menu,
        getOptions: () => options,
        getValue: () => (selectEl ? selectEl.value : ""),
        getTriggerLabel: (value, list) => {
          const found = list.find((item) => item.value === value);
          return found ? found.label : placeholder;
        },
        onSelect: (option) => {
          if (!selectEl) return;
          selectEl.value = option.value;
          selectEl.dispatchEvent(new Event("change"));
        },
        renderOptionContent:
          renderOptionContent ||
          ((option) => {
            const title = esc(option.label || option.value || "");
            const meta = option.description || option.meta;
            if (!meta)
              return `<span class="round-option-title">${title}</span>`;
            return `<span class="round-option-title">${title}</span><span class="round-option-meta">${esc(meta)}</span>`;
          }),
        isDisabled,
      });
    }

    function createTimeFieldController({
      inputEl,
      hourCombobox,
      hourTrigger,
      hourMenu,
      fallback = DEFAULT_AUTO_REFRESH_TIME,
      isDisabled = () => Boolean(inputEl && inputEl.disabled),
    }) {
      const updateHour = (value) => {
        if (!inputEl) return;
        const { minute } = getTimeParts(inputEl.value || fallback, fallback);
        inputEl.value = `${value}:${minute}`;
        inputEl.dispatchEvent(new Event("change"));
      };

      const renderTimeOption = (option) =>
        `<span class="round-option-title">${esc(option.label)}</span>`;
      const hourController = createSelectMenuController({
        combobox: hourCombobox,
        trigger: hourTrigger,
        menu: hourMenu,
        getOptions: () => TIME_HOUR_OPTIONS,
        getValue: () =>
          getTimeParts(inputEl ? inputEl.value : fallback, fallback).hour,
        getTriggerLabel: (value, options) => {
          const found = options.find((item) => item.value === value);
          return found
            ? found.label
            : `${getTimeParts(inputEl ? inputEl.value : fallback, fallback).hour} 时`;
        },
        onSelect: (option) => updateHour(option.value),
        renderOptionContent: renderTimeOption,
        isDisabled,
      });

      return {
        bind() {
          hourController.bind();
        },
        close() {
          hourController.close();
        },
        refresh() {
          if (inputEl)
            inputEl.value = normalizeAutoRefreshDailyTimeValue(
              inputEl.value || fallback,
            );
          hourController.refresh();
        },
        setValue(value) {
          if (!inputEl) return;
          inputEl.value = normalizeAutoRefreshDailyTimeValue(value || fallback);
          this.refresh();
        },
      };
    }

    function isViewingCurrentWeek() {
      return (
        currentScheduleWeekNumber != null &&
        viewedScheduleWeekNumber != null &&
        currentScheduleWeekNumber === viewedScheduleWeekNumber
      );
    }

    function setScheduleManualWeekViewLocked(locked) {
      scheduleManualWeekViewLocked = Boolean(locked);
      if (!scheduleManualWeekViewLocked) scheduleAutoWeekAttemptKey = "";
    }

    function resolveWeekNavTarget(direction) {
      if (viewedScheduleWeekNumber == null) return null;
      const nextWeek = viewedScheduleWeekNumber + direction;
      if (nextWeek < 1) return null;
      if (scheduleAvailableWeeks.length) {
        const labels = scheduleAvailableWeeks
          .map((item) => parseWeekNumberFromLabel(item.label))
          .filter(Boolean);
        if (labels.length) {
          const maxWeek = Math.max(...labels);
          if (nextWeek > maxWeek) return null;
        }
      }
      return nextWeek;
    }

    /**
     * Compare the week currently being viewed against the real current week.
     *
     * - negative: viewing a past week, all cards should be treated as completed
     * - zero: viewing the current week, use weekday/time comparisons
     * - positive: viewing a future week, no card is completed yet
     */
    function getViewedWeekRelation() {
      if (currentScheduleWeekNumber == null || viewedScheduleWeekNumber == null)
        return 0;
      return viewedScheduleWeekNumber - currentScheduleWeekNumber;
    }

    function getScheduleCardKey(row) {
      return [row.weekday, row.section, row.course_name, row.room].join("|");
    }

    function normalizeScheduleDateKey(value) {
      const raw = String(value || "").trim();
      const match = raw.match(/\d{4}-\d{2}-\d{2}/);
      return match ? match[0] : "";
    }

    function normalizeScheduleWeekDates(value) {
      if (!value || typeof value !== "object" || Array.isArray(value))
        return {};
      const normalized = {};
      Object.entries(value).forEach(([weekday, rawDate]) => {
        const normalizedWeekday = String(weekday || "").trim();
        const dateKey = normalizeScheduleDateKey(rawDate);
        if (!normalizedWeekday || !dateKey) return;
        normalized[normalizedWeekday] = dateKey;
      });
      return normalized;
    }

    function formatLocalDateKey(value) {
      const pad = (num) => String(num).padStart(2, "0");
      return `${value.getFullYear()}-${pad(value.getMonth() + 1)}-${pad(value.getDate())}`;
    }

    function compareScheduleDateKeys(left, right) {
      const leftKey = normalizeScheduleDateKey(left);
      const rightKey = normalizeScheduleDateKey(right);
      if (!leftKey || !rightKey) return 0;
      if (leftKey < rightKey) return -1;
      if (leftKey > rightKey) return 1;
      return 0;
    }

    function resolveScheduleRowDateKey(row, weekdayIndex) {
      const directCandidates = [row.class_date, row.classDate, row.date];
      for (const candidate of directCandidates) {
        const dateKey = normalizeScheduleDateKey(candidate);
        if (dateKey) return dateKey;
      }
      const weekdayLabel =
        row.weekday || SCHEDULE_WEEKDAYS[weekdayIndex - 1] || "";
      return normalizeScheduleDateKey(scheduleWeekDates[weekdayLabel] || "");
    }

    function parseClockPair(value) {
      if (!value) return null;
      const matches = String(value).match(/\d{1,2}:\d{2}/g) || [];
      if (matches.length < 2) return null;
      return { startText: matches[0], endText: matches[matches.length - 1] };
    }

    function inferClockRangeFromSection(row) {
      const bounds = parseSectionBounds(row);
      if (!bounds)
        return {
          startText: "",
          endText: "",
          startMinutes: null,
          endMinutes: null,
        };

      const startBlockIndex = mapSectionToBlock(bounds.start);
      const endBlockIndex = mapSectionToBlock(bounds.end);
      const startBlock = scheduleBlocks.find(
        (item) => item.index === startBlockIndex,
      );
      const endBlock = scheduleBlocks.find(
        (item) => item.index === endBlockIndex,
      );
      if (!startBlock || !endBlock)
        return {
          startText: "",
          endText: "",
          startMinutes: null,
          endMinutes: null,
        };

      const startSource =
        (bounds.start % 2 === 1
          ? parseClockPair(startBlock.small1)
          : parseClockPair(startBlock.small2)) ||
        parseClockPair(startBlock.blockTime);
      const endSource =
        (bounds.end % 2 === 1
          ? parseClockPair(endBlock.small1)
          : parseClockPair(endBlock.small2)) ||
        parseClockPair(endBlock.blockTime);
      if (!startSource || !endSource)
        return {
          startText: "",
          endText: "",
          startMinutes: null,
          endMinutes: null,
        };

      const startMinutes = parseClockToMinutes(startSource.startText);
      const endMinutes = parseClockToMinutes(endSource.endText);
      if (startMinutes == null || endMinutes == null)
        return {
          startText: "",
          endText: "",
          startMinutes: null,
          endMinutes: null,
        };

      return {
        startText: startSource.startText,
        endText: endSource.endText,
        startMinutes,
        endMinutes,
      };
    }

    function extractClockRange(row) {
      const candidates = [
        row.block_time,
        row.small_section_1,
        row.small_section_2,
        row.time_range,
        row.time_text,
      ]
        .filter(Boolean)
        .join(" ");
      const matches = candidates.match(/\d{1,2}:\d{2}/g) || [];
      if (matches.length >= 2) {
        return {
          startText: matches[0],
          endText: matches[matches.length - 1],
          startMinutes: parseClockToMinutes(matches[0]),
          endMinutes: parseClockToMinutes(matches[matches.length - 1]),
        };
      }
      return inferClockRangeFromSection(row);
    }

    function buildLiveScheduleRows(rows) {
      return rows
        .map((row) => {
          const weekdayIndex = resolveWeekdayIndex(row.weekday);
          const clockRange = extractClockRange(row);
          if (
            !weekdayIndex ||
            clockRange.startMinutes == null ||
            clockRange.endMinutes == null
          )
            return null;
          const classDateKey = resolveScheduleRowDateKey(row, weekdayIndex);
          return {
            ...row,
            weekdayIndex,
            key: getScheduleCardKey(row),
            classDateKey,
            startText: clockRange.startText,
            endText: clockRange.endText,
            startMinutes: clockRange.startMinutes,
            endMinutes: clockRange.endMinutes,
          };
        })
        .filter(Boolean)
        .sort((left, right) => {
          if (
            left.classDateKey &&
            right.classDateKey &&
            left.classDateKey !== right.classDateKey
          ) {
            return compareScheduleDateKeys(
              left.classDateKey,
              right.classDateKey,
            );
          }
          if (left.weekdayIndex !== right.weekdayIndex)
            return left.weekdayIndex - right.weekdayIndex;
          if (left.startMinutes !== right.startMinutes)
            return left.startMinutes - right.startMinutes;
          return left.course_name.localeCompare(right.course_name, "zh-CN");
        });
    }

    function getRelativeDayText(todayIndex, targetIndex) {
      const delta = (targetIndex - todayIndex + 7) % 7;
      if (delta === 0) return "今天";
      if (delta === 1) return "明天";
      if (delta === 2) return "后天";
      return SCHEDULE_WEEKDAYS[targetIndex - 1] || "之后";
    }

    function findNextScheduleRowThisWeek(rows, todayIndex) {
      return rows.find((row) => row.weekdayIndex > todayIndex) || null;
    }

    function getRelativeScheduleDateText(
      todayDateKey,
      targetDateKey,
      todayIndex,
      targetIndex,
    ) {
      const normalizedToday = normalizeScheduleDateKey(todayDateKey);
      const normalizedTarget = normalizeScheduleDateKey(targetDateKey);
      if (normalizedToday && normalizedTarget) {
        const start = new Date(`${normalizedToday}T00:00:00`);
        const target = new Date(`${normalizedTarget}T00:00:00`);
        const diffDays = Math.round(
          (target.getTime() - start.getTime()) / (24 * 60 * 60 * 1000),
        );
        if (diffDays === 0) return "今天";
        if (diffDays === 1) return "明天";
        if (diffDays === 2) return "后天";
      }
      return getRelativeDayText(todayIndex, targetIndex);
    }

    function setScheduleLiveCopy(_status) {
      // 顶部实时提醒已移除，改为直接在课程卡片中显示。
    }

    function setCardLiveInline(card, text, tone = "muted") {
      const node = card.querySelector(".card-live-inline");
      if (!node) return;
      node.classList.remove(
        "tone-warning",
        "tone-current",
        "tone-muted",
        "tone-alert",
        "is-emphasis",
      );
      if (!text) {
        node.innerHTML = "";
        node.textContent = "";
        return;
      }
      if (typeof text === "object" && text !== null) {
        const label = esc(text.label || "");
        const countdown = esc(text.countdown || "");
        node.innerHTML = `<span class="live-inline-label">${label}</span><span class="live-inline-countdown">${countdown}</span>`;
        node.classList.add("is-emphasis");
      } else {
        node.textContent = text;
      }
      if (tone === "warning") node.classList.add("tone-warning");
      else if (tone === "alert") node.classList.add("tone-alert");
      else if (tone === "current") node.classList.add("tone-current");
      else node.classList.add("tone-muted");
    }

    function renderCardLiveInlineMarkup(text, tone = "muted") {
      const classes = ["card-live-inline"];
      let content = "";
      if (text) {
        if (typeof text === "object" && text !== null) {
          const label = esc(text.label || "");
          const countdown = esc(text.countdown || "");
          content = `<span class="live-inline-label">${label}</span><span class="live-inline-countdown">${countdown}</span>`;
          classes.push("is-emphasis");
        } else {
          content = esc(text);
        }
        if (tone === "warning") classes.push("tone-warning");
        else if (tone === "alert") classes.push("tone-alert");
        else if (tone === "current") classes.push("tone-current");
        else classes.push("tone-muted");
      }
      return `<div class="${classes.join(" ")}" aria-live="polite">${content}</div>`;
    }

    function ensureScheduleCardDecoration(decorations, key) {
      let decoration = decorations.get(key);
      if (!decoration) {
        decoration = {
          classes: new Set(),
          liveProgress: "",
          inlineText: "",
          inlineTone: "muted",
        };
        decorations.set(key, decoration);
      }
      return decoration;
    }

    function setScheduleBoardLoading(loading, message = "正在加载课表...") {
      if (schedLoadingLabel) schedLoadingLabel.textContent = message;
      if (schedBoardWrap)
        schedBoardWrap.classList.toggle("is-loading", loading);
      if (schedLoading) schedLoading.classList.toggle("hidden", !loading);
    }

    function clearScheduleLiveDecorations() {
      schedBoard.querySelectorAll(".schedule-course-card").forEach((card) => {
        card.classList.remove(
          "live-current",
          "live-next",
          "live-next-urgent",
          "live-tomorrow",
          "live-completed",
          "live-finished-day",
          "live-ending",
        );
        card.style.removeProperty("--live-progress");
        setCardLiveInline(card, "");
      });
    }

    function getCurrentTimeInfo() {
      const now = new Date();
      const referenceDate = new Date(now);
      let todayIndex = now.getDay() === 0 ? 7 : now.getDay();
      let currentMinutes = now.getHours() * 60 + now.getMinutes();
      let currentSeconds =
        now.getHours() * 3600 + now.getMinutes() * 60 + now.getSeconds();

      if (isDeveloperOptionsEnabled() && devMockDay && devMockDay.value) {
        const dayValue = parseInt(devMockDay.value, 10);
        if (dayValue >= 1 && dayValue <= 7) {
          referenceDate.setDate(
            referenceDate.getDate() + (dayValue - todayIndex),
          );
          todayIndex = dayValue;
        }
      }

      if (isDeveloperOptionsEnabled() && devMockTime && devMockTime.value) {
        const parts = devMockTime.value.split(":");
        const hours = parseInt(parts[0], 10);
        const minutes = parseInt(parts[1] || "0", 10);
        const seconds = parseInt(parts[2] || "0", 10);
        if (
          !Number.isNaN(hours) &&
          !Number.isNaN(minutes) &&
          !Number.isNaN(seconds)
        ) {
          currentMinutes = hours * 60 + minutes;
          currentSeconds = hours * 3600 + minutes * 60 + seconds;
        }
      }

      return {
        todayIndex,
        todayDateKey: formatLocalDateKey(referenceDate),
        currentMinutes,
        currentSeconds,
      };
    }

    function hasDateAwareScheduleRows(rows) {
      return (
        Array.isArray(rows) &&
        rows.length > 0 &&
        rows.every((row) => Boolean(row.classDateKey))
      );
    }

    function shouldUseDisplayedWeekRelativeStatus(rows) {
      const viewedWeekRelation = getViewedWeekRelation();
      if (viewedWeekRelation == null || viewedWeekRelation <= 0) return false;
      if (!currentSettings.schedule.weekend_auto_switch_enabled) return false;
      if (scheduleManualWeekViewLocked) return false;
      return Array.isArray(rows) && rows.length > 0;
    }

    function shouldUseDateAwareLiveStatus(rows) {
      return (
        hasDateAwareScheduleRows(rows) &&
        !shouldUseDisplayedWeekRelativeStatus(rows)
      );
    }

    function isLiveRowCompleted(row, timeInfo, options = {}) {
      const { useDateAware = Boolean(row.classDateKey) } = options;
      if (useDateAware && row.classDateKey) {
        const dateCompare = compareScheduleDateKeys(
          row.classDateKey,
          timeInfo.todayDateKey,
        );
        if (dateCompare < 0) return true;
        if (dateCompare > 0) return false;
        return row.endMinutes < timeInfo.currentMinutes;
      }
      return (
        row.weekdayIndex < timeInfo.todayIndex ||
        (row.weekdayIndex === timeInfo.todayIndex &&
          row.endMinutes < timeInfo.currentMinutes)
      );
    }

    function computeScheduleLiveStatus(rows) {
      if (!rows.length) {
        return {
          label: "课表提醒",
          title: "刷新课表后，将根据当前时间生成提醒",
          meta: "当前会结合当天课程和下一节课时间自动更新。",
          current: null,
          next: null,
          tomorrow: null,
          finished: null,
          chipText: "",
          chipTone: "muted",
          currentPill: "",
          nextPill: "",
          tomorrowPill: "",
          finishedPill: "",
          highlightMode: "",
        };
      }

      const timeInfo = getCurrentTimeInfo();
      const { todayIndex, todayDateKey, currentMinutes, currentSeconds } =
        timeInfo;
      const dateAware = shouldUseDateAwareLiveStatus(rows);
      const todayRows = dateAware
        ? rows.filter((row) => row.classDateKey === todayDateKey)
        : rows.filter((row) => row.weekdayIndex === todayIndex);
      const previous =
        [...todayRows]
          .reverse()
          .find((row) => currentMinutes > row.endMinutes) || null;
      const current =
        todayRows.find(
          (row) =>
            currentMinutes >= row.startMinutes &&
            currentMinutes <= row.endMinutes,
        ) || null;
      const next =
        todayRows.find((row) => currentMinutes < row.startMinutes) || null;

      const tomorrow = dateAware
        ? rows.find(
            (row) =>
              compareScheduleDateKeys(row.classDateKey, todayDateKey) > 0,
          ) || null
        : findNextScheduleRowThisWeek(rows, todayIndex);

      if (current) {
        const remaining = Math.max(0, current.endMinutes - currentMinutes);
        const upcomingNext = next
          ? ` · 下节 ${next.course_name} ${next.startText || ""} 开始`
          : "";
        const endingSoon = remaining <= 10;
        return {
          label: endingSoon ? "即将下课" : "正在上课",
          title: `${current.course_name} ${endingSoon ? "即将下课" : "正在进行"}`,
          meta: `${current.teacher || "任课教师待定"} · ${current.room || "地点待定"} · 距下课 ${formatMinutesCompact(remaining)}${upcomingNext}`,
          current,
          next,
          tomorrow,
          finished: null,
          chipText: `距下课 ${formatMinutesCompact(remaining)}`,
          chipTone: endingSoon ? "warning" : "in-progress",
          currentPill: `距下课 ${formatMinutesCompact(remaining)}`,
          nextPill: "",
          tomorrowPill: "",
          finishedPill: "",
          highlightMode: endingSoon ? "ending" : "current",
        };
      }

      if (next) {
        const distanceSeconds = Math.max(
          0,
          next.startMinutes * 60 - currentSeconds,
        );
        const distance = Math.max(0, Math.ceil(distanceSeconds / 60));
        const isSoon = distanceSeconds <= 15 * 60;
        const previousInfo = previous
          ? ` · 上节 ${previous.course_name} 已结束`
          : "";
        const countdownText = isSoon
          ? formatSecondsCompact(distanceSeconds)
          : formatMinutesCompact(distance);
        return {
          label: isSoon ? "即将上课" : "课间",
          title: `${next.course_name} 将在 ${formatMinutesCompact(distance)} 后开始`,
          meta: `${next.weekday} ${next.startText || ""}-${next.endText || ""} · ${next.teacher || "任课教师待定"} · ${next.room || "地点待定"}${previousInfo}`,
          current: null,
          next,
          tomorrow,
          finished: null,
          chipText: `距上课 ${countdownText}`,
          chipTone: isSoon ? "alert" : "warning",
          currentPill: "",
          nextPill: isSoon
            ? { label: "距上课", countdown: countdownText }
            : `距上课 ${countdownText}`,
          tomorrowPill: "",
          finishedPill: "",
          highlightMode: "next",
        };
      }

      if (todayRows.length) {
        const finished = todayRows[todayRows.length - 1] || null;
        const sinceEnd = finished
          ? Math.max(0, currentMinutes - finished.endMinutes)
          : 0;
        const justFinished = sinceEnd <= 30;
        if (tomorrow) {
          const relative = getRelativeScheduleDateText(
            todayDateKey,
            tomorrow.classDateKey,
            todayIndex,
            tomorrow.weekdayIndex,
          );
          return {
            label: justFinished ? "刚下课" : "今日课程结束",
            title: justFinished
              ? `${finished.course_name} 刚下课`
              : "今天课程已全部结束",
            meta: `${relative}第一节是 ${tomorrow.course_name}，${tomorrow.startText || "时间待定"} 开始，地点 ${tomorrow.room || "待定"}`,
            current: null,
            next: null,
            tomorrow,
            finished,
            chipText: justFinished
              ? `已下课 ${formatMinutesCompact(sinceEnd)}`
              : "今日已结束",
            chipTone: "muted",
            currentPill: "",
            nextPill: "",
            tomorrowPill: `下一次上课 · ${relative}`,
            finishedPill: justFinished
              ? `刚下课 · ${formatMinutesCompact(sinceEnd)}`
              : "今日已上完",
            highlightMode: "finished",
          };
        }
        return {
          label: justFinished ? "刚下课" : "今日课程结束",
          title: justFinished
            ? `${finished.course_name} 刚下课`
            : "本周剩余时间没有新的排课",
          meta: latestScheduleWeekLabel
            ? `当前查看的是 ${latestScheduleWeekLabel}`
            : "可以切换周次或刷新课表查看最新安排。",
          current: null,
          next: null,
          tomorrow: null,
          finished,
          chipText: justFinished
            ? `已下课 ${formatMinutesCompact(sinceEnd)}`
            : "今日已结束",
          chipTone: "muted",
          currentPill: "",
          nextPill: "",
          tomorrowPill: "",
          finishedPill: justFinished
            ? `刚下课 · ${formatMinutesCompact(sinceEnd)}`
            : "今日已上完",
          highlightMode: "finished",
        };
      }

      if (tomorrow) {
        const relative = getRelativeScheduleDateText(
          todayDateKey,
          tomorrow.classDateKey,
          todayIndex,
          tomorrow.weekdayIndex,
        );
        return {
          label: "今天无课",
          title: "今天没有课程安排",
          meta: `${relative}第一节是 ${tomorrow.course_name}，${tomorrow.startText || "时间待定"} 开始，地点 ${tomorrow.room || "待定"}`,
          current: null,
          next: null,
          tomorrow,
          finished: null,
          chipText: "今天无课",
          chipTone: "muted",
          currentPill: "",
          nextPill: "",
          tomorrowPill: `下一次上课 · ${relative}`,
          finishedPill: "",
          highlightMode: "tomorrow",
        };
      }

      const finished = rows[rows.length - 1] || null;
      if (finished) {
        return {
          label: "本周课程已结束",
          title: "这周课程已经全部结束",
          meta: latestScheduleWeekLabel
            ? `当前查看的是 ${latestScheduleWeekLabel}，可切换周次查看后续安排。`
            : "可切换周次查看后续安排。",
          current: null,
          next: null,
          tomorrow: null,
          finished,
          chipText: "本周已结束",
          chipTone: "muted",
          currentPill: "",
          nextPill: "",
          tomorrowPill: "",
          finishedPill: "本周已上完",
          highlightMode: "finished",
        };
      }

      return {
        label: "课表提醒",
        title: "当前课表里没有识别到可提醒的课程",
        meta: latestScheduleWeekLabel
          ? `当前查看的是 ${latestScheduleWeekLabel}`
          : "刷新课表后会继续尝试生成提醒。",
        current: null,
        next: null,
        tomorrow: null,
        finished: null,
        chipText: "",
        chipTone: "muted",
        currentPill: "",
        nextPill: "",
        tomorrowPill: "",
        finishedPill: "",
        highlightMode: "",
      };
    }

    function buildScheduleLiveDecorations(rows) {
      const decorations = new Map();
      if (!rows.length) return decorations;

      const liveRows = buildLiveScheduleRows(rows);
      const timeInfo = getCurrentTimeInfo();
      const useDateAwareStatus = shouldUseDateAwareLiveStatus(liveRows);
      if (useDateAwareStatus) {
        const firstDateKey = liveRows[0] ? liveRows[0].classDateKey : "";
        const lastDateKey = liveRows[liveRows.length - 1]
          ? liveRows[liveRows.length - 1].classDateKey
          : "";
        if (compareScheduleDateKeys(lastDateKey, timeInfo.todayDateKey) < 0) {
          liveRows.forEach((row) => {
            ensureScheduleCardDecoration(decorations, row.key).classes.add(
              "live-completed",
            );
          });
          return decorations;
        }

        if (compareScheduleDateKeys(firstDateKey, timeInfo.todayDateKey) > 0) {
          return decorations;
        }

        const status = computeScheduleLiveStatus(liveRows);

        liveRows.forEach((row) => {
          if (!isLiveRowCompleted(row, timeInfo, { useDateAware: true }))
            return;
          ensureScheduleCardDecoration(decorations, row.key).classes.add(
            "live-completed",
          );
        });

        if (status.current) {
          const decoration = ensureScheduleCardDecoration(
            decorations,
            status.current.key,
          );
          const totalMins =
            status.current.endMinutes - status.current.startMinutes;
          const elapsedMins = Math.max(
            0,
            timeInfo.currentMinutes - status.current.startMinutes,
          );
          const pct =
            totalMins > 0 ? Math.round((elapsedMins / totalMins) * 100) : 0;
          decoration.liveProgress = `${pct}%`;
          decoration.classes.add(
            status.highlightMode === "ending" ? "live-ending" : "live-current",
          );
          decoration.inlineText = status.currentPill || status.chipText;
          decoration.inlineTone =
            status.highlightMode === "ending" ? "warning" : "current";
        }

        if (!status.current && status.next) {
          const decoration = ensureScheduleCardDecoration(
            decorations,
            status.next.key,
          );
          decoration.classes.add("live-next");
          if (status.chipTone === "alert")
            decoration.classes.add("live-next-urgent");
          decoration.inlineText = status.nextPill || status.chipText;
          decoration.inlineTone =
            status.chipTone === "alert" ? "alert" : "warning";
        }

        if (!status.current && !status.next && status.tomorrow) {
          const decoration = ensureScheduleCardDecoration(
            decorations,
            status.tomorrow.key,
          );
          decoration.classes.add("live-tomorrow");
          decoration.inlineText = status.tomorrowPill || "下一次上课";
          decoration.inlineTone = "muted";
        }

        if (!status.current && !status.next && status.finished) {
          const decoration = ensureScheduleCardDecoration(
            decorations,
            status.finished.key,
          );
          decoration.classes.add("live-finished-day");
          decoration.inlineText = status.finishedPill || status.chipText;
          decoration.inlineTone = "muted";
        }
        return decorations;
      }

      const viewedWeekRelation = getViewedWeekRelation();

      if (viewedWeekRelation < 0) {
        liveRows.forEach((row) => {
          ensureScheduleCardDecoration(decorations, row.key).classes.add(
            "live-completed",
          );
        });
        return decorations;
      }

      const status = computeScheduleLiveStatus(liveRows);
      const { currentMinutes } = timeInfo;

      liveRows.forEach((row) => {
        const isCompleted = isLiveRowCompleted(row, timeInfo, {
          useDateAware: false,
        });
        if (!isCompleted) return;
        ensureScheduleCardDecoration(decorations, row.key).classes.add(
          "live-completed",
        );
      });

      if (status.current) {
        const decoration = ensureScheduleCardDecoration(
          decorations,
          status.current.key,
        );
        const totalMins =
          status.current.endMinutes - status.current.startMinutes;
        const elapsedMins = Math.max(
          0,
          currentMinutes - status.current.startMinutes,
        );
        const pct =
          totalMins > 0 ? Math.round((elapsedMins / totalMins) * 100) : 0;
        decoration.liveProgress = `${pct}%`;
        decoration.classes.add(
          status.highlightMode === "ending" ? "live-ending" : "live-current",
        );
        decoration.inlineText = status.currentPill || status.chipText;
        decoration.inlineTone =
          status.highlightMode === "ending" ? "warning" : "current";
      }

      if (!status.current && status.next) {
        const decoration = ensureScheduleCardDecoration(
          decorations,
          status.next.key,
        );
        decoration.classes.add("live-next");
        if (status.chipTone === "alert")
          decoration.classes.add("live-next-urgent");
        decoration.inlineText = status.nextPill || status.chipText;
        decoration.inlineTone =
          status.chipTone === "alert" ? "alert" : "warning";
      }

      if (!status.current && !status.next && status.tomorrow) {
        const decoration = ensureScheduleCardDecoration(
          decorations,
          status.tomorrow.key,
        );
        decoration.classes.add("live-tomorrow");
        decoration.inlineText = status.tomorrowPill || "下一次上课";
        decoration.inlineTone = "muted";
      }

      if (!status.current && !status.next && status.finished) {
        const decoration = ensureScheduleCardDecoration(
          decorations,
          status.finished.key,
        );
        decoration.classes.add("live-finished-day");
        decoration.inlineText = status.finishedPill || "今日已上完";
        decoration.inlineTone = "muted";
      }

      return decorations;
    }

    function applyScheduleLiveDecorations(decorations) {
      clearScheduleLiveDecorations();
      decorations.forEach((decoration, key) => {
        const card = schedBoard.querySelector(
          `[data-schedule-key="${CSS.escape(key)}"]`,
        );
        if (!card) return;
        decoration.classes.forEach((className) =>
          card.classList.add(className),
        );
        if (decoration.liveProgress)
          card.style.setProperty("--live-progress", decoration.liveProgress);
        if (decoration.inlineText)
          setCardLiveInline(card, decoration.inlineText, decoration.inlineTone);
      });
    }

    function applyScheduleLiveState() {
      if (!latestScheduleRows.length) {
        clearScheduleLiveDecorations();
        scheduleAutoWeekAttemptKey = "";
        syncLessonReminderRuntime();
        return;
      }
      applyScheduleLiveDecorations(
        buildScheduleLiveDecorations(latestScheduleRows),
      );
      if (
        currentSettings.schedule.weekend_auto_switch_enabled &&
        !scheduleManualWeekViewLocked
      ) {
        void maybeAutoSwitchToNextWeek({
          allowNetwork: true,
          sourceHint: schedSource ? schedSource.value : "preferred",
        });
      } else {
        scheduleAutoWeekAttemptKey = "";
      }
      syncLessonReminderRuntime();
    }

    function stopScheduleLiveTracking() {
      if (scheduleLiveTimer) {
        clearInterval(scheduleLiveTimer);
        scheduleLiveTimer = null;
      }
      clearScheduleLiveDecorations();
    }

    function startScheduleLiveTracking() {
      if (scheduleLiveTimer) {
        clearInterval(scheduleLiveTimer);
        scheduleLiveTimer = null;
      }
      if (runtimeWorkloadSuspended || runtimeProtectionMode) return;
      if (!latestScheduleRows.length) return;
      applyScheduleLiveState();
      scheduleLiveTimer = setInterval(() => {
        applyScheduleLiveState();
      }, SCHEDULE_LIVE_REFRESH_INTERVAL_MS);
    }

    function resetLessonReminderState() {
      lessonReminderLastDateKey = "";
      sentLessonReminderKeys = new Set();
    }

    function buildLessonReminderKey(row) {
      if (!row || typeof row !== "object") return "";
      const classDateKey = String(row.classDateKey || row.date_key || "").trim();
      const weekdayKey = Number.isInteger(row.weekdayIndex)
        ? String(row.weekdayIndex)
        : "";
      const startKey = Number.isFinite(Number(row.startMinutes))
        ? String(row.startMinutes)
        : String(row.startText || "").trim();
      const rowKey = String(row.key || row.course_name || "").trim();
      return [classDateKey || weekdayKey, startKey, rowKey].filter(Boolean).join("|");
    }

    function getLessonReminderSettings() {
      const scheduleSettings =
        currentSettings && typeof currentSettings.schedule === "object"
          ? currentSettings.schedule
          : {};
      return {
        enabled: Boolean(scheduleSettings.lesson_reminder_enabled),
        minutes: normalizeLessonReminderMinutesValue(
          scheduleSettings.lesson_reminder_minutes,
        ),
      };
    }

    function resolveLessonReminderCandidate(
      rows = latestScheduleRows,
      timeInfo = getCurrentTimeInfo(),
      reminderMinutes = 15,
    ) {
      const liveRows = buildLiveScheduleRows(rows);
      if (!liveRows.length) return null;
      const maxLeadSeconds =
        normalizeLessonReminderMinutesValue(reminderMinutes) * 60;
      return (
        liveRows.find((row) => {
          if (!row) return false;
          const startSeconds = Number(row.startMinutes) * 60;
          if (!Number.isFinite(startSeconds)) return false;
          const secondsUntilStart = startSeconds - timeInfo.currentSeconds;
          if (secondsUntilStart <= 0 || secondsUntilStart > maxLeadSeconds)
            return false;
          if (row.classDateKey) {
            return (
              compareScheduleDateKeys(row.classDateKey, timeInfo.todayDateKey) ===
              0
            );
          }
          return row.weekdayIndex === timeInfo.todayIndex;
        }) || null
      );
    }

    function buildLessonReminderPayload(row, secondsUntilStart) {
      const leadMinutes = Math.max(1, Math.ceil(secondsUntilStart / 60));
      const title = `课程提醒 · ${row.course_name || "下一节课程"}`;
      const summary = `${row.course_name || "课程"} 将在 ${formatMinutesCompact(leadMinutes)} 后开始`;
      const details = [
        row.weekday,
        row.startText ? `${row.startText}-${row.endText || ""}` : "",
        row.room || "",
        row.teacher || "",
      ]
        .map((item) => String(item || "").trim())
        .filter(Boolean)
        .join(" · ");
      return {
        title,
        message: details ? `${summary} · ${details}` : summary,
      };
    }

    async function primeLessonReminderPermission() {
      if (lessonReminderPermissionPrimed) return true;
      lessonReminderPermissionPrimed = true;
      try {
        return await desktopRuntime.ensureNotificationPermission();
      } catch {
        return false;
      }
    }

    async function maybeSendLessonReminder() {
      const { enabled, minutes } = getLessonReminderSettings();
      if (!enabled || runtimeWorkloadSuspended || runtimeProtectionMode)
        return false;
      if (!latestScheduleRows.length) return false;
      const timeInfo = getCurrentTimeInfo();
      const dayKey = String(timeInfo.todayDateKey || timeInfo.todayIndex || "");
      if (dayKey && lessonReminderLastDateKey !== dayKey) {
        lessonReminderLastDateKey = dayKey;
        sentLessonReminderKeys = new Set();
      }
      const row = resolveLessonReminderCandidate(
        latestScheduleRows,
        timeInfo,
        minutes,
      );
      if (!row) return false;
      const reminderKey = buildLessonReminderKey(row);
      if (!reminderKey || sentLessonReminderKeys.has(reminderKey)) return false;
      const allowed = await primeLessonReminderPermission();
      if (!allowed && !desktopRuntime.isDesktopShellAvailable()) return false;
      const secondsUntilStart = Math.max(
        1,
        Number(row.startMinutes) * 60 - timeInfo.currentSeconds,
      );
      const payload = buildLessonReminderPayload(row, secondsUntilStart);
      const result = await desktopRuntime.notify(payload);
      if (result === "unsupported" || result === "denied") return false;
      sentLessonReminderKeys.add(reminderKey);
      log(
        `课程提醒已发送：${row.course_name || "课程"} 将在 ${formatMinutesCompact(
          Math.max(1, Math.ceil(secondsUntilStart / 60)),
        )} 后开始`,
      );
      return true;
    }

    function syncLessonReminderRuntime() {
      const { enabled } = getLessonReminderSettings();
      const timeInfo = getCurrentTimeInfo();
      const dayKey = String(timeInfo.todayDateKey || timeInfo.todayIndex || "");
      if (dayKey && lessonReminderLastDateKey !== dayKey) {
        lessonReminderLastDateKey = dayKey;
        sentLessonReminderKeys = new Set();
      }
      if (!enabled || !latestScheduleRows.length) return;
      void maybeSendLessonReminder();
    }

    // ── Logging ─────────────────────────────────────────────
    function formatCourseGrabInterval(value) {
      const numeric = Number(value);
      if (!Number.isFinite(numeric))
        return String(DEFAULT_COURSE_GRAB_MIN_INTERVAL_SEC);
      return Number.isInteger(numeric)
        ? String(numeric)
        : String(Number(numeric.toFixed(3)));
    }

    function createDefaultCourseGrabRuntimeState() {
      return {
        status: "idle",
        running: false,
        cooldownActive: false,
        cooldownUntil: "",
        cooldownRemainingSec: 0,
        runtimeDeadlineAt: "",
        runtimeRemainingSec: 0,
        lastStartedAt: "",
        lastStoppedAt: "",
        lastReason: "",
        zbid: "",
        kcid: "",
        jx0404id: "",
        intervalSec: 0,
      };
    }

    function createDefaultCourseFrameworkState() {
      const deferredState = createDefaultCourseDeferredState();
      return {
        student_id: "",
        storage_dir: "",
        ...deferredState,
      };
    }

    function normalizeCourseFrameworkStatePayload(raw = {}) {
      const payload = raw && typeof raw === "object" ? raw : {};
      const connection = normalizeCourseConnectionState(
        payload.course_connection && typeof payload.course_connection === "object"
          ? payload.course_connection
          : {},
      );
      const features =
        payload.features && typeof payload.features === "object"
          ? payload.features
          : {};
      const grabState =
        payload.grab_state && typeof payload.grab_state === "object"
          ? payload.grab_state
          : {};
      const base = createDefaultCourseFrameworkState();
      return {
        student_id: String(payload.student_id || "").trim(),
        storage_dir: String(payload.storage_dir || "").trim(),
        course_connection: connection,
        features: {
          rounds: features.rounds !== false,
          preview: features.preview !== false,
          grab: Boolean(features.grab),
          grab_reason: String(
            features.grab_reason || base.features.grab_reason,
          ).trim(),
        },
        grab_state: {
          status: String(grabState.status || base.grab_state.status).trim(),
          running: Boolean(grabState.running),
          deferred:
            grabState.deferred == null
              ? base.grab_state.deferred
              : Boolean(grabState.deferred),
          reason: String(grabState.reason || base.grab_state.reason).trim(),
          message: String(grabState.message || base.grab_state.message).trim(),
        },
      };
    }

    function parseRuntimeTimestampMs(value) {
      const text = String(value || "").trim();
      if (!text) return Number.NaN;
      return Date.parse(text);
    }

    function normalizeCourseGrabRuntimeStatePayload(raw = {}) {
      const payload = raw && typeof raw === "object" ? raw : {};
      const base = createDefaultCourseGrabRuntimeState();
      const now = Date.now();
      const runtimeDeadlineAt = String(
        payload.runtime_deadline_at ?? payload.runtimeDeadlineAt ?? "",
      ).trim();
      const cooldownUntil = String(
        payload.cooldown_until ?? payload.cooldownUntil ?? "",
      ).trim();
      const runtimeDeadlineMs = parseRuntimeTimestampMs(runtimeDeadlineAt);
      const cooldownUntilMs = parseRuntimeTimestampMs(cooldownUntil);
      const runtimeRemainingFromTime = Number.isFinite(runtimeDeadlineMs)
        ? Math.max(0, Math.ceil((runtimeDeadlineMs - now) / 1000))
        : 0;
      const cooldownRemainingFromTime = Number.isFinite(cooldownUntilMs)
        ? Math.max(0, Math.ceil((cooldownUntilMs - now) / 1000))
        : 0;
      const runtimeRemainingRaw = Number.parseInt(
        String(payload.runtime_remaining_sec ?? payload.runtimeRemainingSec ?? ""),
        10,
      );
      const cooldownRemainingRaw = Number.parseInt(
        String(
          payload.cooldown_remaining_sec ?? payload.cooldownRemainingSec ?? "",
        ),
        10,
      );
      const runtimeRemainingSec = Number.isInteger(runtimeRemainingRaw)
        ? Math.max(0, runtimeRemainingRaw)
        : runtimeRemainingFromTime;
      const cooldownRemainingSec = Number.isInteger(cooldownRemainingRaw)
        ? Math.max(0, cooldownRemainingRaw)
        : cooldownRemainingFromTime;
      const rawStatus = String(payload.status || "").trim().toLowerCase();
      const running =
        (Boolean(payload.running) || rawStatus === "running") &&
        runtimeRemainingSec > 0;
      const cooldownActive =
        rawStatus === "cooldown" ||
        Boolean(payload.cooldown_active ?? payload.cooldownActive) ||
        cooldownRemainingSec > 0;
      let status = rawStatus || base.status;
      if (running) status = "running";
      else if (status === "stopping") status = "stopping";
      else if (cooldownActive) status = "cooldown";
      else status = "idle";
      const intervalParsed = Number.parseFloat(
        String(payload.interval_sec ?? payload.intervalSec ?? 0),
      );
      return {
        status,
        running,
        cooldownActive,
        cooldownUntil,
        cooldownRemainingSec,
        runtimeDeadlineAt,
        runtimeRemainingSec,
        lastStartedAt: String(
          payload.last_started_at ?? payload.lastStartedAt ?? "",
        ).trim(),
        lastStoppedAt: String(
          payload.last_stopped_at ?? payload.lastStoppedAt ?? "",
        ).trim(),
        lastReason: String(payload.last_reason ?? payload.lastReason ?? "").trim(),
        zbid: String(payload.zbid || "").trim(),
        kcid: String(payload.kcid || "").trim(),
        jx0404id: String(payload.jx0404id || "").trim(),
        intervalSec: Number.isFinite(intervalParsed) ? intervalParsed : 0,
      };
    }

    function clearCourseGrabRuntimeTimer() {
      if (courseGrabRuntimeTimer) {
        clearTimeout(courseGrabRuntimeTimer);
        courseGrabRuntimeTimer = null;
      }
    }

    function scheduleCourseGrabRuntimeSync() {
      clearCourseGrabRuntimeTimer();
      if (
        !courseGrabRuntimeState.running &&
        !courseGrabRuntimeState.cooldownActive &&
        courseGrabRuntimeState.status !== "stopping"
      ) {
        return;
      }
      courseGrabRuntimeTimer = window.setTimeout(() => {
        courseGrabRuntimeTimer = null;
        applyCourseGrabRuntimeState(courseGrabRuntimeState, {
          refreshHint: true,
        });
      }, 1000);
    }

    function applyCourseGrabRuntimeState(raw = {}, { refreshHint = true } = {}) {
      courseGrabRuntimeState = normalizeCourseGrabRuntimeStatePayload(raw);
      isCourseGrabRunning =
        courseGrabRuntimeState.running ||
        courseGrabRuntimeState.status === "stopping";
      scheduleCourseGrabRuntimeSync();
      syncCourseActionState({ refreshHint });
      return courseGrabRuntimeState;
    }

    function resetCourseGrabRuntimeState() {
      clearCourseGrabRuntimeTimer();
      courseGrabRuntimeState = createDefaultCourseGrabRuntimeState();
      isCourseGrabRunning = false;
    }

    function getRemoteCourseGrabPolicy(config = currentRemoteConfig) {
      const featureFlags =
        config && typeof config.feature_flags === "object"
          ? config.feature_flags
          : {};
      const featureLimits =
        config && typeof config.feature_limits === "object"
          ? config.feature_limits
          : {};
      const courseGrabLimits =
        featureLimits.course_grab &&
        typeof featureLimits.course_grab === "object"
          ? featureLimits.course_grab
          : {};
      const minIntervalSec = Number.parseFloat(
        String(
          courseGrabLimits.min_interval_sec ??
            DEFAULT_COURSE_GRAB_MIN_INTERVAL_SEC,
        ),
      );
      const maxRuntimeSec = Number.parseInt(
        String(
          courseGrabLimits.max_runtime_sec ??
            DEFAULT_COURSE_GRAB_MAX_RUNTIME_SEC,
        ),
        10,
      );
      const cooldownSec = Number.parseInt(
        String(
          courseGrabLimits.cooldown_sec ??
            DEFAULT_COURSE_GRAB_DAILY_COOLDOWN_SEC,
        ),
        10,
      );
      return {
        enabled: featureFlags.course_grab === true,
        minIntervalSec:
          Number.isFinite(minIntervalSec) && minIntervalSec >= 0.1
            ? Math.round(minIntervalSec * 1000) / 1000
            : DEFAULT_COURSE_GRAB_MIN_INTERVAL_SEC,
        maxRuntimeSec:
          Number.isInteger(maxRuntimeSec) && maxRuntimeSec >= 1
            ? maxRuntimeSec
            : DEFAULT_COURSE_GRAB_MAX_RUNTIME_SEC,
        cooldownSec:
          Number.isInteger(cooldownSec) && cooldownSec >= 0
            ? cooldownSec
            : DEFAULT_COURSE_GRAB_DAILY_COOLDOWN_SEC,
      };
    }

    function resolveCourseGrabIntervalValue() {
      const policy = getRemoteCourseGrabPolicy();
      const parsed = Number.parseFloat(
        String(grabInterval && grabInterval.value ? grabInterval.value : ""),
      );
      if (!Number.isFinite(parsed)) return policy.minIntervalSec;
      return Math.max(parsed, policy.minIntervalSec);
    }

    function renderCourseGrabStatus({
      state = "info",
      badge = "状态",
      message = "",
      meta = [],
    } = {}) {
      if (!grabMsg) return;
      const metaText = (Array.isArray(meta) ? meta : [meta])
        .map((item) => String(item || "").trim())
        .filter(Boolean)
        .join(" · ");
      grabMsg.className = "course-grab-status";
      grabMsg.dataset.state = state;
      grabMsg.innerHTML = `
      <span class="course-grab-status-badge">${esc(badge)}</span>
      <p class="course-grab-status-message">${esc(message)}</p>
      ${metaText ? `<span class="course-grab-status-meta">${esc(metaText)}</span>` : ""}
    `;
    }

    function getCourseConnection() {
      return currentCourseState.course_connection || {};
    }

    function isCourseConnected() {
      return canUseCourseCenter(getCourseConnection());
    }

    function resolveCourseConnectionMessage() {
      return resolveDeferredCourseMessage(getCourseConnection(), Boolean(sessionId));
    }

    function renderCourseFrameworkState() {
      const connection = getCourseConnection();
      const connected = canUseCourseCenter(connection);
      const statusLabel = !sessionId
        ? "未登录"
        : connected
          ? "Course 已连接"
          : connection.status === "deferred"
            ? "验证码后置"
            : "未连接";
      if (courseConnectionStatus) {
        courseConnectionStatus.textContent = statusLabel;
        courseConnectionStatus.title = resolveCourseConnectionMessage();
      }
      if (btnCourseConnect && !btnCourseConnect.classList.contains("loading")) {
        btnCourseConnect.textContent = connected ? "已连接" : "后续接入";
        btnCourseConnect.disabled = true;
      }
      if (courseInfo && !courseInfo.textContent.trim()) {
        courseInfo.textContent = resolveCourseConnectionMessage();
      }
      syncCourseActionState();
    }

    function applyCourseFrameworkState(raw = {}, { updateInfo = true } = {}) {
      currentCourseState = normalizeCourseFrameworkStatePayload(raw);
      clearCourseGrabRuntimeTimer();
      isCourseGrabRunning = false;
      const grabState = currentCourseState.grab_state || {};
      courseGrabRuntimeState = {
        ...createDefaultCourseGrabRuntimeState(),
        status: grabState.status || "deferred",
        running: false,
        lastReason: grabState.reason || COURSE_GRAB_DEFERRED_REASON,
      };
      if (updateInfo && courseInfo) {
        courseInfo.textContent = resolveCourseConnectionMessage();
      }
      renderCourseFrameworkState();
      syncCourseGrabHint();
    }

    function resetCourseFrameworkState({ clearInfo = true } = {}) {
      currentCourseState = createDefaultCourseFrameworkState();
      clearCourseGrabRuntimeTimer();
      courseRounds = [];
      if (selectRound) {
        selectRound.innerHTML = '<option value="">点击刷新轮次后选择</option>';
      }
      if (inputZbid) inputZbid.value = "";
      resetSelectedCourseState({ clearGrabState: true });
      if (clearInfo && courseInfo) {
        courseInfo.textContent = sessionId
          ? COURSE_DEFERRED_MESSAGE
          : "";
      }
      renderCourseFrameworkState();
    }

    async function loadCourseFrameworkStatus({ silent = false } = {}) {
      if (!sessionId) {
        resetCourseFrameworkState({ clearInfo: false });
        return null;
      }
      try {
        const res = await getWithSession("/course/status");
        applyCourseFrameworkState(res.data || {}, { updateInfo: !silent });
        if (!silent) log(res.msg || "选课中心状态已加载");
        return res.data || {};
      } catch (error) {
        currentCourseState = createDefaultCourseFrameworkState();
        if (!silent) {
          const message = error.message || "选课中心状态加载失败";
          if (courseInfo) courseInfo.textContent = message;
          log(`选课中心状态加载失败: ${message}`);
        }
        renderCourseFrameworkState();
        return null;
      }
    }

    async function connectCourseFramework({ silent = false } = {}) {
      if (!sessionId) return false;
      currentCourseState = normalizeCourseFrameworkStatePayload({
        ...currentCourseState,
        course_connection: {
          connected: false,
          status: "deferred",
          reason: COURSE_DEFERRED_REASON,
          message: COURSE_DEFERRED_MESSAGE,
          updated_at: new Date().toISOString(),
        },
      });
      const message = resolveCourseConnectionMessage();
      if (courseInfo) courseInfo.textContent = message;
      renderCourseFrameworkState();
      syncCourseGrabHint();
      if (!silent) log(`选课中心连接未完成: ${message}`);
      return false;
    }

    function syncCourseGrabHint() {
      if (!grabMsg) return;
      // course grab intentionally deferred; do not add real polling/JW mutation in this slice
      renderCourseGrabStatus({
        state: isCourseConnected() ? "info" : "muted",
        badge: "后续切片",
        message: COURSE_GRAB_DEFERRED_ACTION_MESSAGE,
        meta: [
          sessionId ? resolveCourseConnectionMessage() : "请先登录",
          "当前仅开放选课轮次与课程预览框架",
        ],
      });
      return;
      const policy = getRemoteCourseGrabPolicy();
      const hasRound = !!inputZbid.value.trim();
      const hasSelectedCourse = Boolean(
        grabKcid &&
        grabKcid.value.trim() &&
        grabJx0404id &&
        grabJx0404id.value.trim(),
      );
      const courseCenterReady =
        Boolean(sessionId) && canUseCourseCenter(getCourseConnection());
      const minIntervalLabel = `最低频率 ${formatCourseGrabInterval(policy.minIntervalSec)} 秒`;
      const runtimeLimitLabel = `单次最多 ${policy.maxRuntimeSec} 秒`;
      if (!policy.enabled) {
        renderCourseGrabStatus({
          state: "muted",
          badge: "功能关闭",
          message: "抢课功能开发中，敬请期待。",
          meta: [minIntervalLabel, runtimeLimitLabel],
        });
        return;
      }
      if (courseGrabRuntimeState.cooldownActive) {
        renderCourseGrabStatus({
          state: "muted",
          badge: "今日冷却",
          message: `抢课额度已锁定，请在 ${formatTimestampText(courseGrabRuntimeState.cooldownUntil)} 后再试。`,
          meta: [
            `冷却剩余 ${formatSecondsCompact(courseGrabRuntimeState.cooldownRemainingSec)}`,
            runtimeLimitLabel,
          ],
        });
        return;
      }
      if (isCourseGrabRunning) {
        renderCourseGrabStatus({
          state: "success",
          badge: "执行中",
          message: `正在按 ${formatCourseGrabInterval(resolveCourseGrabIntervalValue())} 秒频率持续提交请求。`,
          meta: [
            `当前频率 ${formatCourseGrabInterval(resolveCourseGrabIntervalValue())} 秒`,
            `剩余窗口 ${formatSecondsCompact(courseGrabRuntimeState.runtimeRemainingSec || policy.maxRuntimeSec)}`,
          ],
        });
        return;
      }
      if (!courseCenterReady) {
        renderCourseGrabStatus({
          state: "info",
          badge: "准备步骤",
          message: "选课中心真实连接后置，当前只保留框架与预留状态。",
          meta: [minIntervalLabel, runtimeLimitLabel],
        });
        return;
      }
      if (!hasRound) {
        renderCourseGrabStatus({
          state: "info",
          badge: "准备步骤",
          message: "先选择一个选课轮次。",
          meta: [minIntervalLabel, runtimeLimitLabel],
        });
        return;
      }
      if (!hasSelectedCourse) {
        renderCourseGrabStatus({
          state: "info",
          badge: "准备步骤",
          message: "先从课程列表锁定目标课程，代码会自动回填。",
          meta: [minIntervalLabel, runtimeLimitLabel],
        });
        return;
      }
      renderCourseGrabStatus({
        state: "ready",
        badge: "已就绪",
        message: "轮次、课程和频率都已准备好，可以直接启动。",
        meta: [
          minIntervalLabel,
          runtimeLimitLabel,
          `当前频率 ${formatCourseGrabInterval(resolveCourseGrabIntervalValue())} 秒`,
        ],
      });
    }

    function syncCourseActionState({ refreshHint = true } = {}) {
      const flags = currentRemoteConfig.feature_flags || {};
      const policy = getRemoteCourseGrabPolicy();
      const hasRound = !!inputZbid.value.trim();
      const courseCenterReady =
        Boolean(sessionId) && canUseCourseCenter(getCourseConnection());
      const canManageCourseContext = courseCenterReady && !isCourseGrabRunning;
      const cooldownActive = Boolean(courseGrabRuntimeState.cooldownActive);
      const canStartGrab = false;
      const canStopGrab = false;
      selectRound.disabled = !canManageCourseContext || !courseRounds.length;
      if (btnCourseConnect && !btnCourseConnect.classList.contains("loading")) {
        btnCourseConnect.disabled = true;
      }
      btnLoadRounds.disabled = !Boolean(sessionId) || isCourseGrabRunning;
      btnPreview.disabled =
        !canManageCourseContext ||
        !hasRound ||
        flags.public_course_preview === false;
      if (btnConnectJw && !btnConnectJw.classList.contains("loading")) {
        btnConnectJw.disabled =
          isCourseGrabRunning || isJwConnected || !Boolean(sessionId);
      }
      if (grabInterval) {
        const normalizedValue = resolveCourseGrabIntervalValue();
        grabInterval.min = formatCourseGrabInterval(policy.minIntervalSec);
        grabInterval.value = formatCourseGrabInterval(normalizedValue);
        grabInterval.disabled = true;
      }
      if (btnGrabStart) btnGrabStart.disabled = !canStartGrab;
      if (btnGrabStop) btnGrabStop.disabled = !canStopGrab;
      if (refreshHint) syncCourseGrabHint();
    }

    function updateStudentProfile(profile) {
      currentStudentProfile = profile ? { ...profile } : null;
      currentStudentId =
        profile && profile.student_id ? profile.student_id : "";
      renderStudentProfile({
        profile,
        refs: {
          studentName,
          studentMeta,
          profileAcademy,
          profileMajorClass,
          profileName,
          profileLevel,
        },
        avatarController,
      });
      scheduleProfileHydrationDiagnostic(profile);
      if (studentName) {
        studentName.tabIndex = profile ? 0 : -1;
        if (profile) studentName.setAttribute("role", "button");
        else studentName.removeAttribute("role");
      }
      syncNetworkConfigUI();
      syncAboutTelemetryUploadActionState({ resetStatus: true });
      syncUpdatesFeedbackActionState({ resetStatus: true });
    }

    function scheduleProfileHydrationDiagnostic(profile) {
      if (profileHydrationDiagnosticLogged || !profile) return;
      window.setTimeout(() => {
        if (profileHydrationDiagnosticLogged) return;
        const mismatches = collectProfileHydrationMismatches(profile, {
          academy: profileAcademy && profileAcademy.textContent,
          major_class: profileMajorClass && profileMajorClass.textContent,
          training_level: profileLevel && profileLevel.textContent,
        });
        if (!mismatches.length) return;
        profileHydrationDiagnosticLogged = true;
        console.warn("XKE profile hydration mismatch", {
          frontend_build: FRONTEND_BUILD,
          host_frontend_build: currentDevelopmentConfig.frontend_build || "",
          base_url: currentDevelopmentConfig.base_url || "",
          web_root: currentDevelopmentConfig.web_root || "",
          student_id: profile.student_id || "",
          received_fields: {
            academy_name: Boolean(profile.academy_name),
            major_name: Boolean(profile.major_name),
            class_name: Boolean(profile.class_name),
            training_level: Boolean(profile.training_level),
          },
          mismatches,
        });
        log(
          `资料字段已收到但界面仍显示未提供：${mismatches
            .map((item) => item.field)
            .join("、")}；请检查 WebView 缓存或前端资源版本。`,
        );
      }, 0);
    }

    function getEditableProfileName(profile = currentStudentProfile) {
      if (!profile) return "";
      if (profile.use_nickname && profile.nickname) return profile.nickname;
      return profile.name || profile.student_id || "";
    }

    function getNicknameFallbackName(profile = currentStudentProfile) {
      if (!profile) return "";
      return profile.name || profile.student_id || "未识别姓名";
    }

    function normalizeNicknameDraft(value) {
      return String(value || "")
        .replace(/\u00a0/g, " ")
        .replace(/\r?\n/g, " ")
        .replace(/\s+/g, " ")
        .trim();
    }

    function isWideNicknameChar(char) {
      return /[\u1100-\u11ff\u2e80-\ua4cf\uac00-\ud7af\uf900-\ufaff\ufe10-\ufe6f\uff00-\uff60\uffe0-\uffe6]/.test(
        char,
      );
    }

    function trimNicknameToLimit(value) {
      const normalized = normalizeNicknameDraft(value);
      let weight = 0;
      let result = "";
      for (const char of Array.from(normalized)) {
        const nextWeight = isWideNicknameChar(char)
          ? NICKNAME_WIDE_CHAR_WEIGHT
          : NICKNAME_NARROW_CHAR_WEIGHT;
        if (weight + nextWeight > NICKNAME_MAX_WEIGHT) break;
        result += char;
        weight += nextWeight;
      }
      return result;
    }

    function ensureNicknameMeasureEl() {
      if (nicknameEditState.measureEl) return nicknameEditState.measureEl;
      const el = document.createElement("span");
      el.className = "profile-name-input-measure";
      document.body.appendChild(el);
      nicknameEditState.measureEl = el;
      return el;
    }

    function syncNicknameInputWidth(inputEl = nicknameEditState.inputEl) {
      if (!studentName || !inputEl) return;
      const measureEl = ensureNicknameMeasureEl();
      const computed = window.getComputedStyle(inputEl);
      measureEl.style.font = computed.font;
      measureEl.style.letterSpacing = computed.letterSpacing;
      measureEl.textContent = `${inputEl.value || inputEl.placeholder || " "} `;
      const minWidth = 18;
      const nextWidth = Math.max(
        minWidth,
        Math.ceil(measureEl.getBoundingClientRect().width) + 2,
      );
      inputEl.style.width = `${nextWidth}px`;
    }

    function destroyNicknameInput() {
      if (nicknameEditState.inputEl && nicknameEditState.inputEl.parentNode) {
        nicknameEditState.inputEl.remove();
      }
      nicknameEditState.inputEl = null;
    }

    function clearNicknameEditingState() {
      if (!studentName) return;
      destroyNicknameInput();
      studentName.classList.remove("is-editing", "is-saving");
      studentName.removeAttribute("data-editing");
      nicknameEditState.composing = false;
    }

    function startNicknameEdit() {
      if (
        !studentName ||
        !sessionId ||
        !currentStudentId ||
        !currentStudentProfile
      )
        return;
      if (nicknameEditState.active || nicknameEditState.saving) return;
      nicknameEditState.active = true;
      nicknameEditState.composing = false;
      nicknameEditState.initialNickname = currentStudentProfile.nickname || "";
      studentName.classList.add("is-editing");
      studentName.classList.remove("is-saving");
      studentName.setAttribute("data-editing", "true");
      studentName.removeAttribute("title");
      studentName.textContent = "";
      const inputEl = document.createElement("input");
      inputEl.type = "text";
      inputEl.className = "profile-name-inline-input";
      inputEl.value = nicknameEditState.initialNickname;
      inputEl.placeholder = getNicknameFallbackName();
      inputEl.autocomplete = "off";
      inputEl.spellcheck = false;
      inputEl.setAttribute("aria-label", "自定义昵称");
      inputEl.addEventListener("compositionstart", () => {
        nicknameEditState.composing = true;
        syncNicknameInputWidth(inputEl);
      });
      inputEl.addEventListener("compositionend", () => {
        nicknameEditState.composing = false;
        inputEl.value = trimNicknameToLimit(inputEl.value);
        syncNicknameInputWidth(inputEl);
      });
      inputEl.addEventListener("input", (event) => {
        if (nicknameEditState.composing || event.isComposing) {
          syncNicknameInputWidth(inputEl);
          return;
        }
        inputEl.value = trimNicknameToLimit(inputEl.value);
        syncNicknameInputWidth(inputEl);
      });
      inputEl.addEventListener("keydown", (event) => {
        if (
          event.isComposing ||
          event.keyCode === 229 ||
          nicknameEditState.composing
        )
          return;
        if (event.key === "Enter") {
          event.preventDefault();
          inputEl.blur();
          return;
        }
        if (event.key === "Escape") {
          event.preventDefault();
          void finishNicknameEdit({ revert: true });
        }
      });
      inputEl.addEventListener("blur", () => {
        if (!nicknameEditState.active || nicknameEditState.saving) return;
        void finishNicknameEdit();
      });
      studentName.appendChild(inputEl);
      nicknameEditState.inputEl = inputEl;
      syncNicknameInputWidth(inputEl);
      requestAnimationFrame(() => {
        inputEl.focus({ preventScroll: true });
        const pos = inputEl.value.length;
        inputEl.setSelectionRange(pos, pos);
      });
    }

    async function finishNicknameEdit({ revert = false } = {}) {
      if (!studentName || !currentStudentProfile) return;
      if (!nicknameEditState.active || nicknameEditState.saving) return;

      const previousProfile = { ...currentStudentProfile };
      const nextNickname = revert
        ? nicknameEditState.initialNickname
        : trimNicknameToLimit(
            nicknameEditState.inputEl ? nicknameEditState.inputEl.value : "",
          );
      const hasChanged = nextNickname !== nicknameEditState.initialNickname;

      nicknameEditState.active = false;

      if (revert || !hasChanged) {
        clearNicknameEditingState();
        updateStudentProfile(previousProfile);
        return;
      }

      nicknameEditState.saving = true;
      destroyNicknameInput();
      studentName.classList.remove("is-editing");
      studentName.classList.add("is-saving");
      studentName.textContent =
        nextNickname || getNicknameFallbackName(previousProfile);

      try {
        const res = await postWithSession("/auth/profile", {
          nickname: nextNickname,
          use_nickname: Boolean(nextNickname),
        });
        const nextStudent =
          res.data && res.data.student
            ? res.data.student
            : {
                ...previousProfile,
                nickname: nextNickname,
                use_nickname: Boolean(nextNickname),
              };
        clearNicknameEditingState();
        updateStudentProfile(nextStudent);
        log(
          nextNickname ? `昵称已更新: ${nextNickname}` : "已恢复默认姓名显示",
        );
      } catch (error) {
        clearNicknameEditingState();
        updateStudentProfile(previousProfile);
        log(`昵称保存失败: ${error.message}`);
      } finally {
        nicknameEditState.saving = false;
        nicknameEditState.initialNickname = "";
      }
    }

    function getAppearanceScopeText() {
      return currentStudentId
        ? `${currentStudentId} / 本地账号`
        : "当前设备 / 未登录时沿用上次皮肤";
    }

    function resolveThemePreference(preferences) {
      const serverTheme =
        preferences && preferences.appearance && preferences.appearance.theme
          ? preferences.appearance.theme
          : preferences && preferences.theme
            ? preferences.theme
            : "";
      const accountTheme = currentStudentId
        ? getThemeForStudent(currentStudentId)
        : "";
      return resolveThemeId(
        serverTheme || accountTheme || currentPreferences.theme,
      );
    }

    function applyPreferences(preferences, options = {}) {
      const { persist = true, themeAnimation = null } = options;
      currentPreferences = {
        theme: resolveThemePreference(preferences),
      };
      if (persist && currentStudentId)
        saveThemeForStudent(currentStudentId, currentPreferences.theme);
      themeController.applyTheme(currentPreferences.theme, {
        persist,
        scopeText: getAppearanceScopeText(),
        animate: Boolean(themeAnimation && themeAnimation.animate),
      });
    }

    function formatWeekendDayStatus(dayKey, blocks = []) {
      const normalizedBlocks = normalizeIgnoredWeekendBlocksValue({
        [dayKey]: blocks,
      })[dayKey];
      if (!normalizedBlocks.length)
        return "未忽略课程，周末只要还有课就会继续停留在本周。";
      if (isAllScheduleBlocksSelected(normalizedBlocks))
        return "已忽略整天，本日课程不会阻止默认切到下周。";
      return `已忽略 ${normalizedBlocks.length} 个大节，其余周末课程仍会阻止自动跳周。`;
    }

    function getWeekendDayBlockInputs(dayKey) {
      if (!schedWeekendIgnoreGrid) return [];
      return Array.from(
        schedWeekendIgnoreGrid.querySelectorAll(
          `input[type="checkbox"][data-weekend-day="${dayKey}"][data-weekend-block]`,
        ),
      );
    }

    function getWeekendDayAllToggle(dayKey) {
      if (!schedWeekendIgnoreGrid) return null;
      return schedWeekendIgnoreGrid.querySelector(
        `input[type="checkbox"][data-weekend-day-all="${dayKey}"]`,
      );
    }

    function syncWeekendDayCardState(dayKey) {
      if (!schedWeekendIgnoreGrid) return;
      const blockInputs = getWeekendDayBlockInputs(dayKey);
      const selectedBlocks = blockInputs
        .filter((inputEl) => inputEl.checked)
        .map((inputEl) =>
          Number.parseInt(String(inputEl.dataset.weekendBlock || ""), 10),
        )
        .filter(Number.isInteger)
        .sort((left, right) => left - right);
      const isFullDay = isAllScheduleBlocksSelected(selectedBlocks);
      const allToggle = getWeekendDayAllToggle(dayKey);
      const card = schedWeekendIgnoreGrid.querySelector(
        `[data-weekend-day-card="${dayKey}"]`,
      );
      const statusEl = schedWeekendIgnoreGrid.querySelector(
        `[data-weekend-day-status="${dayKey}"]`,
      );
      if (allToggle) allToggle.checked = isFullDay;
      if (card) card.classList.toggle("is-full-day", isFullDay);
      if (statusEl)
        statusEl.textContent = formatWeekendDayStatus(dayKey, selectedBlocks);
    }

    function renderWeekendIgnoreGrid() {
      if (!schedWeekendIgnoreGrid) return;
      const blocksMarkup = (dayKey) =>
        DEFAULT_SCHEDULE_BLOCKS.map(
          (block) => `
      <label class="schedule-weekend-option">
        <input type="checkbox" data-weekend-day="${esc(dayKey)}" data-weekend-block="${block.index}">
        <span class="schedule-weekend-option-copy">
          <strong>${esc(block.label)}</strong>
          <small>${esc(block.sectionRange)} · ${esc(block.blockTime)}</small>
        </span>
      </label>
    `,
        ).join("");

      schedWeekendIgnoreGrid.innerHTML = WEEKEND_IGNORE_DAY_OPTIONS.map(
        (day) => `
      <section class="schedule-weekend-day" data-weekend-day-card="${esc(day.key)}">
        <header class="schedule-weekend-day-head">
          <div class="schedule-weekend-day-title">
            <h5>${esc(day.label)}</h5>
            <span>可以整天忽略，也可以只忽略具体大节。</span>
          </div>
          <label class="checkbox-field inline schedule-weekend-all-toggle">
            <input type="checkbox" data-weekend-day-all="${esc(day.key)}">
            <span>忽略整天</span>
          </label>
        </header>
        <div class="schedule-weekend-day-status" data-weekend-day-status="${esc(day.key)}">未忽略课程，周末只要还有课就会继续停留在本周。</div>
        <div class="schedule-weekend-day-options">${blocksMarkup(day.key)}</div>
      </section>
    `,
      ).join("");
      WEEKEND_IGNORE_DAY_OPTIONS.forEach((day) => {
        syncWeekendDayCardState(day.key);
      });
    }

    function readIgnoredWeekendBlocks() {
      const normalized = { saturday: [], sunday: [] };
      if (!schedWeekendIgnoreGrid) return normalized;
      schedWeekendIgnoreGrid
        .querySelectorAll(
          'input[type="checkbox"][data-weekend-day][data-weekend-block]',
        )
        .forEach((inputEl) => {
          const day = String(inputEl.dataset.weekendDay || "").trim();
          const blockIndex = Number.parseInt(
            String(inputEl.dataset.weekendBlock || ""),
            10,
          );
          if (
            !(day in normalized) ||
            !inputEl.checked ||
            !Number.isInteger(blockIndex)
          )
            return;
          if (!normalized[day].includes(blockIndex))
            normalized[day].push(blockIndex);
        });
      normalized.saturday.sort((left, right) => left - right);
      normalized.sunday.sort((left, right) => left - right);
      return normalized;
    }

    function applyIgnoredWeekendSelections(blocks = {}) {
      if (!schedWeekendIgnoreGrid) return;
      const normalized = normalizeIgnoredWeekendBlocksValue(blocks);
      schedWeekendIgnoreGrid
        .querySelectorAll(
          'input[type="checkbox"][data-weekend-day][data-weekend-block]',
        )
        .forEach((inputEl) => {
          const day = String(inputEl.dataset.weekendDay || "").trim();
          const blockIndex = Number.parseInt(
            String(inputEl.dataset.weekendBlock || ""),
            10,
          );
          inputEl.checked = Boolean(
            day &&
            Number.isInteger(blockIndex) &&
            normalized[day] &&
            normalized[day].includes(blockIndex),
          );
        });
      WEEKEND_IGNORE_DAY_OPTIONS.forEach((day) => {
        syncWeekendDayCardState(day.key);
      });
    }

    function setWeekendIgnoreInputsDisabled(disabled) {
      if (!schedWeekendIgnoreGrid) return;
      schedWeekendIgnoreGrid
        .querySelectorAll('input[type="checkbox"]')
        .forEach((inputEl) => {
          inputEl.disabled = disabled;
        });
      schedWeekendIgnoreGrid.classList.toggle("is-disabled", disabled);
    }

    function syncAutoRefreshSettingsUI() {
      const enabled = Boolean(
        schedAutoRefreshEnabled && schedAutoRefreshEnabled.checked,
      );
      const mode = normalizeAutoRefreshModeValue(
        schedAutoRefreshMode ? schedAutoRefreshMode.value : "daily",
      );
      if (schedAutoRefreshMode) schedAutoRefreshMode.disabled = !enabled;
      if (schedAutoRefreshDailyTime)
        schedAutoRefreshDailyTime.disabled = !enabled || mode !== "daily";
      if (schedAutoRefreshIntervalHours)
        schedAutoRefreshIntervalHours.disabled =
          !enabled || mode !== "interval";
      if (schedAutoRefreshDailyField)
        schedAutoRefreshDailyField.classList.toggle("hidden", mode !== "daily");
      if (schedAutoRefreshIntervalField)
        schedAutoRefreshIntervalField.classList.toggle(
          "hidden",
          mode !== "interval",
        );
      if (schedAutoRefreshModeController)
        schedAutoRefreshModeController.refresh();
      if (schedAutoRefreshTimeController)
        schedAutoRefreshTimeController.refresh();
      if (schedAutoRefreshIntervalController)
        schedAutoRefreshIntervalController.refresh();
    }

    function syncLessonReminderSettingsUI() {
      const enabled = Boolean(
        schedLessonReminderEnabled && schedLessonReminderEnabled.checked,
      );
      if (schedLessonReminderMinutes)
        schedLessonReminderMinutes.disabled = !enabled;
      if (schedLessonReminderController)
        schedLessonReminderController.refresh();
    }

    function syncWeekendAutoSwitchUI() {
      const enabled = Boolean(
        schedWeekendAutoSwitchEnabled && schedWeekendAutoSwitchEnabled.checked,
      );
      setWeekendIgnoreInputsDisabled(!enabled);
    }

    function formatAutoRefreshSummary(
      scheduleSettings = currentSettings.schedule,
    ) {
      if (!scheduleSettings || !scheduleSettings.auto_refresh_enabled)
        return "已关闭";
      if (scheduleSettings.auto_refresh_mode === "interval") {
        return `每 ${scheduleSettings.auto_refresh_interval_hours} 小时`;
      }
      const { hour, minute } = getTimeParts(
        scheduleSettings.auto_refresh_daily_time || DEFAULT_AUTO_REFRESH_TIME,
      );
      return minute === "00" ? `每天 ${hour} 时` : `每天 ${hour}:${minute}`;
    }

    function formatWeekendAutoSwitchSummary(
      scheduleSettings = currentSettings.schedule,
    ) {
      if (!scheduleSettings || !scheduleSettings.weekend_auto_switch_enabled)
        return "已关闭";
      const blocks = normalizeIgnoredWeekendBlocksValue(
        scheduleSettings.ignored_weekend_blocks || {},
      );
      const daySummary = WEEKEND_IGNORE_DAY_OPTIONS.map((day) => {
        const selectedBlocks = blocks[day.key] || [];
        if (!selectedBlocks.length) return null;
        if (isAllScheduleBlocksSelected(selectedBlocks))
          return `${day.label}整天`;
        return `${day.label}${selectedBlocks.length}节`;
      }).filter(Boolean);
      if (!daySummary.length) return "已开启 · 未忽略课程";
      return daySummary.join(" · ");
    }

    function getSettingsPanelStates() {
      return {
        schedule: scheduleSettingsBody
          ? !scheduleSettingsBody.classList.contains("hidden")
          : false,
        login_mode: loginModeSettingsBody
          ? !loginModeSettingsBody.classList.contains("hidden")
          : false,
        appearance: appearanceSettingsBody
          ? !appearanceSettingsBody.classList.contains("hidden")
          : false,
      };
    }

    function collectCurrentSettings() {
      return normalizeSettingsPayload({
        appearance: { theme: currentPreferences.theme },
        schedule: {
          source: schedSource ? schedSource.value : "preferred",
          week_mode: schedWeekMode ? schedWeekMode.value : "current",
          manual_week: schedManualWeek ? schedManualWeek.value : "",
          cookie_rq: "",
          term_label: schedTermLabel ? schedTermLabel.value : "",
          academic_term: schedAcademicTerm ? schedAcademicTerm.value : "",
          mock_day: devMockDay ? devMockDay.value : "",
          mock_time: devMockTime ? devMockTime.value : "",
          auto_refresh_enabled: Boolean(
            schedAutoRefreshEnabled && schedAutoRefreshEnabled.checked,
          ),
          auto_refresh_mode: schedAutoRefreshMode
            ? schedAutoRefreshMode.value
            : "daily",
          auto_refresh_daily_time: schedAutoRefreshDailyTime
            ? schedAutoRefreshDailyTime.value
            : DEFAULT_AUTO_REFRESH_TIME,
          auto_refresh_interval_hours: schedAutoRefreshIntervalHours
            ? schedAutoRefreshIntervalHours.value
            : DEFAULT_AUTO_REFRESH_INTERVAL_HOURS,
          lesson_reminder_enabled: Boolean(
            schedLessonReminderEnabled && schedLessonReminderEnabled.checked,
          ),
          lesson_reminder_minutes: schedLessonReminderMinutes
            ? schedLessonReminderMinutes.value
            : 15,
          weekend_auto_switch_enabled: Boolean(
            schedWeekendAutoSwitchEnabled &&
            schedWeekendAutoSwitchEnabled.checked,
          ),
          ignored_weekend_blocks: readIgnoredWeekendBlocks(),
        },
        auth: {
          prelogin_mode: selectMode ? selectMode.value : "capture",
        },
        network:
          currentSettings && currentSettings.network
            ? currentSettings.network
            : createDefaultSettings().network,
        ui: {
          panels: getSettingsPanelStates(),
        },
      });
    }

    function setSettingsPanelExpanded(body, expanded) {
      if (!body) return;
      body.classList.toggle("hidden", !expanded);
      const trigger = document.querySelector(`[data-target="${body.id}"]`);
      if (trigger) trigger.textContent = expanded ? "收起设置" : "展开设置";
      const card = body.closest(".sidebar-settings-card");
      if (card) card.classList.toggle("collapsed", !expanded);
    }

    function applyStoredSettings(rawSettings = {}, options = {}) {
      const { persistTheme = true } = options;
      const settings = normalizeSettingsPayload(rawSettings);
      currentSettings = settings;
      isApplyingStoredSettings = true;
      try {
        if (selectMode) selectMode.value = settings.auth.prelogin_mode;
        if (schedSource) schedSource.value = normalizeScheduleSourceValue(settings.schedule.source);
        if (schedWeekMode) schedWeekMode.value = settings.schedule.week_mode;
        if (schedManualWeek)
          schedManualWeek.value = settings.schedule.manual_week;
        if (schedCookieRq) {
          schedCookieRq.value = settings.schedule.cookie_rq || "";
          schedCookieRq.disabled = false;
          schedCookieRq.placeholder = "仅作为课表日期/周次参数，不是 Cookie";
          schedCookieRq.title = "cookie_rq 只传给教务系统课表查询，不保存 Cookie、请求头或登录上下文。";
        }
        if (schedTermLabel) schedTermLabel.value = settings.schedule.term_label;
        if (schedAcademicTerm)
          schedAcademicTerm.value = settings.schedule.academic_term;
        if (devMockDay) devMockDay.value = settings.schedule.mock_day;
        if (devMockTime) devMockTime.value = settings.schedule.mock_time;
        if (schedAutoRefreshEnabled)
          schedAutoRefreshEnabled.checked =
            settings.schedule.auto_refresh_enabled;
        if (schedAutoRefreshMode)
          schedAutoRefreshMode.value = settings.schedule.auto_refresh_mode;
        if (schedAutoRefreshDailyTime)
          schedAutoRefreshDailyTime.value =
            settings.schedule.auto_refresh_daily_time;
        if (schedAutoRefreshIntervalHours)
          schedAutoRefreshIntervalHours.value = String(
            settings.schedule.auto_refresh_interval_hours,
          );
        if (schedLessonReminderEnabled)
          schedLessonReminderEnabled.checked =
            settings.schedule.lesson_reminder_enabled;
        if (schedLessonReminderMinutes)
          schedLessonReminderMinutes.value = String(
            settings.schedule.lesson_reminder_minutes,
          );
        if (schedWeekendAutoSwitchEnabled)
          schedWeekendAutoSwitchEnabled.checked =
            settings.schedule.weekend_auto_switch_enabled;
        if (networkProvider) networkProvider.value = settings.network.provider;
        if (networkHeartbeatEnabled)
          networkHeartbeatEnabled.checked = settings.network.heartbeat_enabled;
        if (networkHeartbeatInterval)
          networkHeartbeatInterval.value = String(
            settings.network.heartbeat_interval_minutes,
          );
        applyIgnoredWeekendSelections(settings.schedule.ignored_weekend_blocks);
        applyDeveloperSettingsVisibility();
        setSettingsPanelExpanded(
          scheduleSettingsBody,
          settings.ui.panels.schedule,
        );
        setSettingsPanelExpanded(
          loginModeSettingsBody,
          settings.ui.panels.login_mode,
        );
        setSettingsPanelExpanded(
          appearanceSettingsBody,
          settings.ui.panels.appearance,
        );
        applyPreferences(
          { appearance: settings.appearance },
          { persist: persistTheme },
        );
        currentSettings.appearance.theme = currentPreferences.theme;
        if (typeof syncLoginModeTrigger === "function") syncLoginModeTrigger();
        if (
          schedSourceMenuController &&
          typeof schedSourceMenuController.refresh === "function"
        )
          schedSourceMenuController.refresh();
        if (
          schedAutoRefreshModeController &&
          typeof schedAutoRefreshModeController.refresh === "function"
        )
          schedAutoRefreshModeController.refresh();
        if (
          schedAutoRefreshTimeController &&
          typeof schedAutoRefreshTimeController.refresh === "function"
        )
          schedAutoRefreshTimeController.refresh();
        if (
          schedAutoRefreshIntervalController &&
          typeof schedAutoRefreshIntervalController.refresh === "function"
        )
          schedAutoRefreshIntervalController.refresh();
        if (
          schedLessonReminderController &&
          typeof schedLessonReminderController.refresh === "function"
        )
          schedLessonReminderController.refresh();
        if (
          schedWeekModeMenuController &&
          typeof schedWeekModeMenuController.refresh === "function"
        )
          schedWeekModeMenuController.refresh();
        if (
          devMockDayMenuController &&
          typeof devMockDayMenuController.refresh === "function"
        )
          devMockDayMenuController.refresh();
        if (
          loginModeMenuController &&
          typeof loginModeMenuController.refresh === "function"
        )
          loginModeMenuController.refresh();
        if (
          networkProviderController &&
          typeof networkProviderController.refresh === "function"
        )
          networkProviderController.refresh();
        if (
          networkHeartbeatIntervalController &&
          typeof networkHeartbeatIntervalController.refresh === "function"
        )
          networkHeartbeatIntervalController.refresh();
        syncAutoRefreshSettingsUI();
        syncLessonReminderSettingsUI();
        syncWeekendAutoSwitchUI();
        syncNetworkConfigUI();
        syncLoginModeSettings();
        updateScheduleSettingsSummary();
        syncScheduleAutomationRuntime();
        syncLessonReminderRuntime();
        syncNetworkHeartbeatRuntime();
        triggerMockRefresh();
      } finally {
        isApplyingStoredSettings = false;
      }
    }

    async function persistSettingsNow(options = {}) {
      const { successMessage = "", silent = true } = options;
      if (!sessionId || isApplyingStoredSettings) return null;
      recordTelemetryModuleHit("settings");
      const payload = collectCurrentSettings();
      currentSettings = payload;
      try {
        const res = await postWithSession("/auth/preferences", payload);
        applyStoredSettings(
          res.data && res.data.settings ? res.data.settings : payload,
          { persistTheme: true },
        );
        if (successMessage) log(successMessage);
        return res;
      } catch (error) {
        if (!silent) log(`设置同步失败: ${error.message}`);
        return null;
      }
    }

    function queueSettingsSave(options = {}) {
      const { delay = 260, successMessage = "", silent = true } = options;
      if (!sessionId || isApplyingStoredSettings) return;
      if (settingsSaveTimer) clearTimeout(settingsSaveTimer);
      settingsSaveTimer = window.setTimeout(() => {
        settingsSaveTimer = null;
        persistSettingsNow({ successMessage, silent });
      }, delay);
    }

    function syncLocalSettingsDraft() {
      currentSettings = collectCurrentSettings();
      scheduleAutoWeekAttemptKey = "";
      updateScheduleSettingsSummary();
      syncScheduleAutomationRuntime();
      syncLessonReminderRuntime();
    }

    async function saveThemePreference(themeId) {
      currentSettings.appearance.theme = resolveThemeId(themeId);
      applyPreferences(
        { appearance: currentSettings.appearance },
        {
          themeAnimation: {
            animate: true,
          },
        },
      );
      if (!sessionId) {
        log(`皮肤已保存到本机: ${themeController.getActiveTheme().label}`);
        return;
      }
      const res = await persistSettingsNow({
        successMessage: `外观已保存到本地账号目录: ${themeController.getActiveTheme().label}`,
        silent: false,
      });
      if (!res) {
        log(
          `皮肤已切换，但同步到账号目录失败: ${themeController.getActiveTheme().label}`,
        );
      }
    }

    function getCurrentActiveTabId() {
      const activeTab = $(".tab.active");
      return activeTab && activeTab.dataset ? activeTab.dataset.tab : "";
    }

    function createEmptyTelemetryFeatureUsageDeltas() {
      return TELEMETRY_FEATURE_MODULE_IDS.reduce((acc, moduleId) => {
        acc[moduleId] = {
          open_count: 0,
          foreground_ms: 0,
          background_ms: 0,
        };
        return acc;
      }, {});
    }

    function resolveTelemetryModuleId(moduleHint = "", activeTab = "") {
      const normalizedHint = String(moduleHint || "").trim();
      if (TELEMETRY_FEATURE_MODULE_IDS.includes(normalizedHint)) {
        return normalizedHint;
      }
      const normalizedTab = String(activeTab || "").trim();
      return TELEMETRY_TAB_MODULE_MAP[normalizedTab] || "";
    }

    function cloneTelemetryFeatureUsagePayload() {
      return TELEMETRY_FEATURE_MODULE_IDS.reduce((acc, moduleId) => {
        const bucket =
          telemetryFeatureUsageDeltas[moduleId] &&
          typeof telemetryFeatureUsageDeltas[moduleId] === "object"
            ? telemetryFeatureUsageDeltas[moduleId]
            : {};
        const openCount = Number(bucket.open_count || 0);
        const foregroundSecondsDelta = Math.round(
          Number(bucket.foreground_ms || 0) / 1000,
        );
        const backgroundSecondsDelta = Math.round(
          Number(bucket.background_ms || 0) / 1000,
        );
        if (openCount || foregroundSecondsDelta || backgroundSecondsDelta) {
          acc[moduleId] = {
            open_count: Math.max(0, openCount),
            foreground_seconds_delta: Math.max(0, foregroundSecondsDelta),
            background_seconds_delta: Math.max(0, backgroundSecondsDelta),
          };
        }
        return acc;
      }, {});
    }

    function resetTelemetryFeatureUsageDeltas() {
      telemetryFeatureUsageDeltas = createEmptyTelemetryFeatureUsageDeltas();
    }

    function ensureTelemetryFeatureUsageBucket(moduleId) {
      if (!TELEMETRY_FEATURE_MODULE_IDS.includes(moduleId)) return null;
      if (
        !telemetryFeatureUsageDeltas ||
        typeof telemetryFeatureUsageDeltas !== "object"
      ) {
        telemetryFeatureUsageDeltas = createEmptyTelemetryFeatureUsageDeltas();
      }
      if (
        !telemetryFeatureUsageDeltas[moduleId] ||
        typeof telemetryFeatureUsageDeltas[moduleId] !== "object"
      ) {
        telemetryFeatureUsageDeltas[moduleId] = {
          open_count: 0,
          foreground_ms: 0,
          background_ms: 0,
        };
      }
      return telemetryFeatureUsageDeltas[moduleId];
    }

    function recordTelemetryModuleHit(moduleId) {
      const bucket = ensureTelemetryFeatureUsageBucket(moduleId);
      if (!bucket) return;
      bucket.open_count = Number(bucket.open_count || 0) + 1;
    }

    function setTelemetryCurrentModule(moduleId, options = {}) {
      const { countOpen = true } = options;
      const nextModuleId = resolveTelemetryModuleId(moduleId);
      if (!nextModuleId) return;
      flushTelemetryUsageBucket();
      const changed = nextModuleId !== telemetryCurrentModuleId;
      telemetryCurrentModuleId = nextModuleId;
      if (countOpen && changed) {
        recordTelemetryModuleHit(nextModuleId);
      }
    }

    function readNoticeUiState() {
      const state = getNoticeState(currentStudentId);
      return state && typeof state === "object" ? state : {};
    }

    function readSeenNoticeIds() {
      const state = readNoticeUiState();
      const seen = state && typeof state.seen === "object" ? state.seen : {};
      return seen && typeof seen === "object" ? seen : {};
    }

    function hasSeenNotice(noticeId) {
      return Boolean(readSeenNoticeIds()[noticeId]);
    }

    function markNoticeSeen(noticeId) {
      const trimmedId = String(noticeId || "").trim();
      if (!trimmedId) return;
      const state = readNoticeUiState();
      const seen =
        state && typeof state.seen === "object" ? { ...state.seen } : {};
      seen[trimmedId] = new Date().toISOString();
      saveNoticeState(currentStudentId, { ...state, seen });
    }

    function readSeenUpdateVersions() {
      const state = readNoticeUiState();
      const seen =
        state && typeof state.seen_updates === "object"
          ? state.seen_updates
          : {};
      return seen && typeof seen === "object" ? seen : {};
    }

    function hasSeenUpdateVersion(versionKey) {
      const trimmedKey = String(versionKey || "").trim();
      if (!trimmedKey) return false;
      return Boolean(readSeenUpdateVersions()[trimmedKey]);
    }

    function markUpdateVersionSeen(versionKey) {
      const trimmedKey = String(versionKey || "").trim();
      if (!trimmedKey) return;
      const state = readNoticeUiState();
      const seenUpdates =
        state && typeof state.seen_updates === "object"
          ? { ...state.seen_updates }
          : {};
      seenUpdates[trimmedKey] = new Date().toISOString();
      saveNoticeState(currentStudentId, {
        ...state,
        seen_updates: seenUpdates,
      });
    }

    function readStoredActiveNoticeId() {
      const state = readNoticeUiState();
      return String(state.active_notice_id || "").trim();
    }

    function storeActiveNoticeId(noticeId) {
      const trimmedId = String(noticeId || "").trim();
      const state = readNoticeUiState();
      saveNoticeState(currentStudentId, {
        ...state,
        active_notice_id: trimmedId,
      });
    }

    function findActiveUpdatesListItem() {
      if (!updatesTimeline || !activeUpdatesItemId) return null;
      return (
        Array.from(updatesTimeline.querySelectorAll(".updates-list-item")).find(
          (item) =>
            item &&
            item.dataset &&
            item.dataset.noticeId === activeUpdatesItemId,
        ) || null
      );
    }

    function revealActiveUpdatesContext(options = {}) {
      const { behavior = "smooth", scrollDetail = false } = options;
      const activeItem = findActiveUpdatesListItem();
      if (activeItem && typeof activeItem.scrollIntoView === "function") {
        activeItem.scrollIntoView({
          block: "nearest",
          inline: "nearest",
          behavior,
        });
      }
      const isNarrowLayout = Boolean(
        window.matchMedia && window.matchMedia("(max-width: 960px)").matches,
      );
      if (
        scrollDetail &&
        isNarrowLayout &&
        updatesDetail &&
        typeof updatesDetail.scrollIntoView === "function"
      ) {
        updatesDetail.scrollIntoView({
          block: "start",
          behavior,
        });
      }
    }

    function flushTelemetryUsageBucket(
      nextVisibleState = telemetryVisibleState,
    ) {
      const now = Date.now();
      const delta = Math.max(0, now - telemetryLastTickAt);
      if (delta > 0) {
        const moduleId = resolveTelemetryModuleId(
          telemetryCurrentModuleId,
          getCurrentActiveTabId(),
        );
        const bucket = moduleId
          ? ensureTelemetryFeatureUsageBucket(moduleId)
          : null;
        if (telemetryVisibleState) {
          telemetryAccumulatedForegroundMs += delta;
          if (bucket) {
            bucket.foreground_ms += delta;
          }
        } else {
          telemetryAccumulatedBackgroundMs += delta;
          if (bucket) {
            bucket.background_ms += delta;
          }
        }
      }
      telemetryLastTickAt = now;
      telemetryVisibleState = Boolean(nextVisibleState);
    }

    function clearTelemetryTimer() {
      if (telemetryTimer) {
        clearTimeout(telemetryTimer);
        telemetryTimer = null;
      }
    }

    function prefersReducedMotion() {
      return Boolean(
        typeof window.matchMedia === "function" &&
          window.matchMedia("(prefers-reduced-motion: reduce)").matches,
      );
    }

    function detectPerformanceLiteMode() {
      try {
        if (prefersReducedMotion()) return true;
        const memory = Number(window.navigator?.deviceMemory || 0);
        const concurrency = Number(window.navigator?.hardwareConcurrency || 0);
        if (memory > 0 && memory <= 4) return true;
        if (concurrency > 0 && concurrency <= 4) return true;
      } catch (error) {
        void error;
      }
      return false;
    }

    function isEffectivePerformanceLiteMode() {
      return (
        performanceLiteMode || runtimeProtectionMode || runtimeWorkloadSuspended
      );
    }

    function applyPerformanceMode() {
      const effectiveLiteMode = isEffectivePerformanceLiteMode();
      document.body.classList.toggle("performance-lite", effectiveLiteMode);
      document.body.classList.toggle(
        "runtime-protection",
        runtimeProtectionMode,
      );
      document.body.classList.toggle(
        "runtime-suspended",
        runtimeWorkloadSuspended,
      );
      document.documentElement.dataset.performanceMode = runtimeProtectionMode
        ? "protected"
        : effectiveLiteMode
          ? "lite"
          : "default";
    }

    function scheduleUiIdleTask(callback, delay = 0) {
      const safeDelay = Math.max(0, Number(delay) || 0);
      const runner = () => window.setTimeout(callback, safeDelay);
      if (typeof window.requestAnimationFrame === "function") {
        window.requestAnimationFrame(runner);
        return;
      }
      runner();
    }

    function clearPostAuthWarmupTimer() {
      if (postAuthWarmupTimer) {
        clearTimeout(postAuthWarmupTimer);
        postAuthWarmupTimer = null;
      }
    }

    function clearScheduleWarmupPrefetchTimer() {
      if (scheduleWarmupPrefetchTimer) {
        clearTimeout(scheduleWarmupPrefetchTimer);
        scheduleWarmupPrefetchTimer = null;
      }
    }

    function clearManualUpdatesRefreshCooldownTimer() {
      if (manualUpdatesRefreshCooldownTimer) {
        clearTimeout(manualUpdatesRefreshCooldownTimer);
        manualUpdatesRefreshCooldownTimer = null;
      }
    }

    function resolveRuntimeWindowActiveState() {
      return Boolean(
        !document.hidden &&
          document.hasFocus() &&
          desktopWindowRuntimeState.visible !== false &&
          !desktopWindowRuntimeState.minimized,
      );
    }

    function clearHealthCheckTimer() {
      if (healthCheckTimer) {
        clearTimeout(healthCheckTimer);
        healthCheckTimer = null;
      }
    }

    function resolveHealthCheckIntervalMs() {
      if (runtimeProtectionMode) return HEALTH_CHECK_PROTECTION_INTERVAL_MS;
      if (runtimeWorkloadSuspended) return HEALTH_CHECK_BACKGROUND_INTERVAL_MS;
      return HEALTH_CHECK_INTERVAL_MS;
    }

    function scheduleNextHealthCheck() {
      clearHealthCheckTimer();
      healthCheckTimer = window.setTimeout(() => {
        healthCheckTimer = null;
        void checkHealth();
      }, resolveHealthCheckIntervalMs());
    }

    function syncRuntimeWorkloadState() {
      const nextSuspended = !resolveRuntimeWindowActiveState();
      if (runtimeWorkloadSuspended === nextSuspended) {
        scheduleNextHealthCheck();
        return;
      }
      runtimeWorkloadSuspended = nextSuspended;
      applyPerformanceMode();
      if (runtimeWorkloadSuspended) {
        stopScheduleLiveTracking();
        clearScheduleWarmupPrefetchTimer();
        clearPostAuthWarmupTimer();
      } else if (latestScheduleRows.length) {
        startScheduleLiveTracking();
      }
      scheduleNextHealthCheck();
    }

    function setRuntimeProtectionMode(reason = "runtime-protection") {
      if (runtimeProtectionMode) return;
      runtimeProtectionMode = true;
      runtimeProtectionReason = String(reason || "runtime-protection").trim();
      applyPerformanceMode();
      stopScheduleLiveTracking();
      clearScheduleWarmupPrefetchTimer();
      clearPostAuthWarmupTimer();
      scheduleNextHealthCheck();
      log("已启用界面保护模式，已自动降低渲染负载。");
    }

    function clearRuntimeProtectionWatchdog() {
      if (runtimeProtectionWatchdogTimer) {
        clearInterval(runtimeProtectionWatchdogTimer);
        runtimeProtectionWatchdogTimer = null;
      }
    }

    function startRuntimeProtectionWatchdog() {
      clearRuntimeProtectionWatchdog();
      runtimeProtectionWatchdogLastTickAt = Date.now();
      runtimeProtectionLagStrikeCount = 0;
      runtimeProtectionWatchdogTimer = window.setInterval(() => {
        const now = Date.now();
        const drift =
          now -
          runtimeProtectionWatchdogLastTickAt -
          RUNTIME_PROTECTION_WATCHDOG_INTERVAL_MS;
        runtimeProtectionWatchdogLastTickAt = now;
        if (runtimeWorkloadSuspended || document.hidden) {
          runtimeProtectionLagStrikeCount = 0;
          return;
        }
        if (drift >= RUNTIME_PROTECTION_SLEEP_RESET_MS) {
          runtimeProtectionLagStrikeCount = 0;
          return;
        }
        if (drift >= RUNTIME_PROTECTION_LAG_THRESHOLD_MS) {
          runtimeProtectionLagStrikeCount += 1;
          if (runtimeProtectionLagStrikeCount >= RUNTIME_PROTECTION_LAG_STRIKE_LIMIT) {
            setRuntimeProtectionMode("event-loop-lag");
            runtimeProtectionLagStrikeCount = 0;
          }
          return;
        }
        runtimeProtectionLagStrikeCount = Math.max(
          0,
          runtimeProtectionLagStrikeCount - 1,
        );
      }, RUNTIME_PROTECTION_WATCHDOG_INTERVAL_MS);
    }

    function resolveRemoteMaintenanceIntervalMs() {
      const runtimePolicy =
        currentRemoteConfig &&
        typeof currentRemoteConfig.runtime_policy === "object"
          ? currentRemoteConfig.runtime_policy
          : {};
      const telemetryPolicy =
        currentRemoteConfig && typeof currentRemoteConfig.telemetry === "object"
          ? currentRemoteConfig.telemetry
          : {};
      const intervalSec = Number(
        runtimePolicy.config_pull_interval_sec ||
          runtimePolicy.summary_report_interval_sec ||
          telemetryPolicy.config_pull_interval_sec ||
          telemetryPolicy.summary_report_interval_sec ||
          DEFAULT_REMOTE_SUMMARY_REPORT_INTERVAL_SEC,
      );
      return Math.max(5 * 60 * 1000, intervalSec * 1000);
    }

    function syncUpdatesManualRefreshActionState() {
      if (!btnUpdatesManualRefresh) return;
      clearManualUpdatesRefreshCooldownTimer();
      const cooldownRemainingMs = Math.max(
        0,
        lastManualRemoteMaintenanceAt + 5000 - Date.now(),
      );
      const canRefresh = Boolean(sessionId || currentStudentId);
      btnUpdatesManualRefresh.disabled =
        remoteMaintenanceInFlight || cooldownRemainingMs > 0 || !canRefresh;
      if (remoteMaintenanceInFlight) {
        btnUpdatesManualRefresh.textContent = "正在拉取公告...";
      } else if (cooldownRemainingMs > 0) {
        btnUpdatesManualRefresh.textContent = "请稍候...";
        manualUpdatesRefreshCooldownTimer = window.setTimeout(() => {
          manualUpdatesRefreshCooldownTimer = null;
          syncUpdatesManualRefreshActionState();
        }, cooldownRemainingMs);
      } else {
        btnUpdatesManualRefresh.textContent = isAccessBlockedMode()
          ? "刷新公告"
          : "拉取公告";
      }
      btnUpdatesManualRefresh.title = canRefresh
        ? ""
        : "请先登录或等待可信环境恢复后再拉取公告";
    }

    function syncUpdatesFeedbackStatus(text, tone = "neutral") {
      if (!updatesFeedbackStatus) return;
      updatesFeedbackStatus.textContent = text || "";
      updatesFeedbackStatus.dataset.tone = tone || "neutral";
    }

    function setUpdatesFeedbackPanelOpen(open) {
      if (resolveRemoteBlockingState().blocked) open = false;
      updatesFeedbackPanelOpen = Boolean(open);
      if (updatesFeedbackCard) {
        updatesFeedbackCard.classList.toggle("hidden", !updatesFeedbackPanelOpen);
      }
      if (updatesContentGrid) {
        updatesContentGrid.classList.toggle(
          "is-feedback-open",
          updatesFeedbackPanelOpen,
        );
      }
      if (btnUpdatesToggleFeedback) {
        btnUpdatesToggleFeedback.textContent = updatesFeedbackPanelOpen
          ? "收起反馈"
          : "反馈";
        btnUpdatesToggleFeedback.setAttribute(
          "aria-expanded",
          updatesFeedbackPanelOpen ? "true" : "false",
        );
      }
    }

    function syncUpdatesFeedbackActionState(options = {}) {
      const { resetStatus = false } = options;
      const isBlocked = resolveRemoteBlockingState().blocked;
      const canSubmit = Boolean(sessionId) && !isBlocked;
      if (btnUpdatesSubmitFeedback) {
        btnUpdatesSubmitFeedback.disabled =
          feedbackSubmissionInFlight || !canSubmit;
        btnUpdatesSubmitFeedback.textContent = feedbackSubmissionInFlight
          ? "正在提交..."
          : "提交反馈";
        btnUpdatesSubmitFeedback.title = canSubmit ? "" : "请先登录后再提交反馈";
      }
      if (!resetStatus) return;
      if (isBlocked) {
        syncUpdatesFeedbackStatus("当前账号已被限制，暂不开放反馈", "neutral");
        return;
      }
      if (!canSubmit) {
        syncUpdatesFeedbackStatus("登录后可提交反馈", "neutral");
        return;
      }
      syncUpdatesFeedbackStatus(
        feedbackSubmissionInFlight ? "正在提交反馈..." : "你的反馈很重要",
        feedbackSubmissionInFlight ? "warning" : "neutral",
      );
    }

    function clearUpdatesFeedbackForm(options = {}) {
      const { preserveContact = true } = options;
      if (updatesFeedbackCategory) updatesFeedbackCategory.value = "bug";
      updatesFeedbackCategoryController?.refresh?.();
      if (updatesFeedbackContent) updatesFeedbackContent.value = "";
      if (!preserveContact && updatesFeedbackContact) {
        updatesFeedbackContact.value = "";
      }
    }

    async function submitUpdatesFeedback() {
      if (!sessionId) {
        syncUpdatesFeedbackStatus("请先登录后再提交反馈", "warning");
        syncUpdatesFeedbackActionState();
        return;
      }
      const category = String(
        (updatesFeedbackCategory && updatesFeedbackCategory.value) || "other",
      )
        .trim()
        .toLowerCase();
      const content = String(
        (updatesFeedbackContent && updatesFeedbackContent.value) || "",
      ).trim();
      const contact = String(
        (updatesFeedbackContact && updatesFeedbackContact.value) || "",
      ).trim();
      if (!content) {
        syncUpdatesFeedbackStatus("反馈内容不能为空", "warning");
        if (updatesFeedbackContent) updatesFeedbackContent.focus();
        return;
      }
      feedbackSubmissionInFlight = true;
      syncUpdatesFeedbackActionState({ resetStatus: true });
      try {
        const res = await postWithSession("/feedback/report", {
          category,
          content,
          contact,
          build: FRONTEND_BUILD,
          current_tab: getCurrentActiveTabId(),
        });
        clearUpdatesFeedbackForm({ preserveContact: true });
        syncUpdatesFeedbackStatus("反馈已提交，感谢你的补充。", "success");
        log(res.msg || "反馈已提交");
      } catch (error) {
        syncUpdatesFeedbackStatus(
          error && error.message ? error.message : "反馈提交失败，请稍后重试。",
          "warning",
        );
        log(`反馈提交失败: ${error.message}`);
      } finally {
        feedbackSubmissionInFlight = false;
        syncUpdatesFeedbackActionState();
      }
    }

    function scheduleNextTelemetryReport() {
      clearTelemetryTimer();
      if (!sessionId) return;
      const intervalMs = resolveRemoteMaintenanceIntervalMs();
      telemetryTimer = window.setTimeout(() => {
        telemetryTimer = null;
        void performRemoteMaintenanceSync({ trigger: "runtime", silent: true });
      }, intervalMs);
    }

    function resolveTelemetryReasonForRemoteSync(trigger = "runtime") {
      const normalizedTrigger = String(trigger || "runtime").trim().toLowerCase();
      if (normalizedTrigger === "manual") return "manual";
      if (normalizedTrigger === "login") return "login";
      if (normalizedTrigger === "startup") return "startup";
      return "heartbeat";
    }

    async function performRemoteMaintenanceSync(options = {}) {
      const { trigger = "runtime", silent = true } = options;
      const normalizedTrigger = String(trigger || "runtime").trim().toLowerCase();
      const canLoadNotice = Boolean(sessionId || currentStudentId);
      const canReportCloud = Boolean(sessionId);
      if (!canLoadNotice && !canReportCloud) {
        syncUpdatesManualRefreshActionState();
        return null;
      }
      if (remoteMaintenanceInFlight) {
        if (!silent) log("同步正在进行，请稍候。");
        syncUpdatesManualRefreshActionState();
        return null;
      }
      remoteMaintenanceInFlight = true;
      syncUpdatesManualRefreshActionState();
      try {
        const telemetryPromise = canReportCloud
          ? reportTelemetry(resolveTelemetryReasonForRemoteSync(normalizedTrigger), {
              force: true,
              silent,
              moduleHint: normalizedTrigger === "manual" ? "updates" : "",
              forceCloudUpload: normalizedTrigger === "manual",
              throwOnError: !silent,
            })
          : Promise.resolve(null);
        const noticePromise = canLoadNotice
          ? loadUpdatesFeed({
              silent,
              trigger: normalizedTrigger,
              studentId: currentStudentId,
            })
          : Promise.resolve(null);
        const [noticeResult, telemetryResult] = await Promise.allSettled([
          noticePromise,
          telemetryPromise,
        ]);
        const noticeSucceeded =
          !canLoadNotice ||
          (noticeResult.status === "fulfilled" && Boolean(noticeResult.value));
        const telemetrySucceeded =
          !canReportCloud || telemetryResult.status === "fulfilled";
        if (!silent && noticeSucceeded && telemetrySucceeded) {
          log("公告已刷新");
        }
        return {
          notice:
            noticeResult.status === "fulfilled" ? noticeResult.value : null,
          telemetry:
            telemetryResult.status === "fulfilled"
              ? telemetryResult.value
              : null,
        };
      } finally {
        remoteMaintenanceInFlight = false;
        syncUpdatesManualRefreshActionState();
      }
    }

    function buildTelemetryPayload(reason, options = {}) {
      const cloudUploadEnabled = options.cloudUploadEnabled !== false;
      const cloudSkipReason = String(options.cloudSkipReason || "").trim();
      const moduleHint = resolveTelemetryModuleId(
        options.moduleHint || "",
        getCurrentActiveTabId(),
      );
      flushTelemetryUsageBucket();
      return {
        reason,
        session_started_at: telemetrySessionStartedAt,
        foreground_seconds_delta: Math.round(
          telemetryAccumulatedForegroundMs / 1000,
        ),
        background_seconds_delta: Math.round(
          telemetryAccumulatedBackgroundMs / 1000,
        ),
        active_tab: getCurrentActiveTabId(),
        module_hint: moduleHint,
        feature_usage_deltas: cloneTelemetryFeatureUsagePayload(),
        build: FRONTEND_BUILD,
        student_id: currentStudentId || "",
        cloud_upload_enabled: cloudUploadEnabled,
        cloud_skip_reason: cloudSkipReason,
      };
    }

    async function sendTelemetryPayload(payload, { keepalive = false } = {}) {
      const response = await fetch(`${API}/telemetry/report`, {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          ...buildSessionHeaders(sessionId),
        },
        body: JSON.stringify(payload),
        keepalive,
      });
      const data = await response.json().catch(() => ({}));
      if (!response.ok) {
        const error = new Error(
          data.detail || data.msg || `HTTP ${response.status}`,
        );
        error.status = response.status;
        throw error;
      }
      return data;
    }

    async function reportTelemetry(reason, options = {}) {
      const {
        silent = true,
        force = false,
        keepalive = false,
        forceCloudUpload = false,
        throwOnError = false,
      } = options;
      if (currentBetaStatus.expired) return null;
      if (!sessionId) return null;
      const cloudTelemetryEnabled = forceCloudUpload
        ? true
        : Boolean(
            currentRemoteConfig.telemetry &&
              currentRemoteConfig.telemetry.enabled !== false,
          );
      let cloudSkipReason = "";
      if (!cloudTelemetryEnabled) {
        cloudSkipReason = "remote-disabled";
      }
      const payload = buildTelemetryPayload(reason, {
        cloudUploadEnabled: !cloudSkipReason,
        cloudSkipReason,
        moduleHint: options.moduleHint || "",
      });
      const hasUsageDelta =
        payload.foreground_seconds_delta > 0 ||
        payload.background_seconds_delta > 0;
      const hasFeatureDelta = Object.keys(payload.feature_usage_deltas || {}).length > 0;
      if (!force && !hasUsageDelta && !hasFeatureDelta && reason === "heartbeat") {
        scheduleNextTelemetryReport();
        return null;
      }
      try {
        const response = await sendTelemetryPayload(payload, { keepalive });
        telemetryAccumulatedForegroundMs = 0;
        telemetryAccumulatedBackgroundMs = 0;
        resetTelemetryFeatureUsageDeltas();
        return response;
      } catch (error) {
        if (throwOnError) throw error;
        return null;
      } finally {
        scheduleNextTelemetryReport();
      }
    }

    async function uploadTelemetryManually() {
      if (!btnAboutUploadTelemetry) return;
      if (!sessionId) {
        const message = "请先保持登录状态，然后再手动同步当前状态。";
        setAboutTelemetryUploadStatus(message, "critical");
        return;
      }
      manualTelemetryUploadInFlight = true;
      syncAboutTelemetryUploadActionState();
      setLoading(btnAboutUploadTelemetry, true);
      setAboutTelemetryUploadStatus("正在同步当前状态，请稍候…", "important");
      try {
        const response = await reportTelemetry("manual", {
          force: true,
          silent: true,
          moduleHint: "about",
          forceCloudUpload: true,
          throwOnError: true,
        });
        const payload =
          response && response.data && typeof response.data === "object"
            ? response.data
            : {};
        const cloud =
          payload.cloud && typeof payload.cloud === "object" ? payload.cloud : {};
        const reportedAt = String(payload.reported_at || "").trim();
        if (cloud.skipped) {
          const skipReason = String(cloud.reason || "").trim();
          const message =
            skipReason === "student-unresolved"
              ? "未识别到当前学号，本次同步未继续执行。"
              : skipReason === "data-empty"
                ? "当前没有需要同步的内容。"
                : "本次未继续同步。";
          setAboutTelemetryUploadStatus(message, "critical");
          return;
        }
        if (cloud.queued) {
          const message = "服务暂不可达。";
          setAboutTelemetryUploadStatus(message, "important");
          return;
        }
        const message = reportedAt
          ? `状态已同步，时间 ${reportedAt}`
          : response && response.msg
            ? response.msg
            : "状态已同步。";
        setAboutTelemetryUploadStatus(message, "success");
      } catch (error) {
        const message = `状态同步失败：${error.message || "未知错误"}`;
        setAboutTelemetryUploadStatus(message, "critical");
      } finally {
        manualTelemetryUploadInFlight = false;
        setLoading(btnAboutUploadTelemetry, false);
        syncAboutTelemetryUploadActionState();
      }
    }

    function resetTelemetryRuntime() {
      clearTelemetryTimer();
      telemetrySessionStartedAt = new Date().toISOString();
      telemetryAccumulatedForegroundMs = 0;
      telemetryAccumulatedBackgroundMs = 0;
      telemetryLastTickAt = Date.now();
      telemetryVisibleState = resolveRuntimeWindowActiveState();
      resetTelemetryFeatureUsageDeltas();
      telemetryCurrentModuleId =
        resolveTelemetryModuleId("", getCurrentActiveTabId()) || "schedule";
      recordTelemetryModuleHit(telemetryCurrentModuleId);
    }

    function startTelemetryRuntime(syncTrigger = "startup") {
      resetTelemetryRuntime();
      void performRemoteMaintenanceSync({
        trigger: syncTrigger,
        silent: true,
      });
    }

    function syncUpdatesFeedStatus(text, tone = "neutral") {
      if (!updatesFeedStatus) return;
      updatesFeedStatus.textContent = text || "";
      updatesFeedStatus.dataset.tone = tone || "neutral";
    }

    function resolveUpdatesSourceLabel(feed = {}) {
      if (!sessionId && !currentStudentId) return "登录后可用";
      const rawSource = String(
        (feed && typeof feed === "object" ? feed.source : "") ||
          currentRemoteConfig.source ||
          "",
      )
        .trim()
        .toLowerCase();
      if (!rawSource) return "未标注";
      const sourceLabels = {
        cos: "COS 云端",
        local: "本地清单",
        "local-cache": "本地缓存",
        inline: "内置配置",
        "cos-fallback": "缓存回退",
        manifest: "公告清单",
      };
      return sourceLabels[rawSource] || rawSource.toUpperCase();
    }

    function syncUpdatesFeedSummary(
      items = latestUpdatesFeed,
      seenIds = readSeenNoticeIds(),
      feed = {},
    ) {
      const normalizedItems = Array.isArray(items)
        ? items.filter((item) => item && item.id)
        : [];
      const seenMap = seenIds && typeof seenIds === "object" ? seenIds : {};
      const total = normalizedItems.length;
      const unread = normalizedItems.reduce(
        (count, item) => count + (item && item.id && !seenMap[item.id] ? 1 : 0),
        0,
      );
      if (updatesMetaTotal) updatesMetaTotal.textContent = String(total);
      if (updatesMetaUnread) updatesMetaUnread.textContent = String(unread);
      if (updatesMetaSource)
        updatesMetaSource.textContent = resolveUpdatesSourceLabel(feed);
      if (!total) {
        syncUpdatesFeedStatus(
          sessionId || currentStudentId
            ? isAccessBlockedMode()
              ? "当前仅保留公告查看"
              : "暂无公告"
            : "登录后可查看公告",
          "neutral",
        );
        return;
      }
      if (isAccessBlockedMode()) {
        syncUpdatesFeedStatus("当前仅保留公告查看", "warning");
        return;
      }
      if (unread > 0) {
        syncUpdatesFeedStatus(`${unread} 条未读`, "success");
        return;
      }
      syncUpdatesFeedStatus("全部已读", "neutral");
    }

    function resolveCurrentUpdateVersionKey(config = currentRemoteConfig) {
      const release =
        config.update_info && typeof config.update_info === "object"
          ? config.update_info
          : {};
      return String(
        config.latest_version || release.latest_version || "",
      ).trim();
    }

    function hasPendingReleaseUpdate(config = currentRemoteConfig) {
      const versionKey = resolveCurrentUpdateVersionKey(config);
      if (!versionKey) return false;
      return compareVersionTokens(FRONTEND_VERSION, versionKey) < 0;
    }

    function resolveNoticeTonePriority(tone) {
      if (tone === "critical") return 4;
      if (tone === "important") return 3;
      if (tone === "accent") return 2;
      return 1;
    }

    function resolveTopUnreadNotice(items = latestUpdatesFeed) {
      return (
        (Array.isArray(items) ? items : [])
          .filter(
            (item) =>
              item &&
              item.id &&
              !hasSeenNotice(item.id) &&
              item.kind !== "update",
          )
          .sort(
            (left, right) =>
              resolveNoticeTonePriority(resolveNoticeTone(right)) -
              resolveNoticeTonePriority(resolveNoticeTone(left)),
          )[0] || null
      );
    }

    function resolveReleaseBannerState(config = currentRemoteConfig) {
      const release =
        config.update_info && typeof config.update_info === "object"
          ? config.update_info
          : {};
      const blockedState = resolveRemoteBlockingState(config);
      const latestVersion = String(
        config.latest_version || release.latest_version || "",
      ).trim();
      const minSupportedVersion = String(
        config.min_supported_version || release.min_supported_version || "",
      ).trim();
      const downloadUrl = String(
        config.download_url || release.download_url || "",
      ).trim();
      const meta = [];
      if (latestVersion) meta.push(`最新版本 ${latestVersion}`);
      if (minSupportedVersion) meta.push(`最低支持 ${minSupportedVersion}`);

      if (blockedState.blocked) {
        const forceUpdateRequired = Boolean(
          minSupportedVersion &&
          compareVersionTokens(FRONTEND_VERSION, minSupportedVersion) < 0,
        );
        return {
          visible: true,
          tone: "critical",
          badgeLabel: "需要更新",
          eyebrow: forceUpdateRequired ? "版本过低" : "版本限制",
          title:
            blockedState.title ||
            (forceUpdateRequired
              ? "当前版本低于最低支持版本"
              : "当前版本已被限制使用"),
          body:
            blockedState.message ||
            (forceUpdateRequired
              ? `请先更新到 ${minSupportedVersion} 或更高版本后继续使用。`
              : "当前版本暂时无法继续使用，请先完成版本更新。"),
          actionLabel: downloadUrl ? "立即更新" : "",
          actionUrl: downloadUrl,
          meta,
        };
      }

      if (hasPendingReleaseUpdate(config)) {
        return {
          visible: true,
          tone: "accent",
          badgeLabel: "可升级",
          eyebrow: "发现新版本",
          title: latestVersion ? `可升级到 ${latestVersion}` : "发现可用更新",
          body: `当前版本 ${FRONTEND_VERSION}${minSupportedVersion ? `，建议保持不低于 ${minSupportedVersion}` : ""}。`,
          actionLabel: downloadUrl ? "立即更新" : "",
          actionUrl: downloadUrl,
          meta,
        };
      }

      return {
        visible: false,
        tone: "neutral",
        badgeLabel: "已是最新",
        eyebrow: "版本状态",
        title: "当前已是最新版本",
        body: "未检测到需要处理的版本更新或限制。",
        actionLabel: "",
        actionUrl: "",
        meta: [],
      };
    }

    function filterUpdatesFeedItems(items = []) {
      return (Array.isArray(items) ? items : []).filter((item) => {
        const normalized = normalizeNoticeItem(item);
        return normalized.kind !== "update";
      });
    }

    function syncUpdatesNavigationState(
      items = latestUpdatesFeed,
      config = currentRemoteConfig,
    ) {
      const unreadNotice = resolveTopUnreadNotice(items);
      const tone = unreadNotice ? resolveNoticeTone(unreadNotice) : "";
      if (updatesTabDot) {
        updatesTabDot.classList.toggle("hidden", !unreadNotice);
        updatesTabDot.dataset.tone = tone || "neutral";
      }
      if (updatesTab) {
        updatesTab.classList.toggle("has-unread-notice", Boolean(unreadNotice));
        if (tone) updatesTab.dataset.noticeTone = tone;
        else delete updatesTab.dataset.noticeTone;
      }
      if (aboutTab) {
        const versionKey = resolveCurrentUpdateVersionKey(config);
        const showUpdateAlert = Boolean(
          versionKey &&
          hasPendingReleaseUpdate(config) &&
          !hasSeenUpdateVersion(versionKey),
        );
        aboutTab.classList.toggle("has-update-alert", showUpdateAlert);
      }
    }

    function resolveUpdatesActiveItemId(
      items = latestUpdatesFeed,
      preferredId = "",
    ) {
      const preferred = String(
        preferredId || activeUpdatesItemId || readStoredActiveNoticeId() || "",
      ).trim();
      if (preferred && items.some((item) => item && item.id === preferred))
        return preferred;
      const firstUnread = items.find(
        (item) => item && item.id && !hasSeenNotice(item.id),
      );
      if (firstUnread && firstUnread.id) return firstUnread.id;
      const firstItem = items.find((item) => item && item.id);
      return firstItem ? firstItem.id : "";
    }

    function renderUpdatesCollectionHeader(feed = {}, config = {}) {
      const blockedState = resolveRemoteBlockingState(config);
      const notice =
        feed.notice && typeof feed.notice === "object"
          ? feed.notice
          : config.notice && typeof config.notice === "object"
            ? config.notice
          : {};
      const rawSubtitle = String(notice.subtitle || "").trim();
      const eyebrow = rawSubtitle && rawSubtitle.length <= 12
          ? rawSubtitle
          : "系统公告";
      if (updatesPanelEyebrow) {
        updatesPanelEyebrow.textContent = blockedState.blocked ? "访问受限" : eyebrow;
        updatesPanelEyebrow.classList.remove("hidden");
      }
      if (updatesPanelTitle) {
        updatesPanelTitle.textContent = "公告";
      }
      if (updatesPanelSubtitle) {
        updatesPanelSubtitle.textContent = blockedState.blocked
          ? "当前账号仅保留公告查看。"
          : "查看系统公告与维护通知";
      }
    }

    function bindUpdatesAutoHideScrollbar(element) {
      if (!element || element.dataset.updatesScrollbarBound === "true") return;
      element.dataset.updatesScrollbarBound = "true";

      let hideTimer = null;

      const clearHideTimer = () => {
        if (hideTimer) {
          clearTimeout(hideTimer);
          hideTimer = null;
        }
      };

      const revealScrollbar = () => {
        clearHideTimer();
        element.classList.add("is-scrollbar-visible");
      };

      const scheduleHideScrollbar = (delay = 720) => {
        clearHideTimer();
        hideTimer = window.setTimeout(() => {
          const active = document.activeElement;
          const keepVisible =
            element.matches(":hover") ||
            active === element ||
            element.contains(active);
          if (!keepVisible) element.classList.remove("is-scrollbar-visible");
        }, delay);
      };

      element.addEventListener(
        "scroll",
        () => {
          revealScrollbar();
          scheduleHideScrollbar();
        },
        { passive: true },
      );

      element.addEventListener("mouseenter", () => {
        revealScrollbar();
      });

      element.addEventListener("mouseleave", () => {
        scheduleHideScrollbar(180);
      });

      element.addEventListener("focus", revealScrollbar);
      element.addEventListener("blur", () => {
        scheduleHideScrollbar(120);
      });
    }

    function syncUpdatesScrollChrome() {
      bindUpdatesAutoHideScrollbar(updatesTimeline);
      if (!updatesDetail) return;
      const detailScroller = updatesDetail.querySelector(
        ".updates-detail-content",
      );
      bindUpdatesAutoHideScrollbar(detailScroller);
    }

    function renderUpdatesFeed(items = [], feed = {}) {
      latestUpdatesFeed = sortNoticeItems(filterUpdatesFeedItems(items));
      const headerConfig = currentRemoteConfig;
      const headerFeed =
        feed && typeof feed === "object" && feed.source
          ? feed
          : { source: currentRemoteConfig.source || "cos" };
      renderUpdatesCollectionHeader(headerFeed, headerConfig);
      syncUpdatesBlockedBanner(resolveRemoteBlockingState(headerConfig));
      syncUpdatesBlockedPresentation(resolveRemoteBlockingState(headerConfig).blocked);

      const activeId = resolveUpdatesActiveItemId(latestUpdatesFeed);
      activeUpdatesItemId = activeId;
      storeActiveNoticeId(activeId);
      const seenIds = readSeenNoticeIds();
      const activeItem =
        latestUpdatesFeed.find((item) => item && item.id === activeId) || null;

      if (updatesEmpty)
        updatesEmpty.classList.toggle("hidden", latestUpdatesFeed.length > 0);
      if (updatesTimeline) {
        updatesTimeline.classList.toggle(
          "hidden",
          latestUpdatesFeed.length === 0,
        );
        renderUpdatesList(updatesTimeline, latestUpdatesFeed, {
          activeId,
          seenIds,
        });
      }
      if (updatesDetail) {
        renderUpdatesDetail(updatesDetail, activeItem, { seenIds });
      }
      syncUpdatesScrollChrome();
      syncUpdatesFeedSummary(latestUpdatesFeed, seenIds, headerFeed);
      syncUpdatesNavigationState(latestUpdatesFeed, headerConfig);
    }

    function activateUpdatesItem(noticeId, options = {}) {
      const { markSeen = false } = options;
      const trimmedId = String(noticeId || "").trim();
      const matched =
        latestUpdatesFeed.find((item) => item && item.id === trimmedId) || null;
      if (!matched) return;

      activeUpdatesItemId = matched.id;
      storeActiveNoticeId(activeUpdatesItemId);
      if (markSeen && matched.id) markNoticeSeen(matched.id);

      const seenIds = readSeenNoticeIds();
      if (updatesTimeline) {
        renderUpdatesList(updatesTimeline, latestUpdatesFeed, {
          activeId: activeUpdatesItemId,
          seenIds,
        });
      }
      if (updatesDetail) {
        renderUpdatesDetail(updatesDetail, matched, { seenIds });
      }
      syncUpdatesScrollChrome();
      window.requestAnimationFrame(() => {
        revealActiveUpdatesContext({
          behavior: "smooth",
          scrollDetail: true,
        });
      });
      syncUpdatesFeedSummary(latestUpdatesFeed, seenIds);
      syncUpdatesNavigationState();
      if (latestReminderItemId && latestReminderItemId === matched.id) {
        hideNoticeReminderToast();
      }
    }

    function acknowledgeCurrentUpdateReminder() {
      const versionKey = resolveCurrentUpdateVersionKey();
      if (!versionKey || !hasPendingReleaseUpdate()) return;
      markUpdateVersionSeen(versionKey);
      syncUpdatesNavigationState();
    }

    function handleAboutTabActivated() {
      acknowledgeCurrentUpdateReminder();
    }

    function handleUpdatesTabActivated() {
      hideNoticeReminderToast();
      const activeId = resolveUpdatesActiveItemId(latestUpdatesFeed);
      if (!activeId) return;
      activeUpdatesItemId = activeId;
      storeActiveNoticeId(activeId);
      if (updatesTimeline) {
        renderUpdatesList(updatesTimeline, latestUpdatesFeed, {
          activeId,
          seenIds: readSeenNoticeIds(),
        });
      }
      window.requestAnimationFrame(() => {
        revealActiveUpdatesContext({ behavior: "smooth" });
      });
    }

    function hideNoticeReminderToast() {
      if (noticeReminderToast) noticeReminderToast.classList.add("hidden");
      latestReminderItemId = "";
    }

    function showNoticeReminderToast(item) {
      if (!noticeReminderToast || !item) return;
      const normalizedItem = normalizeNoticeItem(item);
      latestReminderItemId = item.id;
      if (noticeReminderBadge)
        noticeReminderBadge.textContent =
          item.kind === "maintenance" ? "维护提醒" : "公告提醒";
      if (noticeReminderTitle)
        noticeReminderTitle.textContent = item.title || "云端公告";
      if (noticeReminderBody)
        noticeReminderBody.textContent =
          normalizedItem.summary || "请查看公告面板。";
      noticeReminderToast.classList.remove("hidden");
    }

    let latestModalItemId = "";
    let noticeModalDismissed = false;

    function hideNoticeModal() {
      if (noticeModalOverlay) noticeModalOverlay.classList.add("hidden");
      latestModalItemId = "";
    }

    function showNoticeModal(item) {
      if (!noticeModalOverlay || !item) return;
      const normalizedItem = normalizeNoticeItem(item);
      hideNoticeReminderToast();
      latestModalItemId = item.id;
      noticeModalDismissed = false;
      const kindLabel =
        item.kind === "maintenance"
          ? "维护通知"
          : item.kind === "operation"
            ? "运营通知"
            : "公告";
      if (noticeModalBadge) noticeModalBadge.textContent = kindLabel;
      if (noticeModalTitle) noticeModalTitle.textContent = item.title || "公告";
      if (noticeModalBody) {
        const content =
          normalizedItem.contentMarkdown ||
          normalizedItem.content ||
          normalizedItem.summary ||
          "";
        const looksLikeMarkdown =
          normalizedItem.contentFormat === "markdown" ||
          looksLikeMarkdownContent(content);
        noticeModalBody.innerHTML = looksLikeMarkdown
          ? renderMarkdownContent(content)
          : renderPlainTextContent(content);
      }
      noticeModalOverlay.classList.remove("hidden");
      markNoticeSeen(item.id);
    }

    function maybePresentNoticeModal(items = latestUpdatesFeed) {
      if (noticeModalDismissed) return;
      if (isDevToolsVisible() && currentDevToolsState.muteNoticeReminder)
        return;
      const candidate = (Array.isArray(items) ? items : []).find((item) => {
        if (!item || item.displayMode !== "modal") return false;
        if (!item.id || hasSeenNotice(item.id)) return false;
        return true;
      });
      if (candidate) showNoticeModal(candidate);
    }

    function maybePresentNoticeReminder(items = latestUpdatesFeed) {
      hideNoticeReminderToast();
      if (isDevToolsVisible() && currentDevToolsState.muteNoticeReminder)
        return;
      if (
        noticeModalOverlay &&
        !noticeModalOverlay.classList.contains("hidden")
      )
        return;
      if (getCurrentActiveTabId() === "updates") return;
      const candidate = (Array.isArray(items) ? items : []).find((item) => {
        if (!item || item.displayMode !== "reminder") return false;
        if (!item.id || hasSeenNotice(item.id)) return false;
        return true;
      });
      if (candidate) showNoticeReminderToast(candidate);
    }

    function enterBlockedWorkspace(options = {}) {
      const {
        studentId = "",
        studentIdHash = "",
        message = "",
        title = "账号已停用",
        reason = "access-policy-blocked",
      } = options;
      if (studentId) currentStudentId = String(studentId).trim();
      currentRemoteConfig = normalizeRemoteConfigPayload({
        ...currentRemoteConfig,
        source: currentRemoteConfig.source || "cos",
        access_decision: {
          blocked: true,
          message,
          student_id_hash:
            studentIdHash ||
            (currentRemoteConfig.access_decision &&
            currentRemoteConfig.access_decision.student_id_hash
              ? currentRemoteConfig.access_decision.student_id_hash
              : ""),
          reason,
        },
        app_control: {
          ...(currentRemoteConfig.app_control || {}),
          state: "blocked",
          reason,
        },
        status_notice: {
          message:
            message || "当前账号无法继续使用业务功能，如有疑问请联系开发者。",
        },
      });
      sessionId = null;
      clearSessionId();
      resetLessonReminderState();
      syncConnectionUI(false);
      setUpdatesFeedbackPanelOpen(false);
      syncUpdatesBlockedBanner({
        blocked: true,
        title,
        message:
          message || "当前账号不能继续使用业务功能，现仅保留公告查看。",
      });
      applyRemoteConfigToUI();
      document.body.classList.remove("boot-auth-pending");
      if (loginCard) loginCard.classList.add("hidden");
      if (dashboard) dashboard.classList.remove("hidden");
      if (tabsController && updatesTab && !updatesTab.classList.contains("hidden")) {
        tabsController.activate(updatesTab);
      }
      syncUpdatesManualRefreshActionState();
      syncUpdatesFeedbackActionState({ resetStatus: true });
      syncUpdatesFeedStatus("当前账号已被限制，仍可查看公告。", "warning");
      log(
        message
          ? `访问受限：${message}`
          : "账号已停用，已切换到公告查看模式",
      );
    }

    async function exitBlockedWorkspace(options = {}) {
      const { trigger = "access-policy-removed" } = options;
      const wasBlocked = isAccessBlockedMode();
      if (!wasBlocked) return false;

      currentRemoteConfig = normalizeRemoteConfigPayload({
        ...currentRemoteConfig,
        access_decision: {
          blocked: false,
          message: "",
          student_id_hash: "",
          reason: "",
        },
      });
      applyRemoteConfigToUI();
      syncUpdatesBlockedBanner(resolveRemoteBlockingState(currentRemoteConfig));
      syncUpdatesManualRefreshActionState();
      syncUpdatesFeedbackActionState({ resetStatus: true });

      if (await attemptTrustedLoginIfPossible()) {
        log("访问限制已解除，已恢复登录状态。");
        return true;
      }

      if (dashboard) {
        dashboard.classList.add("hidden");
        dashboard.classList.remove("is-access-blocked");
      }
      if (loginCard) loginCard.classList.remove("hidden");
      document.body.classList.remove("boot-auth-pending");
      applyLoginAvailability(false);
      syncConnectionUI(false);
      showLoginEntryState();
      syncUpdatesFeedStatus("访问限制已解除，请重新登录。", "success");
      log(
        trigger === "access-policy-removed"
          ? "访问限制已解除，请重新登录。"
          : "访问限制已解除。",
      );
      return true;
    }

    async function loadUpdatesFeed(options = {}) {
      const { silent = true, trigger = "view", studentId = "" } = options;
      const resolvedStudentId = String(studentId || currentStudentId || "").trim();
      if (!sessionId && !resolvedStudentId) {
        latestUpdatesFeed = [];
        activeUpdatesItemId = "";
        latestUpdatesConfig = null;
        renderUpdatesFeed([], {});
        syncUpdatesFeedStatus("登录后可查看公告", "neutral");
        syncUpdatesFeedbackActionState({ resetStatus: true });
        hideNoticeReminderToast();
        syncUpdatesManualRefreshActionState();
        return null;
      }
      try {
        const wasBlocked = isAccessBlockedMode();
        const query = `/updates/feed?version=${encodeURIComponent(FRONTEND_VERSION)}&channel=beta&trigger=${encodeURIComponent(String(trigger || "view"))}&student_id=${encodeURIComponent(resolvedStudentId)}&_ts=${Date.now()}`;
        const res = sessionId ? await getWithSession(query) : await get(query);
        const payload = res.data || {};
        const returnedStudentId = String(payload.student_id || "").trim();
        if (returnedStudentId) currentStudentId = returnedStudentId;
        const feed =
          payload.feed && typeof payload.feed === "object" ? payload.feed : {};
        const remoteAccessPolicy =
          feed.access_policy && typeof feed.access_policy === "object"
            ? feed.access_policy
            : {};
        const remoteAccessDecision =
          feed.access_decision && typeof feed.access_decision === "object"
            ? feed.access_decision
            : {};
        latestUpdatesConfig =
          feed.config && typeof feed.config === "object" ? feed.config : null;
        currentRemoteConfig = normalizeRemoteConfigPayload({
          ...(latestUpdatesConfig || {}),
          source: feed.source || "",
          warnings: feed.warnings || [],
          access_policy: remoteAccessPolicy,
          access_decision: remoteAccessDecision,
        });
        applyRemoteConfigToUI();
        const blockingState = resolveRemoteBlockingState();
        if (blockingState.blocked && currentStudentId) {
          enterBlockedWorkspace({
            studentId: currentStudentId,
            title: blockingState.title,
            message: blockingState.message,
            reason:
              currentRemoteConfig.access_decision &&
              currentRemoteConfig.access_decision.reason
                ? currentRemoteConfig.access_decision.reason
                : "access-policy-blocked",
          });
        } else if (wasBlocked) {
          await exitBlockedWorkspace({
            trigger:
              String(trigger || "view").trim().toLowerCase() === "manual"
                ? "manual-refresh-unblocked"
                : "access-policy-removed",
          });
        }
        const items = Array.isArray(feed.items) ? feed.items : [];
        renderUpdatesFeed(items, feed);
        if (getCurrentActiveTabId() === "updates") handleUpdatesTabActivated();
        if (getCurrentActiveTabId() === "about") handleAboutTabActivated();
        maybePresentNoticeModal(latestUpdatesFeed);
        maybePresentNoticeReminder(latestUpdatesFeed);
        syncUpdatesManualRefreshActionState();
        if (!silent) log(res.msg || "公告已加载");
        return feed;
      } catch (error) {
        if (!latestUpdatesFeed.length) renderUpdatesFeed([], {});
        syncUpdatesFeedStatus(
          latestUpdatesFeed.length
            ? "暂时无法获取新公告，先查看当前内容。"
            : "暂时无法加载公告，请稍后再试。",
          "warning",
        );
        syncUpdatesManualRefreshActionState();
        if (!silent) log(`公告加载失败: ${error.message}`);
        return null;
      }
    }

    function previewCurrentReminder() {
      const reminderItem = (latestUpdatesFeed || []).find(
        (item) => item && item.displayMode === "reminder",
      );
      if (!reminderItem) {
        log("当前没有可预览的提醒内容");
        return;
      }
      showNoticeReminderToast(reminderItem);
      log(`已预览提醒弹层: ${reminderItem.title}`);
    }

    function clearFrontendCacheForDev() {
      clearFrontendClientState({
        preserveTheme: false,
        preserveDevTools: true,
      });
      latestReminderItemId = "";
      latestUpdatesFeed = [];
      activeUpdatesItemId = "";
      latestUpdatesConfig = null;
      renderUpdatesFeed([], {});
      hideNoticeReminderToast();
      restoreCachedAvatar();
      if (loginCard && !loginCard.classList.contains("hidden")) {
        showLoginEntryState();
      }
      syncUpdatesFeedStatus("公告列表已清空", "neutral");
      log("已清空前端缓存（保留开发开关）");
    }

    function hydrateAuthState(payload = {}, options = {}) {
      const { noticeTrigger = "" } = options;
      bindAvatarController();
      bindHitokotoController();
      updateStudentProfile(payload.student || null);
      applyCourseGrabRuntimeState(payload.course_grab_state || {}, {
        refreshHint: false,
      });
      applyStoredSettings(
        payload.settings ||
          payload.preferences || { prelogin_mode: payload.prelogin_mode },
        { persistTheme: true },
      );
      syncConnectionUI(payload.connected);
      void loadCourseFrameworkStatus({ silent: true });
      desktopChromeController.setPanelLabel(resolveDesktopChromePanelLabel());
      fetchHitokoto(true);
      if (noticeTrigger) {
        void loadUpdatesFeed({ silent: true, trigger: noticeTrigger });
      }
    }

    async function enterAuthenticatedWorkspace(
      authData,
      { noticeTrigger = "", syncTrigger = "" } = {},
    ) {
      const resolvedEntryUser = String(
        (authData &&
          authData.student &&
          authData.student.student_id) ||
          inputUser.value ||
          "",
      ).trim();
      if (resolvedEntryUser) {
        saveLastAccount(resolvedEntryUser);
      }
      document.body.classList.remove("boot-auth-pending");
      if (dashboard) dashboard.classList.remove("is-access-blocked");
      hydrateAuthState(authData, { noticeTrigger });
      if (chipMode)
        chipMode.textContent = formatPreloginMode(selectMode.value);
      loginCard.classList.add("hidden");
      dashboard.classList.remove("hidden");
      desktopChromeController.setPanelLabel(resolveDesktopChromePanelLabel());
      setScheduleManualWeekViewLocked(false);
      recordTelemetryModuleHit("auth");
      if (syncTrigger) startTelemetryRuntime(syncTrigger);
      schedulePostAuthWarmup(authData.cached_schedule, {
        refreshNetworkStatus: true,
      });
    }

    function resolveTrustedAccountDisplayName(account = {}) {
      return String(account.user || "").trim() || "本机账号";
    }

    function formatTrustedAccountMeta(account = {}) {
      const parts = [];
      if (account.prelogin_mode) {
        parts.push(`流程 ${formatPreloginMode(account.prelogin_mode)}`);
      }
      if (account.last_used_at) {
        parts.push(`最近使用 ${formatTimestampText(account.last_used_at)}`);
      }
      return parts.join(" · ") || "本机已保存可信登录凭据";
    }

    function syncLoginSubmitLabel() {
      const label = btnLogin
        ? btnLogin.querySelector(".login-submit-text")
        : null;
      if (!label) return;
      label.textContent = getTrustedAccounts().length
        ? "使用密码初始化 / 恢复"
        : "登录并初始化";
    }

    function setManualLoginVisibility(visible) {
      const userField = inputUser ? inputUser.closest(".field") : null;
      const passwordField = inputPassword ? inputPassword.closest(".field") : null;
      if (userField) userField.classList.toggle("hidden", !visible);
      if (passwordField) passwordField.classList.toggle("hidden", !visible);
      if (btnLogin) btnLogin.classList.toggle("hidden", !visible);
      if (btnReinitializeAuth) {
        btnReinitializeAuth.classList.toggle(
          "hidden",
          !visible || !lastIncompleteAuthUser,
        );
      }
    }

    function markLocalInitializationIncomplete(user = "", message = "") {
      lastIncompleteAuthUser = String(user || inputUser.value || getLastAccount() || "").trim();
      if (lastIncompleteAuthUser && inputUser) inputUser.value = lastIncompleteAuthUser;
      if (btnReinitializeAuth) btnReinitializeAuth.classList.toggle("hidden", !lastIncompleteAuthUser);
      if (message && loginMsg) showMsg(loginMsg, "error", message);
    }

    function clearLocalInitializationRepairState() {
      lastIncompleteAuthUser = "";
      if (btnReinitializeAuth) btnReinitializeAuth.classList.add("hidden");
    }

    function showManualLoginForm(options = {}) {
      const { prefillUser = "", preloginMode = "" } = options;
      trustedAccountUiMode = "manual";
      if (trustedAccountPanel) trustedAccountPanel.classList.add("hidden");
      setManualLoginVisibility(true);
      syncLoginSubmitLabel();
      if (prefillUser) inputUser.value = prefillUser;
      if (preloginMode) {
        selectMode.value = normalizePreloginModeValue(preloginMode);
        currentSettings.auth.prelogin_mode = normalizePreloginModeValue(
          selectMode.value,
        );
        if (typeof syncLoginModeTrigger === "function") syncLoginModeTrigger();
        syncLoginModeSettings();
      }
      if (loginCard) {
        loginCard.dataset.entryMode = "manual";
        loginCard.dataset.remember = getTrustedAccounts().length ? "true" : "false";
      }
      refreshLoginFormState();
    }

    function showTrustedAccountChooser(accounts = getTrustedAccounts()) {
      trustedAccountUiMode = "chooser";
      const normalizedAccounts = accounts.length ? accounts : getTrustedAccounts();
      const lastAccount = getLastAccount();
      if (!trustedAccountPanel || !trustedAccountList) {
        showManualLoginForm({
          prefillUser: lastAccount,
        });
        return;
      }

      setManualLoginVisibility(false);
      syncLoginSubmitLabel();
      trustedAccountPanel.classList.remove("hidden");
      trustedAccountList.innerHTML = normalizedAccounts
        .map((account) => {
          const user = String(account.user || "").trim();
          const isLastUsed = user && user === lastAccount;
          return `<article class="trusted-account-item${isLastUsed ? " is-last-used" : ""}" data-trusted-user="${esc(user)}">
            <div class="trusted-account-copy">
              <div class="trusted-account-name">
                <span>${esc(resolveTrustedAccountDisplayName(account))}</span>
                ${isLastUsed ? '<span class="trusted-account-chip">最近</span>' : ""}
              </div>
              <div class="trusted-account-meta">${esc(formatTrustedAccountMeta(account))}</div>
            </div>
            <div class="trusted-account-actions">
              <button class="btn primary small" type="button" data-trusted-action="login" data-trusted-user="${esc(user)}">进入</button>
              <button class="btn ghost small" type="button" data-trusted-action="forget" data-trusted-user="${esc(user)}">忘记</button>
            </div>
          </article>`;
        })
        .join("");
      if (trustedAccountHint) {
        trustedAccountHint.textContent =
          normalizedAccounts.length > 1
            ? "选择一个本机可信账号进入；忘记账号只删除本机 trusted entry，不影响服务器数据。"
            : "该账号已保存为本机可信账号，可直接进入。";
      }
      if (loginCard) {
        loginCard.dataset.entryMode = "trusted";
        loginCard.dataset.remember = "true";
      }
    }

    function showLoginEntryState() {
      resetLoginFormState();
      const fallback = resolveAuthBootFallbackState({
        accounts: getTrustedAccounts(),
        lastAccount: getLastAccount(),
        localInitializationIncomplete: Boolean(lastIncompleteAuthUser),
        repairUser: lastIncompleteAuthUser,
      });
      if (fallback.mode === "chooser") {
        showTrustedAccountChooser(fallback.accounts);
        return;
      }
      showManualLoginForm({
        prefillUser: fallback.prefillUser || getLastAccount(),
      });
    }

    async function handleTrustedAccountChooserClick(event) {
      const trigger = event.target.closest("[data-trusted-action]");
      if (!trigger) return;
      const action = String(trigger.dataset.trustedAction || "").trim();
      const user = String(trigger.dataset.trustedUser || "").trim();
      if (!action || !user) return;
      event.preventDefault();

      if (action === "forget") {
        removeTrustedAccount(user);
        log(`已忘记本机可信账号：${user}`);
        showLoginEntryState();
        return;
      }

      if (action === "login") {
        const account = findTrustedAccount(user);
        if (!account) {
          showLoginEntryState();
          return;
        }
        setLoading(trigger, true);
        try {
          const restored = await attemptTrustedLoginForAccount(account, {
            syncTrigger: "account-select",
          });
          if (!restored) showLoginEntryState();
        } finally {
          setLoading(trigger, false);
        }
      }
    }

    function buildTrustedAccountPayload(authData = {}, fallbackUser = "") {
      const user = String(fallbackUser || inputUser.value || "").trim();
      const trustedLoginToken = String(authData.trusted_login_token || "").trim();
      if (!user || !trustedLoginToken) return null;
      return {
        user,
        trusted_login_token: trustedLoginToken,
        prelogin_mode: String(authData.prelogin_mode || selectMode.value || "capture").trim() || "capture",
      };
    }

    function maybePromptToSaveTrustedAccount(authData = {}, fallbackUser = "") {
      const payload = buildTrustedAccountPayload(authData, fallbackUser);
      if (!payload) return;
      if (
        !shouldPromptToTrustAccount({
          user: payload.user,
          trustedLoginToken: payload.trusted_login_token,
          accounts: getTrustedAccounts(),
        })
      ) {
        touchTrustedAccount(payload.user, {
          trusted_login_token: payload.trusted_login_token,
          prelogin_mode: payload.prelogin_mode,
        });
        saveLastAccount(payload.user);
        return;
      }

      const accepted = window.confirm(
        `是否将 ${payload.user} 保存为本机可信账号？\n\n同意后，下次启动可直接进入本地工作状态，无需再次输入密码。`,
      );
      if (!accepted) return;

      upsertTrustedAccount(payload);
      saveLastAccount(payload.user);
      log(`已保存本机可信账号：${payload.user}`);
    }

    async function attemptTrustedLoginForAccount(account = {}, options = {}) {
      const trustedUser = String(account.user || "").trim();
      const trustedLoginToken = String(account.trusted_login_token || "").trim();
      const preloginMode = normalizePreloginModeValue(
        account.prelogin_mode || selectMode.value || "capture",
      );
      if (!trustedUser || !trustedLoginToken) return false;
      try {
        const res = await post("/auth/trusted-login", {
          user: trustedUser,
          prelogin_mode: preloginMode,
          trusted_login_token: trustedLoginToken,
        });
        const authData = res.data || {};
        clearLocalInitializationRepairState();
        sessionId = authData.session_id;
        saveSessionId(sessionId);
        saveLastAccount(trustedUser);
        selectMode.value = normalizePreloginModeValue(
          authData.prelogin_mode || preloginMode,
        );
        currentSettings.auth.prelogin_mode = normalizePreloginModeValue(
          selectMode.value,
        );
        if (typeof syncLoginModeTrigger === "function") syncLoginModeTrigger();
        syncLoginModeSettings();
        if (chipSession) chipSession.textContent = sessionId.slice(0, 8) + "…";
        touchTrustedAccount(trustedUser, {
          trusted_login_token: authData.trusted_login_token || trustedLoginToken,
          prelogin_mode: authData.prelogin_mode || preloginMode,
        });
        await enterAuthenticatedWorkspace(authData, {
          noticeTrigger: "view",
          syncTrigger: options.syncTrigger || "startup",
        });
        if (authData.storage_dir) log(`学生数据目录: ${authData.storage_dir}`);
        log(res.msg || "已进入可信环境");
        return true;
      } catch (error) {
        const blockedDetail = extractBlockedErrorDetail(error);
        if (blockedDetail) {
          enterBlockedWorkspace({
            studentId: blockedDetail.student_id || trustedUser,
            studentIdHash: blockedDetail.student_id_hash,
            message: blockedDetail.message,
            reason: blockedDetail.reason,
          });
          void loadUpdatesFeed({
            silent: true,
            trigger: "view",
            studentId: blockedDetail.student_id || trustedUser,
          });
          return true;
        }
        if (isLocalInitializationIncompleteError(error) && loginMsg) {
          markLocalInitializationIncomplete(trustedUser, error.message);
        }
        log(`可信环境恢复失败: ${error.message}`);
        return false;
      }
    }

    async function attemptTrustedLoginIfPossible() {
      const bootstrap = resolveTrustedBootstrap({
        sessionId: "",
        accounts: getTrustedAccounts(),
      });
      if (bootstrap.mode !== "auto" || !bootstrap.account) {
        if (bootstrap.mode === "chooser") showTrustedAccountChooser(bootstrap.accounts);
        return false;
      }
      return attemptTrustedLoginForAccount(bootstrap.account);
    }

    function isSessionExpiredError(error) {
      const status = Number(error && error.status);
      const message = String((error && error.message) || "").trim();
      return (
        status === 401 ||
        message.includes("会话不存在或已过期") ||
        message.includes("请重新登录")
      );
    }

    function isLocalInitializationIncompleteError(error) {
      const status = Number(error && error.status);
      const message = String((error && error.message) || "").trim();
      return status === 409 && message.includes("本地初始化数据不完整");
    }

    async function restoreTrustedSessionAfterExpiry(actionLabel = "当前操作") {
      sessionId = null;
      resetLessonReminderState();
      clearSessionId();
      if (await attemptTrustedLoginIfPossible()) return true;
      dashboard.classList.add("hidden");
      loginCard.classList.remove("hidden");
      syncConnectionUI(false);
      resetCourseFrameworkState({ clearInfo: false });
      showLoginEntryState();
      resetNetworkPanelState();
      log(`${actionLabel}失败：本地会话已过期，请重新登录`);
      return false;
    }

    function resolveDesktopChromePanelLabel() {
      if (dashboard && dashboard.classList.contains("hidden"))
        return "登录入口";
      const activeTab = $(".side-nav .tab.active");
      return activeTab ? activeTab.textContent : "课表";
    }

    function syncConnectionUI(connected) {
      // Keep the legacy variable name for now; it tracks mobile JW validation
      // only and must not be treated as traditional JW/captcha readiness.
      isJwConnected = Boolean(connected);
      updateConnectionUI({ chipStatus, btnConnectJw }, connected);
      desktopChromeController.setConnectionState(connected);
      desktopChromeController.setPanelLabel(resolveDesktopChromePanelLabel());
      renderCourseFrameworkState();
      syncNetworkActionState();
      syncUpdatesManualRefreshActionState();
    }

    function resolveMobileRemoteCooldownMessage(actionLabel = "移动教务请求") {
      const snapshot = mobileRemoteCooldown.snapshot();
      const seconds = Math.max(1, Math.ceil(snapshot.remaining_ms / 1000));
      const reason = snapshot.reason ? `：${snapshot.reason}` : "";
      return `${actionLabel}暂缓，上一轮移动教务请求刚刚失败${reason}。请等待约 ${seconds} 秒后再试，本地资料和课表缓存仍可使用。`;
    }

    function shouldBlockMobileRemoteAction(actionLabel, options = {}) {
      const snapshot = mobileRemoteCooldown.snapshot();
      if (!snapshot.active) return false;
      const message = resolveMobileRemoteCooldownMessage(actionLabel);
      if (!options.silent) {
        log(message);
        setScheduleInfoBar({
          summary: "移动教务暂缓请求",
          meta: message,
        });
      }
      return true;
    }

    function markMobileRemoteFailure(error) {
      const message = String((error && error.message) || error || "").trim();
      mobileRemoteCooldown.mark(message || "远端不可用");
    }

    function clearMobileRemoteFailure() {
      mobileRemoteCooldown.clear();
    }

    function updateScheduleSettingsSummary() {
      if (schedSourceQuick && schedSource) {
      const selected = schedSource.options[schedSource.selectedIndex];
      schedSourceQuick.textContent = selected ? selected.textContent : "--";
      }
      if (schedRefreshQuick) {
        schedRefreshQuick.textContent = formatAutoRefreshSummary(
          currentSettings.schedule,
        );
      }
      if (schedWeekendQuick) {
        schedWeekendQuick.textContent = formatWeekendAutoSwitchSummary(
          currentSettings.schedule,
        );
      }
      if (schedSettingsState && scheduleSettingsBody) {
        schedSettingsState.textContent =
          scheduleSettingsBody.classList.contains("hidden")
            ? "已收起"
            : "已展开";
      }
      if (btnToggleScheduleSettings && scheduleSettingsBody) {
        btnToggleScheduleSettings.textContent =
          scheduleSettingsBody.classList.contains("hidden")
            ? "展开设置"
            : "收起设置";
      }
    }

    function maskNetworkAccountText(value) {
      const text = String(value || "").trim();
      if (!text) return "--";
      if (text.length <= 3)
        return `${text[0]}${"*".repeat(Math.max(text.length - 1, 0))}`;
      if (text.length <= 7)
        return `${text.slice(0, 2)}${"*".repeat(text.length - 3)}${text.slice(-1)}`;
      return `${text.slice(0, 3)}${"*".repeat(text.length - 5)}${text.slice(-2)}`;
    }

    function formatTimestampText(value) {
      if (!value) return "--";
      const date = new Date(value);
      if (Number.isNaN(date.getTime())) return String(value).replace("T", " ");
      const pad = (item) => String(item).padStart(2, "0");
      return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`;
    }

    function formatFlowBytesText(value) {
      const bytes = Number(value);
      if (!Number.isFinite(bytes) || bytes < 0) return "--";
      const units = ["B", "KB", "MB", "GB", "TB"];
      let size = bytes;
      let unitIndex = 0;
      while (size >= 1024 && unitIndex < units.length - 1) {
        size /= 1024;
        unitIndex += 1;
      }
      const digits = size >= 100 || unitIndex === 0 ? 0 : size >= 10 ? 1 : 2;
      return `${size.toFixed(digits)} ${units[unitIndex]}`;
    }

    function setNetworkStatusPill(node, tone, text) {
      if (!node) return;
      node.classList.remove("is-ok", "is-warning", "is-danger", "is-muted");
      node.classList.add(`is-${tone || "muted"}`);
      node.textContent = text;
    }

    function syncNetworkActionState() {
      const flags = currentRemoteConfig.feature_flags || {};
      const enabled =
        Boolean(sessionId) &&
        (flags.campus_login !== false || flags.keepalive !== false);
      const canHeartbeat = Boolean(
        enabled &&
        flags.keepalive !== false &&
        lastNetworkStatus &&
        lastNetworkStatus.campus_connected &&
        !lastNetworkStatus.internet_connected,
      );
      if (btnNetworkRefresh) btnNetworkRefresh.disabled = !enabled;
      if (btnNetworkHeartbeat) {
        btnNetworkHeartbeat.disabled = !canHeartbeat;
        btnNetworkHeartbeat.title = canHeartbeat
          ? "当前可补发一次心跳"
          : "仅在已连接校园网但外网不可用时可补发心跳";
      }
      if (btnNetworkSaveConfig)
        btnNetworkSaveConfig.disabled =
          !enabled || flags.campus_login === false;
    }

    function getSelectedNetworkProvider() {
      return normalizeNetworkProviderValue(
        networkProvider
          ? networkProvider.value
          : currentSettings.network.provider,
      );
    }

    function stashNetworkProviderDraft(provider = activeNetworkDraftProvider) {
      const providerKey = normalizeNetworkProviderValue(provider);
      if (!isCarrierNetworkProvider(providerKey)) return;
      currentNetworkCredentials[providerKey] = {
        account: networkAccount ? networkAccount.value.trim() : "",
        password: networkPassword ? networkPassword.value.trim() : "",
      };
    }

    function fillNetworkProviderDraft(provider = getSelectedNetworkProvider()) {
      const providerKey = normalizeNetworkProviderValue(provider);
      if (!networkAccount || !networkPassword) return;
      if (!isCarrierNetworkProvider(providerKey)) {
        networkAccount.value = "";
        networkPassword.value = "";
        return;
      }
      const bundle = currentNetworkCredentials[providerKey] || {
        account: "",
        password: "",
      };
      networkAccount.value = bundle.account || "";
      networkPassword.value = bundle.password || "";
    }

    function syncNetworkConfigUI() {
      const settings =
        currentSettings && currentSettings.network
          ? currentSettings.network
          : createDefaultSettings().network;
      const provider = getSelectedNetworkProvider();
      const heartbeatEnabled = Boolean(
        networkHeartbeatEnabled && networkHeartbeatEnabled.checked,
      );
      const intervalMinutes = normalizeNetworkHeartbeatIntervalValue(
        networkHeartbeatInterval
          ? networkHeartbeatInterval.value
          : settings.heartbeat_interval_minutes,
      );
      const isCarrier = isCarrierNetworkProvider(provider);
      const isCampus = provider === DEFAULT_NETWORK_PROVIDER;
      const meta = getNetworkProviderMeta(provider);

      activeNetworkDraftProvider = provider;
      if (networkProvider) networkProvider.value = provider;
      if (networkHeartbeatInterval) {
        networkHeartbeatInterval.value = String(intervalMinutes);
        networkHeartbeatInterval.disabled = !heartbeatEnabled;
      }
      if (networkCampusFields)
        networkCampusFields.classList.toggle("hidden", !isCampus);
      if (networkCampusIp) {
        networkCampusIp.disabled = !isCampus;
        networkCampusIp.value = isCampus
          ? currentNetworkCampusDraft.campus_ip
          : "";
      }
      if (networkCampusSubnetPrefix) {
        networkCampusSubnetPrefix.disabled = !isCampus;
        networkCampusSubnetPrefix.value = isCampus
          ? String(currentNetworkCampusDraft.campus_subnet_prefix)
          : "24";
      }
      if (networkCampusGateway) {
        networkCampusGateway.disabled = !isCampus;
        networkCampusGateway.value = isCampus
          ? currentNetworkCampusDraft.campus_gateway
          : "";
      }
      if (networkCampusDns) {
        networkCampusDns.disabled = !isCampus;
        networkCampusDns.value = isCampus
          ? currentNetworkCampusDraft.campus_dns
          : "";
      }
      if (networkOperatorFields)
        networkOperatorFields.classList.toggle("hidden", !isCarrier);
      if (networkAccount) {
        networkAccount.disabled = !isCarrier;
        networkAccount.placeholder =
          provider === "telecom"
            ? "电信手机号 / 宽带账号"
            : provider === "unicom"
              ? "联通手机号 / 宽带账号"
              : "移动手机号 / 宽带账号";
      }
      if (networkPassword) {
        networkPassword.disabled = !isCarrier;
        networkPassword.placeholder = isCarrier ? `${meta.label}密码` : "";
      }
      if (networkAccountLabel) networkAccountLabel.textContent = "";
      if (networkPasswordLabel) networkPasswordLabel.textContent = "";
      fillNetworkProviderDraft(provider);
      if (
        networkProviderController &&
        typeof networkProviderController.refresh === "function"
      )
        networkProviderController.refresh();
      if (
        networkHeartbeatIntervalController &&
        typeof networkHeartbeatIntervalController.refresh === "function"
      )
        networkHeartbeatIntervalController.refresh();
      if (!lastNetworkStatus) {
        if (networkHeartbeatState) {
          setNetworkStatusPill(
            networkHeartbeatState,
            heartbeatEnabled ? "ok" : "muted",
            heartbeatEnabled
              ? `自动保活 · 每 ${intervalMinutes} 分钟`
              : "保活未开启",
          );
        }
        if (networkActiveAccount) {
          networkActiveAccount.textContent =
            provider === DEFAULT_NETWORK_PROVIDER
              ? currentStudentId || "--"
              : maskNetworkAccountText(
                  (currentNetworkCredentials[provider] || {}).account,
                );
        }
      }
      syncNetworkActionState();
    }

    function applyNetworkSettingsToUI(settings = currentSettings.network) {
      const normalized = normalizeSettingsPayload({
        network: settings,
      }).network;
      syncNetworkCampusDraftFromSettings(normalized);
      if (networkProvider) networkProvider.value = normalized.provider;
      if (networkHeartbeatEnabled)
        networkHeartbeatEnabled.checked = normalized.heartbeat_enabled;
      if (networkHeartbeatInterval)
        networkHeartbeatInterval.value = String(
          normalized.heartbeat_interval_minutes,
        );
      activeNetworkDraftProvider = normalized.provider;
      syncNetworkConfigUI();
    }

    function mergeNetworkSettings(settings) {
      currentSettings = normalizeSettingsPayload({
        ...currentSettings,
        network: settings || (currentSettings && currentSettings.network),
      });
    }

    function renderNetworkAttempts(status = {}) {
      const attempts = Array.isArray(status.attempts) ? status.attempts : [];
      if (!attempts.length) {
        if (!status.campus_connected) return "当前没有检测到校园网接入。";
        if (status.internet_connected)
          return "外网访问正常，当前无需补发心跳。";
        return status && status.external && status.external.detail
          ? `外网检测结果：${status.external.detail}`
          : "最近一次检测结果会显示在这里。";
      }
      return attempts
        .map(
          (item) =>
            `${maskNetworkAccountText(item.username)} · 响应 ${item.status_code || 0}`,
        )
        .join(" / ");
    }

    function updateNetworkStatusView(payload = null) {
      const status = payload && typeof payload === "object" ? payload : null;
      if (!status) {
        if (networkPortalHost) networkPortalHost.textContent = "10.199.199.199";
        if (networkLocalIp) networkLocalIp.textContent = "--";
        if (networkAuthIp) networkAuthIp.textContent = "--";
        if (networkInterfaceName) networkInterfaceName.textContent = "--";
        if (networkSubnetMask) networkSubnetMask.textContent = "--";
        if (networkGateway) networkGateway.textContent = "--";
        if (networkDnsServers) networkDnsServers.textContent = "--";
        if (networkOnlineDuration) networkOnlineDuration.textContent = "--";
        if (networkUsedFlow) networkUsedFlow.textContent = "--";
        if (networkCheckedAt) networkCheckedAt.textContent = "--";
        if (networkStatusSummary) networkStatusSummary.textContent = "";
        if (networkAttempts) networkAttempts.textContent = "";
        if (networkCampusStatus)
          setNetworkStatusPill(networkCampusStatus, "muted", "校园网未检测");
        if (networkInternetStatus)
          setNetworkStatusPill(networkInternetStatus, "muted", "外网未检测");
        syncNetworkConfigUI();
        return;
      }

      lastNetworkStatus = status;
      if (status.settings) {
        mergeNetworkSettings(status.settings);
        applyNetworkSettingsToUI(currentSettings.network);
      }
      const provider = normalizeNetworkProviderValue(
        status.provider || currentSettings.network.provider,
      );
      const heartbeatEnabled = Boolean(
        currentSettings.network.heartbeat_enabled,
      );
      const intervalMinutes = normalizeNetworkHeartbeatIntervalValue(
        currentSettings.network.heartbeat_interval_minutes,
      );
      const portal =
        status.portal && typeof status.portal === "object" ? status.portal : {};
      const interfaceInfo =
        status.interface && typeof status.interface === "object"
          ? status.interface
          : {};
      const external =
        status.external && typeof status.external === "object"
          ? status.external
          : {};
      const heartbeatText = heartbeatEnabled
        ? `自动保活 · 每 ${intervalMinutes} 分钟`
        : "保活未开启";
      const activeAccount =
        provider === DEFAULT_NETWORK_PROVIDER
          ? portal.online_user || currentStudentId || "--"
          : maskNetworkAccountText(
              (currentNetworkCredentials[provider] || {}).account,
            );

      if (networkCampusStatus) {
        setNetworkStatusPill(
          networkCampusStatus,
          status.campus_connected ? "ok" : "danger",
          status.campus_connected ? "校园网已连接" : "校园网未连接",
        );
      }
      if (networkInternetStatus) {
        setNetworkStatusPill(
          networkInternetStatus,
          status.internet_connected
            ? "ok"
            : status.campus_connected
              ? "warning"
              : "muted",
          status.internet_connected
            ? "外网可用"
            : status.campus_connected
              ? "外网不可用"
              : "等待接入",
        );
      }
      if (networkHeartbeatState) {
        setNetworkStatusPill(
          networkHeartbeatState,
          heartbeatEnabled
            ? status.needs_heartbeat
              ? "warning"
              : "ok"
            : "muted",
          heartbeatText,
        );
      }
      if (networkStatusSummary) {
        networkStatusSummary.textContent =
          status.message ||
          (status.campus_connected
            ? status.internet_connected
              ? "校园网认证正常"
              : "检测到外网不可用，可补发一次心跳"
            : "当前未接入校园网");
      }
      if (networkPortalHost)
        networkPortalHost.textContent = status.portal_host || "10.199.199.199";
      if (networkLocalIp)
        networkLocalIp.textContent =
          interfaceInfo.ipv4 || portal.local_ip || "--";
      if (networkAuthIp) networkAuthIp.textContent = portal.auth_ip || "--";
      if (networkInterfaceName)
        networkInterfaceName.textContent =
          interfaceInfo.name || interfaceInfo.description || "--";
      if (networkSubnetMask)
        networkSubnetMask.textContent = interfaceInfo.subnet_mask || "--";
      if (networkGateway)
        networkGateway.textContent = interfaceInfo.default_gateway || "--";
      if (networkDnsServers) {
        networkDnsServers.textContent =
          Array.isArray(interfaceInfo.dns_servers) &&
          interfaceInfo.dns_servers.length
            ? interfaceInfo.dns_servers.join(" / ")
            : "--";
      }
      if (networkActiveAccount)
        networkActiveAccount.textContent = activeAccount;
      if (networkOnlineDuration) {
        networkOnlineDuration.textContent = Number.isFinite(
          Number(portal.online_seconds),
        )
          ? formatSecondsCompact(Number(portal.online_seconds))
          : "--";
      }
      if (networkUsedFlow)
        networkUsedFlow.textContent = formatFlowBytesText(
          portal.used_flow_bytes,
        );
      if (networkCheckedAt)
        networkCheckedAt.textContent = formatTimestampText(status.checked_at);
      if (networkAttempts)
        networkAttempts.textContent = renderNetworkAttempts(status);
    }

    function resetNetworkPanelState() {
      networkConfigLoaded = false;
      lastNetworkStatus = null;
      currentNetworkCredentials = createEmptyNetworkCredentialBundle();
      activeNetworkDraftProvider = DEFAULT_NETWORK_PROVIDER;
      applyNetworkSettingsToUI(currentSettings.network);
      updateNetworkStatusView(null);
      syncNetworkActionState();
    }

    function clearNetworkHeartbeatTimer() {
      if (networkHeartbeatTimer) {
        clearTimeout(networkHeartbeatTimer);
        networkHeartbeatTimer = null;
      }
    }

    function scheduleNextNetworkHeartbeatRun() {
      clearNetworkHeartbeatTimer();
      if (!sessionId) return;
      const settings =
        currentSettings && currentSettings.network
          ? currentSettings.network
          : createDefaultSettings().network;
      if (!settings.heartbeat_enabled) return;
      const intervalMinutes = normalizeNetworkHeartbeatIntervalValue(
        settings.heartbeat_interval_minutes,
      );
      networkHeartbeatTimer = window.setTimeout(
        () => {
          networkHeartbeatTimer = null;
          void runScheduledNetworkHeartbeat();
        },
        intervalMinutes * 60 * 1000,
      );
    }

    async function runScheduledNetworkHeartbeat() {
      scheduleNextNetworkHeartbeatRun();
      if (!sessionId || !currentSettings.network.heartbeat_enabled) return;
      try {
        const res = await postWithSession("/network/maintain", {
          force: false,
        });
        const status = res.data || {};
        updateNetworkStatusView(status);
        if (
          status.action === "heartbeat" ||
          !status.internet_connected ||
          !status.campus_connected
        ) {
          log(status.message || "校园网保活已执行");
        }
      } catch (error) {
        log(`校园网定时保活失败: ${error.message}`);
      }
    }

    function syncNetworkHeartbeatRuntime() {
      clearNetworkHeartbeatTimer();
      if (!sessionId || !currentSettings.network.heartbeat_enabled) return;
      scheduleNextNetworkHeartbeatRun();
    }

    async function loadNetworkConfig(options = {}) {
      const { refreshStatus = false, force = false, silent = true } = options;
      if (!sessionId) {
        resetNetworkPanelState();
        return null;
      }
      if (networkConfigLoaded && !force) {
        syncNetworkActionState();
        syncNetworkHeartbeatRuntime();
        if (refreshStatus) await refreshNetworkStatus({ silent });
        return {
          settings: currentSettings.network,
          credentials: currentNetworkCredentials,
        };
      }
      try {
        const res = await getWithSession("/network/config");
        const data = res.data || {};
        mergeNetworkSettings(data.settings);
        currentNetworkCredentials = normalizeNetworkCredentialBundle(
          data.credentials,
        );
        networkConfigLoaded = true;
        activeNetworkDraftProvider = currentSettings.network.provider;
        applyNetworkSettingsToUI(currentSettings.network);
        syncNetworkHeartbeatRuntime();
        if (!silent) log(res.msg || "校园网配置已加载");
        if (refreshStatus) await refreshNetworkStatus({ silent: true });
        return data;
      } catch (error) {
        networkConfigLoaded = false;
        updateNetworkStatusView(null);
        if (!silent) log(`校园网配置加载失败: ${error.message}`);
        return null;
      }
    }

    async function refreshNetworkStatus(options = {}) {
      const { silent = false } = options;
      if (!sessionId) return null;
      try {
        const res = await getWithSession("/network/status");
        updateNetworkStatusView(res.data || {});
        if (!silent) log(res.msg || "校园网状态已刷新");
        return res.data || {};
      } catch (error) {
        if (!silent) log(`校园网状态刷新失败: ${error.message}`);
        return null;
      }
    }

    function readNetworkConfigDraft() {
      stashNetworkProviderDraft(activeNetworkDraftProvider);
      const provider = getSelectedNetworkProvider();
      const settings = {
        provider,
        heartbeat_enabled: Boolean(
          networkHeartbeatEnabled && networkHeartbeatEnabled.checked,
        ),
        heartbeat_interval_minutes: normalizeNetworkHeartbeatIntervalValue(
          networkHeartbeatInterval
            ? networkHeartbeatInterval.value
            : currentSettings.network.heartbeat_interval_minutes,
        ),
        campus_ip:
          provider === DEFAULT_NETWORK_PROVIDER && networkCampusIp
            ? networkCampusIp.value.trim()
            : currentSettings.network.campus_ip,
        campus_subnet_prefix:
          provider === DEFAULT_NETWORK_PROVIDER && networkCampusSubnetPrefix
            ? normalizeCampusSubnetPrefixValue(networkCampusSubnetPrefix.value)
            : currentSettings.network.campus_subnet_prefix,
        campus_gateway:
          provider === DEFAULT_NETWORK_PROVIDER && networkCampusGateway
            ? networkCampusGateway.value.trim()
            : currentSettings.network.campus_gateway,
        campus_dns:
          provider === DEFAULT_NETWORK_PROVIDER && networkCampusDns
            ? networkCampusDns.value.trim()
            : currentSettings.network.campus_dns,
      };
      const credentials = normalizeNetworkCredentialBundle(
        currentNetworkCredentials,
      );
      if (isCarrierNetworkProvider(provider)) {
        credentials[provider] = {
          account: networkAccount ? networkAccount.value.trim() : "",
          password: networkPassword ? networkPassword.value.trim() : "",
        };
      }
      return { settings, credentials };
    }

    function isValidIpv4Text(value) {
      const cleaned = String(value || "").trim();
      if (!cleaned) return false;
      const parts = cleaned.split(".");
      if (parts.length !== 4) return false;
      return parts.every((part) => {
        if (!/^\d+$/.test(part)) return false;
        if (part.length > 1 && part.startsWith("0")) return false;
        const numeric = Number(part);
        return Number.isInteger(numeric) && numeric >= 0 && numeric <= 255;
      });
    }

    function setNetworkPanelMessage(message) {
      if (!networkStatusSummary) return;
      networkStatusSummary.textContent = String(message || "").trim();
    }

    function validateNetworkConfigDraft(draft) {
      if (isCarrierNetworkProvider(draft.settings.provider)) {
        const carrier = draft.credentials[draft.settings.provider] || {
          account: "",
          password: "",
        };
        if (!carrier.account || !carrier.password) {
          return `请先填写${getNetworkProviderMeta(draft.settings.provider).label}账号和密码`;
        }
        return "";
      }
      if (draft.settings.campus_ip && !isValidIpv4Text(draft.settings.campus_ip))
        return "校园网 IP 地址格式不正确";
      if (
        draft.settings.campus_gateway &&
        !isValidIpv4Text(draft.settings.campus_gateway)
      )
        return "默认网关格式不正确";
      if (
        draft.settings.campus_dns &&
        !isValidIpv4Text(draft.settings.campus_dns)
      )
        return "DNS 格式不正确";
      return "";
    }

    async function saveNetworkConfig() {
      if (!sessionId) return null;
      recordTelemetryModuleHit("network");
      const draft = readNetworkConfigDraft();
      const validationMessage = validateNetworkConfigDraft(draft);
      if (validationMessage) {
        setNetworkPanelMessage(validationMessage);
        log(validationMessage);
        return null;
      }

      setLoading(btnNetworkSaveConfig, true);
      setNetworkPanelMessage("正在保存校园网配置...");
      try {
        const res = await postWithSession("/network/config", {
          provider: draft.settings.provider,
          heartbeat_enabled: draft.settings.heartbeat_enabled,
          heartbeat_interval_minutes: draft.settings.heartbeat_interval_minutes,
          campus_ip: draft.settings.campus_ip,
          campus_subnet_prefix: draft.settings.campus_subnet_prefix,
          campus_gateway: draft.settings.campus_gateway,
          campus_dns: draft.settings.campus_dns,
          telecom_account: draft.credentials.telecom.account,
          telecom_password: draft.credentials.telecom.password,
          unicom_account: draft.credentials.unicom.account,
          unicom_password: draft.credentials.unicom.password,
          mobile_account: draft.credentials.mobile.account,
          mobile_password: draft.credentials.mobile.password,
        });
        const data = res.data || {};
        mergeNetworkSettings(data.settings || draft.settings);
        currentNetworkCredentials = normalizeNetworkCredentialBundle(
          data.credentials || draft.credentials,
        );
        networkConfigLoaded = true;
        activeNetworkDraftProvider = currentSettings.network.provider;
        applyNetworkSettingsToUI(currentSettings.network);
        syncNetworkHeartbeatRuntime();
        setNetworkPanelMessage(res.msg || "校园网配置已保存");
        log(res.msg || "校园网配置已保存");
        if (data.apply_result && data.apply_result.interface_name) {
          log(
            `已应用到网卡: ${data.apply_result.interface_name} / ${data.apply_result.ip || "--"}`,
          );
        } else if (data.apply_result && data.apply_result.error) {
          log(
            data.apply_result.requires_elevation
              ? "配置已保存，但当前后端未以管理员权限运行，未实际修改本机网卡 IP"
              : `配置已保存，但本机 IP 未同步：${data.apply_result.error}`,
          );
        }
        await refreshNetworkStatus({ silent: true });
        return data;
      } catch (error) {
        setNetworkPanelMessage(`配置保存失败：${error.message}`);
        log(`校园网配置保存失败: ${error.message}`);
        return null;
      } finally {
        setLoading(btnNetworkSaveConfig, false);
      }
    }

    async function maintainNetwork(options = {}) {
      const { force = false, silent = false, source = "manual" } = options;
      if (!sessionId) return null;
      if (source !== "timer") recordTelemetryModuleHit("network");
      if (force) {
        const canHeartbeat = Boolean(
          lastNetworkStatus &&
          lastNetworkStatus.campus_connected &&
          !lastNetworkStatus.internet_connected,
        );
        if (!canHeartbeat) {
          if (!silent)
            log("当前无需补发心跳，只有校园网已连接且外网不可用时才能使用");
          syncNetworkActionState();
          return null;
        }
      }
      const button = force ? btnNetworkHeartbeat : btnNetworkRefresh;
      if (source !== "timer") setLoading(button, true);
      try {
        const res = await postWithSession("/network/maintain", { force });
        const status = res.data || {};
        updateNetworkStatusView(status);
        if (!silent) log(res.msg || status.message || "校园网检测完成");
        return status;
      } catch (error) {
        if (!silent) log(`校园网检测失败: ${error.message}`);
        return null;
      } finally {
        if (source !== "timer") setLoading(button, false);
      }
    }

    function triggerMockRefresh() {
      if (latestScheduleRows && latestScheduleRows.length > 0) {
        applyScheduleLiveState();
      }
    }
    if (devMockDay) devMockDay.addEventListener("change", triggerMockRefresh);
    if (devMockTime) devMockTime.addEventListener("input", triggerMockRefresh);

    function resetSelectedCourseState({ clearGrabState = false } = {}) {
      coursesData = [];
      courseBody.innerHTML = "";
      courseEmpty.classList.remove("hidden");
      selectedCourse.textContent = "未选中目标课程";
      grabKcid.value = "";
      grabJx0404id.value = "";
      if (clearGrabState) {
        resetCourseGrabRuntimeState();
      }
      syncCourseActionState();
    }

    // ── Connection check & config load ─────────────────────
    async function checkHealth() {
      if (healthCheckInFlight) return healthCheckInFlight;
      healthCheckInFlight = (async () => {
        try {
          const res = await fetch(`${API}/health`);
          if (res.ok) {
            const healthPayload = await res.json().catch(() => null);
            applyHealthDiagnosticsPayload(healthPayload);
            connDot.classList.add("ok");
            connText.textContent = "已连接";
          } else {
            connDot.classList.remove("ok");
            connText.textContent = "未连接";
          }
        } catch {
          connDot.classList.remove("ok");
          connText.textContent = "未连接";
        } finally {
          healthCheckInFlight = null;
          scheduleNextHealthCheck();
        }
      })();
      return healthCheckInFlight;
    }

    async function loadConfig() {
      try {
        const query = new URLSearchParams({
          version: FRONTEND_VERSION,
          channel: "beta",
          _ts: String(Date.now()),
        });
        const res = await fetch(`${API}/config?${query.toString()}`);
        if (!res.ok) {
          scheduleNextRemoteConfigPull();
          return;
        }
        const cfg = await res.json();
        currentDevelopmentConfig = normalizeDevelopmentConfigPayload(
          {
            ...currentDevelopmentConfig,
            ...(cfg.development || {}),
          },
        );
        applyDevToolsVisibility();
        applyDevToolsStateToUI();
        applyBetaStatusToUI(cfg.beta);
        currentRemoteConfig = normalizeRemoteConfigPayload(
          cfg.remote && typeof cfg.remote === "object"
            ? {
                ...(cfg.remote.config || {}),
                source: cfg.remote.source,
                warnings: cfg.remote.warnings,
              }
            : {},
        );
        applyRemoteConfigToUI();
        if (currentBetaStatus.expired) return;
        if (cfg.user && !inputUser.value) inputUser.value = cfg.user;
        if (cfg.prelogin_mode) {
          selectMode.value = cfg.prelogin_mode;
          currentSettings.auth.prelogin_mode = normalizePreloginModeValue(
            cfg.prelogin_mode,
          );
        }
        if (typeof syncLoginModeTrigger === "function") syncLoginModeTrigger();
        syncLoginModeSettings();
        if (cfg.user || cfg.prelogin_mode)
          log(`已加载默认配置：用户 ${cfg.user || "(空)"}`);
      } catch {
        scheduleNextRemoteConfigPull();
      }
    }

    function initializeTrustedAccountState() {
      const migration = migrateRememberedLoginToTrustedAccounts();
      const accounts = migration.accounts || getTrustedAccounts();
      const lastAccount = getLastAccount();
      const preferredAccount =
        accounts.find((entry) => entry.user === lastAccount) || accounts[0];
      if (preferredAccount) {
        inputUser.value = preferredAccount.user;
        selectMode.value = normalizePreloginModeValue(
          preferredAccount.prelogin_mode || selectMode.value,
        );
        currentSettings.auth.prelogin_mode = normalizePreloginModeValue(
          selectMode.value,
        );
      } else if (lastAccount) {
        inputUser.value = lastAccount;
      }
      if (typeof syncLoginModeTrigger === "function") syncLoginModeTrigger();
      syncLoginModeSettings();
      if (migration.migrated) log("已迁移旧记住项到本机可信账号列表");
      if (loginCard) {
        loginCard.dataset.remember = accounts.length ? "true" : "false";
      }
    }

    function shouldFastRestoreOnBoot() {
      const bootstrap = resolveTrustedBootstrap({
        sessionId: loadSessionId(),
        accounts: getTrustedAccounts(),
      });
      return bootstrap.mode === "session" || bootstrap.mode === "auto";
    }

    function refreshLoginFormState() {
      inputUser.dispatchEvent(new Event("input", { bubbles: true }));
      inputPassword.dispatchEvent(new Event("input", { bubbles: true }));
      inputUser.dispatchEvent(new Event("blur", { bubbles: true }));
      inputPassword.dispatchEvent(new Event("blur", { bubbles: true }));
    }

    function resetLoginFormState() {
      hideMsg(loginMsg);
      if (
        document.activeElement &&
        typeof document.activeElement.blur === "function"
      ) {
        document.activeElement.blur();
      }
      inputPassword.value = "";
      inputPassword.type = "password";
      btnPasswordVisibility.setAttribute("aria-pressed", "false");
      btnPasswordVisibility.setAttribute("aria-label", "显示密码");
      btnPasswordVisibility.setAttribute("title", "显示密码");
      if (!getTrustedAccounts().length) inputUser.value = "";
      if (loginCard) {
        delete loginCard.dataset.loginState;
        delete loginCard.dataset.loginProgress;
        delete loginCard.dataset.loginFlow;
        loginCard.dataset.remember = getTrustedAccounts().length ? "true" : "false";
      }
      if (loginHero) {
        delete loginHero.dataset.loginState;
        delete loginHero.dataset.loginProgress;
        delete loginHero.dataset.loginFlow;
        delete loginHero.dataset.remember;
      }
      refreshLoginFormState();
    }

    async function restoreSessionIfPossible() {
      if (bootAuthFlowCompleted) return;
      bootAuthFlowCompleted = true;
      restoreCachedAvatar();
      const sid = loadSessionId();
      if (!sid) {
        const bootstrap = resolveTrustedBootstrap({
          sessionId: "",
          accounts: getTrustedAccounts(),
        });
        const restored =
          bootstrap.mode === "auto" && bootstrap.account
            ? await attemptTrustedLoginForAccount(bootstrap.account)
            : false;
        if (restored) {
          document.body.classList.remove("boot-auth-pending");
          return;
        }
        showLoginEntryState();
        document.body.classList.remove("boot-auth-pending");
        dashboard.classList.add("hidden");
        loginCard.classList.remove("hidden");
        return;
      }
      try {
        const res = await getWithSession("/auth/session", sid);
        const authData = res.data || {};
        sessionId = sid;
        if (chipSession) chipSession.textContent = sid.slice(0, 8) + "…";
        await enterAuthenticatedWorkspace(authData, {
          noticeTrigger: "view",
          syncTrigger: "startup",
        });
        log("已恢复本地会话，无需重复登录");
      } catch (error) {
        const blockedDetail = extractBlockedErrorDetail(error);
        if (blockedDetail) {
          clearSessionId();
          enterBlockedWorkspace({
            studentId: blockedDetail.student_id || currentStudentId,
            studentIdHash: blockedDetail.student_id_hash,
            message: blockedDetail.message,
            reason: blockedDetail.reason,
          });
          void loadUpdatesFeed({
            silent: true,
            trigger: "view",
            studentId: blockedDetail.student_id || currentStudentId,
          });
          return;
        }
        sessionId = null;
        resetLessonReminderState();
        clearSessionId();
        if (await attemptTrustedLoginIfPossible()) return;
        document.body.classList.remove("boot-auth-pending");
        dashboard.classList.add("hidden");
        loginCard.classList.remove("hidden");
        syncConnectionUI(false);
        resetCourseFrameworkState({ clearInfo: false });
        showLoginEntryState();
        if (isLocalInitializationIncompleteError(error) && loginMsg) {
          markLocalInitializationIncomplete(getLastAccount(), error.message);
        }
        resetNetworkPanelState();
      }
    }

    // ── Login ──────────────────────────────────────────────
    async function handleLoginSubmit(event) {
      if (event && typeof event.preventDefault === "function") {
        event.preventDefault();
      }
      if (loginRequestInFlight) return;
      hideMsg(loginMsg);
      if (currentBetaStatus.expired) {
        showMsg(
          loginMsg,
          "error",
          currentBetaStatus.message || "内测已结束，当前版本已停用。",
        );
        return;
      }
      const user = inputUser.value.trim();
      const password = inputPassword.value.trim();
      if (!user || !password) {
        showMsg(loginMsg, "error", "请填写账号和密码");
        return;
      }

      loginRequestInFlight = true;
      setLoading(btnLogin, true);
      log("正在登录并初始化本地数据...");
      if (!isEffectivePerformanceLiteMode()) {
        prefetchHitokoto();
      }

      try {
        const res = await post("/auth/login", {
          user,
          password,
          prelogin_mode: selectMode.value,
        }, { timeoutMs: API_TIMEOUTS.authInitialization });
        const authData = res.data || {};
        sessionId = authData.session_id;
        saveSessionId(sessionId);
        saveLastAccount(user);
        if (chipSession) chipSession.textContent = sessionId.slice(0, 8) + "…";
        maybePromptToSaveTrustedAccount(authData, user);
        await enterAuthenticatedWorkspace(authData, {
          noticeTrigger: "view",
          syncTrigger: "login",
        });
        clearMobileRemoteFailure();
        if (authData.storage_dir) log(`学生数据目录: ${authData.storage_dir}`);
        log(
          res.msg ||
            (authData.connected
              ? "登录成功，已完成移动教务验证"
              : "登录成功，当前为离线会话（未验证移动教务）"),
        );
      } catch (e) {
        const blockedDetail = extractBlockedErrorDetail(e);
        if (blockedDetail) {
          enterBlockedWorkspace({
            studentId: blockedDetail.student_id || user,
            studentIdHash: blockedDetail.student_id_hash,
            message: blockedDetail.message,
            reason: blockedDetail.reason,
          });
          void loadUpdatesFeed({
            silent: true,
            trigger: "view",
            studentId: blockedDetail.student_id || user,
          });
          return;
        }
        sessionId = null;
        resetLessonReminderState();
        clearSessionId();
        dashboard.classList.add("hidden");
        loginCard.classList.remove("hidden");
        syncConnectionUI(false);
        resetCourseFrameworkState({ clearInfo: false });
        if (isLocalInitializationIncompleteError(e)) {
          markLocalInitializationIncomplete(user, e.message);
        } else {
          clearLocalInitializationRepairState();
          showMsg(loginMsg, "error", e.message);
        }
        if (shouldMarkRemoteFailureForAuthError(e)) {
          markMobileRemoteFailure(e);
        }
        log(`登录失败: ${e.message}`);
        resetNetworkPanelState();
      } finally {
        loginRequestInFlight = false;
        setLoading(btnLogin, false);
      }
    }

    if (loginForm) {
      loginForm.addEventListener("submit", handleLoginSubmit);
    } else {
      btnLogin.addEventListener("click", handleLoginSubmit);
    }

    if (btnReinitializeAuth) {
      btnReinitializeAuth.addEventListener("click", async () => {
        if (reinitializeRequestInFlight) return;
        hideMsg(loginMsg);
        const user = String(lastIncompleteAuthUser || inputUser.value || "").trim();
        const password = inputPassword.value.trim();
        if (!user || !password) {
          showMsg(loginMsg, "error", "请填写账号和密码后重新初始化本地数据");
          return;
        }

        const confirmed = window.confirm(
          `将重新联网初始化 ${user} 的本地资料和课表。\n\n此操作不会删除本机 trusted account，失败时会保留原有本地凭据。`,
        );
        if (!confirmed) return;

        reinitializeRequestInFlight = true;
        setLoading(btnReinitializeAuth, true);
        log("正在重新初始化本地数据...");
        try {
          const res = await post("/auth/reinitialize", {
            user,
            password,
            prelogin_mode: selectMode.value,
          }, { timeoutMs: API_TIMEOUTS.authInitialization });
          const authData = res.data || {};
          clearLocalInitializationRepairState();
          sessionId = authData.session_id;
          saveSessionId(sessionId);
          saveLastAccount(user);
          if (chipSession) chipSession.textContent = sessionId.slice(0, 8) + "…";
          maybePromptToSaveTrustedAccount(authData, user);
          await enterAuthenticatedWorkspace(authData, {
            noticeTrigger: "view",
            syncTrigger: "login",
          });
          clearMobileRemoteFailure();
          log(res.msg || "本地数据已重新初始化");
        } catch (error) {
          markLocalInitializationIncomplete(user, error.message);
          markMobileRemoteFailure(error);
          log(`重新初始化失败: ${error.message}`);
        } finally {
          reinitializeRequestInFlight = false;
          setLoading(btnReinitializeAuth, false);
        }
      });
    }

    // ── Connect / Logout ───────────────────────────────────
    btnConnectJw.addEventListener("click", async () => {
      if (!sessionId) return;
      if (connectRequestInFlight) {
        log("移动教务验证正在进行，已忽略重复点击");
        return;
      }
      if (shouldBlockMobileRemoteAction("移动教务验证")) return;
      if (isCourseGrabRunning) {
        log("已阻止重新验证移动教务：当前抢课任务仍在执行");
        renderCourseGrabStatus({
          state: "info",
          badge: "执行中",
          message: "请先停止当前抢课任务，再重新验证移动教务。",
          meta: ["操作已锁定：移动教务验证"],
        });
        return;
      }
      recordTelemetryModuleHit("auth");
      connectRequestInFlight = true;
      setLoading(btnConnectJw, true);
      log("正在验证移动教务...");
      const connectOnce = async () => {
        const res = await postWithSession("/auth/connect", {
          prelogin_mode: selectMode.value,
        });
        hydrateAuthState(res.data || {}, { noticeTrigger: "view" });
        schedulePostAuthWarmup((res.data || {}).cached_schedule, {
          refreshNetworkStatus: false,
        });
        return res;
      };
      try {
        const res = await connectOnce();
        clearMobileRemoteFailure();
        log(res.msg || "移动教务连接成功");
      } catch (e) {
        if (isSessionExpiredError(e)) {
          log("检测到会话已过期，正在尝试自动恢复本地可信会话...");
          const restored = await restoreTrustedSessionAfterExpiry("验证移动教务");
          if (restored) {
            try {
              const retryRes = await connectOnce();
              clearMobileRemoteFailure();
              log(retryRes.msg || "会话已刷新，移动教务重新连接成功");
              return;
            } catch (retryError) {
              e = retryError;
            }
          }
        }
        markMobileRemoteFailure(e);
        log(`连接失败: ${e.message}`);
        setScheduleInfoBar({
          summary: "移动教务连接失败",
          meta: `当前仍可查看本地缓存课表：${e.message}`,
        });
      } finally {
        connectRequestInFlight = false;
        setLoading(btnConnectJw, false);
        syncConnectionUI(isJwConnected);
      }
    });

    btnLogout.addEventListener("click", async () => {
      recordTelemetryModuleHit("auth");
      if (isCourseGrabRunning) {
        renderCourseGrabStatus({
          state: "info",
          badge: "停止中",
          message: "正在停止当前抢课任务并退出登录。",
          meta: ["执行状态：收尾"],
        });
        log("检测到抢课任务仍在执行，正在随注销流程一并停止");
      }
      if (sessionId) {
        await reportTelemetry("shutdown", {
          force: true,
          silent: true,
          moduleHint: "auth",
        }).catch(() => null);
      }
      if (sessionId) {
        try {
          await postWithSession("/auth/logout");
        } catch {
          /* ignore */
        }
      }
      if (settingsSaveTimer) {
        clearTimeout(settingsSaveTimer);
        settingsSaveTimer = null;
      }
      clearManualUpdatesRefreshCooldownTimer();
      clearPostAuthWarmupTimer();
      clearScheduleWarmupPrefetchTimer();
      clearScheduleAutoRefreshTimer();
      clearNetworkHeartbeatTimer();
      clearTelemetryTimer();
      clearCourseGrabRuntimeTimer();
      remoteMaintenanceInFlight = false;
      lastManualRemoteMaintenanceAt = 0;
      sessionId = null;
      resetLessonReminderState();
      clearSessionId();
      if (dashboard) dashboard.classList.remove("is-access-blocked");
      dashboard.classList.add("hidden");
      loginCard.classList.remove("hidden");
      resetCourseFrameworkState({ clearInfo: false });
      updateStudentProfile(null);
      applyPreferences({ appearance: currentSettings.appearance });
      setScheduleBoardLoading(false);
      applyDeveloperSettingsVisibility();
      latestScheduleRows = [];
      latestScheduleWeekLabel = "";
      currentScheduleWeekNumber = null;
      viewedScheduleWeekNumber = null;
      scheduleAvailableWeeks = [];
      scheduleWeekDates = {};
      schedulePayloadMemoryCache = new Map();
      schedulePrefetchInFlight = new Map();
      scheduleAutoWeekAttemptKey = "";
      setScheduleManualWeekViewLocked(false);
      stopScheduleLiveTracking();
      syncConnectionUI(false);
      syncLoginModeSettings();
      showLoginEntryState();
      currentStudentProfile = null;
      setScheduleInfoBar({
        summary: "暂无课表数据",
        meta: "登录后会优先读取本地缓存课表，并在这里标注数据源与更新时间。",
      });
      resetNetworkPanelState();
      syncUpdatesManualRefreshActionState();
      log("已注销");
    });

    document.addEventListener("visibilitychange", () => {
      flushTelemetryUsageBucket(resolveRuntimeWindowActiveState());
      syncRuntimeWorkloadState();
    });

    window.addEventListener("focus", () => {
      flushTelemetryUsageBucket(resolveRuntimeWindowActiveState());
      syncRuntimeWorkloadState();
    });

    window.addEventListener("blur", () => {
      flushTelemetryUsageBucket(resolveRuntimeWindowActiveState());
      syncRuntimeWorkloadState();
    });

    window.addEventListener("xke-window-state", (event) => {
      const detail =
        event && event.detail && typeof event.detail === "object"
          ? event.detail
          : {};
      desktopWindowRuntimeState = {
        ...desktopWindowRuntimeState,
        visible:
          detail.visible == null
            ? desktopWindowRuntimeState.visible
            : Boolean(detail.visible),
        minimized:
          detail.minimized == null
            ? desktopWindowRuntimeState.minimized
            : Boolean(detail.minimized),
        state: String(detail.state || desktopWindowRuntimeState.state || "normal"),
      };
      syncRuntimeWorkloadState();
    });

    window.addEventListener("beforeunload", () => {
      clearRuntimeProtectionWatchdog();
      clearHealthCheckTimer();
      void reportTelemetry("shutdown", {
        force: true,
        silent: true,
        keepalive: true,
        moduleHint: "auth",
      });
    });

    // ── Tabs ───────────────────────────────────────────────
    tabsController = createTabsController({
      tabs: Array.from($$(".tab")),
      panels: Array.from($$(".tab-panel")),
      resolvePanel: (tab) =>
        $(
          `#panel${tab.dataset.tab[0].toUpperCase() + tab.dataset.tab.slice(1)}`,
        ),
      onChange: (tab) => {
        desktopChromeController.setPanelLabel(tab ? tab.textContent : "工作台");
        if (tab && tab.dataset) {
          setTelemetryCurrentModule(tab.dataset.tab);
        }
        if (tab && tab.dataset && tab.dataset.tab === "network" && sessionId) {
          void loadNetworkConfig({ refreshStatus: true, silent: true });
        }
        if (tab && tab.dataset && tab.dataset.tab === "about") {
          handleAboutTabActivated();
        }
        if (tab && tab.dataset && tab.dataset.tab === "updates") {
          handleUpdatesTabActivated();
        }
      },
      onMissingPanel: (tab) => {
        log(`${tab.textContent.trim()} 模块暂未实现，当前保留导航入口`);
      },
    });
    desktopChromeController.setPanelLabel(resolveDesktopChromePanelLabel());
    tabsController.bind();

    Array.from($$(".side-nav .tab")).forEach((tab) => {
      tab.addEventListener("click", () => {
        if (!tab || !tab.dataset) return;
        if (tab.dataset.tab === "settings") {
          registerDeveloperSettingsUnlockClick();
          return;
        }
        resetDeveloperSettingsUnlockProgress();
      });
    });

    if (updatesTimeline) {
      updatesTimeline.addEventListener("click", (event) => {
        const trigger = event.target.closest("[data-notice-id]");
        if (!trigger || !trigger.dataset) return;
        activateUpdatesItem(trigger.dataset.noticeId, { markSeen: true });
      });
    }

    if (btnNoticeReminderDismiss) {
      btnNoticeReminderDismiss.addEventListener("click", () => {
        if (latestReminderItemId) markNoticeSeen(latestReminderItemId);
        hideNoticeReminderToast();
        renderUpdatesFeed(latestUpdatesFeed, {
          config: currentRemoteConfig,
          source: currentRemoteConfig.source || "local-cache",
        });
      });
    }

    if (btnNoticeReminderOpen) {
      btnNoticeReminderOpen.addEventListener("click", () => {
        const reminderId = latestReminderItemId;
        const updatesTabButton =
          updatesTab || document.querySelector('.tab[data-tab="updates"]');
        if (updatesTabButton && tabsController)
          tabsController.activate(updatesTabButton);
        if (reminderId) activateUpdatesItem(reminderId, { markSeen: true });
        hideNoticeReminderToast();
      });
    }

    if (btnNoticeModalDismiss) {
      btnNoticeModalDismiss.addEventListener("click", () => {
        noticeModalDismissed = true;
        hideNoticeModal();
        renderUpdatesFeed(latestUpdatesFeed, {
          config: currentRemoteConfig,
          source: currentRemoteConfig.source || "local-cache",
        });
      });
    }

    schedWeekMode.addEventListener("change", () => {
      if (!isDeveloperOptionsEnabled()) return;
      if (schedWeekMode.value !== "manual") schedManualWeek.value = "";
    });

    function initCollapsibleSettings() {
      document.querySelectorAll(".btn-toggle-settings").forEach((btn) => {
        const targetId = btn.dataset.target;
        const target = document.getElementById(targetId);
        if (!target) return;

        btn.addEventListener("click", () => {
          const isHidden = target.classList.toggle("hidden");
          btn.textContent = isHidden ? "展开设置" : "收起设置";
          const card = target.closest(".sidebar-settings-card");
          if (card) card.classList.toggle("collapsed", isHidden);
          syncLocalSettingsDraft();
          queueSettingsSave({ silent: true });
        });
      });
    }

    initCollapsibleSettings();

    if (desktopLaunchAtStartup) {
      desktopLaunchAtStartup.addEventListener("change", () => {
        if (isApplyingDesktopSettings) return;
        if (!desktopLaunchAtStartup.checked && desktopSilentStartup)
          desktopSilentStartup.checked = false;
        void saveDesktopSettings({
          successMessage: desktopLaunchAtStartup.checked
            ? "已开启开机自启"
            : "已关闭开机自启",
        });
      });
    }

    if (desktopSilentStartup) {
      desktopSilentStartup.addEventListener("change", () => {
        if (
          isApplyingDesktopSettings ||
          !desktopLaunchAtStartup ||
          !desktopLaunchAtStartup.checked
        )
          return;
        void saveDesktopSettings({
          successMessage: desktopSilentStartup.checked
            ? "已开启静默启动"
            : "已关闭静默启动",
        });
      });
    }

    if (desktopCloseToTray) {
      desktopCloseToTray.addEventListener("change", () => {
        if (isApplyingDesktopSettings) return;
        void saveDesktopSettings({
          successMessage: desktopCloseToTray.checked
            ? "已开启关闭到托盘"
            : "已关闭关闭到托盘",
        });
      });
    }

    const schedSourceMenuController = createSelectMenuController({
      combobox: schedSourceCombobox,
      trigger: btnSchedSourceSelect,
      menu: schedSourceMenu,
      getOptions: () => SCHEDULE_SOURCE_OPTIONS,
      getValue: () => (schedSource ? schedSource.value : "preferred"),
      getTriggerLabel: (value, options) => {
        const found = options.find((item) => item.value === value);
        return found ? found.label : "智能节点 (推荐)";
      },
      onSelect: (option) => {
        if (!schedSource) return;
        schedSource.value = normalizeScheduleSourceValue(option.value);
        schedSource.dispatchEvent(new Event("change"));
      },
      renderOptionContent: (option) =>
        `<span class="round-option-title">${esc(option.label)}</span>${option.description ? `<span class="round-option-subtitle">${esc(option.description)}</span>` : ""}`,
    });
    const schedAutoRefreshModeController = createHiddenSelectController({
      selectEl: schedAutoRefreshMode,
      combobox: schedAutoRefreshModeCombobox,
      trigger: btnSchedAutoRefreshModeSelect,
      menu: schedAutoRefreshModeMenu,
      options: SCHEDULE_AUTO_REFRESH_MODE_OPTIONS,
      placeholder: "请选择刷新方式",
    });
    const schedAutoRefreshTimeController = createTimeFieldController({
      inputEl: schedAutoRefreshDailyTime,
      hourCombobox: schedAutoRefreshDailyHourCombobox,
      hourTrigger: btnSchedAutoRefreshDailyHourSelect,
      hourMenu: schedAutoRefreshDailyHourMenu,
    });
    const schedAutoRefreshIntervalController = createHiddenSelectController({
      selectEl: schedAutoRefreshIntervalHours,
      combobox: schedAutoRefreshIntervalCombobox,
      trigger: btnSchedAutoRefreshIntervalSelect,
      menu: schedAutoRefreshIntervalMenu,
      options: SCHEDULE_AUTO_REFRESH_INTERVAL_OPTIONS.map((value) => ({
        value: String(value),
        label: `每 ${value} 小时`,
      })),
      placeholder: "请选择间隔",
    });
    const schedLessonReminderController = createHiddenSelectController({
      selectEl: schedLessonReminderMinutes,
      combobox: schedLessonReminderCombobox,
      trigger: btnSchedLessonReminderSelect,
      menu: schedLessonReminderMenu,
      options: SCHEDULE_REMINDER_MINUTE_OPTIONS.map((value) => ({
        value: String(value),
        label: `提前 ${value} 分钟`,
      })),
      placeholder: "请选择提醒时间",
    });
    const schedWeekModeMenuController = createHiddenSelectController({
      selectEl: schedWeekMode,
      combobox: schedWeekModeCombobox,
      trigger: btnSchedWeekModeSelect,
      menu: schedWeekModeMenu,
      options: SCHEDULE_WEEK_MODE_OPTIONS,
      placeholder: "请选择周次模式",
    });
    const devMockDayMenuController = createHiddenSelectController({
      selectEl: devMockDay,
      combobox: devMockDayCombobox,
      trigger: btnDevMockDaySelect,
      menu: devMockDayMenu,
      options: DEV_MOCK_DAY_OPTIONS,
      placeholder: "关闭模拟",
    });
    const networkProviderController = createHiddenSelectController({
      selectEl: networkProvider,
      combobox: networkProviderCombobox,
      trigger: btnNetworkProviderSelect,
      menu: networkProviderMenu,
      options: NETWORK_PROVIDER_OPTIONS,
      placeholder: "校园账号",
      renderOptionContent: (option) =>
        `<span class="round-option-title">${esc(option.label || option.value || "")}</span>`,
    });
    const networkHeartbeatIntervalController = createHiddenSelectController({
      selectEl: networkHeartbeatInterval,
      combobox: networkHeartbeatIntervalCombobox,
      trigger: btnNetworkHeartbeatIntervalSelect,
      menu: networkHeartbeatIntervalMenu,
      options: NETWORK_HEARTBEAT_INTERVAL_OPTIONS.map((value) => ({
        value: String(value),
        label: `每 ${value} 分钟`,
      })),
      placeholder: "每 5 分钟",
    });
    updatesFeedbackCategoryController = createHiddenSelectController({
      selectEl: updatesFeedbackCategory,
      combobox: updatesFeedbackCategoryCombobox,
      trigger: btnUpdatesFeedbackCategorySelect,
      menu: updatesFeedbackCategoryMenu,
      options: [
        { value: "bug", label: "问题" },
        { value: "suggestion", label: "建议" },
        { value: "other", label: "其他" },
      ],
      placeholder: "问题",
    });

    schedSourceMenuController.bind();
    schedSourceMenuController.refresh();
    schedAutoRefreshModeController.bind();
    schedAutoRefreshModeController.refresh();
    schedAutoRefreshTimeController.bind();
    schedAutoRefreshTimeController.refresh();
    schedAutoRefreshIntervalController.bind();
    schedAutoRefreshIntervalController.refresh();
    schedLessonReminderController.bind();
    schedLessonReminderController.refresh();
    schedWeekModeMenuController.bind();
    schedWeekModeMenuController.refresh();
    devMockDayMenuController.bind();
    devMockDayMenuController.refresh();
    networkProviderController.bind();
    networkProviderController.refresh();
    networkHeartbeatIntervalController.bind();
    networkHeartbeatIntervalController.refresh();
    updatesFeedbackCategoryController.bind();
    updatesFeedbackCategoryController.refresh();

    if (schedSource) {
      schedSource.addEventListener("change", () => {
        schedSourceMenuController.refresh();
        syncLocalSettingsDraft();
        queueSettingsSave({ silent: true });
      });
    }

    if (schedAutoRefreshEnabled) {
      schedAutoRefreshEnabled.addEventListener("change", () => {
        syncAutoRefreshSettingsUI();
        syncLocalSettingsDraft();
        queueSettingsSave({ silent: true });
        log(
          schedAutoRefreshEnabled.checked
            ? `已开启定时刷新：${formatAutoRefreshSummary(collectCurrentSettings().schedule)}`
            : "已关闭定时刷新课表",
        );
      });
    }

    if (schedAutoRefreshMode) {
      schedAutoRefreshMode.addEventListener("change", () => {
        syncAutoRefreshSettingsUI();
        syncLocalSettingsDraft();
        queueSettingsSave({ silent: true });
      });
    }

    [schedAutoRefreshDailyTime, schedAutoRefreshIntervalHours]
      .filter(Boolean)
      .forEach((element) => {
        element.addEventListener("change", () => {
          if (isApplyingStoredSettings) return;
          syncLocalSettingsDraft();
          queueSettingsSave({ silent: true });
        });
      });

    if (schedLessonReminderEnabled) {
      schedLessonReminderEnabled.addEventListener("change", () => {
        syncLessonReminderSettingsUI();
        syncLocalSettingsDraft();
        queueSettingsSave({ silent: true });
        log(
          schedLessonReminderEnabled.checked
            ? `已开启课程提醒：提前 ${normalizeLessonReminderMinutesValue(schedLessonReminderMinutes ? schedLessonReminderMinutes.value : 15)} 分钟`
            : "已关闭课程提醒",
        );
      });
    }

    if (schedLessonReminderMinutes) {
      schedLessonReminderMinutes.addEventListener("change", () => {
        if (isApplyingStoredSettings) return;
        syncLocalSettingsDraft();
        queueSettingsSave({ silent: true });
      });
    }

    if (schedWeekendAutoSwitchEnabled) {
      schedWeekendAutoSwitchEnabled.addEventListener("change", () => {
        syncWeekendAutoSwitchUI();
        syncLocalSettingsDraft();
        queueSettingsSave({ silent: true });
        log(
          schedWeekendAutoSwitchEnabled.checked
            ? "已开启周末默认跳到下周"
            : "已关闭周末默认跳到下周",
        );
      });
    }

    if (schedWeekendIgnoreGrid) {
      schedWeekendIgnoreGrid.addEventListener("change", (event) => {
        const target = event.target.closest('input[type="checkbox"]');
        if (!target || isApplyingStoredSettings) return;
        const fullDayKey = String(target.dataset.weekendDayAll || "").trim();
        if (fullDayKey) {
          getWeekendDayBlockInputs(fullDayKey).forEach((inputEl) => {
            inputEl.checked = target.checked;
          });
          syncWeekendDayCardState(fullDayKey);
          syncLocalSettingsDraft();
          queueSettingsSave({ silent: true });
          return;
        }
        const dayKey = String(target.dataset.weekendDay || "").trim();
        if (!dayKey) return;
        syncWeekendDayCardState(dayKey);
        syncLocalSettingsDraft();
        queueSettingsSave({ silent: true });
      });
    }

    if (networkProvider) {
      networkProvider.addEventListener("change", () => {
        if (isApplyingStoredSettings) return;
        stashNetworkProviderDraft(activeNetworkDraftProvider);
        activeNetworkDraftProvider = getSelectedNetworkProvider();
        syncNetworkConfigUI();
      });
    }

    if (networkHeartbeatEnabled) {
      networkHeartbeatEnabled.addEventListener("change", () => {
        if (isApplyingStoredSettings) return;
        syncNetworkConfigUI();
      });
    }

    if (networkHeartbeatInterval) {
      networkHeartbeatInterval.addEventListener("change", () => {
        if (isApplyingStoredSettings) return;
        syncNetworkConfigUI();
      });
    }

    [networkAccount, networkPassword].filter(Boolean).forEach((element) => {
      element.addEventListener("input", () => {
        if (isApplyingStoredSettings) return;
        stashNetworkProviderDraft(getSelectedNetworkProvider());
      });
    });
    [
      networkCampusIp,
      networkCampusSubnetPrefix,
      networkCampusGateway,
      networkCampusDns,
    ]
      .filter(Boolean)
      .forEach((element) => {
        const eventName = element.type === "number" ? "change" : "input";
        element.addEventListener(eventName, () => {
          if (
            isApplyingStoredSettings ||
            getSelectedNetworkProvider() !== DEFAULT_NETWORK_PROVIDER
          )
            return;
          stashNetworkCampusDraft();
        });
      });

    if (btnNetworkSaveConfig) {
      btnNetworkSaveConfig.addEventListener("click", () => {
        void saveNetworkConfig();
      });
    }

    if (btnNetworkRefresh) {
      btnNetworkRefresh.addEventListener("click", () => {
        void refreshNetworkStatus();
      });
    }

    if (btnNetworkHeartbeat) {
      btnNetworkHeartbeat.addEventListener("click", () => {
        void maintainNetwork({ force: true });
      });
    }

    [
      schedWeekMode,
      schedManualWeek,
      schedCookieRq,
      schedTermLabel,
      schedAcademicTerm,
      devMockDay,
      devMockTime,
    ]
      .filter(Boolean)
      .forEach((element) => {
        const eventName = element.tagName === "SELECT" ? "change" : "input";
        element.addEventListener(eventName, () => {
          if (isApplyingStoredSettings) return;
          syncLocalSettingsDraft();
          queueSettingsSave({ silent: true });
        });
      });

    function clearScheduleAutoRefreshTimer() {
      if (scheduleAutoRefreshTimer) {
        clearTimeout(scheduleAutoRefreshTimer);
        scheduleAutoRefreshTimer = null;
      }
    }

    function resolveViewedWeekFetchOverrides() {
      if (currentScheduleWeekNumber == null || viewedScheduleWeekNumber == null)
        return {};
      if (viewedScheduleWeekNumber === currentScheduleWeekNumber) {
        return {
          mode: "current",
          manual_week: "",
          cookie_rq: null,
        };
      }
      if (viewedScheduleWeekNumber === currentScheduleWeekNumber + 1) {
        return {
          mode: "next",
          manual_week: "",
          cookie_rq: null,
        };
      }
      return {
        mode: "manual",
        manual_week: String(viewedScheduleWeekNumber),
        cookie_rq: null,
      };
    }

    function resolveWeekLabelForNumber(weekNumber) {
      if (!Number.isInteger(weekNumber) || weekNumber <= 0) return "";
      const matched = scheduleAvailableWeeks.find(
        (item) => parseWeekNumberFromLabel(item.label) === weekNumber,
      );
      return matched && matched.label ? matched.label : `第${weekNumber}周`;
    }

    function resolveCurrentWeekTargetNumber() {
      if (currentScheduleWeekNumber != null) return currentScheduleWeekNumber;
      const selectedWeek = Array.isArray(scheduleAvailableWeeks)
        ? scheduleAvailableWeeks.find((item) => item && item.selected)
        : null;
      return parseWeekNumberFromLabel(
        selectedWeek && selectedWeek.label ? selectedWeek.label : "",
      );
    }

    function getIgnoredWeekendBlocks() {
      return normalizeIgnoredWeekendBlocksValue(
        currentSettings && currentSettings.schedule
          ? currentSettings.schedule.ignored_weekend_blocks
          : {},
      );
    }

    function shouldIgnoreWeekendRow(row, ignoredBlocks) {
      const dayKey = Object.keys(WEEKEND_DAY_INDEX_MAP).find(
        (key) => WEEKEND_DAY_INDEX_MAP[key] === row.weekdayIndex,
      );
      if (!dayKey) return false;
      const ignored =
        ignoredBlocks && Array.isArray(ignoredBlocks[dayKey])
          ? ignoredBlocks[dayKey]
          : [];
      if (!ignored.length) return false;
      const bounds = parseSectionBounds(row);
      if (!bounds) return false;
      const startBlock = mapSectionToBlock(bounds.start);
      const endBlock = mapSectionToBlock(bounds.end);
      for (
        let blockIndex = startBlock;
        blockIndex <= endBlock;
        blockIndex += 1
      ) {
        if (!ignored.includes(blockIndex)) return false;
      }
      return true;
    }

    function shouldDefaultToNextWeek(rows = latestScheduleRows) {
      if (!currentSettings.schedule.weekend_auto_switch_enabled) return false;
      if (!Array.isArray(rows) || !rows.length) return false;
      if (currentScheduleWeekNumber == null || viewedScheduleWeekNumber == null)
        return false;
      if (viewedScheduleWeekNumber !== currentScheduleWeekNumber) return false;

      const liveRows = buildLiveScheduleRows(rows);
      if (!liveRows.length) return false;

      const { todayIndex, currentMinutes } = getCurrentTimeInfo();
      const remainingRows = liveRows.filter(
        (row) =>
          row.weekdayIndex > todayIndex ||
          (row.weekdayIndex === todayIndex && row.endMinutes >= currentMinutes),
      );
      if (!remainingRows.length) return true;

      const ignoredBlocks = getIgnoredWeekendBlocks();
      return remainingRows.every((row) =>
        shouldIgnoreWeekendRow(row, ignoredBlocks),
      );
    }

    async function maybeAutoSwitchToNextWeek(options = {}) {
      const {
        allowNetwork = false,
        sourceHint = schedSource ? schedSource.value : "preferred",
      } = options;
      if (!shouldDefaultToNextWeek()) {
        scheduleAutoWeekAttemptKey = "";
        return false;
      }
      if (currentScheduleWeekNumber == null) return false;
      const nextWeekNumber = currentScheduleWeekNumber + 1;
      const targetWeekLabel = resolveWeekLabelForNumber(nextWeekNumber);
      const attemptKey = [
        currentScheduleWeekNumber,
        viewedScheduleWeekNumber,
        targetWeekLabel,
        normalizeScheduleSourceValue(sourceHint),
        JSON.stringify(getIgnoredWeekendBlocks()),
      ].join("|");
      if (scheduleAutoWeekAttemptKey === attemptKey) return false;
      scheduleAutoWeekAttemptKey = attemptKey;

      const loadedFromCache = await loadCachedSchedule({
        weekLabel: targetWeekLabel,
        sourceHint,
        allowAutoWeekSwitch: false,
        suppressStateResetOnMiss: true,
      });
      if (loadedFromCache) {
        log(`已按周末忽略规则切换到 ${targetWeekLabel}`);
        return true;
      }

      const normalizedSource = normalizeScheduleSourceValue(sourceHint);
      if (!allowNetwork || !sessionId) return false;
      const switched = await fetchSchedule({
        mode: "next",
        manual_week: "",
        cookie_rq: null,
        sourceOverride: normalizedSource,
        allowAutoWeekSwitch: false,
        silentError: true,
        bypassScheduleFetchGate: true,
      });
      if (switched) {
        log(
          `已按周末忽略规则切换到 ${resolveWeekLabelForNumber(nextWeekNumber)}`,
        );
      }
      return switched;
    }

    function scheduleNextAutoRefreshRun() {
      clearScheduleAutoRefreshTimer();
      if (!sessionId) return;
      const scheduleSettings = currentSettings.schedule || {};
      if (!scheduleSettings.auto_refresh_enabled) return;

      let delayMs = 0;
      if (scheduleSettings.auto_refresh_mode === "interval") {
        delayMs =
          normalizeAutoRefreshIntervalHoursValue(
            scheduleSettings.auto_refresh_interval_hours,
          ) *
          60 *
          60 *
          1000;
      } else {
        const [hourText, minuteText] = normalizeAutoRefreshDailyTimeValue(
          scheduleSettings.auto_refresh_daily_time,
        ).split(":");
        const now = new Date();
        const nextRun = new Date(now);
        nextRun.setHours(
          Number.parseInt(hourText, 10),
          Number.parseInt(minuteText, 10),
          0,
          0,
        );
        if (nextRun.getTime() <= now.getTime())
          nextRun.setDate(nextRun.getDate() + 1);
        delayMs = Math.max(60 * 1000, nextRun.getTime() - now.getTime());
      }

      scheduleAutoRefreshTimer = window.setTimeout(() => {
        scheduleAutoRefreshTimer = null;
        void runScheduledAutoRefresh();
      }, delayMs);
    }

    async function runScheduledAutoRefresh() {
      scheduleNextAutoRefreshRun();
      if (!sessionId) return;
      log(
        `触发定时刷新：${formatAutoRefreshSummary(currentSettings.schedule)} / 移动教务`,
      );
      await fetchSchedule({
        ...resolveViewedWeekFetchOverrides(),
        sourceOverride: "mobile",
        allowAutoWeekSwitch: true,
        silentError: false,
      });
    }

    function syncScheduleAutomationRuntime() {
      clearScheduleAutoRefreshTimer();
      if (!sessionId || !currentSettings.schedule.auto_refresh_enabled) return;
      scheduleNextAutoRefreshRun();
    }

    // ── Schedule ───────────────────────────────────────────
    function resolveScheduleRequest(overrides = {}) {
      const selection = resolveScheduleSourceSelection(
        overrides.sourceOverride ||
          (schedSource ? schedSource.value : "preferred"),
      );
      const source = selection.source;
      const endpoint = selection.endpoint;
      if (!isDeveloperOptionsEnabled()) {
        const payload = {
          mode: overrides.mode || "current",
          manual_week: overrides.manual_week || "",
          cookie_rq: null,
        };
        return { endpoint, payload, requestSource: source };
      }

      const payload = {
        mode: overrides.mode || schedWeekMode.value,
        manual_week: overrides.manual_week || schedManualWeek.value.trim(),
        cookie_rq:
          endpoint === "cookie" && schedCookieRq
            ? schedCookieRq.value.trim() || null
            : null,
        term_label: schedTermLabel.value.trim(),
        academic_term: schedAcademicTerm.value.trim(),
      };
      return {
        endpoint,
        payload,
        requestSource: source,
      };
    }

    function enrichRowsWithWarningConflicts(rows, warnings) {
      const safeRows = Array.isArray(rows) ? rows : [];
      const safeWarnings = Array.isArray(warnings) ? warnings : [];
      if (!safeRows.length || !safeWarnings.length) return safeRows;

      const roomWarnings = safeWarnings
        .filter(
          (item) => item && item.type === "duplicate_slot" && item.course_name,
        )
        .map((item) => {
          const discardedRooms = Array.isArray(item.discarded)
            ? item.discarded.map((d) => d && d.room).filter(Boolean)
            : [];
          return {
            courseName: String(item.course_name || "").trim(),
            keptRoom: String(item.kept_room || "").trim(),
            keptWeeks: String(item.kept_weeks || "").trim(),
            discardedRooms: Array.from(
              new Set(
                discardedRooms.map((r) => String(r).trim()).filter(Boolean),
              ),
            ),
          };
        })
        .filter((item) => item.courseName && item.discardedRooms.length);

      if (!roomWarnings.length) return safeRows;

      return safeRows.map((row) => {
        if (!row || row.conflict) return row;
        const rowCourse = String(row.course_name || "").trim();
        if (!rowCourse) return row;

        const matched = roomWarnings.find((item) => {
          if (item.courseName !== rowCourse) return false;
          // Prefer strict room match when warning carries kept_room.
          if (item.keptRoom)
            return item.keptRoom === String(row.room || "").trim();
          return true;
        });

        if (!matched) return row;

        return {
          ...row,
          conflict: {
            kind: "room_conflict",
            kept_room: matched.keptRoom || String(row.room || "").trim(),
            kept_weeks: matched.keptWeeks,
            discarded_rooms: matched.discardedRooms,
          },
        };
      });
    }

    function resolveScheduleLoadingText(payload = {}) {
      const mode = payload.mode || "current";
      const manualWeek = String(payload.manual_week || "").trim();
      if (manualWeek) return `正在加载第 ${manualWeek} 周课表...`;
      if (mode === "current") return "正在加载本周课表...";
      return "正在加载课表...";
    }

    function formatScheduleUpdatedAt(value) {
      const raw = String(value || "").trim();
      if (!raw) return "时间未知";
      const date = new Date(raw.includes("T") ? raw : raw.replace(" ", "T"));
      if (Number.isNaN(date.getTime())) return raw;
      const pad = (num) => String(num).padStart(2, "0");
      return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`;
    }

    function resolveScheduleSourceLabel(payload = {}) {
      const sourceLabel = String(
        payload.source_label || payload.source || "",
      ).trim();
      if (
        sourceLabel &&
        !["课表", "本地缓存课表", "统一课表"].includes(sourceLabel)
      )
        return sourceLabel;
      const sourceType = String(payload.source_type || payload.source_key || "")
        .trim()
        .toLowerCase();
      if (
        ["mobile", "mobile_schedule", "preferred", "encrypted"].includes(
          sourceType,
        )
      )
        return "移动教务课表";
      if (["jw", "jw_schedule", "cookie"].includes(sourceType))
        return "传统教务课表（后续）";
      return sourceLabel || "课表数据";
    }

    function resolveScheduleOriginLabel(payload = {}, originHint = "") {
      if (
        payload.cached === true ||
        payload.data_origin === "local_cache" ||
        originHint === "local_cache"
      )
        return "本地缓存";
      if (
        payload.cached === false ||
        payload.data_origin === "live_fetch" ||
        originHint === "live_fetch"
      )
        return "实时刷新";
      return "状态待确认";
    }

    function setScheduleInfoBar({ summary = "", meta = "" } = {}) {
      if (schedInfo) schedInfo.textContent = summary;
      if (schedDataMeta) schedDataMeta.textContent = meta;
      if (schedStatusBar)
        schedStatusBar.classList.toggle("is-empty", !summary && !meta);
    }

    function cloneSchedulePayload(payload) {
      if (!payload || typeof payload !== "object") return null;
      return JSON.parse(JSON.stringify(payload));
    }

    function buildSchedulePayloadCacheKey(sourceHint, weekLabel) {
      const normalizedSource = normalizeScheduleSourceValue(sourceHint);
      const normalizedWeekLabel = String(weekLabel || "").trim();
      const weekNumber = parseWeekNumberFromLabel(normalizedWeekLabel);
      return `${normalizedSource}|${weekNumber != null ? weekNumber : normalizedWeekLabel}`;
    }

    function rememberSchedulePayload(payload, sourceHint = "preferred") {
      const cloned = cloneSchedulePayload(payload);
      if (!cloned) return;
      const weekLabel = String(cloned.week_label || "").trim();
      if (!weekLabel) return;
      const sourceCandidates = new Set([
        normalizeScheduleSourceValue(sourceHint),
        normalizeScheduleSourceValue(
          cloned.source_type || cloned.source_key || "",
        ),
      ]);
      sourceCandidates.forEach((candidate) => {
        if (!candidate) return;
        schedulePayloadMemoryCache.set(
          buildSchedulePayloadCacheKey(candidate, weekLabel),
          cloned,
        );
      });
    }

    function readSchedulePayloadFromMemory(
      weekLabel,
      sourceHint = "preferred",
    ) {
      if (!weekLabel) return null;
      const candidates = [
        normalizeScheduleSourceValue(sourceHint),
        "preferred",
        "mobile",
      ];
      for (const candidate of candidates) {
        const cached = schedulePayloadMemoryCache.get(
          buildSchedulePayloadCacheKey(candidate, weekLabel),
        );
        if (cached) return cloneSchedulePayload(cached);
      }
      return null;
    }

    function readSchedulePrefetchTask(weekLabel, sourceHint = "preferred") {
      if (!weekLabel) return null;
      const candidates = [
        normalizeScheduleSourceValue(sourceHint),
        "preferred",
        "mobile",
      ];
      for (const candidate of candidates) {
        const task = schedulePrefetchInFlight.get(
          buildSchedulePayloadCacheKey(candidate, weekLabel),
        );
        if (task) return task;
      }
      return null;
    }

    async function requestLocalSchedulePayload({
      weekLabel = "",
      sourceHint = "preferred",
    } = {}) {
      const query = new URLSearchParams();
      const normalizedWeekLabel = String(weekLabel || "").trim();
      if (normalizedWeekLabel) query.set("week_label", normalizedWeekLabel);
      query.set("source", normalizeScheduleSourceValue(sourceHint));
      const res = await getWithSession(`/schedule/local?${query.toString()}`);
      return res.data || {};
    }

    async function warmSchedulePayloadIntoMemory(
      weekLabel,
      sourceHint = "preferred",
    ) {
      const normalizedWeekLabel = String(weekLabel || "").trim();
      if (!normalizedWeekLabel) return null;

      const memoryPayload = readSchedulePayloadFromMemory(
        normalizedWeekLabel,
        sourceHint,
      );
      if (memoryPayload) return memoryPayload;

      const existingTask = readSchedulePrefetchTask(
        normalizedWeekLabel,
        sourceHint,
      );
      if (existingTask) {
        try {
          await existingTask;
        } catch {
          return null;
        }
        return readSchedulePayloadFromMemory(normalizedWeekLabel, sourceHint);
      }

      const normalizedSource = normalizeScheduleSourceValue(sourceHint);
      const cacheKey = buildSchedulePayloadCacheKey(
        normalizedSource,
        normalizedWeekLabel,
      );
      const task = (async () => {
        const payload = await requestLocalSchedulePayload({
          weekLabel: normalizedWeekLabel,
          sourceHint: normalizedSource,
        });
        rememberSchedulePayload(payload, normalizedSource);
        return payload;
      })();
      schedulePrefetchInFlight.set(cacheKey, task);
      try {
        const payload = await task;
        return cloneSchedulePayload(payload);
      } finally {
        schedulePrefetchInFlight.delete(cacheKey);
      }
    }

    async function prefetchScheduleWeekWindow(centerWeekNumber, options = {}) {
      const {
        sourceHint = schedSource ? schedSource.value : "preferred",
        behind = 1,
        ahead = 2,
      } = options;
      if (
        !sessionId ||
        !Number.isInteger(centerWeekNumber) ||
        centerWeekNumber <= 0
      )
        return;

      const normalizedSource = normalizeScheduleSourceValue(sourceHint);
      const targets = [];
      for (let offset = -behind; offset <= ahead; offset += 1) {
        const weekNumber = centerWeekNumber + offset;
        if (weekNumber <= 0) continue;
        const weekLabel = resolveWeekLabelForNumber(weekNumber);
        if (!weekLabel) continue;
        targets.push({ weekNumber, weekLabel });
      }

      await Promise.all(
        targets.map(async (target) => {
          try {
            await warmSchedulePayloadIntoMemory(
              target.weekLabel,
              normalizedSource,
            );
          } catch {
            // Missing local week cache is acceptable during prefetch.
          }
        }),
      );
    }

    async function ensureCurrentWeekScheduleView(options = {}) {
      const {
        sourceHint = schedSource ? schedSource.value : "preferred",
        showBoardLoading = false,
      } = options;
      const targetWeek = resolveCurrentWeekTargetNumber();
      if (!Number.isInteger(targetWeek) || targetWeek <= 0) return false;
      if (viewedScheduleWeekNumber === targetWeek) return true;
      return switchScheduleWeekFromCache(targetWeek, {
        sourceHint,
        showBoardLoading,
        allowAutoWeekSwitch: true,
      });
    }

    function renderScheduleWarnings(dataWarnings = []) {
      if (!schedWarnings) return;
      if (dataWarnings.length) {
        const triIcon = `<svg class="sched-warning-icon" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" aria-hidden="true"><path stroke-linecap="round" stroke-linejoin="round" d="M12 9v4m0 4h.01M10.29 3.86L1.82 18a2 2 0 001.71 3h16.94a2 2 0 001.71-3L13.71 3.86a2 2 0 00-3.42 0z"/></svg>`;
        schedWarnings.innerHTML = dataWarnings
          .map((w) => {
            const title = esc(w.course_name || "课程冲突");
            const detail = esc(
              `${w.week_label || ""} ${w.weekday || ""}`.trim() || "当前周",
            );
            const kept = esc(
              `${w.kept_weeks || ""} ${w.kept_room || ""}`.trim() ||
                "已保留一条匹配结果",
            );
            return `<div class="sched-warning-item">
          ${triIcon}
          <span class="sched-warning-text"><span class="sched-warning-title">${title}</span><span class="sched-warning-detail">${detail} 检测到重复排课，已按移动教务归一化规则保留可用数据。</span><span class="sched-warning-kept">保留：${kept}</span>
          </span>
        </div>`;
          })
          .join("");
        schedWarnings.classList.remove("hidden");
        dataWarnings.forEach((w) =>
          log(`[数据警告] ${w.message || w.type || "课表冲突"}`),
        );
        return;
      }
      schedWarnings.classList.add("hidden");
      schedWarnings.innerHTML = "";
    }

    async function applySchedulePayload(
      d,
      {
        previousViewedWeekNumber = viewedScheduleWeekNumber,
        originHint = "",
        sourceHint = "",
      } = {},
    ) {
      const dataWarnings = Array.isArray(d.data_warnings)
        ? d.data_warnings
        : [];
      latestScheduleRows = enrichRowsWithWarningConflicts(
        d.rows || [],
        dataWarnings,
      );
      latestScheduleWeekLabel = d.week_label || "";
      scheduleNeedsMobileBootstrap = false;
      scheduleAvailableWeeks = Array.isArray(d.available_weeks)
        ? d.available_weeks
        : [];
      scheduleWeekDates = normalizeScheduleWeekDates(d.week_dates);
      const resolvedCurrentWeekLabel =
        resolveCurrentWeekLabelFromAvailableWeeks(scheduleAvailableWeeks) ||
        d.current_week_label ||
        "";
      currentScheduleWeekNumber = parseWeekNumberFromLabel(
        resolvedCurrentWeekLabel,
      );
      if (
        currentScheduleWeekNumber == null &&
        Number.isInteger(d.current_week_number)
      )
        currentScheduleWeekNumber = d.current_week_number;
      viewedScheduleWeekNumber = Number.isInteger(d.viewed_week_number)
        ? d.viewed_week_number
        : parseWeekNumberFromLabel(d.week_label || "");
      scheduleAutoWeekAttemptKey = "";
      scheduleWeekShiftContext = {
        fromWeek: previousViewedWeekNumber,
        toWeek: viewedScheduleWeekNumber,
      };
      scheduleBlocks = resolveScheduleBlocks(d.block_times || []);
      const didRender = await renderScheduleBoard(latestScheduleRows);
      if (didRender) startScheduleLiveTracking();

      renderScheduleWarnings(dataWarnings);

      const source = resolveScheduleSourceLabel(d);
      const fallback = d.fallback_reason || "";
      const termLabel = d.term && d.term.term_label ? d.term.term_label : "";
      const updatedAt =
        d.updated_at || d.file_updated_at || d._file_updated_at || "";
      if (d.student) updateStudentProfile(mergeProfileSnapshots(currentStudentProfile, d.student));
      const count = (d.rows || []).length;
      const summaryParts = [];
      if (termLabel) summaryParts.push(termLabel);
      if (d.week_label) summaryParts.push(d.week_label);
      summaryParts.push(`${count} 条课程`);
      const metaParts = [
        resolveScheduleOriginLabel(d, originHint),
        source,
        `更新时间 ${formatScheduleUpdatedAt(updatedAt)}`,
      ];
      if (fallback) metaParts.push("已触发备用数据源回退");
      setScheduleInfoBar({
        summary: summaryParts.join(" · "),
        meta: metaParts.filter(Boolean).join(" · "),
      });
      autoRqText.textContent = d.week_label
        ? isViewingCurrentWeek()
          ? `当前${formatWeekLabel(d.week_label)}`
          : `正在查看${formatWeekLabel(d.week_label)}`
        : "课表";
      rememberSchedulePayload(
        d,
        sourceHint || (schedSource ? schedSource.value : "preferred"),
      );
      if (viewedScheduleWeekNumber != null) {
        void prefetchScheduleWeekWindow(viewedScheduleWeekNumber, {
          sourceHint:
            sourceHint || (schedSource ? schedSource.value : "preferred"),
          behind: 1,
          ahead: 2,
        });
      }
      return {
        source,
        count: (d.rows || []).length,
        cached: Boolean(d.cached),
      };
    }

    async function loadCachedSchedule(options = {}) {
      const {
        showBoardLoading = false,
        seedData = null,
        weekLabel = "",
        sourceHint = schedSource ? schedSource.value : "preferred",
        allowAutoWeekSwitch = true,
        allowAutoWeekSwitchNetwork = true,
        suppressStateResetOnMiss = false,
      } = options;
      if (!sessionId) return false;
      try {
        if (seedData && typeof seedData === "object") {
          const seedOrigin =
            seedData.cached === false || seedData.data_origin === "live_fetch"
              ? "live_fetch"
              : "local_cache";
          scheduleNeedsMobileBootstrap = false;
          const summary = await applySchedulePayload(seedData, {
            originHint: seedOrigin,
            sourceHint,
          });
          if (allowAutoWeekSwitch && !scheduleManualWeekViewLocked) {
            await maybeAutoSwitchToNextWeek({
              allowNetwork: allowAutoWeekSwitchNetwork,
              sourceHint,
            });
          }
          log(
            `${seedOrigin === "local_cache" ? "已加载本地课表" : "已拉取当前课表"}: ${summary.source || "课表数据"}, ${summary.count} 条`,
          );
          return true;
        }
        const normalizedWeekLabel = String(weekLabel || "").trim();
        const memoryPayload = normalizedWeekLabel
          ? readSchedulePayloadFromMemory(normalizedWeekLabel, sourceHint)
          : null;
        const warmTask = normalizedWeekLabel
          ? readSchedulePrefetchTask(normalizedWeekLabel, sourceHint)
          : null;
        const shouldShowLoading = Boolean(
          showBoardLoading && !memoryPayload && !warmTask,
        );
        if (shouldShowLoading)
          setScheduleBoardLoading(true, "正在读取本地课表...");
        const payload =
          memoryPayload ||
          (normalizedWeekLabel
            ? await warmSchedulePayloadIntoMemory(
                normalizedWeekLabel,
                sourceHint,
              )
            : await requestLocalSchedulePayload({
                weekLabel: normalizedWeekLabel,
                sourceHint,
              }));
        if (!payload)
          throw new Error(
            `未找到 ${normalizedWeekLabel || "当前周"} 的本地课表缓存`,
          );
        if (!memoryPayload && !normalizedWeekLabel)
          rememberSchedulePayload(payload, sourceHint);
        const summary = await applySchedulePayload(payload, {
          originHint: "local_cache",
          sourceHint,
        });
        if (allowAutoWeekSwitch && !scheduleManualWeekViewLocked) {
          await maybeAutoSwitchToNextWeek({
            allowNetwork: allowAutoWeekSwitchNetwork,
            sourceHint,
          });
        }
        log(
          `${memoryPayload ? "已从内存切换课表" : "已加载本地课表"}: ${summary.source || "缓存课表"}, ${summary.count} 条`,
        );
        return true;
      } catch (error) {
        if (suppressStateResetOnMiss) return false;
        const backendTooOld =
          error &&
          error.status === 404 &&
          /not found/i.test(String(error.message || ""));
        if (!String(weekLabel || "").trim())
          scheduleNeedsMobileBootstrap = true;
        if (backendTooOld) {
          log("本地课表读取失败：当前后端未提供缓存接口，请重启后端到最新版本");
        } else {
          log(`未找到可用的本地课表缓存: ${error.message}`);
        }
        latestScheduleRows = [];
        latestScheduleWeekLabel = "";
        currentScheduleWeekNumber = null;
        viewedScheduleWeekNumber = null;
        scheduleAvailableWeeks = [];
        scheduleWeekDates = {};
        stopScheduleLiveTracking();
        if (schedBoard) schedBoard.innerHTML = "";
        if (schedEmpty) schedEmpty.classList.remove("hidden");
        if (schedWarnings) {
          schedWarnings.classList.add("hidden");
          schedWarnings.innerHTML = "";
        }
        setScheduleInfoBar({
          summary: backendTooOld ? "本地课表读取失败" : "暂无本地缓存课表",
          meta: backendTooOld
            ? "当前后端版本未提供缓存接口，请重启服务后重试。"
            : "当前账号还没有本地课表缓存；请使用密码登录重新完成首次初始化。",
        });
        return false;
      } finally {
        if (showBoardLoading) setScheduleBoardLoading(false);
      }
    }

    async function switchScheduleWeekFromCache(targetWeekNumber, options = {}) {
      const {
        sourceHint = schedSource ? schedSource.value : "preferred",
        showBoardLoading = true,
        allowAutoWeekSwitch = false,
      } = options;
      if (!Number.isInteger(targetWeekNumber) || targetWeekNumber <= 0)
        return false;

      const targetWeekLabel = resolveWeekLabelForNumber(targetWeekNumber);
      if (!targetWeekLabel) return false;

      const previousManualLock = scheduleManualWeekViewLocked;
      const shouldLockManualView =
        currentScheduleWeekNumber != null &&
        targetWeekNumber !== currentScheduleWeekNumber;
      const shouldShowBoardLoading = Boolean(
        showBoardLoading &&
        !readSchedulePayloadFromMemory(targetWeekLabel, sourceHint) &&
        !readSchedulePrefetchTask(targetWeekLabel, sourceHint),
      );
      const loaded = await loadCachedSchedule({
        showBoardLoading: shouldShowBoardLoading,
        weekLabel: targetWeekLabel,
        sourceHint,
        allowAutoWeekSwitch,
        allowAutoWeekSwitchNetwork: false,
        suppressStateResetOnMiss: true,
      });
      if (loaded) {
        setScheduleManualWeekViewLocked(shouldLockManualView);
        log(`已切换到本地缓存课表：${targetWeekLabel}`);
        return true;
      }

      const fetchOverrides = buildMobileFetchOverridesForMissingWeek(
        targetWeekNumber,
        currentScheduleWeekNumber,
      );
      if (
        sessionId &&
        fetchOverrides
      ) {
        log(`本地缓存缺少 ${targetWeekLabel}，正在改用移动教务拉取该周课表...`);
        const fetched = await fetchSchedule({
          ...fetchOverrides,
          showBoardLoading,
          allowAutoWeekSwitch: false,
          silentError: false,
          bypassScheduleFetchGate: true,
        });
        if (fetched) {
          setScheduleManualWeekViewLocked(shouldLockManualView);
          log(`已通过移动教务拉取 ${targetWeekLabel}`);
          return true;
        }
      }

      setScheduleManualWeekViewLocked(previousManualLock);
      setScheduleInfoBar({
        summary: `未能加载 ${targetWeekLabel}`,
        meta: "本地缓存不存在，且移动教务拉取失败，请检查账号状态后重试。",
      });
      log(`本地缓存缺少 ${targetWeekLabel}，且移动教务拉取失败`);
      return false;
    }

    function shouldForceMobileScheduleBootstrap() {
      if (!scheduleNeedsMobileBootstrap) return false;
      if (latestScheduleWeekLabel) return false;
      if (currentScheduleWeekNumber != null || viewedScheduleWeekNumber != null)
        return false;
      return true;
    }

    function normalizeScheduleFetchOverrides(overrides = {}) {
      if (!shouldForceMobileScheduleBootstrap()) return overrides;
      const requestedSource = normalizeScheduleSourceValue(
        overrides.sourceOverride ||
          (schedSource ? schedSource.value : "preferred"),
      );
      if (
        requestedSource === "mobile" &&
        (overrides.mode || "current") === "current"
      )
        return overrides;
      return {
        ...overrides,
        mode: "current",
        manual_week: "",
        cookie_rq: null,
        sourceOverride: "mobile",
        bootstrapFetch: true,
      };
    }

    async function ensureScheduleDataReadyAfterAuth(seedData = null) {
      const loaded = await loadCachedSchedule({
        showBoardLoading: true,
        seedData,
      });
      if (loaded) {
        await ensureCurrentWeekScheduleView({ showBoardLoading: false });
        queueScheduleWarmupPrefetch(
          viewedScheduleWeekNumber || resolveCurrentWeekTargetNumber(),
        );
        return true;
      }

      log("当前账号暂无本地课表缓存；请使用密码登录重新完成首次初始化");
      return false;
    }

    function queueScheduleWarmupPrefetch(targetWeekNumber = null) {
      if (isEffectivePerformanceLiteMode()) return;
      clearScheduleWarmupPrefetchTimer();
      const weekNumber =
        Number(targetWeekNumber) || resolveCurrentWeekTargetNumber();
      scheduleWarmupPrefetchTimer = window.setTimeout(() => {
        scheduleWarmupPrefetchTimer = null;
        void prefetchScheduleWeekWindow(weekNumber, {
          sourceHint: schedSource ? schedSource.value : "preferred",
          behind: 1,
          ahead: 2,
        });
      }, 240);
    }

    function schedulePostAuthWarmup(seedData = null, options = {}) {
      const { refreshNetworkStatus = true } = options;
      const expectedSessionId = sessionId;
      clearPostAuthWarmupTimer();
      scheduleUiIdleTask(() => {
        postAuthWarmupTimer = window.setTimeout(() => {
          postAuthWarmupTimer = null;
          if (!expectedSessionId || expectedSessionId !== sessionId) return;
          void (async () => {
            await ensureScheduleDataReadyAfterAuth(seedData);
            if (!expectedSessionId || expectedSessionId !== sessionId) return;
            await loadNetworkConfig({
              refreshStatus: refreshNetworkStatus,
              silent: true,
            });
          })();
        }, isEffectivePerformanceLiteMode()
          ? LOW_PERFORMANCE_POST_AUTH_WARMUP_DELAY_MS
          : DEFAULT_POST_AUTH_WARMUP_DELAY_MS);
      });
    }

    async function fetchSchedule(overrides = {}) {
      return scheduleFetchGate.run(() => runScheduleFetch(overrides), {
        bypass: Boolean(overrides.bypassScheduleFetchGate),
      });
    }

    function isJwCaptchaChallengePayload(data) {
      return (
        data &&
        typeof data === "object" &&
        data.status === "captcha_required" &&
        typeof data.challenge_id === "string"
      );
    }

    function ensureJwCaptchaModal() {
      let overlay = document.getElementById("jwCaptchaModalOverlay");
      if (!overlay) {
        const style = document.createElement("style");
        style.id = "jwCaptchaModalStyle";
        style.textContent = `
          .jw-captcha-overlay{position:fixed;inset:0;z-index:80;display:flex;align-items:center;justify-content:center;padding:24px;background:rgba(17,24,39,.46);backdrop-filter:blur(10px)}
          .jw-captcha-overlay.hidden{display:none}
          .jw-captcha-modal{width:min(420px,100%);border:1px solid rgba(120,90,42,.22);border-radius:24px;background:linear-gradient(145deg,#fffaf0,#f5efe0);box-shadow:0 24px 70px rgba(55,43,24,.28);padding:24px;color:#2f2415}
          .jw-captcha-modal h2{margin:0 0 8px;font-size:22px}
          .jw-captcha-modal p{margin:0 0 16px;color:#6f614d;line-height:1.6}
          .jw-captcha-image{display:flex;align-items:center;justify-content:center;min-height:82px;margin:10px 0 16px;border:1px dashed rgba(120,90,42,.35);border-radius:16px;background:#fffdf7}
          .jw-captcha-image img{max-width:100%;height:auto;border-radius:10px}
          .jw-captcha-modal input{width:100%;box-sizing:border-box;margin-bottom:16px;padding:12px 14px;border:1px solid rgba(120,90,42,.32);border-radius:14px;background:#fff;color:#2f2415;font-size:18px;letter-spacing:.08em}
          .jw-captcha-actions{display:flex;gap:10px;justify-content:flex-end}
          .jw-captcha-meta{font-size:12px;color:#7c6b53}
        `;
        document.head.appendChild(style);

        overlay = document.createElement("div");
        overlay.id = "jwCaptchaModalOverlay";
        overlay.className = "jw-captcha-overlay hidden";
        overlay.setAttribute("role", "dialog");
        overlay.setAttribute("aria-modal", "true");
        overlay.innerHTML = `
          <div class="jw-captcha-modal">
            <h2>输入教务系统验证码</h2>
            <p>传统教务需要先完成预登录和人工验证码。前端只提交 challenge_id 与验证码，不保存 Cookie 或请求头。</p>
            <div class="jw-captcha-image"><img id="jwCaptchaImage" alt="教务系统验证码" /></div>
            <input id="jwCaptchaInput" type="text" inputmode="text" autocomplete="off" placeholder="验证码" />
            <p class="jw-captcha-meta" id="jwCaptchaMeta"></p>
            <div class="jw-captcha-actions">
              <button class="btn default" type="button" id="btnJwCaptchaCancel">取消</button>
              <button class="btn primary" type="button" id="btnJwCaptchaSubmit">提交验证码</button>
            </div>
          </div>
        `;
        document.body.appendChild(overlay);
      }

      return {
        overlay,
        image: document.getElementById("jwCaptchaImage"),
        input: document.getElementById("jwCaptchaInput"),
        meta: document.getElementById("jwCaptchaMeta"),
        cancel: document.getElementById("btnJwCaptchaCancel"),
        submit: document.getElementById("btnJwCaptchaSubmit"),
      };
    }

    function promptJwCaptchaCode(challenge) {
      const modal = ensureJwCaptchaModal();
      if (modal.image) {
        modal.image.src = challenge.captcha_image || "";
        modal.image.classList.toggle("hidden", !challenge.captcha_image);
      }
      if (modal.input) modal.input.value = "";
      if (modal.meta) {
        modal.meta.textContent = `challenge_id: ${challenge.challenge_id} / ${challenge.expires_in_seconds || 0} 秒内有效`;
      }
      modal.overlay.classList.remove("hidden");
      if (modal.input) modal.input.focus({ preventScroll: true });

      return new Promise((resolve) => {
        const cleanup = (value) => {
          modal.overlay.classList.add("hidden");
          modal.cancel.removeEventListener("click", onCancel);
          modal.submit.removeEventListener("click", onSubmit);
          modal.input.removeEventListener("keydown", onKeydown);
          document.removeEventListener("keydown", onDocumentKeydown);
          resolve(value);
        };
        const onCancel = () => cleanup(null);
        const onSubmit = () => {
          const code = modal.input.value.trim();
          if (!code) {
            modal.input.focus({ preventScroll: true });
            return;
          }
          cleanup(code);
        };
        const onKeydown = (event) => {
          if (event.key === "Enter") {
            event.preventDefault();
            onSubmit();
          }
        };
        const onDocumentKeydown = (event) => {
          if (event.key === "Escape") cleanup(null);
        };
        modal.cancel.addEventListener("click", onCancel);
        modal.submit.addEventListener("click", onSubmit);
        modal.input.addEventListener("keydown", onKeydown);
        document.addEventListener("keydown", onDocumentKeydown);
      });
    }

    async function handleJwCaptchaChallenge(
      challenge,
      { req, previousViewedWeekNumber, effectiveOverrides },
    ) {
      log("教务系统需要人工验证码，已打开输入窗口");
      setScheduleInfoBar({
        summary: "等待教务系统验证码",
        meta: "验证码只用于继续本次服务端登录流程，前端不会保存 Cookie 或请求头。",
      });
      const captchaCode = await promptJwCaptchaCode(challenge);
      if (!captchaCode) {
        log("已取消教务系统验证码输入");
        return false;
      }

      try {
        const res = await postWithSession(
          "/auth/jw-captcha/submit",
          {
            challenge_id: challenge.challenge_id,
            captcha_code: captchaCode,
          },
          "",
          { timeoutMs: API_TIMEOUTS.scheduleRefresh },
        );
        const summary = await applySchedulePayload(res.data, {
          previousViewedWeekNumber,
          originHint: "live_fetch",
          sourceHint: req.requestSource || "cookie",
        });
        if (
          effectiveOverrides.allowAutoWeekSwitch !== false &&
          !scheduleManualWeekViewLocked
        ) {
          await maybeAutoSwitchToNextWeek({
            allowNetwork: true,
            sourceHint: req.requestSource || "cookie",
          });
        }
        log(`Cookie 课表获取成功: ${summary.source || "教务系统课表"}, ${summary.count} 条`);
        return true;
      } catch (error) {
        setScheduleInfoBar({
          summary: "教务系统验证码提交失败",
          meta: error.message || "验证码错误、过期或会话不匹配",
        });
        log(`教务系统验证码提交失败: ${error.message}`);
        return false;
      }
    }

    async function runScheduleFetch(overrides = {}) {
      recordTelemetryModuleHit("schedule");
      const effectiveOverrides = normalizeScheduleFetchOverrides(overrides);
      const req = resolveScheduleRequest(effectiveOverrides);
      const isCookieScheduleFetch = req.endpoint === "cookie";
      const fetchVersion = ++scheduleFetchVersion;
      const shouldShowBoardLoading = Boolean(
        effectiveOverrides.showBoardLoading,
      );
      const previousViewedWeekNumber = viewedScheduleWeekNumber;
      if (
        !isCookieScheduleFetch &&
        !effectiveOverrides.ignoreRemoteCooldown &&
        shouldBlockMobileRemoteAction("课表刷新", {
          silent: Boolean(effectiveOverrides.silentError),
        })
      ) {
        return false;
      }
      setLoading(btnSchedFetch, true);
      if (shouldShowBoardLoading)
        setScheduleBoardLoading(true, resolveScheduleLoadingText(req.payload));
      if (effectiveOverrides.bootstrapFetch) {
        log(
          isCookieScheduleFetch
            ? "检测到本地课表为空，正在使用教务系统来源拉取当前周课表"
            : "检测到本地课表为空，已切换到移动教务拉取当前周课表",
        );
      }
      log(
        `获取课表: ${req.endpoint}, source=${req.requestSource || (schedSource ? schedSource.value : "preferred")}`,
      );

      try {
        const res = await postWithSession(
          `/schedule/${req.endpoint}`,
          req.payload,
          "",
          { timeoutMs: API_TIMEOUTS.scheduleRefresh },
        );
        if (isJwCaptchaChallengePayload(res.data)) {
          return await handleJwCaptchaChallenge(res.data, {
            req,
            previousViewedWeekNumber,
            effectiveOverrides,
          });
        }

        const summary = await applySchedulePayload(res.data, {
          previousViewedWeekNumber,
          originHint: "live_fetch",
          sourceHint:
            req.requestSource ||
            (schedSource ? schedSource.value : "preferred"),
        });
        if (
          effectiveOverrides.allowAutoWeekSwitch !== false &&
          !scheduleManualWeekViewLocked
        ) {
          await maybeAutoSwitchToNextWeek({
            allowNetwork: true,
            sourceHint:
              req.requestSource ||
              (schedSource ? schedSource.value : "preferred"),
          });
        }
        log(
          `课表获取成功: ${summary.source || "统一课表"}, ${summary.count} 条`,
        );
        if (!isCookieScheduleFetch) clearMobileRemoteFailure();
        return true;
      } catch (e) {
        if (!isCookieScheduleFetch) markMobileRemoteFailure(e);
        if (!effectiveOverrides.silentError) {
          setScheduleInfoBar({
            summary: "课表获取失败",
            meta: `当前仍可查看本地缓存课表：${e.message}`,
          });
        }
        log(`课表获取失败: ${e.message}`);
        return false;
      } finally {
        setLoading(btnSchedFetch, false);
        if (shouldShowBoardLoading && fetchVersion === scheduleFetchVersion)
          setScheduleBoardLoading(false);
      }
    }

    function extractSectionNumbers(text) {
      let value = String(text || "");
      if (!value) return [];
      if (value.includes("周")) {
        const parts = value.split("周");
        value = parts[parts.length - 1] || value;
      }
      const weekdayMatch = value.match(/星期[一二三四五六日天]/);
      if (weekdayMatch && typeof weekdayMatch.index === "number") {
        value = value.slice(weekdayMatch.index + weekdayMatch[0].length);
      }
      const explicitMatches = [
        ...value.matchAll(/(\d{1,2})\s*[-~]\s*(\d{1,2})/g),
      ];
      if (explicitMatches.length) {
        return explicitMatches.flatMap((match) => {
          const start = Number(match[1]);
          const end = Number(match[2]);
          if (!start || !end) return [];
          if (start > end) return [start, end];
          return Array.from(
            { length: end - start + 1 },
            (_, index) => start + index,
          );
        });
      }
      const numbers = [];
      for (const token of value.match(/\d+/g) || []) {
        if (token.length > 2 && token.length % 2 === 0) {
          for (let index = 0; index < token.length; index += 2) {
            const number = Number(token.slice(index, index + 2));
            if (number) numbers.push(number);
          }
        } else {
          const number = Number(token);
          if (number) numbers.push(number);
        }
      }
      return [...new Set(numbers)];
    }

    function parseSectionBounds(row) {
      const candidates = [row.section, row.time_range, row.time_text];
      let numbers = [];
      for (const candidate of candidates) {
        numbers = extractSectionNumbers(candidate);
        if (numbers.length) break;
      }
      if (!numbers.length) return null;
      return { start: Math.min(...numbers), end: Math.max(...numbers) };
    }

    function mapSectionToBlock(sectionNumber) {
      return Math.ceil(sectionNumber / 2);
    }

    function resolveScheduleBlocks(blockTimes) {
      const next = DEFAULT_SCHEDULE_BLOCKS.map((item) => ({ ...item }));
      if (!Array.isArray(blockTimes) || !blockTimes.length) return next;

      for (const meta of blockTimes) {
        const sectionNumbers = extractSectionNumbers(meta.section || "");
        if (!sectionNumbers.length) continue;
        const blockIndex = mapSectionToBlock(Math.min(...sectionNumbers));
        const found = next.find((item) => item.index === blockIndex);
        if (!found) continue;
        found.label = meta.block_label || found.label;
        found.sectionRange = (meta.section || found.sectionRange)
          .replace(/^第/, "")
          .replace(/节$/, "节");
        found.blockTime = meta.block_time || found.blockTime;
        found.small1 = meta.small_section_1 || found.small1;
        found.small2 = meta.small_section_2 || found.small2;
        found.breakMinutes = Number(meta.break_minutes) || found.breakMinutes;
      }
      return next;
    }

    function resolveWeekdays(rows) {
      // Keep a stable 7-day grid across week switches. Even if a week has no
      // Sunday classes, we still render Sunday as an empty column so layout
      // width and card positions remain stable.
      return [...SCHEDULE_WEEKDAYS];
    }

    function normalizeSchedulePlacements(rows) {
      return rows
        .map((row) => {
          const weekdayIndex = activeWeekdays.indexOf(row.weekday);
          const bounds = parseSectionBounds(row);
          if (weekdayIndex === -1 || !bounds) return null;
          const start = Math.max(1, mapSectionToBlock(bounds.start));
          const end = Math.min(
            scheduleBlocks.length,
            mapSectionToBlock(bounds.end),
          );
          if (start > end) return null;
          return {
            row,
            weekdayIndex,
            start,
            end,
            span: end - start + 1,
          };
        })
        .filter(Boolean)
        .sort((left, right) => {
          if (left.start !== right.start) return left.start - right.start;
          if (left.weekdayIndex !== right.weekdayIndex)
            return left.weekdayIndex - right.weekdayIndex;
          return right.span - left.span;
        });
    }

    function buildScheduleMatrix(placements) {
      const occupied = Array.from({ length: scheduleBlocks.length }, () =>
        Array(SCHEDULE_WEEKDAYS.length).fill(false),
      );
      const anchors = new Map();

      for (const placement of placements) {
        const anchorKey = `${placement.start}-${placement.weekdayIndex}`;
        const existing = anchors.get(anchorKey);
        if (existing) {
          existing.items.push(placement.row);
          existing.rowspan = Math.max(existing.rowspan, placement.span);
          continue;
        }

        anchors.set(anchorKey, {
          rowspan: placement.span,
          items: [placement.row],
        });

        for (let offset = 0; offset < placement.span; offset += 1) {
          const rowIndex = placement.start - 1 + offset;
          if (rowIndex < occupied.length)
            occupied[rowIndex][placement.weekdayIndex] = true;
        }
      }

      return { occupied, anchors };
    }

    function getRenderedScheduleKeys() {
      if (!schedBoard) return new Set();
      return new Set(
        Array.from(schedBoard.querySelectorAll(".schedule-course-card"))
          .map((card) => card.dataset.scheduleKey)
          .filter(Boolean),
      );
    }

    function resolveViewedScheduleWeekHeading() {
      if (latestScheduleWeekLabel)
        return formatWeekLabel(latestScheduleWeekLabel);
      if (viewedScheduleWeekNumber != null)
        return formatWeekLabel(
          resolveWeekLabelForNumber(viewedScheduleWeekNumber),
        );
      const currentWeek = resolveCurrentWeekTargetNumber();
      return currentWeek
        ? formatWeekLabel(resolveWeekLabelForNumber(currentWeek))
        : "本周";
    }

    function buildScheduleWeekSwitcherMarkup(extraClass = "") {
      const weekHead = resolveViewedScheduleWeekHeading();
      const prevWeek = resolveWeekNavTarget(-1);
      const nextWeek = resolveWeekNavTarget(1);
      const classNames = [
        "schedule-week-switcher",
        isViewingCurrentWeek() ? "is-current" : "is-shifted",
      ];
      if (extraClass) classNames.push(extraClass);
      return `
      <div class="${classNames.join(" ")}">
        <button class="schedule-week-nav prev ${prevWeek ? "" : "disabled"}" type="button" data-week-nav="prev" ${prevWeek ? "" : "disabled"} aria-label="查看上一周">&#x2039;</button>
        <button class="schedule-week-center" type="button" data-week-nav="current" ${isViewingCurrentWeek() ? "disabled" : ""}>${esc(weekHead)}</button>
        <button class="schedule-week-nav next ${nextWeek ? "" : "disabled"}" type="button" data-week-nav="next" ${nextWeek ? "" : "disabled"} aria-label="查看下一周">&#x203A;</button>
      </div>
    `;
    }

    function buildScheduleEmptyActionMarkup(action, label, options = {}) {
      const { emphasis = false } = options;
      const disabled =
        action === "current"
          ? isViewingCurrentWeek() || !resolveCurrentWeekTargetNumber()
          : !resolveWeekNavTarget(action === "prev" ? -1 : 1);
      const classNames = ["schedule-empty-action"];
      if (emphasis) classNames.push("is-emphasis");
      return `<button class="${classNames.join(" ")}" type="button" data-week-nav="${action}" ${disabled ? "disabled" : ""}>${esc(label)}</button>`;
    }

    function buildScheduleEmptyStateMarkup() {
      const showReturnAction =
        resolveCurrentWeekTargetNumber() != null && !isViewingCurrentWeek();
      const primaryActionMarkup = showReturnAction
        ? buildScheduleEmptyActionMarkup("current", "返回当前周", {
            emphasis: true,
          })
        : "";
      const titleText = "这周没有课程安排";
      const hintText = showReturnAction
        ? "这一周没有课程，可以稍作休息。需要继续查看时，可从左上切换周次，或直接返回当前教学周。"
        : "这一周没有课程，可以稍作休息。需要继续查看时，可从左上切换周次。";
      return `
      <div class="schedule-empty-state">
        <div class="schedule-empty-toolbar">
          ${buildScheduleWeekSwitcherMarkup("schedule-empty-switcher")}
        </div>
        <div class="schedule-empty-shell">
          <div class="schedule-empty-scene">
            <svg class="schedule-empty-illustration" viewBox="0 0 320 220" focusable="false" aria-hidden="true">
              <path class="schedule-empty-flight primary" d="M30 154C68 133 103 141 139 125C177 107 197 79 226 63C246 52 267 45 292 44" />
              <path class="schedule-empty-flight secondary" d="M50 182C93 168 121 176 157 162C190 150 219 128 255 120" />
              <g class="schedule-empty-sheet-group">
                <rect class="sheet-back" x="44" y="92" width="118" height="88" rx="22" />
                <rect class="sheet-front" x="58" y="76" width="126" height="96" rx="24" />
                <rect class="sheet-mark wide" x="82" y="102" width="68" height="9" rx="4.5" />
                <rect class="sheet-mark" x="82" y="121" width="84" height="7" rx="3.5" />
                <rect class="sheet-mark short" x="82" y="138" width="52" height="7" rx="3.5" />
                <rect class="sheet-mark short" x="82" y="152" width="64" height="7" rx="3.5" />
              </g>
              <g class="schedule-empty-secondary-sheet-group">
                <rect class="secondary-sheet-back" x="212" y="114" width="88" height="66" rx="18" transform="rotate(-12 256 147)" />
                <rect class="secondary-sheet-front" x="198" y="74" width="86" height="64" rx="18" transform="rotate(8 241 106)" />
                <rect class="secondary-sheet-mark wide" x="216" y="92" width="40" height="6" rx="3" transform="rotate(8 241 106)" />
                <rect class="secondary-sheet-mark" x="216" y="105" width="48" height="5" rx="2.5" transform="rotate(8 241 106)" />
                <rect class="secondary-sheet-mark short" x="216" y="117" width="30" height="5" rx="2.5" transform="rotate(8 241 106)" />
              </g>
              <circle class="schedule-empty-orb orb-one" cx="244" cy="150" r="8" />
              <circle class="schedule-empty-orb orb-two" cx="272" cy="136" r="5" />
              <rect class="schedule-empty-pill pill-one" x="204" y="56" width="36" height="14" rx="7" transform="rotate(-34 222 63)" />
              <rect class="schedule-empty-pill pill-two" x="222" y="170" width="42" height="16" rx="8" transform="rotate(-34 243 178)" />
            </svg>
          </div>
          <div class="schedule-empty-copy">
            <h3 class="schedule-empty-title">${esc(titleText)}</h3>
            <p class="schedule-empty-text">${esc(hintText)}</p>
            <div class="schedule-empty-actions">
              ${primaryActionMarkup}
            </div>
          </div>
        </div>
      </div>
    `;
    }

    function getRenderedCompletionMap() {
      if (!schedBoard) return new Map();
      const map = new Map();
      schedBoard.querySelectorAll(".schedule-course-card").forEach((card) => {
        const key = card.dataset.scheduleKey;
        if (!key) return;
        map.set(key, card.classList.contains("live-completed"));
      });
      return map;
    }

    function applyWeekShiftProgressTransition(previousCompletionMap, context) {
      if (
        !schedBoard ||
        !context ||
        !previousCompletionMap ||
        !previousCompletionMap.size
      )
        return;
      const fromWeek = context.fromWeek;
      const toWeek = context.toWeek;
      if (fromWeek == null || toWeek == null || fromWeek === toWeek) return;

      const changedCards = [];
      schedBoard.querySelectorAll(".schedule-course-card").forEach((card) => {
        const key = card.dataset.scheduleKey;
        if (!key || !previousCompletionMap.has(key)) return;
        const wasCompleted = Boolean(previousCompletionMap.get(key));
        const nowCompleted = card.classList.contains("live-completed");
        if (wasCompleted === nowCompleted) return;
        changedCards.push({ card, wasCompleted, nowCompleted });
      });
      if (!changedCards.length) return;

      // Bottom-to-top wave to make the transition feel global across the board.
      changedCards.sort((left, right) => {
        const ly = left.card.getBoundingClientRect().top;
        const ry = right.card.getBoundingClientRect().top;
        return ry - ly;
      });

      let maxDelay = 0;
      changedCards.forEach((entry, index) => {
        const delay = Math.min(index * 55, 420);
        maxDelay = Math.max(maxDelay, delay);
        entry.card.style.setProperty("--week-shift-delay", `${delay}ms`);
        if (entry.wasCompleted && !entry.nowCompleted) {
          entry.card.classList.add("week-shift-revive");
        } else if (!entry.wasCompleted && entry.nowCompleted) {
          entry.card.classList.add("week-shift-fade");
        }
      });

      window.setTimeout(() => {
        changedCards.forEach(({ card }) => {
          card.classList.remove("week-shift-revive", "week-shift-fade");
          card.style.removeProperty("--week-shift-delay");
        });
      }, maxDelay + 620);
    }

    function waitForScheduleDiffAnimation() {
      return new Promise((resolve) => {
        resolveScheduleRenderWait = resolve;
        scheduleRenderTimeout = window.setTimeout(() => {
          scheduleRenderTimeout = null;
          const finish = resolveScheduleRenderWait;
          resolveScheduleRenderWait = null;
          if (finish) finish();
        }, SCHEDULE_DIFF_ANIMATION_MS);
      });
    }

    function renderScheduleCourseCard(
      row,
      enteredKeys = new Set(),
      liveDecorations = new Map(),
    ) {
      const cardKey = getScheduleCardKey(row);
      const decoration = liveDecorations.get(cardKey);
      const courseName = String(row.course_name || "未命名课程");
      const room = row.room || "未标注地点";
      const teacher = row.teacher || "未知教师";
      const roomRaw = String(room || "")
        .trim()
        .toLowerCase();
      const roomType = roomRaw.startsWith("j")
        ? "j"
        : roomRaw.startsWith("s")
          ? "s"
          : "special";
      const roomTypeLabel =
        roomType === "j" ? "教学楼" : roomType === "s" ? "实训楼" : "";
      const stateClass = enteredKeys.has(cardKey) ? " is-entering" : "";
      const titleLength = Array.from(courseName).length;
      const titleStyle =
        titleLength >= 18
          ? ' style="--schedule-title-size: 10.9px; --schedule-title-line-height: 1.22;"'
          : titleLength >= 10
            ? ' style="--schedule-title-size: 12.1px;"'
            : "";
      const roomMarkup = esc(room)
        .replace(/([()（）\[\]【】\/|,，、;；])/g, "$1<wbr>")
        .replace(/-/g, "-<wbr>");
      const conflict =
        row && row.conflict && row.conflict.kind === "room_conflict"
          ? row.conflict
          : null;
      const discardedRooms =
        conflict && Array.isArray(conflict.discarded_rooms)
          ? Array.from(new Set(conflict.discarded_rooms.filter(Boolean)))
          : [];
      const roomDisplay =
        conflict && discardedRooms.length
          ? `<span class="conflict-kept">${esc(conflict.kept_room || room)}</span><span class="conflict-sep"> / </span><span class="conflict-dropped">${discardedRooms.map((item) => `<span class="conflict-dropped-room">${esc(item)}</span>`).join("、")}</span>`
          : roomMarkup;
      const roomRowClass =
        conflict && discardedRooms.length ? " room is-conflict" : " room";
      const roomTitleText =
        conflict && discardedRooms.length
          ? `${String(conflict.kept_room || room)} / ${discardedRooms.join("、")}`
          : String(room || "");
      const classNames = ["schedule-course-card", `room-${roomType}`];
      if (stateClass) classNames.push(stateClass.trim());
      if (decoration)
        decoration.classes.forEach((className) => classNames.push(className));
      const inlineStyle =
        decoration && decoration.liveProgress
          ? ` style="--live-progress:${decoration.liveProgress};"`
          : "";

      // Icon for teacher
      const icoTeacher = `<svg class="card-icon icon-teacher" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M15.75 6a3.75 3.75 0 11-7.5 0 3.75 3.75 0 017.5 0zM4.501 20.118a7.5 7.5 0 0114.998 0A17.933 17.933 0 0112 21.75c-2.676 0-5.216-.584-7.499-1.632z"/></svg>`;

      // Icon for room
      const icoRoom = `<svg class="card-icon icon-room" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2"><path stroke-linecap="round" stroke-linejoin="round" d="M15 10.5a3 3 0 11-6 0 3 3 0 016 0z"/><path stroke-linecap="round" stroke-linejoin="round" d="M19.5 10.5c0 7.142-7.5 11.25-7.5 11.25S4.5 17.642 4.5 10.5a7.5 7.5 0 1115 0z"/></svg>`;

      const chipHtml = roomTypeLabel
        ? `<div class="course-type-chip">${roomTypeLabel}</div>`
        : "";

      return `
      <article class="${classNames.join(" ")}" data-schedule-key="${esc(cardKey)}"${inlineStyle}>
        ${chipHtml}
        <h4 class="schedule-course-title"${titleStyle}>${esc(courseName)}</h4>
        ${renderCardLiveInlineMarkup(decoration ? decoration.inlineText : "", decoration ? decoration.inlineTone : "muted")}
        <div class="card-meta">
          <div class="card-meta-row teacher">
            ${icoTeacher}
            <span class="meta-text">${esc(teacher)}</span>
          </div>
          <div class="card-meta-row${roomRowClass}">
            ${icoRoom}
            <span class="meta-text" title="${esc(roomTitleText)}">${roomDisplay}</span>
          </div>
        </div>
      </article>
    `;
    }

    function buildScheduleBoardTable(
      rows,
      enteredKeys = new Set(),
      liveDecorations = new Map(),
    ) {
      activeWeekdays = resolveWeekdays(rows);

      const placements = normalizeSchedulePlacements(rows);
      const { occupied, anchors } = buildScheduleMatrix(placements);
      const table = document.createElement("table");
      table.className = "schedule-table";
      table.style.setProperty("--day-count", String(activeWeekdays.length));
      table.style.setProperty(
        "--schedule-row-height",
        `${SCHEDULE_ROW_HEIGHT}px`,
      );

      const thead = document.createElement("thead");
      const headRow = document.createElement("tr");
      headRow.innerHTML = `<th class="schedule-time-head schedule-week-head">${buildScheduleWeekSwitcherMarkup()}</th>${activeWeekdays.map((weekday) => `<th>${esc(weekday)}</th>`).join("")}`;
      thead.appendChild(headRow);
      table.appendChild(thead);

      const tbody = document.createElement("tbody");
      for (const block of scheduleBlocks) {
        const tr = document.createElement("tr");
        tr.className = "schedule-row";
        tr.style.setProperty("--row-height", `${SCHEDULE_ROW_HEIGHT}px`);

        const timeTh = document.createElement("th");
        timeTh.className = "schedule-time-cell";
        timeTh.style.setProperty("--slot-span", "1");
        timeTh.innerHTML = `
        <strong>${esc(block.label)}</strong>
        <small>${esc(block.sectionRange)}</small>
        <span class="time-main">${esc(block.blockTime)}</span>
        <div class="small-section-cards">
          <div class="small-section-card">① ${esc(block.small1)}</div>
          <div class="small-break-line" aria-hidden="true"></div>
          <div class="small-section-card">② ${esc(block.small2)}</div>
        </div>
      `;
        tr.appendChild(timeTh);

        for (
          let weekdayIndex = 0;
          weekdayIndex < activeWeekdays.length;
          weekdayIndex += 1
        ) {
          const anchorKey = `${block.index}-${weekdayIndex}`;
          const anchor = anchors.get(anchorKey);
          if (anchor) {
            const td = document.createElement("td");
            td.className = "schedule-course-slot";
            td.rowSpan = anchor.rowspan;
            td.style.setProperty("--slot-span", String(anchor.rowspan));
            td.innerHTML = `<div class="schedule-course-stack" style="--stack-count:${anchor.items.length}">${anchor.items.map((item) => renderScheduleCourseCard(item, enteredKeys, liveDecorations)).join("")}</div>`;
            tr.appendChild(td);
            continue;
          }

          if (occupied[block.index - 1][weekdayIndex]) continue;

          const td = document.createElement("td");
          td.className = "schedule-empty-cell";
          td.style.setProperty("--slot-span", "1");
          tr.appendChild(td);
        }

        tbody.appendChild(tr);
      }

      table.appendChild(tbody);
      return table;
    }

    function commitScheduleBoard(rows, enteredKeys = new Set(), options = {}) {
      schedBoard.innerHTML = "";
      schedEmpty.classList.toggle("hidden", rows.length > 0);
      if (!rows.length) {
        schedEmpty.innerHTML = buildScheduleEmptyStateMarkup();
        window.requestAnimationFrame(() => {
          if (!schedEmpty || schedEmpty.classList.contains("hidden")) return;
          schedEmpty.focus();
        });
        stopScheduleLiveTracking();
        return;
      }
      schedEmpty.innerHTML = "";

      const liveDecorations = buildScheduleLiveDecorations(rows);
      const table = buildScheduleBoardTable(rows, enteredKeys, liveDecorations);
      schedBoard.appendChild(table);

      applyWeekShiftProgressTransition(
        options.previousCompletionMap,
        options.weekShiftContext,
      );
    }

    function handleScheduleWeekNavigation(action) {
      if (action === "current") {
        if (isViewingCurrentWeek()) return;
        const targetWeek = resolveCurrentWeekTargetNumber();
        if (!targetWeek) {
          log("当前周未识别，无法回到本周缓存");
          return;
        }
        void switchScheduleWeekFromCache(targetWeek, {
          sourceHint: schedSource ? schedSource.value : "preferred",
          showBoardLoading: true,
          allowAutoWeekSwitch: false,
        });
        return;
      }

      const direction = action === "prev" ? -1 : action === "next" ? 1 : 0;
      if (!direction) return;
      const targetWeek = resolveWeekNavTarget(direction);
      if (!targetWeek) return;
      void switchScheduleWeekFromCache(targetWeek, {
        sourceHint: schedSource ? schedSource.value : "preferred",
        showBoardLoading: true,
        allowAutoWeekSwitch: false,
      });
    }

    async function renderScheduleBoard(rows) {
      if (scheduleRenderTimeout) {
        clearTimeout(scheduleRenderTimeout);
        scheduleRenderTimeout = null;
        const finish = resolveScheduleRenderWait;
        resolveScheduleRenderWait = null;
        if (finish) finish();
      }

      const renderVersion = ++scheduleRenderVersion;
      const previousCompletionMap = getRenderedCompletionMap();
      const weekShiftContext = scheduleWeekShiftContext;
      scheduleWeekShiftContext = null;
      const nextKeys = new Set(rows.map((row) => getScheduleCardKey(row)));
      const previousKeys = getRenderedScheduleKeys();
      const enteredKeys = new Set(
        Array.from(nextKeys).filter((key) => !previousKeys.has(key)),
      );
      const removedKeys = new Set(
        Array.from(previousKeys).filter((key) => !nextKeys.has(key)),
      );

      if (!previousKeys.size || !removedKeys.size) {
        commitScheduleBoard(rows, enteredKeys, {
          previousCompletionMap,
          weekShiftContext,
        });
        return renderVersion === scheduleRenderVersion;
      }

      schedBoard.querySelectorAll(".schedule-course-card").forEach((card) => {
        const key = card.dataset.scheduleKey || "";
        if (removedKeys.has(key)) card.classList.add("is-leaving");
      });

      await waitForScheduleDiffAnimation();
      if (renderVersion !== scheduleRenderVersion) return false;
      commitScheduleBoard(rows, enteredKeys, {
        previousCompletionMap,
        weekShiftContext,
      });
      return true;
    }

    btnSchedFetch.addEventListener("click", () =>
      fetchSchedule(resolveViewedWeekFetchOverrides()),
    );

    if (schedBoardWrap) {
      schedBoardWrap.addEventListener("click", (event) => {
        const trigger = event.target.closest("[data-week-nav]");
        if (!trigger || trigger.disabled) return;
        handleScheduleWeekNavigation(trigger.dataset.weekNav);
      });
    }

    if (schedEmpty) {
      schedEmpty.addEventListener("keydown", (event) => {
        if (event.key === "ArrowLeft" || event.key === "ArrowRight") {
          handleScheduleWeekNavigation(
            event.key === "ArrowLeft" ? "prev" : "next",
          );
          return;
        }
        if (event.key === "Escape" && !isViewingCurrentWeek()) {
          handleScheduleWeekNavigation("current");
        }
      });
    }

    // ── Login Mode Combobox ────────────────────────────────
    const loginModeMenuController = createSelectMenuController({
      combobox: loginModeCombobox,
      trigger: btnLoginModeSelect,
      menu: loginModeMenu,
      getOptions: () =>
        PRELOGIN_MODE_OPTIONS.map((item) => ({
          value: item.value,
          label: item.label,
          description: item.description,
        })),
      getValue: () => (selectMode ? selectMode.value : "capture"),
      getTriggerLabel: (value, options) => {
        const found = options.find((item) => item.value === value);
        return found ? found.label : "请选择登录流程";
      },
      onSelect: (option) => {
        if (!selectMode) return;
        selectMode.value = option.value;
        selectMode.dispatchEvent(new Event("change"));
      },
      renderOptionContent: (option) =>
        `<span class="round-option-title">${esc(option.label)}</span><span class="round-option-meta">${esc(option.description)}</span>`,
    });

    function syncLoginModeTrigger() {
      loginModeMenuController.syncTrigger();
    }

    // ── Courses ────────────────────────────────────────────
    function getCourseRoundId(round) {
      return String(
        (round && (round.round_id || round.preview_zbid || round.enter_zbid)) ||
          "",
      ).trim();
    }

    function updateRoundSelection(zbid) {
      const previousZbid = inputZbid.value.trim();
      const nextZbid = zbid || "";
      if (isCourseGrabRunning && previousZbid !== nextZbid) {
        courseInfo.textContent = "抢课执行中，请先停止当前任务再切换轮次";
        log("已阻止轮次切换：当前抢课任务仍在执行");
        syncCourseGrabHint();
        roundMenuController.refresh();
        return;
      }
      inputZbid.value = nextZbid;
      const matched = courseRounds.find(
        (r) => getCourseRoundId(r) === nextZbid,
      );
      if (previousZbid !== inputZbid.value.trim()) {
        resetSelectedCourseState();
      }
      if (selectRound) {
        selectRound.value = matched ? zbid : "";
      }
      if (matched) {
        courseInfo.textContent = `${matched.term || "学期待定"} · ${matched.name || "未命名轮次"} · ${matched.time_range || matched.status || "时间待定"}`;
      } else if (!zbid) {
        courseInfo.textContent = "请先点击“刷新轮次”，再手动选择一个选课轮次";
      } else {
        courseInfo.textContent = "未找到已缓存的轮次信息，请先刷新轮次";
      }
      roundMenuController.refresh();
      syncCourseActionState();
    }

    const roundMenuController = createSelectMenuController({
      combobox: roundCombobox,
      trigger: btnRoundSelect,
      menu: roundMenu,
      getOptions: () =>
        courseRounds.map((round) => ({
          value: getCourseRoundId(round),
          label: `${round.term || "学期待定"} · ${round.name || "未命名轮次"}`,
          meta: round.time_range || round.status || "时间未提供",
        })),
      getValue: () => inputZbid.value.trim(),
      getTriggerLabel: (value, options) => {
        const found = options.find((item) => item.value === value);
        return found ? found.label : "请选择选课轮次";
      },
      onSelect: (option) => {
        updateRoundSelection(option.value);
      },
      renderOptionContent: (option) =>
        `<span class="round-option-title">${esc(option.label)}</span><span class="round-option-meta">${esc(option.meta)}</span>`,
      isDisabled: () =>
        !isCourseConnected() || !courseRounds.length || isCourseGrabRunning,
    });

    function renderRoundOptions(rounds) {
      selectRound.innerHTML = "";
      const placeholder = document.createElement("option");
      placeholder.value = "";
      placeholder.textContent = rounds.length
        ? "请选择选课轮次"
        : "未获取到选课轮次";
      selectRound.appendChild(placeholder);

      rounds.forEach((round) => {
        const value = getCourseRoundId(round);
        const option = document.createElement("option");
        option.value = value;
        option.textContent = `${round.term || "学期待定"} · ${round.name || "未命名轮次"}`;
        option.title = round.time_range || round.status || "";
        selectRound.appendChild(option);
      });
      roundMenuController.refresh();
    }

    async function loadCourseRounds() {
      recordTelemetryModuleHit("course");
      if (!sessionId) return;
      if (isCourseGrabRunning) {
        courseInfo.textContent = "抢课执行中，请先停止当前任务再刷新轮次";
        log("已阻止刷新轮次：当前抢课任务仍在执行");
        syncCourseGrabHint();
        return;
      }
      if (!isCourseConnected()) {
        const connected = await connectCourseFramework();
        if (!connected) {
          syncCourseActionState();
          return;
        }
      }

      setLoading(btnLoadRounds, true);
      log("加载选课轮次列表…");
      try {
        const res = await getWithSession("/course/rounds?refresh=true");
        courseRounds = res.data.rounds || [];
        renderRoundOptions(courseRounds);
        log(`选课轮次加载成功: ${courseRounds.length} 个`);
        if (courseRounds.length === 1) {
          const onlyRound = courseRounds[0];
          const zbid = getCourseRoundId(onlyRound);
          updateRoundSelection(zbid);
          courseInfo.textContent = `已自动选择唯一轮次：${onlyRound.name}`;
        } else {
          updateRoundSelection("");
          courseInfo.textContent = courseRounds.length
            ? "轮次已加载，请选择一个选课轮次再获取列表"
            : "当前没有可用的选课轮次";
        }
      } catch (e) {
        courseInfo.textContent = `轮次加载失败: ${e.message}`;
        log(`选课轮次加载失败: ${e.message}`);
        updateRoundSelection("");
      } finally {
        setLoading(btnLoadRounds, false);
      }
    }

    async function fetchCourses() {
      if (isCourseGrabRunning) {
        courseInfo.textContent = "抢课执行中，请先停止当前任务再刷新课程列表";
        log("已阻止刷新课程列表：当前抢课任务仍在执行");
        syncCourseGrabHint();
        return;
      }
      if (!isCourseConnected()) {
        courseInfo.textContent = "选课中心真实连接后置，当前不可加载课程列表";
        log("课程预览未加载：传统教务验证码连接仍后置");
        return;
      }

      const zbid = inputZbid.value.trim();
      if (!courseRounds.length) {
        courseInfo.textContent = "请先点击“加载轮次”，再选择一个选课轮次";
        log("缺少选课轮次缓存，无法加载公选课列表");
        return;
      }
      if (!zbid) {
        courseInfo.textContent = "请先选择一个选课轮次，再获取列表";
        log("缺少选课轮次，无法加载预览");
        roundMenuController.open();
        return;
      }

      setLoading(btnPreview, true);
      log("加载公选课预览");

      try {
        const res = await postWithSession("/course/preview", {
          round_id: zbid,
          refresh: false,
        });
        (res.data.logs || []).forEach((msg) => log(msg));
        coursesData = res.data.courses || [];
        renderCourseTable(coursesData, true);
        courseInfo.textContent = `${coursesData.length} 门课程`;
        // Strip animation classes after entrance finishes so tab-switch doesn't replay them
        const delay = Math.min(coursesData.length, 20) * 35 + 400;
        setTimeout(() => {
          courseBody
            .querySelectorAll(".tr-appear")
            .forEach((el) => el.classList.remove("tr-appear"));
        }, delay);
        log(`公选课加载成功: ${coursesData.length} 门`);
      } catch (e) {
        courseInfo.textContent = `失败: ${e.message}`;
        log(`公选课加载失败: ${e.message}`);
      } finally {
        setLoading(btnPreview, false);
      }
    }

    selectRound.addEventListener("change", () => {
      updateRoundSelection(selectRound.value);
    });

    roundMenuController.bind();
    loginModeMenuController.bind();

    function renderCourseTable(courses, animate) {
      courseBody.innerHTML = "";
      courseEmpty.classList.toggle("hidden", courses.length > 0);
      courses.forEach((c, i) => {
        const tr = document.createElement("tr");
        tr.dataset.index = i;
        tr.innerHTML = `<td>${esc(c.kcmc)}</td><td>${esc(c.skls)}</td><td>${esc(c.sksj)}</td><td>${esc(c.skdd)}</td><td>${esc(c.xf)}</td><td>${esc(c.syrs)}</td>`;
        if (animate) {
          tr.classList.add("tr-appear");
          tr.style.setProperty("--tr-delay", `${Math.min(i, 20) * 35}ms`);
        }
        tr.addEventListener("click", () => selectCourseRow(i, tr));
        courseBody.appendChild(tr);
      });
    }

    function selectCourseRow(index, tr) {
      if (isCourseGrabRunning) {
        log("已阻止切换目标课程：当前抢课任务仍在执行");
        renderCourseGrabStatus({
          state: "info",
          badge: "执行中",
          message: "请先停止当前抢课任务，再重新选择目标课程。",
          meta: ["操作已锁定：目标课程"],
        });
        return;
      }
      $$("#courseBody tr").forEach((r) => r.classList.remove("selected"));
      tr.classList.add("selected");
      const c = coursesData[index];
      selectedCourse.textContent = `${c.kcmc} / ${c.skls} / ${c.jx02id || ""}`;
      grabKcid.value = c.jx02id || "";
      grabJx0404id.value = c.jx0404id || "";
      syncCourseActionState();
      log(`选中: ${c.kcmc}`);
    }

    if (btnCourseConnect) {
      btnCourseConnect.addEventListener("click", async () => {
        recordTelemetryModuleHit("course");
        await connectCourseFramework();
      });
    }

    btnPreview.addEventListener("click", () => fetchCourses());
    btnLoadRounds.addEventListener("click", () => loadCourseRounds());

    if (grabInterval) {
      grabInterval.addEventListener("change", () => {
        grabInterval.value = formatCourseGrabInterval(
          resolveCourseGrabIntervalValue(),
        );
        syncCourseGrabHint();
      });
    }

    // ── Grab ───────────────────────────────────────────────
    btnGrabStart.addEventListener("click", async () => {
      renderCourseGrabStatus({
        state: "muted",
        badge: "后续切片",
        message: COURSE_GRAB_DEFERRED_ACTION_MESSAGE,
        meta: [resolveCourseConnectionMessage(), "当前仅开放选课轮次与课程预览"],
      });
      log("已拦截抢课启动：该能力在当前 Course 框架切片中后置");
    });

    btnGrabStop.addEventListener("click", async () => {
      syncCourseGrabHint();
      log("已拦截抢课停止：当前切片未实现真实抢课任务");
    });

    // ── Log ────────────────────────────────────────────────
    btnClearLog.addEventListener("click", () => {
      logArea.textContent = "";
    });

    if (developerSettingsToggle) {
      developerSettingsToggle.addEventListener("change", () => {
        if (!developerOptionsUnlocked) {
          developerSettingsToggle.checked = false;
          return;
        }
        setDeveloperOptionsEnabled(developerSettingsToggle.checked);
      });
    }

    if (devMuteNoticeReminder) {
      devMuteNoticeReminder.addEventListener("change", () => {
        currentDevToolsState = normalizeDevToolsStatePayload({
          ...currentDevToolsState,
          muteNoticeReminder: devMuteNoticeReminder.checked,
        });
        persistDevToolsState();
        if (devMuteNoticeReminder.checked) hideNoticeReminderToast();
        else maybePresentNoticeReminder();
        log(
          devMuteNoticeReminder.checked
            ? "开发选项：已开启静默弹窗"
            : "开发选项：已关闭静默弹窗",
        );
      });
    }

    if (btnDevReloadUpdates) {
      btnDevReloadUpdates.addEventListener("click", () => {
        void loadUpdatesFeed({ silent: false, trigger: "manual" });
      });
    }

    if (btnUpdatesToggleFeedback) {
      btnUpdatesToggleFeedback.addEventListener("click", () => {
        const nextState = !updatesFeedbackPanelOpen;
        setUpdatesFeedbackPanelOpen(nextState);
        if (nextState) {
          syncUpdatesFeedbackActionState({ resetStatus: true });
          if (updatesFeedbackContent && !updatesFeedbackContent.value.trim()) {
            updatesFeedbackContent.focus();
          }
        }
      });
    }

    if (btnUpdatesManualRefresh) {
      btnUpdatesManualRefresh.addEventListener("click", () => {
        const cooldownRemainingMs = Math.max(
          0,
          lastManualRemoteMaintenanceAt + 5000 - Date.now(),
        );
        if (cooldownRemainingMs > 0) {
          syncUpdatesFeedStatus("5 秒内只能拉取一次公告", "warning");
          log("公告拉取过于频繁，请稍后再试");
          syncUpdatesManualRefreshActionState();
          return;
        }
        lastManualRemoteMaintenanceAt = Date.now();
        syncUpdatesManualRefreshActionState();
        void performRemoteMaintenanceSync({ silent: false, trigger: "manual" });
      });
    }

    if (btnUpdatesSubmitFeedback) {
      btnUpdatesSubmitFeedback.addEventListener("click", () => {
        void submitUpdatesFeedback();
      });
    }

    if (btnDevPreviewReminder) {
      btnDevPreviewReminder.addEventListener("click", () => {
        previewCurrentReminder();
      });
    }

    if (btnDevClearFrontendCache) {
      btnDevClearFrontendCache.addEventListener("click", () => {
        clearFrontendCacheForDev();
      });
    }

    if (trustedAccountList) {
      trustedAccountList.addEventListener("click", (event) => {
        void handleTrustedAccountChooserClick(event);
      });
    }

    if (btnTrustedAccountUsePassword) {
      btnTrustedAccountUsePassword.addEventListener("click", () => {
        showManualLoginForm({
          prefillUser: getLastAccount(),
        });
        inputPassword.focus({ preventScroll: true });
      });
    }

    selectMode.addEventListener("change", () => {
      syncLoginModeTrigger();
      syncLoginModeSettings();
      if (chipMode) chipMode.textContent = formatPreloginMode(selectMode.value);
      currentSettings.auth.prelogin_mode = normalizePreloginModeValue(
        selectMode.value,
      );
      queueSettingsSave({ silent: true });
      log(`联网认证流程已切换为: ${formatPreloginMode(selectMode.value)}`);
    });

    if (studentName) {
      studentName.addEventListener("mousedown", (event) => {
        if (nicknameEditState.active || nicknameEditState.saving) return;
        if (event.button !== 0) return;
        event.preventDefault();
      });
      studentName.addEventListener("click", () => {
        if (nicknameEditState.active || nicknameEditState.saving) return;
        startNicknameEdit();
      });
      studentName.addEventListener("keydown", (event) => {
        if (nicknameEditState.active) return;
        if (event.key !== "Enter" && event.key !== " ") return;
        event.preventDefault();
        startNicknameEdit();
      });
    }

    // ── Init ───────────────────────────────────────────────
    applyPerformanceMode();
    currentDevToolsState = normalizeDevToolsStatePayload(getDevToolsState());
    applyDevToolsVisibility();
    initializeTrustedAccountState();
    if (shouldFastRestoreOnBoot()) {
      document.body.classList.add("boot-auth-pending");
      if (loginCard) loginCard.classList.add("hidden");
      if (dashboard) dashboard.classList.add("hidden");
    }
    setUpdatesFeedbackPanelOpen(false);
    syncUpdatesManualRefreshActionState();
    syncUpdatesFeedbackActionState({ resetStatus: true });
    applyDesktopSettingsToUI(currentDesktopSettings);
    syncRuntimeWorkloadState();
    startRuntimeProtectionWatchdog();
    void checkHealth();
    const configReady = loadConfig();
    syncConnectionUI(false);
    scheduleUiIdleTask(() => {
      initLoginHeroMotionDeferred();
    }, isEffectivePerformanceLiteMode() ? 260 : 160);
    scheduleUiIdleTask(() => {
      renderWeekendIgnoreGrid();
      loginModeMenuController.refresh();
      themeController.render(saveThemePreference);
      applyDevToolsStateToUI();
      applyStoredSettings(currentSettings, { persistTheme: false });
      applyDesktopSettingsToUI(currentDesktopSettings);
      void loadDesktopSettings();
      if (!isEffectivePerformanceLiteMode()) {
        prefetchHitokoto();
      }
      updateRoundSelection("");
    }, isEffectivePerformanceLiteMode() ? 220 : 120);
    if (performanceLiteMode) {
      log("已启用轻量渲染模式");
    }
    if (runtimeProtectionMode) {
      log("已启用 WebView 保护模式，本次将以低负载方式运行。");
    }
    void restoreSessionIfPossible();
    configReady.finally(() => {
      if (currentBetaStatus.expired) {
        document.body.classList.remove("boot-auth-pending");
      }
    });
    log("前端就绪，等待登录");
  } catch (error) {
    console.error(`XKE frontend boot failed [${FRONTEND_BUILD}]:`, error);
    const loginCardEl = document.getElementById("loginCard");
    const dashboardEl = document.getElementById("dashboard");
    const loginMsgEl = document.getElementById("loginMsg");
    if (dashboardEl) dashboardEl.classList.add("hidden");
    if (loginCardEl) loginCardEl.classList.remove("hidden");
    if (loginMsgEl) {
      loginMsgEl.className = "msg error";
      loginMsgEl.style.display = "block";
      loginMsgEl.textContent = `前端启动失败（${FRONTEND_BUILD}）：${error && error.message ? error.message : "未知错误"}`;
    }
  }
})();
