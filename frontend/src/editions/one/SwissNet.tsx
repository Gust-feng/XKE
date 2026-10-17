import { useEffect, useState, type CSSProperties } from 'react'
import { motion, useReducedMotion } from 'motion/react'
import { useStore } from '../../lib/store-context'
import { Ticker } from './parts'
import { DETENT } from './presentation'
import { NetField } from './NetField'

const d = (i: number) => ({ '--i': i }) as CSSProperties
const LETTERS = 'ABCDEFGHIJKLMNOPQRSTUVWXYZ'

function useScramble(active: boolean, length: number) {
  const [value, setValue] = useState('')
  useEffect(() => {
    if (!active) return
    let step = 0
    const timer = setInterval(() => {
      step++
      setValue(Array.from({ length }, (_, i) => LETTERS[(step * 7 + i * 11 + step * i % 5) % LETTERS.length]).join(''))
    }, 55)
    return () => clearInterval(timer)
  }, [active, length])
  return value
}

/**
 * 校园网页面只回答一件事：当前是否已经能上网。
 * 点阵保留唯一的时间展示，左栏只放连接配置，主页不重复账号、IP或计时，
 * 不用空的“内网/外网”卡片把同一个状态重复展示两遍。
 */
export function SwissNet() {
  const { net, netBusy, netError, toggleNet, user } = useStore()
  const online = !!net?.online
  const reduce = useReducedMotion()
  const position = netBusy ? '25%' : online ? '50%' : '0%'
  const intranet = net?.intranet ?? net?.campusConnected ?? true
  const internet = net?.internet ?? online
  const status = netBusy ? online ? '正在断开校园网' : '正在连接校园网' : !net ? netError ? '网络状态读取失败' : '正在读取网络状态' : online ? '校园网已连接' : '校园网未连接'
  const word = net ? online ? 'ONLINE' : 'OFFLINE' : netError ? 'UNKNOWN' : 'CHECKING'
  const scramble = useScramble(netBusy && !reduce, online ? 7 : 6)
  const busyWord = reduce ? 'WAITING' : scramble || 'WAITING'
  const hint = !net ? netError ? '状态未知' : '读取中' : !intranet ? '连不上校园网' : !internet ? '需要认证' : ''

  return (
    <div className={`sw-net sw-grid one-net ${online ? 'is-on' : 'is-off'}${!net ? ' is-unknown' : ''}${netBusy ? ' is-busy' : ''}`} aria-busy={netBusy}>
      <button
        className="one-rock sw-in"
        style={d(0)}
        onClick={toggleNet}
        disabled={netBusy || !user || online && net?.disconnectSupported === false}
        role="switch"
        aria-checked={online}
        aria-busy={netBusy}
        aria-label={online ? '校园网已连接' : '连接校园网'}
      >
        <span className="one-rock-l">断开</span>
        <span className="one-rock-l">连接</span>
        <motion.span className="one-rock-cap" initial={false} animate={{ left: position }} transition={reduce ? { duration: 0 } : DETENT}>
          <span>{netBusy ? (online ? '断开中' : '认证中') : online ? '已连接' : '未连接'}</span>
        </motion.span>
      </button>

      <div className="one-field-wrap sw-in" style={d(4)}>
        <NetField mode={netBusy ? 'busy' : online ? 'on' : 'off'} />
      </div>

      <div className="one-status-note sw-in" style={d(5)}>
        {hint && !netBusy && <i>{hint}</i>}
      </div>

      {/* Every state has a word, including the first animation frame; unknown is not offline. */}
      <div className="sw-word sw-giant" aria-hidden="true">
        {netBusy
          ? <span className="tk">{busyWord.split('').map((char, i) => <span key={i} className="tk-c"><span>{char}</span></span>)}</span>
          : <Ticker key={word} text={word} step={28} />}
      </div>

      {netError && <div className="runtime-net-actions"><span className="runtime-error" role="alert">{netError}</span></div>}

      {/* 大号状态字作为视觉表达，中文状态通过独立区域播报。 */}
      <div className="sr-only" role="status" aria-live="polite"><span>{status}</span><span>内网{net ? intranet ? '已连接' : '未连接' : '状态未知'}</span><span>外网{net ? internet ? '已连接' : '未连接' : '状态未知'}</span></div>
    </div>
  )
}
