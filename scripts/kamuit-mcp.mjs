#!/usr/bin/env node
/**
 * Minimal MCP server for KamuiT — tools wrap the `kamuit` CLI / named pipe.
 *
 * Run (stdio):
 *   node scripts/kamuit-mcp.mjs
 *
 * Grok config.toml example:
 *   [mcp_servers.kamuit]
 *   command = "node"
 *   args = ["C:\\Projetos\\KamuiT\\scripts\\kamuit-mcp.mjs"]
 */
import { spawn } from "node:child_process";
import { createInterface } from "node:readline";
import { homedir, platform } from "node:os";
import { join } from "node:path";
import { existsSync } from "node:fs";
import net from "node:net";

const KAMUIT_PS1_CANDIDATES = [
  join(homedir(), ".local", "bin", "kamuit.ps1"),
  "C:\\Projetos\\KamuiT\\scripts\\kamuit.ps1",
  join(homedir(), ".kamuit", "kamuit.ps1"),
];

function linuxSock() {
  return join(process.env.XDG_RUNTIME_DIR || "/tmp", "kamuit.sock");
}

function findKamuitPs1() {
  for (const p of KAMUIT_PS1_CANDIDATES) {
    if (existsSync(p)) return p;
  }
  return KAMUIT_PS1_CANDIDATES[0];
}

function sendUnix(req, timeoutMs = 15000) {
  return new Promise((resolve, reject) => {
    const sock = net.createConnection(linuxSock());
    const t = setTimeout(() => {
      sock.destroy();
      reject(new Error(`kamuit timeout after ${timeoutMs}ms`));
    }, timeoutMs);
    let buf = "";
    sock.setEncoding("utf8");
    sock.on("data", (d) => {
      buf += d;
      if (buf.includes("\n")) {
        clearTimeout(t);
        sock.end();
        try {
          resolve(JSON.parse(buf.trim().split(/\r?\n/).filter(Boolean).pop()));
        } catch {
          resolve({ ok: false, raw: buf });
        }
      }
    });
    sock.on("error", (e) => {
      clearTimeout(t);
      reject(e);
    });
    sock.on("connect", () => sock.write(JSON.stringify(req) + "\n"));
  });
}

function argsToRequest(args) {
  const req = { op: args[0] };
  for (let i = 1; i < args.length; i++) {
    const a = args[i];
    if ((a === "-C" || a === "--cwd") && args[i + 1]) req.cwd = args[++i];
    else if ((a === "-n" || a === "--count") && args[i + 1]) req.count = Number(args[++i]);
    else if ((a === "-Slot" || a === "-s") && args[i + 1]) req.slot = Number(args[++i]);
    else if ((a === "-Text" || a === "-t") && args[i + 1]) req.text = args[++i];
    else if (a === "-Enter") req.enter = true;
    else if (a === "--no-show") req.show = false;
    else if ((req.op === "open" || req.op === "new") && !req.agent) req.agent = a;
    else if ((req.op === "focus" || req.op === "close") && /^\d+$/.test(a)) req.slot = Number(a);
    else if ((req.op === "focus" || req.op === "close") && !req.id) req.id = a;
  }
  if (req.op === "open") req.show = req.show !== false;
  return req;
}

function runKamuit(args, timeoutMs = 15000) {
  if (platform() !== "win32") {
    return sendUnix(argsToRequest(args), timeoutMs);
  }
  return new Promise((resolve, reject) => {
    const ps1 = findKamuitPs1();
    const child = spawn(
      "pwsh",
      ["-NoProfile", "-File", ps1, ...args, "-Json"],
      { windowsHide: true, stdio: ["ignore", "pipe", "pipe"] }
    );
    let out = "";
    let err = "";
    const t = setTimeout(() => {
      child.kill();
      reject(new Error(`kamuit timeout after ${timeoutMs}ms`));
    }, timeoutMs);
    child.stdout.on("data", (d) => (out += d.toString()));
    child.stderr.on("data", (d) => (err += d.toString()));
    child.on("error", (e) => {
      clearTimeout(t);
      reject(e);
    });
    child.on("close", (code) => {
      clearTimeout(t);
      if (code !== 0 && !out.trim()) {
        reject(new Error(err.trim() || `kamuit exit ${code}`));
        return;
      }
      try {
        resolve(JSON.parse(out.trim().split(/\r?\n/).filter(Boolean).pop()));
      } catch {
        resolve({ ok: code === 0, raw: out, stderr: err });
      }
    });
  });
}

