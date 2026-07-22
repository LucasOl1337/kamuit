#!/usr/bin/env node
/**
 * Lifecycle hook sink for KamuiT ready sounds.
 *
 * Called by Grok/Claude-compatible Stop hooks when an agent turn ends.
 * Drops a one-shot signal file with the KamuiT tab number; the KamuiT app
 * watches that directory and plays SoundEffects/Terminal{N}.mp3.
 *
 * Env (injected into every KamuiT pwsh session):
 *   KAMUIT=1        — marks the session as KamuiT-owned
 *   KAMUIT_TAB      — 1-based tab slot that launched the agent
 */
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';

const home = os.homedir();
const signalsDir = process.env.KAMUIT_SIGNALS_DIR || path.join(home, '.kamuit', 'signals');

function readStdin() {
  return new Promise((resolve) => {
    let body = '';
    if (process.stdin.isTTY) {
      resolve('');
      return;
    }
    process.stdin.setEncoding('utf8');
    process.stdin.on('data', (chunk) => {
      body += chunk;
      if (body.length > 256 * 1024) body = body.slice(-256 * 1024);
    });
    process.stdin.on('end', () => resolve(body));
    process.stdin.on('error', () => resolve(body));
    setTimeout(() => resolve(body), 400);
  });
}

function safeJson(text) {
  if (!text || !String(text).trim()) return {};
  try {
    return JSON.parse(text);
  } catch {
    return {};
  }
}

function eventName(payload) {
  return (
    process.env.GROK_HOOK_EVENT ||
    process.env.CLAUDE_HOOK_EVENT ||
    payload.hook_event_name ||
    payload.hookEventName ||
    payload.event ||
    ''
  );
}

async function main() {
  const tab = String(process.env.KAMUIT_TAB || '').trim();
  if (process.env.KAMUIT !== '1' || !tab) {
    process.exit(0);
    return;
  }

  const raw = await readStdin();
  const payload = safeJson(raw);
  const event = String(eventName(payload)).trim().toLowerCase().replace(/-/g, '_');

  // Only the top-level agent Stop returns control to the user.
  if (event !== 'stop') {
    process.exit(0);
    return;
  }

  const signal = {
    v: 1,
    at: Date.now(),
    event,
    tab: Number(tab),
    agent: process.env.GROK_HOOK_EVENT ? 'grok' : process.env.CLAUDE_HOOK_EVENT ? 'claude' : 'hook',
    cwd: process.env.GROK_WORKSPACE_ROOT || process.env.CLAUDE_PROJECT_DIR || payload.cwd || process.cwd()
  };

  fs.mkdirSync(signalsDir, { recursive: true });
  const name = `${Date.now()}-${Math.random().toString(36).slice(2, 8)}.json`;
  const file = path.join(signalsDir, name);
  // write tmp then rename for atomic watchers
  const tmp = `${file}.tmp`;
  fs.writeFileSync(tmp, JSON.stringify(signal));
  fs.renameSync(tmp, file);
  process.exit(0);
}

main().catch(() => process.exit(0));
