<div align="center">

<p><img src="img/hero.svg" alt="Godot MCP: AI एजेंट godot_mcp addon के ज़रिए Godot editor में काम करते हैं" width="100%"/></p>

[![CI](https://img.shields.io/github/actions/workflow/status/IvanMurzak/Godot-MCP/ci.yml?branch=main&label=CI&color=15803D&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=github)](https://github.com/IvanMurzak/Godot-MCP/actions/workflows/ci.yml) [![Release](https://img.shields.io/github/v/release/IvanMurzak/Godot-MCP?label=release&color=2F6F9F&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=github)](https://github.com/IvanMurzak/Godot-MCP/releases/latest) [![Asset Library](https://img.shields.io/badge/AssetLib-Godot--MCP-2F6F9F?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=godotengine)](https://godotengine.org/asset-library/asset/5245) [![CLI](https://img.shields.io/npm/v/godot-cli?label=CLI&color=BE185D&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=npm)](https://www.npmjs.com/package/godot-cli) \
[![Godot](https://img.shields.io/badge/Godot-4.3%2B%20.NET-0E7490?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=godotengine)](https://godotengine.org/) [![.NET 8](https://img.shields.io/badge/.NET-8%20SDK-0E7490?style=for-the-badge&labelColor=0F172A&logoColor=white)](https://dotnet.microsoft.com/) [![License](https://img.shields.io/badge/license-Apache--2.0-047857?style=for-the-badge&labelColor=0F172A&logoColor=white)](../LICENSE) [![Discord](https://img.shields.io/badge/Discord-join-5865F2?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=discord)](https://discord.gg/cfbdMZX99G) [![Stand with Ukraine](https://raw.githubusercontent.com/IvanMurzak/IvanMurzak/main/images/ukraine/flag-badge.svg)](https://stand-with-ukraine.pp.ua)

[English](../README.md) · [中文](README.zh-CN.md) · [日本語](README.ja.md) · [Español](README.es.md) · [فارسی](README.fa.md) · **हिन्दी** · [العربية](README.ar.md)

</div>

**Godot-MCP** AI एजेंट्स (Claude Code, Cursor, Codex, GitHub Copilot, Gemini या कोई भी दूसरा [MCP](https://modelcontextprotocol.io/) client) को **Godot editor** से जोड़ता है। एजेंट आपके scenes, nodes, resources और scripts पढ़ और बदल सकता है, editor में C# चला सकता है, और screenshots देख सकता है। यह Godot 4.3+ (.NET) के लिए एक C# editor addon है, साथ में एक CLI [`godot-cli`](../cli/docs/README.hi.md) भी है, और यह [Unity-MCP](https://github.com/IvanMurzak/Unity-MCP) का Godot वाला भाई है।

<p align="center">
  <img src="img/terminal.svg" alt="एक terminal session: godot-cli addon install करता है, sign in करता है, Claude Code को configure करता है और editor खोलता है" width="100%"/>
</p>

<a id="how-it-works"></a>
## यह कैसे काम करता है

<p align="center">
  <img src="img/architecture.svg" alt="AI एजेंट एक server (ai-game.dev, या लोकल gamedev-mcp-server) से MCP पर बात करता है; server SignalR के ज़रिए godot_mcp editor addon तक पहुँचता है; addon tools को Godot main thread पर चलाता है" width="100%"/>
</p>

Addon एक MCP server से connect होता है: default रूप से hosted [ai-game.dev](https://ai-game.dev) से, या आपके अपने [GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server) से, जिसे addon आपके लिए download करके चला सकता है। आपका एजेंट उसी server से connect होता है और addon के tools को call करता है।

<a id="features"></a>
## फ़ीचर्स

✅ **एक query language** — [RQL](rql.md) nodes, scenes, resources और settings पढ़ता और edit करता है\
✅ **Undo होने वाले edits** — RQL बदलाव all-or-nothing होते हैं और Ctrl+Z से undo होते हैं\
✅ **C# और GDScript** — `.cs` / `.gd` scripts बनाएँ, अपडेट करें, validate और attach करें\
✅ **Editor में C# चलाएँ** — `script-execute` आपके game के types पर एक snippet चलाता है\
✅ **विज़ुअल फ़ीडबैक** — viewport, किसी camera या एक node के screenshots\
✅ **Cloud या self-hosted** — ai-game.dev sign-in, या addon द्वारा चलाया गया लोकल server\
✅ **Extensions** — animation, navigation, tilemaps और addons के लिए वैकल्पिक RQL modules\
✅ **In-game runtime** — चलते हुए game से अपने tools expose करें (default रूप से बंद)\
✅ **आपके अपने tools** — हर tool के लिए एक attributed C# method ([गाइड](custom-tools.hi.md))

<a id="quick-start"></a>
## क्विक स्टार्ट

ज़रूरतें: Godot 4.3 या नया, **.NET edition** (सिर्फ़ GDScript वाला build addon को compile नहीं कर सकता), .NET 8 SDK, और `godot-cli` के लिए Node.js 22.14+।

```bash
npm install -g godot-cli

godot-cli create-project --dotnet ./MyGodotProject    # वैकल्पिक: एक नया Godot C# प्रोजेक्ट
godot-cli install-plugin ./MyGodotProject             # addon + NuGet references + plugin enable करना
godot-cli login                                       # ai-game.dev में sign in (browser, हर मशीन पर एक बार)
godot-cli setup-mcp claude-code ./MyGodotProject      # एजेंट का MCP config लिखें
godot-cli open ./MyGodotProject                       # C# build, editor शुरू, connect
```

फिर अपने एजेंट से कहें: *"2 radius के एक circle में 3 cubes बनाओ।"*

`godot-cli setup-mcp --list` हर supported एजेंट दिखाता है। Editor के **AssetLib** tab से, किसी [release zip](https://github.com/IvanMurzak/Godot-MCP/releases/latest) से या हाथ से install करने के लिए [install.hi.md](install.hi.md) देखें: तब प्रोजेक्ट की `.csproj` में ये NuGet references चाहिए।

| Package | Version |
| --- | --- |
| `com.IvanMurzak.ReflectorNet` | `5.5.2` |
| `com.IvanMurzak.McpPlugin` | `8.22.2` |
| `com.IvanMurzak.Rql` | `0.3.0` (सिर्फ़ editor) |
| `com.IvanMurzak.CodeExec`, `com.IvanMurzak.CodeExec.Roslyn` | `0.4.1` (सिर्फ़ editor) |
| `Microsoft.CodeAnalysis.CSharp` | `4.12.0` (सिर्फ़ editor) |

<a id="usage"></a>
## इस्तेमाल

अपने एजेंट से सामान्य भाषा में बात करें; tools वह खुद चुनता है। प्रोजेक्ट फ़ोल्डर से आप वही tools terminal में भी call कर सकते हैं:

```bash
godot-cli rql 'nodes | where class == "OmniLight3D" | select name props["light_energy"]'
godot-cli rql 'nodes | where name starts-with "Enemy" | set Visible = false'
godot-cli exec '1 + 2'        # → System.Int32: 3
godot-cli tools               # editor के tools की सूची
```

जब कोई editor नहीं चल रहा होता, तो ये commands प्रोजेक्ट के लिए एक headless editor शुरू कर देते हैं। हर command [CLI reference](../cli/docs/README.hi.md) में है।

<a id="tools"></a>
### Tools

Godot-MCP में **9 families** में **24 built-in tools** आते हैं:

| Family | Tools |
| --- | --- |
| **rql** | `rql-query`, `rql-mutate`, `rql-schema` |
| **script** | `script-read`, `script-create`, `script-update`, `script-delete`, `script-attach-to-node`, `script-validate`, `script-build`, `script-execute` |
| **screenshot** | `screenshot-viewport`, `screenshot-camera`, `screenshot-isolated` |
| **console**, **reflection**, **runtime-errors** | चलते हुए game के अंदर logs, C# reflection और error capture |
| **ping**, **session**, **skills** | `godot-cli` और setup के लिए system tools, एजेंट्स को नहीं दिखते |

हर tool और argument: [tools.hi.md](tools.hi.md)।

<a id="extensions"></a>
### Extensions

Extensions वैकल्पिक NuGet packages हैं जो RQL को बढ़ाते हैं; वे अपने कोई tools नहीं जोड़ते।

```bash
godot-cli install-extension com.IvanMurzak.Godot.MCP.CSG ./MyGodotProject
```

| Extension | क्या जोड़ता है | ज़रूरत |
| --- | --- | --- |
| [Animation](../extensions/Godot-AI-Animation/README.md) | AnimationPlayer libraries, tracks और keys | |
| [Beehave](../extensions/Godot-AI-Beehave/README.md) | Behaviour trees और उनके nodes | [Beehave](https://github.com/bitbrain/beehave) addon |
| [CSG](../extensions/Godot-AI-CSG/README.md) | Validated dimensions वाले CSG shapes | |
| [Dialogic](../extensions/Godot-AI-Dialogic/README.md) | Timelines और characters | [Dialogic](https://github.com/dialogic-godot/dialogic) addon, Godot 4.4+ |
| [GridMap](../extensions/Godot-AI-GridMap/README.md) | GridMap settings, cell rows और cell editing | |
| [Navigation](../extensions/Godot-AI-Navigation/README.md) | Navigation agents, regions, links, baking | |
| [Particles](../extensions/Godot-AI-Particles/README.md) | 2D और 3D में GPU particles | |
| [PhantomCamera](../extensions/Godot-AI-PhantomCamera/README.md) | Virtual cameras, follow और look-at | [Phantom Camera](https://github.com/ramokz/phantom-camera) addon, Godot 4.4+ |
| [Terrain3D](../extensions/Godot-AI-Terrain3D/README.md) | Terrain source, region size और materials | [Terrain3D](https://github.com/TokisanGames/Terrain3D) addon, Godot 4.4+ |
| [Tilemap](../extensions/Godot-AI-Tilemap/README.md) | TileMapLayer cell rows और cell editing | |

ज़रूरी third-party addon प्रोजेक्ट में आपको खुद install करना होगा; dock का **Extensions** section इन सबको install कर सकता है।

<a id="documentation"></a>
## डॉक्यूमेंटेशन

| पेज | इसमें क्या है |
| --- | --- |
| [मैन्युअल installation](install.hi.md) | AssetLib, release zip, NuGet references, एजेंट setup |
| [Connection और server](connection.hi.md) | Cloud और Custom modes, variables, server |
| [Tools reference](tools.hi.md) | हर tool और उसके arguments, screenshots, extensions |
| [Godot-MCP में RQL](rql.md) | Sources, functions, writes, undo और saving |
| [Custom tools](custom-tools.hi.md) | Tools लिखना, C# assembly reloads, .NET 10 की known issue |
| [Runtime इस्तेमाल](runtime.hi.md) | चलते हुए game के अंदर MCP: setup, error capture, game में C# |
| [Runtime security](runtime-security.hi.md) | In-game runtime क्या गारंटी देता है और आपको क्या करना है |
| [CLI reference](../cli/docs/README.hi.md) | हर `godot-cli` command, exit codes और output formats |
| [Architecture](ARCHITECTURE.md) · [Releasing](RELEASING.md) · [Extensions](EXTENSION-MONOREPO.md) | Contributors के लिए |

<a id="contributing"></a>
## योगदान

Build और unit-test के लिए Godot binary की ज़रूरत नहीं है:

```bash
dotnet restore Godot-MCP.sln
dotnet build  Godot-MCP.sln --configuration Debug --no-restore
dotnet test   Godot-MCP.Tests/Godot-MCP.Tests.csproj --configuration Debug --no-build
```

Conventions, headless editor smoke test और मुख्य pitfalls [AGENTS.md](../AGENTS.md) में हैं। Repo को fork करें और `main` के against एक pull request खोलें। सवाल और showcases: [Discord](https://discord.gg/cfbdMZX99G)।

संबंधित: [GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server) · [Unity-MCP](https://github.com/IvanMurzak/Unity-MCP) · [RQL](https://github.com/IvanMurzak/RQL) · [Code-Exec](https://github.com/IvanMurzak/Code-Exec) · [ai-game.dev](https://ai-game.dev)

<a id="license"></a>
## लाइसेंस

[Apache-2.0](../LICENSE) © Ivan Murzak. Addon [`com.IvanMurzak.Rql`](https://www.nuget.org/packages/com.IvanMurzak.Rql) और Code-Exec का भी इस्तेमाल करता है, जिनका अपना license है।

वह license [PolyForm Shield License 1.0.0](https://polyformproject.org/licenses/shield/1.0.0/) है: commercial products समेत इस्तेमाल के लिए मुफ़्त, सिवाय licensor के product से competition करने वाला product बनाने के।
