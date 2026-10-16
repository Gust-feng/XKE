import { OneLayout } from './editions/one/OneLayout'
import { Sheet, Toast } from './components/Sheet'
import { AuthPanel } from './components/AuthPanel'
import { useStore } from './lib/store'
import { CoursePop } from './editions/one/CoursePop'
import { NetworkConfigPop } from './editions/one/NetworkConfigPop'
import { useDesktopBridge } from './lib/desktop'
import { WindowControls } from './components/WindowControls'

/** Product v1 uses the accepted 合成 v7 edition and selected API composition. */
export default function App() {
  const { phase, notify } = useStore()
  const { bridge, maximized } = useDesktopBridge()
  return <>
    {bridge && <WindowControls bridge={bridge} maximized={maximized} onError={notify} />}
    {phase === 'ready' && <><OneLayout desktop={!!bridge} /><Sheet /><CoursePop /><NetworkConfigPop /></>}
    <AuthPanel /><Toast />
  </>
}
