// kamuit-pi-extension: drop agent-ready signal for KamuiT when a Pi turn ends.
// Passive by design: sync write of a tiny file, no network, no stdin reads —
// Pi awaits extension handlers, so this stays off the TUI critical path.
// Only active inside KamuiT tabs (KAMUIT=1 + KAMUIT_TAB_ID / KAMUIT_TAB).
export default function (pi): void {
  pi.on('agent_end', () => {
    if (process.env.KAMUIT !== '1') return
    const tabId = String(process.env.KAMUIT_TAB_ID || '').trim()
    const tab = Number(process.env.KAMUIT_TAB || '0')
    if (!tabId && !tab) return
    try {
      const fs = require('fs')
      const os = require('os')
      const path = require('path')
      const dir = process.env.KAMUIT_SIGNALS_DIR || path.join(os.homedir(), '.kamuit', 'signals')
      fs.mkdirSync(dir, { recursive: true })
      const file = path.join(dir, `${Date.now()}-${Math.random().toString(36).slice(2, 8)}.json`)
      const tmp = `${file}.tmp`
      const signal: Record<string, unknown> = {
        v: 2,
        at: Date.now(),
        event: 'stop',
        agent: 'pi',
        cwd: process.cwd(),
      }
      if (tabId) signal.tabId = tabId
      if (tab) signal.tab = tab
      fs.writeFileSync(tmp, JSON.stringify(signal))
      fs.renameSync(tmp, file)
    } catch {
      // best-effort: sinal perdido nunca pode quebrar o turno do Pi
    }
  })
}
