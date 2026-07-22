// kamuit-pi-extension: drop agent-ready signal for KamuiT when a Pi turn ends.
// Passive by design: sync write of a tiny file, no network, no stdin reads —
// Pi awaits extension handlers, so this stays off the TUI critical path.
// Only active inside KamuiT tabs (KAMUIT=1 + KAMUIT_TAB injected by the app).
export default function (pi): void {
  pi.on('agent_end', () => {
    if (process.env.KAMUIT !== '1') return
    const tab = Number(process.env.KAMUIT_TAB || '0')
    if (!tab) return
    try {
      const fs = require('fs')
      const os = require('os')
      const path = require('path')
      const dir = process.env.KAMUIT_SIGNALS_DIR || path.join(os.homedir(), '.kamuit', 'signals')
      fs.mkdirSync(dir, { recursive: true })
      const file = path.join(dir, `${Date.now()}-${Math.random().toString(36).slice(2, 8)}.json`)
      const tmp = `${file}.tmp`
      fs.writeFileSync(tmp, JSON.stringify({
        v: 1,
        at: Date.now(),
        event: 'stop',
        tab,
        agent: 'pi',
        cwd: process.cwd(),
      }))
      fs.renameSync(tmp, file)
    } catch {
      // best-effort: sinal perdido nunca pode quebrar o turno do Pi
    }
  })
}
