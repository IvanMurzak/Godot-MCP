"""Regenerate the README images in docs/img/ with the shared svgkit style: py -3 docs/img/build-svgs.py

The terminal shows real godot-cli success messages (from cli/src/commands); the last line is a prompt idea,
not CLI output.
"""
import os, sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)
import svgkit as K  # noqa: E402


def hero_right():
    cy = 190
    s = K.wire("M884 190 L912 190", color=K.RED_TEXT, dur=1.6)
    for i, y in enumerate((110, 190, 270)):
        s += K.wire(f"M1008 190 L1056 {y}", color=K.BLUE, dur=2.2, begin=i * 0.6)
    s += K.mark(960, cy, 60)
    s += K.pill(770, cy - 17, "AI agent", color=K.TEXT)[0]
    for y, name in ((110, "Scenes"), (190, "Scripts"), (270, "Resources")):
        s += K.pill(1056, y - 17, name, color=K.TEXT, w=120)[0]
    return s


K.write(os.path.join(HERE, "hero.svg"), K.hero(
    title="Godot ", accent="MCP", eyebrow_text="AI Game Developer",
    subtitle=["Let AI agents work in the Godot editor:", "scenes, nodes, scripts and resources."],
    chip_items=["Godot 4.3+ .NET", "24 tools", "RQL", "CLI"], right=hero_right(),
    label="Godot MCP: AI agents work in the Godot editor on scenes, nodes, scripts and resources"))

lbl = lambda x, y, t: f'<text x="{x}" y="{y}" text-anchor="middle" class="m" font-size="16" fill="{K.SECOND}">{t}</text>'
body = (K.eyebrow(140, 64, "AI agent", "middle") + K.eyebrow(455, 64, "MCP server", "middle")
        + K.eyebrow(785, 64, "Godot addon", "middle") + K.eyebrow(1065, 64, "Your project", "middle")
        + K.wire("M250 225 C290 225 290 165 330 165", color=K.RED_TEXT, dur=1.8)
        + K.wire("M250 225 C290 225 290 285 330 285", color=K.RED_TEXT, dur=1.8, begin=0.9)
        + K.wire("M580 165 C620 165 620 225 660 225", color=K.BLUE, dur=1.9)
        + K.wire("M580 285 C620 285 620 225 660 225", color=K.BLUE, dur=1.9, begin=0.9)
        + K.wire("M910 225 L960 225", color=K.GREEN, dur=1.4)
        + lbl(290, 120, "MCP") + lbl(620, 120, "SignalR")
        + K.card(30, 135, 220, 180, "AI agent", "Claude Code · Codex", accent="blue", sub2="Cursor · Copilot · …")
        + K.card(330, 115, 250, 100, "ai-game.dev", "Cloud, the default", accent="red", mono_title=True)
        + K.card(330, 235, 250, 100, "gamedev-mcp-server", "Custom: local or hosted", accent="red", mono_title=True)
        + K.card(660, 135, 250, 180, "addons/godot_mcp", "24 tools, 9 families", accent="blue",
                 mono_title=True, sub2="RQL, scripts, screenshots")
        + K.card(960, 135, 210, 180, "Your project", "scenes, nodes,", accent="green", sub2="scripts, resources")
        + f'<text x="600" y="410" text-anchor="middle" class="s" font-size="18" fill="{K.SECOND}">'
          'Optional: a running game exposes only the tools you register (GodotMcpRuntime).</text>')
K.write(os.path.join(HERE, "architecture.svg"), K.panel(1200, 460, body,
        "An AI agent talks MCP to a server (ai-game.dev, or a local gamedev-mcp-server); the server reaches the "
        "godot_mcp editor addon over SignalR; the addon changes your scenes, nodes, scripts and resources"))

K.write(os.path.join(HERE, "terminal.svg"), K.terminal(title="~/games — godot-cli", lines=[
    {"cmd": "godot-cli install-plugin ./MyGame"},
    {"out": [("ok", "✓ "), ("text", "godot_mcp addon installed and enabled")], "note": "addon + NuGet refs + plugin on"},
    {"cmd": "godot-cli login"},
    {"out": [("ok", "✓ "), ("text", "Authentication complete.")], "note": "OAuth device login, once"},
    {"cmd": "godot-cli setup-mcp claude-code ./MyGame"},
    {"out": [("ok", "✓ "), ("text", "Claude Code configured successfully")], "note": "pinned to this project"},
    {"cmd": "godot-cli open ./MyGame"},
    {"out": [("ok", "✓ "), ("text", "C# assembly built")]},
    {"out": [("ok", "✓ "), ("text", "Godot editor located")], "note": "editor starts and connects"},
    {"out": [("muted", "# now ask Claude Code: "), ("str", '"Create 3 cubes in a circle with radius 2"')], "pause": 1.0},
], label="A terminal session: godot-cli installs the addon, signs in, configures Claude Code and opens the editor"))

K.write(os.path.join(HERE, "cli-hero.svg"), K.hero(
    title="godot-", accent="cli", eyebrow_text="Godot MCP command line",
    subtitle=["Install the addon, sign in, set up your agent", "and drive the Godot editor from a terminal."],
    chip_items=["Node 22.14+", "npm", "MCP", "RQL"],
    label="godot-cli: install the Godot MCP addon, sign in, set up your AI agent and drive the Godot editor from a terminal"))
