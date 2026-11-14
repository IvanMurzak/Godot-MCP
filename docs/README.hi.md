<div align="center">

<p><img src="img/hero.svg" alt="Godot MCP: AI एजेंट godot_mcp addon के ज़रिए Godot editor में काम करते हैं" width="100%"/></p>

[![CI](https://img.shields.io/github/actions/workflow/status/IvanMurzak/Godot-MCP/ci.yml?branch=main&label=CI&color=15803D&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=github)](https://github.com/IvanMurzak/Godot-MCP/actions/workflows/ci.yml) [![Release](https://img.shields.io/github/v/release/IvanMurzak/Godot-MCP?label=release&color=2F6F9F&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=github)](https://github.com/IvanMurzak/Godot-MCP/releases/latest) [![Asset Library](https://img.shields.io/badge/AssetLib-Godot--MCP-2F6F9F?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=godotengine)](https://godotengine.org/asset-library/asset/5245) [![CLI](https://img.shields.io/npm/v/godot-cli?label=CLI&color=BE185D&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=npm)](https://www.npmjs.com/package/godot-cli) \
[![Godot](https://img.shields.io/badge/Godot-4.3%2B%20.NET-0E7490?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=godotengine)](https://godotengine.org/) [![.NET 8](https://img.shields.io/badge/.NET-8%20SDK-0E7490?style=for-the-badge&labelColor=0F172A&logoColor=white)](https://dotnet.microsoft.com/) [![License](https://img.shields.io/badge/license-Apache--2.0-047857?style=for-the-badge&labelColor=0F172A&logoColor=white)](../LICENSE) [![Discord](https://img.shields.io/badge/Discord-join-5865F2?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=discord)](https://discord.gg/cfbdMZX99G) [![Stand with Ukraine](https://raw.githubusercontent.com/IvanMurzak/IvanMurzak/main/images/ukraine/flag-badge.svg)](https://stand-with-ukraine.pp.ua)

[English](../README.md) · [中文](README.zh-CN.md) · [日本語](README.ja.md) · [Español](README.es.md) · [فارسی](README.fa.md) · **हिन्दी** · [العربية](README.ar.md)

</div>

**Godot-MCP** AI एजेंट्स (Claude Code, Cursor, Codex, GitHub Copilot, Gemini या कोई भी दूसरा [MCP](https://modelcontextprotocol.io/) client) को **Godot editor** से जोड़ता है। एजेंट आपके scenes, nodes, resources और scripts पढ़ और बदल सकता है, C# build करके चला सकता है, और screenshots देख सकता है। यह एक editor addon है, साथ में एक CLI [`godot-cli`](../cli/docs/README.hi.md) भी है, और यह [Unity-MCP](https://github.com/IvanMurzak/Unity-MCP) का Godot वाला भाई है।

**ज़रूरतें:** Godot 4.3+ editor का **.NET build** और **.NET 8 SDK**, जो एक बार install होता है। आपकी scripts GDScript में ही रह सकती हैं। वैकल्पिक `godot-cli` के लिए Node.js 22.14+ चाहिए। Windows, macOS और Linux पर काम करता है।

<a id="why"></a>
## Godot-MCP क्यों

✅ **Godot 4.3 से 4.7** — हर release CI में 4.3, 4.4, 4.5, 4.6 और 4.7 पर test होती है\
✅ **असली C# builds** — `script-build` `dotnet build` चलाता है और compiler errors लौटाता है\
✅ **GDScript checks** — `script-validate` `.gd` files के parse और compile diagnostics लौटाता है\
✅ **Undo होने वाले, atomic edits** — एक query के बदलाव या तो सब लागू होते हैं या कोई नहीं, हर एक एक Ctrl+Z से undo\
✅ **Screenshots** — viewport, कोई camera, या अकेले render किया गया एक node, एजेंट को लौटाया जाता है\
✅ **Runtime tools** — चलते हुए game से logs, C# reflection और runtime errors (opt-in)\
✅ **मुफ़्त cloud या पूरी तरह लोकल** — default रूप से ai-game.dev (कोई open port नहीं), या बिना account के लोकल server\
✅ **कोई telemetry नहीं** — addon और `godot-cli` कोई usage या analytics data नहीं भेजते

<a id="gdscript"></a>
## GDScript प्रोजेक्ट्स के साथ काम करता है

आपका game code GDScript में ही रहता है। Addon खुद C# में है, इसलिए प्रोजेक्ट को कुछ steps एक बार करने होते हैं:

✅ **Godot .NET editor** — प्रोजेक्ट को Godot 4.3+ के .NET build में खोलें (वही प्रोजेक्ट, वही scenes)\
✅ **.NET 8 SDK** — हर मशीन पर एक बार install\
✅ **C# solution** — मौजूदा प्रोजेक्ट: **Project → Tools → C# → Create C# solution**; नया प्रोजेक्ट: `create-project --dotnet`\
✅ **Addon** — `godot-cli install-plugin` इसे, इसके NuGet references के साथ, जोड़ता है और enable करता है

**क्या सिर्फ़ GDScript वाले प्रोजेक्ट को C# solution चाहिए?** हाँ, एक बार: addon C# में है और प्रोजेक्ट की C# assembly में compile होता है। **Create C# solution** प्रोजेक्ट root में एक `.csproj` और एक `.sln` file जोड़ता है। आपकी `.gd` scripts, scenes और resources नहीं बदलते और पहले की तरह चलते हैं; एजेंट GDScript को भी पढ़ता, लिखता और validate करता है। इसके बाद exports Godot के .NET export templates इस्तेमाल करते हैं और उनमें addon की छोटी runtime assemblies शामिल होती हैं, जो तब तक निष्क्रिय रहती हैं जब तक आपका code `GodotMcpRuntime.Initialize()` call न करे ([इन्हें बाहर कैसे रखें](ARCHITECTURE.md#optional-strictly-zero-bytes-export-exclude-for-dev-only-users))।

<p align="center">
  <img src="img/terminal.svg" alt="एक terminal session: godot-cli addon install करता है, sign in करता है, Claude Code को configure करता है और editor खोलता है" width="100%"/>
</p>

<a id="how-it-works"></a>
## यह कैसे काम करता है

<p align="center">
  <img src="img/architecture.svg" alt="AI एजेंट एक server (ai-game.dev, या लोकल gamedev-mcp-server) से MCP पर बात करता है; server SignalR के ज़रिए godot_mcp editor addon तक पहुँचता है; addon tools को Godot main thread पर चलाता है" width="100%"/>
</p>

Addon बाहर की ओर एक MCP server से connect होता है, और आपका एजेंट उसी server से connect होकर addon के tools को call करता है। Server या तो एक लोकल [GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server) होता है जिसे addon आपके लिए download करके चलाता है, या hosted [ai-game.dev](https://ai-game.dev) relay। Addon अपना कोई port नहीं खोलता।

<a id="stability"></a>
### स्थिरता

- **Versioning:** addon और `godot-cli` हमेशा एक ही version number के साथ ship होते हैं; update करने से पहले [release notes](https://github.com/IvanMurzak/Godot-MCP/releases/latest) पढ़ें।
- **Versions के बीच क्या बदलता है:** एक release addon के लिए ज़रूरी NuGet versions और उसके द्वारा download किया जाने वाला GameDev-MCP-Server version बदल सकती है। `godot-cli update` (या `install-plugin`) वही NuGet versions लिखता है जो आपके install किए addon से मेल खाते हैं, और addon मेल खाने वाला server खुद ले आता है।
- **Release gate:** release तभी बनती है जब .NET tests, CLI tests और Godot 4.3–4.7 editor tests pass हो जाते हैं।
- **बदलावों का इतिहास:** हर [GitHub release](https://github.com/IvanMurzak/Godot-MCP/releases) अपने merged बदलावों की सूची देती है; `godot-cli` एक [changelog](../cli/CHANGELOG.md) भी रखता है।

<a id="quick-start"></a>
## क्विक स्टार्ट

`godot-cli` (Node.js 22.14+) install करें और addon को अपने प्रोजेक्ट में जोड़ें:

```bash
npm install -g godot-cli
godot-cli install-plugin ./MyGodotProject             # addon + NuGet references + plugin enable करना
```

फिर चुनें कि एजेंट editor तक कैसे पहुँचे। दोनों पूरे setups हैं। Cloud addon का default है: कोई open port नहीं, दूसरे programs से port conflicts नहीं, और मुफ़्त। लोकल सब कुछ आपकी मशीन पर रखता है, offline या air-gapped काम के लिए।

**विकल्प A: लोकल, बिना account।** Tool calls आपकी मशीन पर ही रहती हैं।

1. **Token सेट करें।** प्रोजेक्ट को Godot .NET editor में खोलें। **AI Game Developer** dock में **Custom** चुनें, **Authorization → token** सेट करें, **Generate** दबाएँ, फिर **Start Server**। **Server URL** नोट करें (`http://localhost:<port>`, प्रोजेक्ट path से निकाला गया port)।
   या खुद एक token चुनें, terminal से उसके साथ editor खोलें, फिर **Start Server** दबाएँ:
   `godot-cli open ./MyGodotProject --mode Custom --auth Required --token <token>`
2. **अपने एजेंट को connect करें**, उसी token के साथ:

```bash
godot-cli setup-mcp claude-code ./MyGodotProject --url http://localhost:<port> --token <token>
```

> **Token के बिना** लोकल server authorization `none` इस्तेमाल करता है। यह सिर्फ़ loopback पर सुनता है, इसलिए सिर्फ़ आपके अपने कंप्यूटर के programs ही उस तक पहुँच सकते हैं।
> उस स्थिति में `godot-cli open ./MyGodotProject` editor और उसका लोकल server एक ही step में शुरू करता है और `setup-mcp --url` के लिए URL print करता है।

**विकल्प B: ai-game.dev cloud।** हर मशीन पर एक बार sign in करें; उसके बाद एजेंट configs में इस प्रोजेक्ट से बंधी एक key होती है।

```bash
godot-cli login                                       # browser में OAuth device login
godot-cli setup-mcp claude-code ./MyGodotProject      # एजेंट का MCP config लिखें
godot-cli open ./MyGodotProject                       # C# build, editor शुरू, connect
```

फिर अपने एजेंट से कहें: *"2 radius के एक circle में 3 cubes बनाओ।"*

Editor के **AssetLib** tab से, किसी [release zip](https://github.com/IvanMurzak/Godot-MCP/releases/latest) से या हाथ से install करने के लिए [install.hi.md](install.hi.md) देखें: तब प्रोजेक्ट की `.csproj` में addon के NuGet references (`com.IvanMurzak.ReflectorNet`, `com.IvanMurzak.McpPlugin`, और सिर्फ़-editor वाले `com.IvanMurzak.Rql`, `com.IvanMurzak.CodeExec`, `com.IvanMurzak.CodeExec.Roslyn` और `Microsoft.CodeAnalysis.CSharp`) उसी release के versions पर चाहिए जिसे आप install कर रहे हैं।

<a id="relay"></a>
### ai-game.dev से क्या गुज़रता है

सिर्फ़ Cloud mode में। Relay stateless है: यह आपके एजेंट और editor के बीच tool calls को उनका content inspect या persist किए बिना route करता है, और आपकी conversations, source code, project files, assets या scene data collect नहीं करता। यह request metadata (IP address, समय और अवधि, tool का नाम और call count, request और response का size) और MCP event metadata (event type, timing, status, session identifiers, tool, prompt या resource का नाम, और error details) record करता है। इन records का इस्तेमाल 90 दिनों की rolling window के भीतर होता है। विवरण: [Privacy Policy](https://ai-game.dev/legal/privacy)। लोकल mode में editor और server सिर्फ़ localhost पर बात करते हैं।

<a id="clients"></a>
## अपना AI client connect करें

`godot-cli setup-mcp <agent> ./MyGodotProject` आपके लिए config लिखता है (`godot-cli setup-mcp --list` हर agent id दिखाता है)। Server entry का नाम `ai-game-developer` है; `<url>` वह URL है जो `setup-mcp` print करता है। Cloud mode में entry में `Authorization: Bearer agd_pk_…` भी होता है, एक key जो सिर्फ़ इसी प्रोजेक्ट तक पहुँचती है।

<details>
<summary><b>Claude Code</b> — प्रोजेक्ट में <code>.mcp.json</code></summary>

```bash
godot-cli setup-mcp claude-code ./MyGodotProject
```

```json
{ "mcpServers": { "ai-game-developer": { "type": "http", "url": "<url>" } } }
```
</details>

<details>
<summary><b>Cursor</b> — <code>.cursor/mcp.json</code></summary>

```bash
godot-cli setup-mcp cursor ./MyGodotProject
```

```json
{ "mcpServers": { "ai-game-developer": { "type": "http", "url": "<url>" } } }
```
</details>

<details>
<summary><b>VS Code (Copilot)</b> — <code>.vscode/mcp.json</code></summary>

```bash
godot-cli setup-mcp vscode-copilot ./MyGodotProject
```

```json
{ "servers": { "ai-game-developer": { "type": "http", "url": "<url>" } } }
```
</details>

<details>
<summary><b>Codex</b> — <code>.codex/config.toml</code></summary>

```bash
godot-cli setup-mcp codex ./MyGodotProject
```

```toml
[mcp_servers.ai-game-developer]
enabled = true
url = "<url>"
tool_timeout_sec = 300
startup_timeout_sec = 30
```

Cloud mode में key `http_headers` में जाती है।
</details>

<details>
<summary><b>Gemini CLI</b> — <code>.gemini/settings.json</code></summary>

```bash
godot-cli setup-mcp gemini ./MyGodotProject
```

```json
{ "mcpServers": { "ai-game-developer": { "type": "http", "url": "<url>" } } }
```
</details>

ये भी supported हैं: Claude Desktop, Visual Studio (Copilot), GitHub Copilot CLI, Rider (Junie), Antigravity, Cline, Open Code, Kilo Code और एक generic `custom` client ([CLI reference](../cli/docs/README.hi.md#supported-ai-agents))।

<a id="what-the-agent-can-do"></a>
## एजेंट क्या कर सकता है

**Coverage:** Godot editor में आप जो कुछ भी कर सकते हैं, एजेंट लगभग वह सब कर सकता है: scenes, nodes, resources, project settings, scripts, C# builds, screenshots और चलता हुआ game।\
**Efficiency:** tools compact हैं, इसलिए एजेंट को कम calls और कम tokens लगते हैं और वह तेज़ी से और सस्ते में काम करता है। यह tests में मापा गया है: एक sandbox game world पर [RQL के agent evaluation](https://github.com/IvanMurzak/RQL/blob/main/RESULTS.md) में एक task में RQL के साथ 2–3 tool calls और लगभग 8k tokens लगे, जबकि पारंपरिक find / get / set reflection tools के साथ लगभग 37 calls और 169k tokens।

Queries आपको नहीं लिखनी पड़तीं: एजेंट tools के साथ आने वाली RQL गाइड (`rql-schema`, `godot-rql` skill) पढ़ता है और उन्हें खुद लिखता है।

| क्षमता | Tools | अपने एजेंट से कहें |
| --- | --- | --- |
| Scenes और nodes | `rql-query`, `rql-mutate` | *"Main scene में एक DirectionalLight3D जोड़ो और उसे save करो।"* |
| Bulk edits | `rql-mutate` | *"हर वह node छिपा दो जिसका नाम Enemy से शुरू होता है।"* |
| Resources और project settings | `rql-query`, `rql-mutate` | *"Space से bound एक jump input action जोड़ो।"* |
| API देखना | `rql-schema` | *"CharacterBody3D में कौन-सी Godot properties होती हैं?"* |
| Scripts (GDScript और C#) | `script-create`, `script-update`, `script-attach-to-node`, `script-validate`, `script-read`, `script-delete` | *"एक GDScript लिखो जो इस node को घुमाए, और उसे attach करो।"* |
| C# build करना | `script-build` | *"C# प्रोजेक्ट build करो और errors ठीक करो।"* |
| Editor में C# चलाना | `script-execute` | *"खुले scene में MeshInstance3D nodes गिनो।"* |
| नतीजा देखना | `screenshot-viewport`, `screenshot-camera`, `screenshot-isolated` | *"Viewport का screenshot लो और lighting जाँचो।"* |
| चलता हुआ game (opt-in) | `console-*`, `reflection-*`, `runtime-errors-*` | *"चलते हुए game में नए runtime errors जाँचो।"* |

`ping`, `session-close`, `godot-skill-create` और `godot-skill-generate` `godot-cli` और setup के लिए system tools हैं; एजेंट्स उन्हें नहीं देखते। हर tool और argument: [tools.hi.md](tools.hi.md)।

वही tools terminal से भी, प्रोजेक्ट फ़ोल्डर में, काम करते हैं (कोई editor न चल रहा हो तो एक headless editor शुरू होता है):

```bash
godot-cli rql 'nodes | where name starts-with "Enemy" | set Visible = false'
godot-cli exec '1 + 2'        # → System.Int32: 3
godot-cli tools               # editor के tools की सूची
```

<a id="security"></a>
## सुरक्षा

| क्षेत्र | क्या लागू होता है |
| --- | --- |
| **लोकल server** | सिर्फ़ loopback पर सुनता है। इसका default authorization `none` है, इसलिए आपकी मशीन का कोई भी process इसे call कर सकता है। Bearer token ज़रूरी करने के लिए dock में `token` चुनें, `GODOT_MCP_TOKEN` सेट करें, या editor को `godot-cli open <path> --mode Custom --auth Required --token <token>` से खोलें। |
| **Origin checks** | Server हर authorization mode में, ऐसे request को जिसका `Origin` header allowed नहीं है, `403` लौटाता है (DNS-rebinding से बचाव)। |
| **Editor addon** | कोई port नहीं खोलता; यह बाहर की ओर server से connect होता है। `127.0.0.1` पर सिर्फ़ development वाला एक control bridge केवल `GODOT_MCP_DEV_CONTROL=1` होने पर शुरू होता है। |
| **Cloud** | एजेंट configs में एक ही प्रोजेक्ट से बंधी key होती है; editor आपकी मशीन का sign-in इस्तेमाल करता है। आपका account token सिर्फ़ ai-game.dev या किसी localhost server को जाता है। Relay stateless है: यह tool calls का content inspect या persist नहीं करता ([Privacy Policy](https://ai-game.dev/legal/privacy))। |
| **Stored tokens** | Editor अपना token `user://godot-mcp-config.json` में सादे JSON के रूप में save करता है; उस file को secret मानें। |
| **Exported games** | Game में कुछ भी तब तक नहीं चलता जब तक आपका code `GodotMcpRuntime.Initialize(...)` और `Connect()` call न करे, और तब भी सिर्फ़ वही tools जो आप register करते हैं। Error capture और game के अंदर C# अलग-अलग opt-ins हैं ([runtime security](runtime-security.hi.md))। |
| **सुरक्षित नहीं** | Tool calls आपका प्रोजेक्ट बदलती हैं और editor में C# चलाती हैं: version control इस्तेमाल करें, और authorization `none` वाले server को कभी network पर expose न करें। |

विवरण: [Connection और server](connection.hi.md) और [GameDev-MCP-Server README](https://github.com/IvanMurzak/GameDev-MCP-Server#readme)।

<a id="extensions"></a>
## Extensions

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

**Game developers सब कुछ मुफ़्त में इस्तेमाल कर सकते हैं, commercial games में भी।** Godot-MCP [Apache-2.0](../LICENSE) के तहत है। इसकी editor-side dependencies RQL और Code-Exec [PolyForm Shield License 1.0.0](https://polyformproject.org/licenses/shield/1.0.0/) इस्तेमाल करती हैं, जो सिर्फ़ उन कंपनियों पर रोक लगाता है जो game development के लिए competing AI tools बनाती हैं।