const TOOLS = [
  {
    name: "kamuit_open",
    description:
      "Open one or more KamuiT tabs already launching an agent TUI (grok, claude, codex, pi, or shell). Agent-first workspace control.",
    inputSchema: {
      type: "object",
      properties: {
        agent: {
          type: "string",
          description: "grok | claude | codex | pi | shell",
          default: "grok",
        },
        cwd: {
          type: "string",
          description: "Working directory for the tab(s). Default C:\\projetos",
        },
        count: {
          type: "integer",
          description: "How many tabs to open (1-9)",
          default: 1,
          minimum: 1,
          maximum: 9,
        },
      },
    },
  },
  {
    name: "kamuit_list",
    description: "List open KamuiT tabs (slot, agent, title, cwd) and limbo.",
    inputSchema: { type: "object", properties: {} },
  },
  {
    name: "kamuit_focus",
    description: "Focus a KamuiT tab by 1-based slot or tab id, and show the window.",
    inputSchema: {
      type: "object",
      properties: {
        slot: { type: "integer", description: "1-based tab slot" },
        id: { type: "string", description: "Stable tab GUID" },
      },
    },
  },
  {
    name: "kamuit_type",
    description:
      "Type text into a KamuiT tab PTY (as if the user typed). Optional Enter.",
    inputSchema: {
      type: "object",
      properties: {
        text: { type: "string" },
        slot: { type: "integer" },
        enter: { type: "boolean", default: false },
      },
      required: ["text"],
    },
  },
  {
    name: "kamuit_show",
    description: "Summon/show the KamuiT window to the foreground.",
    inputSchema: { type: "object", properties: {} },
  },
];

async function callTool(name, args = {}) {
  switch (name) {
    case "kamuit_open": {
      const agent = args.agent || "grok";
      const cli = ["open", agent, "-n", String(args.count || 1)];
      if (args.cwd) cli.push("-C", args.cwd);
      return runKamuit(cli);
    }
    case "kamuit_list":
      return runKamuit(["list"]);
    case "kamuit_focus": {
      const cli = ["focus"];
      if (args.slot != null) cli.push(String(args.slot));
      else if (args.id) cli.push(String(args.id));
      else throw new Error("slot or id required");
      return runKamuit(cli);
    }
    case "kamuit_type": {
      const cli = ["type", "-Text", String(args.text ?? "")];
      if (args.slot != null) cli.push("-Slot", String(args.slot));
      if (args.enter) cli.push("-Enter");
      return runKamuit(cli);
    }
    case "kamuit_show":
      return runKamuit(["show"]);
    default:
      throw new Error(`unknown tool: ${name}`);
  }
}

// --- tiny MCP stdio (JSON-RPC 2.0 subset) -----------------------------------
const rl = createInterface({ input: process.stdin, crlfDelay: Infinity });

function send(msg) {
  process.stdout.write(JSON.stringify(msg) + "\n");
}

rl.on("line", async (line) => {
  if (!line.trim()) return;
  let msg;
  try {
    msg = JSON.parse(line);
  } catch {
    return;
  }
  const { id, method, params } = msg;
  try {
    if (method === "initialize") {
      send({
        jsonrpc: "2.0",
        id,
        result: {
          protocolVersion: "2024-11-05",
          capabilities: { tools: {} },
          serverInfo: { name: "kamuit", version: "0.2.0" },
        },
      });
      return;
    }
    if (method === "notifications/initialized" || method?.startsWith("notifications/")) {
      return;
    }
    if (method === "tools/list") {
      send({ jsonrpc: "2.0", id, result: { tools: TOOLS } });
      return;
    }
    if (method === "tools/call") {
      const name = params?.name;
      const args = params?.arguments || {};
      const result = await callTool(name, args);
      send({
        jsonrpc: "2.0",
        id,
        result: {
          content: [{ type: "text", text: JSON.stringify(result, null, 2) }],
          isError: result?.ok === false,
        },
      });
      return;
    }
    if (method === "ping") {
      send({ jsonrpc: "2.0", id, result: {} });
      return;
    }
    send({
      jsonrpc: "2.0",
      id,
      error: { code: -32601, message: `Method not found: ${method}` },
    });
  } catch (e) {
    if (id === undefined) return;
    send({
      jsonrpc: "2.0",
      id,
      error: { code: -32000, message: String(e?.message || e) },
    });
  }
});
