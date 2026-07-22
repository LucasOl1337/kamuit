#!/usr/bin/env node
/**
 * Install / refresh KamuiT agent Stop hooks for Grok + Claude.
 * Idempotent. Safe to run on every KamuiT launch.
 * NOTE: uses the marker 'kamuit-ready-signal.mjs' so it never clobbers
 * TerminalDE's own hooks (and vice-versa).
 */
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const home = os.homedir();
const kamuitDir = path.join(home, '.kamuit');
const signalScriptSrc = path.join(__dirname, 'kamuit-ready-signal.mjs');
const signalScriptDst = path.join(kamuitDir, 'kamuit-ready-signal.mjs');

function ensureSignalScript() {
  fs.mkdirSync(kamuitDir, { recursive: true });
  fs.mkdirSync(path.join(kamuitDir, 'signals'), { recursive: true });
  if (!fs.existsSync(signalScriptSrc)) {
    throw new Error(`missing signal script: ${signalScriptSrc}`);
  }
  fs.copyFileSync(signalScriptSrc, signalScriptDst);
  return signalScriptDst;
}

function nodeCommand(scriptPath) {
  return `node "${scriptPath.replace(/"/g, '\\"')}"`;
}

function installGrokHook(scriptPath) {
  const hooksDir = path.join(home, '.grok', 'hooks');
  fs.mkdirSync(hooksDir, { recursive: true });
  const file = path.join(hooksDir, 'kamuit-ready.json');
  const doc = {
    // Owned by KamuiT — safe to overwrite this specific file.
    _kamuit: true,
    hooks: {
      Stop: [
        {
          hooks: [
            {
              type: 'command',
              command: nodeCommand(scriptPath),
              timeout: 5
            }
          ]
        }
      ]
    }
  };
  fs.writeFileSync(file, `${JSON.stringify(doc, null, 2)}\n`);
  return file;
}

function installClaudeHook(scriptPath) {
  const settingsPath = path.join(home, '.claude', 'settings.json');
  let settings = {};
  if (fs.existsSync(settingsPath)) {
    try {
      settings = JSON.parse(fs.readFileSync(settingsPath, 'utf8'));
    } catch {
      settings = {};
    }
  }
  if (!settings.hooks || typeof settings.hooks !== 'object') settings.hooks = {};
  if (!Array.isArray(settings.hooks.Stop)) settings.hooks.Stop = [];

  const cmd = nodeCommand(scriptPath);
  const marker = 'kamuit-ready-signal.mjs';

  // Remove previous KamuiT Stop entries, then append one clean entry.
  settings.hooks.Stop = settings.hooks.Stop.filter((entry) => {
    const hooks = entry && Array.isArray(entry.hooks) ? entry.hooks : [];
    return !hooks.some((h) => h && typeof h.command === 'string' && h.command.includes(marker));
  });

  settings.hooks.Stop.push({
    hooks: [
      {
        type: 'command',
        command: cmd,
        timeout: 5
      }
    ]
  });

  fs.mkdirSync(path.dirname(settingsPath), { recursive: true });
  fs.writeFileSync(settingsPath, `${JSON.stringify(settings, null, 2)}\n`);
  return settingsPath;
}

function installPiExtension() {
  const extDir = path.join(home, '.pi', 'agent', 'extensions');
  const src = path.join(__dirname, 'kamuit-pi-extension.ts');
  if (!fs.existsSync(src)) return null;
  fs.mkdirSync(extDir, { recursive: true });
  const dst = path.join(extDir, 'kamuit-ready.ts');
  fs.copyFileSync(src, dst);
  return dst;
}

function main() {
  const scriptPath = ensureSignalScript();
  const grok = installGrokHook(scriptPath);
  const claude = installClaudeHook(scriptPath);
  const pi = installPiExtension();
  console.log(JSON.stringify({ ok: true, scriptPath, grok, claude, pi }, null, 2));
}

main();
