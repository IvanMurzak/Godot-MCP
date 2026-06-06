import { describe, it, expect } from 'vitest';
import {
  parseEnabledPlugins,
  togglePluginInText,
  GODOT_MCP_PLUGIN_PATH,
} from '../src/utils/project-godot.js';

const BASE = `; Engine configuration file.
config_version=5

[application]

config/name="Test"
`;

describe('parseEnabledPlugins', () => {
  it('returns [] when there is no [editor_plugins] section', () => {
    expect(parseEnabledPlugins(BASE)).toEqual([]);
  });

  it('parses a single enabled plugin', () => {
    const text = `${BASE}
[editor_plugins]

enabled=PackedStringArray("res://addons/godot_mcp/plugin.cfg")
`;
    expect(parseEnabledPlugins(text)).toEqual(['res://addons/godot_mcp/plugin.cfg']);
  });

  it('parses multiple enabled plugins', () => {
    const text = `${BASE}
[editor_plugins]

enabled=PackedStringArray("res://addons/other/plugin.cfg", "res://addons/godot_mcp/plugin.cfg")
`;
    expect(parseEnabledPlugins(text)).toEqual([
      'res://addons/other/plugin.cfg',
      'res://addons/godot_mcp/plugin.cfg',
    ]);
  });
});

describe('togglePluginInText — enable', () => {
  it('creates the [editor_plugins] section when absent', () => {
    const r = togglePluginInText(BASE, true);
    expect(r.kind).toBe('changed');
    expect(r.text).toContain('[editor_plugins]');
    expect(r.text).toContain('enabled=PackedStringArray("res://addons/godot_mcp/plugin.cfg")');
    expect(parseEnabledPlugins(r.text)).toEqual([GODOT_MCP_PLUGIN_PATH]);
  });

  it('appends to an existing enabled array, preserving siblings', () => {
    const text = `${BASE}
[editor_plugins]

enabled=PackedStringArray("res://addons/other/plugin.cfg")
`;
    const r = togglePluginInText(text, true);
    expect(r.kind).toBe('changed');
    expect(parseEnabledPlugins(r.text)).toEqual([
      'res://addons/other/plugin.cfg',
      GODOT_MCP_PLUGIN_PATH,
    ]);
  });

  it('is idempotent when already enabled', () => {
    const text = `${BASE}
[editor_plugins]

enabled=PackedStringArray("res://addons/godot_mcp/plugin.cfg")
`;
    const r = togglePluginInText(text, true);
    expect(r.kind).toBe('unchanged');
    expect(r.text).toBe(text);
  });
});

describe('togglePluginInText — disable', () => {
  it('removes the plugin, preserving siblings', () => {
    const text = `${BASE}
[editor_plugins]

enabled=PackedStringArray("res://addons/other/plugin.cfg", "res://addons/godot_mcp/plugin.cfg")
`;
    const r = togglePluginInText(text, false);
    expect(r.kind).toBe('changed');
    expect(parseEnabledPlugins(r.text)).toEqual(['res://addons/other/plugin.cfg']);
  });

  it('is idempotent when already absent', () => {
    const r = togglePluginInText(BASE, false);
    expect(r.kind).toBe('unchanged');
    expect(r.text).toBe(BASE);
  });
});
