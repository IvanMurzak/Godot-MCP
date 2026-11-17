[English](tools.md) · [中文](tools.zh-CN.md) · [日本語](tools.ja.md) · [Español](tools.es.md) · [فارسی](tools.fa.md) · **हिन्दी** · [العربية](tools.ar.md)

# Tools reference

Godot-MCP के built-in tools, Godot editor में आप जो कुछ भी कर सकते हैं उसका लगभग सब कुछ cover करते हैं, और नीचे दी गई families में बंटे हैं। ये compact हैं, इसलिए एजेंट को कम calls और कम tokens लगते हैं ([RQL के agent evaluation में मापा गया](https://github.com/IvanMurzak/RQL/blob/main/RESULTS.md))। Editor की data layer [RQL](rql.md) (Reflection Query Language) है: `rql-query`, `rql-mutate` और `rql-schema` हर operation के लिए एक tool की बजाय एक ही query में scene nodes, Godot properties, resources, files, scenes और editor state पढ़ते और बदलते हैं।

Default `toolFormat` (`Rql`) के साथ बाकी tools arguments के रूप में RQL values लेते हैं और compact text लौटाते हैं; `Json` ReflectorNet के JSON schemas और results रखता है। Screenshots एक PNG image लौटाते हैं। Addon enable होते ही हर editor tool उपलब्ध हो जाता है।

**console**, **reflection** और **runtime-errors** families एक *चलते हुए game* के लिए हैं ([Runtime इस्तेमाल](runtime.hi.md) देखें)। Editor में इनकी जगह RQL `console` source और RQL `call` / `CallMethod` इस्तेमाल करें।

| Family | Tools | क्या करता है |
| --- | --- | --- |
| **rql** | `rql-query`, `rql-mutate`, `rql-schema` | Live editor को query करना और बदलना। All-or-nothing और undo होने लायक; एक call में `;` से अलग कई statements। [rql.md](rql.md) देखें। |
| **script** | `script-read`, `script-create`, `script-update`, `script-delete`, `script-attach-to-node`, `script-validate`, `script-build`, `script-execute` | C# (`.cs`) और GDScript (`.gd`) files, node पर script attach करना, GDScript validation, प्रोजेक्ट का C# build करना, और editor में C# चलाना। |
| **screenshot** | `screenshot-viewport`, `screenshot-camera`, `screenshot-isolated` | Editor viewport, किसी camera, या अकेले render किए एक node को PNG के रूप में capture करना जिसे LLM देख सके। |
| **console** | `console-get-logs`, `console-clear-logs` | चलते हुए game में: इकट्ठे logs पढ़ना और clear करना। |
| **reflection** | `reflection-method-find`, `reflection-method-call` | चलते हुए game में: ReflectorNet के ज़रिए किसी भी loaded assembly में C# methods ढूँढना और call करना। |
| **runtime-errors** | `runtime-errors-get`, `runtime-errors-clear` | चलते हुए game में: GDScript runtime errors, `push_error` / `push_warning`, shader errors और C# exceptions को poll करना। **Default रूप से बंद।** |
| **ping** | `ping` | Readiness probe। System tool। |
| **session** | `session-close` | `godot-cli` द्वारा शुरू किए editor को save करके बंद करना। System tool। |
| **skills** | `godot-skill-create`, `godot-skill-generate` | प्रोजेक्ट में नई C# tool file लिखना; `SKILL.md` files फिर से generate करना। System tools। |

**System tools** MCP server के `/api/system-tools/` HTTP endpoint पर serve होते हैं (उदाहरण के लिए `godot-cli run-system-tool ping`)। ये AI एजेंट्स को `tools/list` में नहीं दिखाए जाते।

<a id="rql"></a>
## rql

- `rql-query`: एक read-only query, compact rows के रूप में लौटाई जाती है। `nodes | where class == "OmniLight3D" | select name props["light_energy"]`
- `rql-mutate`: एक query जो `set` या `call` पर ख़त्म होती है। All-or-nothing, undo होने लायक (Ctrl+Z), और `dry_run` इसका preview देता है। Scene edits scene को unsaved mark करते हैं; edit की गई resource files और `project.godot` save हो जाती हैं।
  - `nodes | where name starts-with "Enemy" | set Visible = false`
  - `root | call AddChild("Node3D", "Props")`
  - `nodes | where name == "Button" | call Connect("pressed", godot.find("UI"), "_on_pressed")`
  - `project | call AddInputAction("jump", ["Key:Space"])`
  - `editor | call SaveScene()`
- `rql-schema`: edit हो रहे scene के sources, functions और node classes; type name देने पर उसके members, methods और Godot properties। `rql-schema "<extension>"` install हुए extension की गाइड print करता है।

Sources में `nodes`, `root`, `selection`, `scenes`, `files`, `folders`, `classes`, `console`, `editor`, `project`, `settings`, `inputActions`, `autoloads`, `groups`, `layers` और `tools` शामिल हैं। पूरी language, TileSets, Themes, ShaderMaterial parameters और MeshLibraries समेत, [rql.md](rql.md) और `godot-rql` skill में है।

<a id="script"></a>
## script

- `script-read`, `script-create`, `script-update`, `script-delete`: `.cs` और `.gd` files पर CRUD।
- `script-attach-to-node`: किसी node पर script attach करना।
- `script-validate`: GDScript files को parse और compile करके structured diagnostics लौटाना।
- `script-build`: प्रोजेक्ट का C# build करना (`dotnet build`, Debug) ताकि नई scripts load हों। Build background में चलता है; सफल होने पर Godot C# assembly reload करता है, plugin फिर से connect होता है, और result (warnings या errors) reload के बाद आता है। [C# assembly reloads](custom-tools.hi.md#c-assembly-reloads) देखें।
- `script-execute`: [Code-Exec](https://github.com/IvanMurzak/Code-Exec) के ज़रिए Roslyn से editor में C# compile करके चलाना। आख़िरी expression को result मानकर सादे statements (अपने `using`s, local classes, `await`), या static method वाली class। Game के compiled types दिखते हैं; `include` `<project>/.ai-game-dev/snippets` से helper `.cs` files जोड़ता है। Value और code ने जो print किया वह लौटाता है (`Console`; प्रोजेक्ट Godot.NET.Sdk 4.5+ से build हो तो `GD.Print`, push_warning और push_error)। Terminal से: `godot-cli exec '1 + 2'`।

<a id="screenshot"></a>
## screenshot

- `screenshot-viewport`: editor viewport।
- `screenshot-camera`: किसी ख़ास camera का view।
- `screenshot-isolated`: एक node अकेले render किया गया, चुने गए angle से।

तीनों image को conversation में लौटाते हैं। File भी लिखने के लिए `savePath` दें: `res://Screenshots/shot.png`, `Screenshots/shot.png` (प्रोजेक्ट root के सापेक्ष) या प्रोजेक्ट के अंदर कोई absolute path। गायब folders बन जाते हैं और extension न हो तो `.png` जोड़ दिया जाता है। `savePath` नामंज़ूर होता है (न image, न file) जब वह प्रोजेक्ट के बाहर हो (`..`, `user://`), किसी symlink या junction से होकर जाए, `res://.godot/` या इस addon के अंदर point करे, `.png` न हो, या किसी मौजूदा file का नाम हो: यह कभी overwrite नहीं करता। Godot PNG को किसी भी प्रोजेक्ट file की तरह import करता है; `.gdignore` file वाला folder इससे बचाता है।

<a id="console-reflection-runtime-errors-running-game"></a>
## console, reflection, runtime-errors (चलता हुआ game)

- `console-get-logs` / `console-clear-logs`: plugin द्वारा इकट्ठे logs, उसके अपने connection diagnostics समेत, पढ़ना (filtering के साथ) और clear करना।
- `reflection-method-find` / `reflection-method-call`: हर loaded assembly में C# methods (private भी) ढूँढना, और किसी एक को arguments के साथ call करना।
- `runtime-errors-get`: capture किए in-game errors, सबसे पुराने पहले, सबसे नए रखे जाते हैं। सिर्फ़ नए errors पाने के लिए पिछला `highestSequence` `sinceSequence` के रूप में दें। Capture कभी enable न हुआ हो तो `available:false` लौटाता है।
- `runtime-errors-clear`: buffer clear करना; sequence counter गिनती जारी रखता है।

Runtime-error capture तब तक बंद रहता है जब तक game `builder.WithRuntimeErrorCapture()` call न करे। विवरण: [In-game runtime errors capture करना](runtime.hi.md#capturing-in-game-runtime-errors)।

<a id="ping-session-skills-system-tools"></a>
## ping, session, skills (system tools)

- `ping`: message को वापस echo करता है, या `pong` लौटाता है। `curl -X POST <host>/api/system-tools/ping -d '{}'`।
- `session-close`: `godot-cli exec` / `open --headless` द्वारा शुरू किए editor को save करके बंद करता है (file वाले खुले scenes और उनके बदले resources)। इसे `.ai-game-dev/session.json` से उस session का nonce चाहिए, इसलिए यह आपका अपना editor कभी बंद नहीं करता। `godot-cli close --owned-only` इसे call करता है।
- `godot-skill-create`: प्रोजेक्ट में नई C# tool file लिखता है। प्रोजेक्ट rebuild होने के बाद यह callable हो जाती है।
- `godot-skill-generate`: editor में registered tools से हर `SKILL.md` को चुने गए AI एजेंट के skills फ़ोल्डर में फिर से generate करता है।

<a id="visibility"></a>
## Visibility

हर tool, prompt और resource का dock की Tools / Prompts / Resources windows में एक **Visible** switch है (`godot-cli configure` terminal से यही करता है)। यह सिर्फ़ यह तय करता है कि MCP clients को item `tools/list` में दिखे या नहीं, जिससे उनका context छोटा रहता है। यह access control नहीं है: छिपा tool भी client के नाम से call करने पर चलता है।

<a id="extensions"></a>
## Extensions

Extensions `rql-*` tools और `godot-rql` skill में RQL sources, adapters और एक एजेंट गाइड जोड़ते हैं। ये अपने कोई MCP tools नहीं जोड़ते। सूची [README](README.hi.md#extensions) में है।

```bash
godot-cli install-extension com.IvanMurzak.Godot.MCP.CSG ./MyGodotProject
godot-cli build ./MyGodotProject
```

Installer सिर्फ़ extension का NuGet package जोड़ता है; ज़रूरी third-party addon प्रोजेक्ट में आपको खुद install करना होगा। Dock के **Extensions** section में **Install All** है। `godot-cli update` addon को अपडेट करता है और floating extensions (`Version="*"`) को उनके latest versions पर फिर से resolve करता है; जिस extension को आपने किसी version पर pin किया है वह वैसा ही रहता है। अगर किसी extension को addon के pin से नया McpPlugin चाहिए, तो restore `NU1605` के साथ fail होता है जब तक आप addon अपडेट न करें या extension को pin न करें।
