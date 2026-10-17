/** Explicit preview bootstrap; never included in the runtime entry. */
import { createRoot } from 'react-dom/client'
import { MotionConfig } from 'motion/react'
import { StoreProvider } from '../lib/store'
import { bootTheme } from '../lib/themes'
import { UpdateDemo } from './UpdateDemo'
import '../styles/tokens.css'
import '../styles/global.css'
import './update-preview.css'

bootTheme()
const root = createRoot(document.getElementById('root')!)
if (__XKE_PREVIEW__) {
  root.render(<MotionConfig reducedMotion="user"><StoreProvider><UpdateDemo /></StoreProvider></MotionConfig>)
} else {
  root.render(<p>此页面仅供显式 preview 模式查看。</p>)
}
