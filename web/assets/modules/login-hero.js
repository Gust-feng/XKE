export function initLoginHeroMotion ({
  loginCard,
  loginHero,
  loginScene,
  inputUser,
  inputPassword,
  btnPasswordVisibility,
  rememberMe,
  resolveRememberState,
  onSubmit,
  reducedEffects = false,
}) {
  if (!loginHero || !loginScene || !inputPassword || !inputUser || !btnPasswordVisibility) return

  const clamp = (value, min = -1, max = 1) => Math.max(min, Math.min(max, value))
  const px = (value) => `${value.toFixed(2)}px`
  const queryScene = (selector) => loginScene.querySelector(selector)
  const installation = queryScene('.login-installation')
  const installationAura = queryScene('.login-installation-aura')
  const installationHaze = queryScene('.login-installation-haze')
  const installationLens = queryScene('.login-installation-lens')
  const installationRibbon = queryScene('.login-installation-ribbon')
  const installationHorizontalOrbit = queryScene('.login-installation-orbit.orbit-horizontal')
  const installationVerticalOrbit = queryScene('.login-installation-orbit.orbit-vertical')
  const installationBackPrism = queryScene('.login-installation-prism.prism-back')
  const installationFrontPrism = queryScene('.login-installation-prism.prism-front')
  const installationMirror = queryScene('.login-installation-mirror')
  const installationPlane = queryScene('.login-installation-plane')
  const installationTrace = queryScene('.login-installation-trace')
  const installationEcho = queryScene('.login-installation-echo')
  const installationSeal = queryScene('.login-installation-seal')
  const reducedMode = Boolean(reducedEffects)
  const pointer = { x: 0, y: 0 }
  const pointerTarget = { x: 0, y: 0 }
  let motionFrame = 0
  let motionPulse = 0

  const stateProfiles = {
    idle: {
      biasX: -0.04,
      biasY: -0.05,
      pointerX: 0.26,
      pointerY: 0.18,
      stage: { x: -2.8, y: -1.2, r: -0.45 },
      aura: { x: 8, y: -6 },
      lens: { x: -5, y: -4, r: -2.2, scale: 1 },
      ribbon: { x: 5, y: -6, r: -4, scale: 1 },
      horizontalOrbit: { x: 4, y: -2, r: -1.4, scaleX: 1 },
      verticalOrbit: { x: 5, y: -3, scaleY: 1 },
      backPrism: { x: 7, y: -5, r: -1.2, scale: 1 },
      frontPrism: { x: 5, y: -4, r: -0.9, scale: 1 },
      mirror: { x: -3, y: 1.6, r: 0.28 },
      plane: { x: -3, y: 1.3, r: 0.32, scale: 1 },
      trace: { x: -1.4, y: 0.8, scaleX: 1 },
      seal: { x: 2.6, y: 0, r: -1.2, scale: 1 },
    },
    account: {
      biasX: 0.12,
      biasY: -0.03,
      pointerX: 0.18,
      pointerY: 0.14,
      stage: { x: -1.4, y: -0.8, r: -0.2 },
      aura: { x: 10, y: -5 },
      lens: { x: -3.4, y: -2.6, r: -1.2, scale: 1.01 },
      ribbon: { x: 6, y: -3.6, r: -2.4, scale: 1.01 },
      horizontalOrbit: { x: 4.6, y: -1.4, r: -1, scaleX: 1.01 },
      verticalOrbit: { x: 5, y: -2, scaleY: 1.01 },
      backPrism: { x: 8, y: -4.6, r: -0.7, scale: 1.01 },
      frontPrism: { x: 6, y: -3.4, r: -0.45, scale: 1.02 },
      mirror: { x: -2.4, y: 1.2, r: 0.18 },
      plane: { x: -2.2, y: 1, r: 0.18, scale: 1.01 },
      trace: { x: -0.8, y: 0, scaleX: 1.03 },
      seal: { x: 2.2, y: -0.6, r: -0.5, scale: 1.01 },
    },
    'password-reveal': {
      biasX: 0.18,
      biasY: -0.01,
      pointerX: 0.16,
      pointerY: 0.12,
      stage: { x: -0.6, y: 0, r: 0.06 },
      aura: { x: 12, y: -2 },
      lens: { x: -1.8, y: -1.6, r: -0.4, scale: 1.03 },
      ribbon: { x: 7.2, y: -1.8, r: -0.8, scale: 1.02 },
      horizontalOrbit: { x: 5.6, y: 0, r: -0.5, scaleX: 1.03 },
      verticalOrbit: { x: 5.6, y: -1.2, scaleY: 1.02 },
      backPrism: { x: 8.4, y: -2.6, r: -0.35, scale: 1.02 },
      frontPrism: { x: 6.4, y: -1.4, r: -0.18, scale: 1.03 },
      mirror: { x: -1.4, y: 0.8, r: 0.12 },
      plane: { x: -1.2, y: 0.8, r: 0.1, scale: 1.01 },
      trace: { x: 1.2, y: -0.4, scaleX: 1.05 },
      seal: { x: 1.8, y: -1.2, r: -0.24, scale: 1.04 },
    },
    'password-mask': {
      fixedX: -0.16,
      fixedY: 0.02,
      stage: { x: 1, y: 0.6, r: 0.25 },
      aura: { x: -7, y: 4 },
      lens: { x: 4.4, y: 3.2, r: 1.4, scale: 0.99 },
      ribbon: { x: -4, y: 4.2, r: 2.6, scale: 0.98 },
      horizontalOrbit: { x: -3.8, y: 1.8, r: 0.8, scaleX: 0.98 },
      verticalOrbit: { x: -3.4, y: 1.4, scaleY: 0.99 },
      backPrism: { x: -4.2, y: 3.4, r: 0.7, scale: 0.99 },
      frontPrism: { x: -3.2, y: 2.4, r: 0.5, scale: 0.99 },
      mirror: { x: 2.4, y: 0.8, r: -0.18 },
      plane: { x: 2, y: 0.8, r: -0.22, scale: 1 },
      trace: { x: 1.8, y: 0.2, scaleX: 0.98 },
      seal: { x: 1.4, y: -0.4, r: 0.7, scale: 1.02 },
    },
  }

  const accentPoses = {
    haze: { x: 4, y: -2.4, r: -0.5, rx: 0.2, ry: 0.6, depth: 2 },
    echo: { x: -3, y: 1.8, r: 0.28, rx: -0.12, ry: -0.4, depth: 2 },
  }

  function applyTransform (element, pose, x, y) {
    if (!element || !pose) return
    const scaleX = pose.scaleX == null ? (pose.scale == null ? 1 : pose.scale) : pose.scaleX
    const scaleY = pose.scaleY == null ? (pose.scale == null ? 1 : pose.scale) : pose.scaleY
    const translateX = x * (pose.x || 0)
    const translateY = y * (pose.y || 0)
    const magnitude = Math.min(1.25, Math.hypot(x, y))
    const translateZ = (pose.z || 0) + magnitude * (pose.depth || 0)
    const rotate = (x * (pose.r || 0)) + (y * (pose.rY || 0))
    const rotateX = -y * (pose.rx || 0)
    const rotateY = x * (pose.ry || 0)
    element.style.transform = `translate3d(${px(translateX)}, ${px(translateY)}, ${px(translateZ)}) rotateX(${rotateX.toFixed(2)}deg) rotateY(${rotateY.toFixed(2)}deg) rotate(${rotate.toFixed(2)}deg) scale(${scaleX.toFixed(3)}, ${scaleY.toFixed(3)})`
  }

  function syncSceneLighting (x, y) {
    const specularX = 54 + (x * 8) + (motionPulse * 2.4)
    const specularY = 30 + (y * 7) - (motionPulse * 1.6)
    const energy = Math.min(1, Math.hypot(x, y))
    const paperLift = Math.min(1, 0.16 + (motionPulse * 0.46) + (Math.max(0, -y) * 0.14))
    const mouseGlow = Math.min(0.36, 0.08 + (energy * 0.14) + (motionPulse * 0.1))
    loginHero.style.setProperty('--login-specular-x', `${specularX.toFixed(2)}%`)
    loginHero.style.setProperty('--login-specular-y', `${specularY.toFixed(2)}%`)
    loginHero.style.setProperty('--login-shadow-shift-x', `${(x * 8).toFixed(2)}px`)
    loginHero.style.setProperty('--login-shadow-shift-y', `${(y * 6).toFixed(2)}px`)
    loginHero.style.setProperty('--login-motion-energy', energy.toFixed(3))
    loginHero.style.setProperty('--login-drift-x', `${(x * 6).toFixed(2)}px`)
    loginHero.style.setProperty('--login-drift-y', `${(y * 5).toFixed(2)}px`)
    loginHero.style.setProperty('--login-paper-lift', paperLift.toFixed(3))
    loginHero.style.setProperty('--login-mouse-glow', mouseGlow.toFixed(3))
  }

  function getHeroState () {
    if (document.activeElement === inputUser) return 'account'
    if (document.activeElement === inputPassword) {
      return inputPassword.type === 'password' ? 'password-mask' : 'password-reveal'
    }
    return 'idle'
  }

  function syncPasswordToggleUI () {
    const revealed = inputPassword.type === 'text'
    btnPasswordVisibility.setAttribute('aria-pressed', revealed ? 'true' : 'false')
    btnPasswordVisibility.setAttribute('aria-label', revealed ? '隐藏密码' : '显示密码')
    btnPasswordVisibility.setAttribute('title', revealed ? '隐藏密码' : '显示密码')
  }

  function syncCredentialProgress () {
    const hasAccount = Boolean(inputUser.value.trim())
    const hasPassword = Boolean(inputPassword.value)
    if (hasAccount && hasPassword) return 'ready'
    if (hasPassword) return 'password'
    if (hasAccount) return 'account'
    return 'empty'
  }

  function syncLoginSurfaceState (state, progress) {
    const rememberState = typeof resolveRememberState === 'function'
      ? (resolveRememberState() ? 'true' : 'false')
      : (rememberMe && rememberMe.checked ? 'true' : 'false')
    const handoffState = progress === 'account' || (state !== 'idle' && progress === 'ready') ? 'primed' : 'idle'
    loginHero.dataset.loginProgress = progress
    loginHero.dataset.loginFlow = handoffState
    loginHero.dataset.remember = rememberState
    if (loginCard) {
      loginCard.dataset.loginState = state
      loginCard.dataset.loginProgress = progress
      loginCard.dataset.loginFlow = handoffState
      loginCard.dataset.remember = rememberState
    }
  }

  function resolveMotionVector () {
    const state = getHeroState()
    const profile = stateProfiles[state]
    if (state === 'password-mask') {
      return {
        state,
        profile,
        x: clamp(profile.fixedX + (pointer.x * 0.12)),
        y: clamp(profile.fixedY + (pointer.y * 0.08)),
      }
    }
    return {
      state,
      profile,
      x: clamp((pointer.x * profile.pointerX) + profile.biasX),
      y: clamp((pointer.y * profile.pointerY) + profile.biasY),
    }
  }

  function applySceneMotion (state, profile, x, y) {
    loginHero.dataset.loginState = state
    loginHero.classList.toggle('is-password-focus', state === 'password-mask')
    loginHero.classList.toggle('is-password-revealed', state === 'password-reveal')
    loginHero.classList.toggle('is-field-focused', state !== 'idle')

    syncSceneLighting(x, y)
    applyTransform(installation, { ...profile.stage, x: (profile.stage.x || 0) * 0.18, y: (profile.stage.y || 0) * 0.18, r: (profile.stage.r || 0) * 0.14, rx: 0.28, ry: 0.72, depth: 1.4 }, x, y)
    applyTransform(installationAura, { ...profile.aura, x: (profile.aura.x || 0) * 0.24, y: (profile.aura.y || 0) * 0.24, rx: 0.12, ry: 0.4, depth: 0.8 }, x, y)
    applyTransform(installationHaze, accentPoses.haze, x, y)
    applyTransform(installationLens, { ...profile.lens, rx: 0.22, ry: 0.62, depth: 1 }, x, y)
    applyTransform(installationRibbon, { ...profile.ribbon, x: (profile.ribbon.x || 0) * 0.3, y: (profile.ribbon.y || 0) * 0.3, rx: 0.12, ry: 0.44, depth: 0.6 }, x, y)
    applyTransform(installationHorizontalOrbit, { ...profile.horizontalOrbit, x: (profile.horizontalOrbit.x || 0) * 0.26, y: (profile.horizontalOrbit.y || 0) * 0.26, rx: 0.08, ry: 0.28, depth: 0.4 }, x, y)
    applyTransform(installationVerticalOrbit, { ...profile.verticalOrbit, x: (profile.verticalOrbit.x || 0) * 0.26, y: (profile.verticalOrbit.y || 0) * 0.26, rx: 0.08, ry: 0.28, depth: 0.4 }, x, y)
    applyTransform(installationBackPrism, { ...profile.backPrism, x: (profile.backPrism.x || 0) * 0.4, y: (profile.backPrism.y || 0) * 0.4, rx: 0.34, ry: 0.8, depth: 1.6 }, x, y)
    applyTransform(installationFrontPrism, { ...profile.frontPrism, x: (profile.frontPrism.x || 0) * 0.48, y: (profile.frontPrism.y || 0) * 0.48, rx: 0.5, ry: 1.08, depth: 2.4 }, x, y)
    applyTransform(installationMirror, { ...profile.mirror, x: (profile.mirror.x || 0) * 0.3, y: (profile.mirror.y || 0) * 0.3, rx: -0.16, ry: -0.34, depth: 0.8 }, x, y)
    applyTransform(installationPlane, { ...profile.plane, x: (profile.plane.x || 0) * 0.26, y: (profile.plane.y || 0) * 0.26, rx: -0.24, ry: -0.42, depth: 1.2 }, x, y)
    applyTransform(installationTrace, { ...profile.trace, x: (profile.trace.x || 0) * 0.3, y: (profile.trace.y || 0) * 0.3, rx: -0.08, ry: 0.18, depth: 0.6 }, x, y)
    applyTransform(installationEcho, accentPoses.echo, x, y)
    applyTransform(installationSeal, { ...profile.seal, x: (profile.seal.x || 0) * 0.24, y: (profile.seal.y || 0) * 0.24, rx: 0.3, ry: 0.8, depth: 1.6 }, x, y)
  }

  function renderHeroScene () {
    const { state, profile, x, y } = resolveMotionVector()
    syncPasswordToggleUI()
    syncLoginSurfaceState(state, syncCredentialProgress())
    loginHero.dataset.loginState = state
    loginHero.classList.toggle('is-password-focus', state === 'password-mask')
    loginHero.classList.toggle('is-password-revealed', state === 'password-reveal')
    loginHero.classList.toggle('is-field-focused', state !== 'idle')
    if (reducedMode) return
    applySceneMotion(state, profile, x, y)
  }

  function ensureMotionFrame () {
    if (reducedMode) return
    if (motionFrame) return
    motionFrame = window.requestAnimationFrame(stepMotion)
  }

  function stepMotion () {
    const deltaX = pointerTarget.x - pointer.x
    const deltaY = pointerTarget.y - pointer.y
    motionPulse = Math.max(Math.min(1, Math.hypot(deltaX, deltaY) * 1.8), motionPulse * 0.9)
    pointer.x += deltaX * 0.12
    pointer.y += deltaY * 0.12
    renderHeroScene()

    if (Math.abs(deltaX) < 0.0015 && Math.abs(deltaY) < 0.0015 && motionPulse < 0.015) {
      pointer.x = pointerTarget.x
      pointer.y = pointerTarget.y
      motionPulse = 0
      renderHeroScene()
      motionFrame = 0
      return
    }

    motionFrame = window.requestAnimationFrame(stepMotion)
  }

  function resetPointer () {
    if (reducedMode) return
    pointerTarget.x = 0
    pointerTarget.y = 0
    ensureMotionFrame()
  }

  function updatePointer (event) {
    if (reducedMode) return
    if (!loginCard || loginCard.classList.contains('hidden')) return
    const rect = loginScene.getBoundingClientRect()
    if (!rect.width || !rect.height) return
    const targetX = rect.left + rect.width / 2
    const targetY = rect.top + rect.height * 0.48
    pointerTarget.x = clamp((event.clientX - targetX) / (rect.width * 0.74))
    pointerTarget.y = clamp((event.clientY - targetY) / (rect.height * 0.7))
    ensureMotionFrame()
  }

  function togglePasswordVisibility () {
    const nextType = inputPassword.type === 'password' ? 'text' : 'password'
    const selectionStart = inputPassword.selectionStart
    const selectionEnd = inputPassword.selectionEnd
    inputPassword.type = nextType
    syncPasswordToggleUI()
    inputPassword.focus({ preventScroll: true })
    try {
      if (selectionStart != null && selectionEnd != null) {
        inputPassword.setSelectionRange(selectionStart, selectionEnd)
      }
    } catch {
      /* ignore */
    }
    renderHeroScene()
  }

  function handleUserEnter (event) {
    if (event.key !== 'Enter') return
    event.preventDefault()
    inputPassword.focus({ preventScroll: true })
  }

  function handlePasswordEnter (event) {
    if (event.key !== 'Enter') return
    event.preventDefault()
    if (typeof onSubmit === 'function') onSubmit()
  }

  if (!reducedMode) {
    document.addEventListener('pointermove', updatePointer, { passive: true })
    loginCard.addEventListener('pointerleave', resetPointer)
    window.addEventListener('blur', resetPointer)
  }
  inputUser.addEventListener('focus', renderHeroScene)
  inputUser.addEventListener('blur', renderHeroScene)
  inputUser.addEventListener('input', renderHeroScene)
  inputUser.addEventListener('keydown', handleUserEnter)
  inputPassword.addEventListener('focus', renderHeroScene)
  inputPassword.addEventListener('blur', renderHeroScene)
  inputPassword.addEventListener('input', renderHeroScene)
  inputPassword.addEventListener('keydown', handlePasswordEnter)
  if (rememberMe) rememberMe.addEventListener('change', renderHeroScene)
  btnPasswordVisibility.addEventListener('mousedown', (event) => event.preventDefault())
  btnPasswordVisibility.addEventListener('click', togglePasswordVisibility)

  syncPasswordToggleUI()
  renderHeroScene()
}
