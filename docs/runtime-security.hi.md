[English](runtime-security.md) · [中文](runtime-security.zh-CN.md) · [日本語](runtime-security.ja.md) · [Español](runtime-security.es.md) · [فارسی](runtime-security.fa.md) · **हिन्दी** · [العربية](runtime-security.ar.md)

# Runtime (in-game) security contract

Godot-MCP सिर्फ़ editor में ही नहीं, बल्कि **चलते हुए / export किए game build के अंदर** (debug या release) भी चल सकता है —
पूरी how-to के लिए [Runtime इस्तेमाल (in-game)](runtime.hi.md) देखें। Ship किए गए game के अंदर MCP
connection चलाने से एक **remote-control surface** खुलती है: आपके registered `[AiToolType]` tools जो कुछ भी
कर सकते हैं, एक connected MCP client उसे चला सकता है। यह document उस mode का security contract है।
`GodotMcpRuntime.Initialize(...)` call करने वाला build ship करने से पहले इसे पढ़ें।

<a id="guarantees-the-runtime-gives-you"></a>
## Runtime आपको क्या गारंटी देता है

- **सिर्फ़ opt-in — कभी auto-connect नहीं।** Editor plugin boot पर connect होता है; game build **नहीं** होता।
  `GodotMcpRuntime.Initialize(...).Build()` एक **default-OFF** `GodotMcpRuntimeHandle` लौटाता है। जब तक *आपका*
  code explicitly `handle.Connect()` call न करे, कुछ भी connect नहीं होता। Non-editor build में कोई auto-connect
  code path नहीं है।
- **Default रूप से zero tools — पूरी तरह manual।** नया बना runtime **कोई** MCP tool register नहीं करता।
  Attack surface ठीक उतनी ही है जितनी `[AiToolType]` classes आप `builder.WithToolsFromAssembly(...)` /
  `builder.WithTools(...)` से opt in करते हैं — उससे ज़्यादा नहीं। कोई register न हो, तब भी connection
  खाली tool set के साथ build और connect होता है (बिल्कुल Unity-MCP का model)।
- **Editor tool families अंदर नहीं आ सकतीं।** Addon की editor tool families (`Tool_Rql`, `Tool_Script`,
  `Tool_Screenshot`, `Tool_Session`) `#if TOOLS` के पीछे gated हैं और export किए game build में **compile नहीं
  होतीं**। Addon की अपनी assembly पर `WithToolsFromAssembly(...)` भी उन्हें register नहीं कर सकता।
- **Persisted config का auto-load नहीं।** Game build कभी चुपचाप saved (`user://`) config file नहीं पढ़ता —
  वह सिर्फ़ editor की सुविधा है। Host और token सिर्फ़ आपके code (`WithConfig`) से या `GODOT_MCP_*` process
  environment / प्रोजेक्ट की `.env` से आते हैं, जिन्हें `GodotMcpConfig` live पढ़ता है।

<a id="what-you-must-do-when-enabling-it"></a>
## इसे enable करते समय आपको क्या करना होगा

- **सोच-समझकर connect करें।** `handle.Connect()` सिर्फ़ तब call करें जब आप सच में tools expose करना चाहते हों
  (dev build, QA harness, सोच-समझकर design किया in-game एजेंट feature)। उन्हें expose करना बंद करने के लिए
  `handle.Disconnect()` / `handle.Dispose()` call करें।
- **कम से कम tool set register करें।** जब कुछ ही tools चाहिए हों, तो broad `WithToolsFromAssembly(...)` की
  बजाय `WithTools(typeof(MyToolFamily))` बेहतर है। हर registered tool connected client की पहुँच में है।
- **Loopback + token बेहतर है।** लोकल tooling के लिए Custom mode को loopback host
  (`http://localhost:…` / `127.0.0.1`) से bind करें और असली `Token` के साथ `AuthOption = Consts.MCP.Server.AuthOption.token`
  सेट करें:

  ```csharp
  // using McpServerConsts = com.IvanMurzak.McpPlugin.Common.Consts.MCP.Server;
  builder.WithConfig(config =>
  {
      config.ConnectionMode = GodotMcpConnectionMode.Custom;
      config.Host           = "http://localhost:8080";   // loopback
      config.AuthOption     = McpServerConsts.AuthOption.token;
      config.Token          = "your-secret-token";  // सलाह नहीं दी जाती: नीचे env-var वाला तरीका देखें — hard-coded secret ship न करें
  });
  ```

  नीचे वाला out-of-band तरीका बेहतर है, ताकि build में कोई embedded secret न रहे:

  ```bash
  export GODOT_MCP_CONNECTION_MODE=Custom
  export GODOT_MCP_HOST=http://localhost:8080
  export GODOT_MCP_AUTH_OPTION=token
  export GODOT_MCP_TOKEN=your-secret-token
  ```

- **Release build में public interface पर unauthenticated MCP surface expose न करें**, जब तक आपने उस
  surface को explicitly design और secure न किया हो। किसी भी non-loopback host को एक सोचा-समझा, review किया
  गया फ़ैसला मानें।
- **Host या mode सिर्फ़ disconnected रहते हुए बदलें।** जब तक आप अपना `CredentialProvider` assign न करें, हर
  connection का bearer उस mode और host से बंधा होता है जिस पर वह dial करता है: बदलाव के बाद मौजूदा link के
  reconnect पर कोई token नहीं भेजा जाता, इसलिए server बदलने के लिए `handle.Disconnect()` और `handle.Connect()` call करें।
- **Ship किए binary में लंबे समय तक चलने वाले secrets embed न करें।** Hard-coded `config.Token` की बजाय
  environment से `GODOT_MCP_TOKEN` बेहतर है, ताकि token build से निकाला न जा सके।
- **Runtime-error capture सिर्फ़ भरोसेमंद connection पर enable करें।** `builder.WithRuntimeErrorCapture()`
  **default रूप से OFF** है। Enable होने पर capture किए errors **पूरा** message और (C# faults के लिए) **पूरा
  managed stack trace** `runtime-errors-get` tool के ज़रिए connected एजेंट को भेजते हैं। उन strings में
  संवेदनशील runtime data हो सकता है — absolute filesystem paths, machine/user names, query strings, या कोई
  secret/token जो किसी exception message या argument में आ गया हो। यही इसका इच्छित diagnostic value है,
  लेकिन इससे connection पर expose होने वाला data बढ़ जाता है। इसे सिर्फ़ loopback host पर
  `AuthOption = Consts.MCP.Server.AuthOption.token` और असली token के साथ enable करें — release build में
  unauthenticated public interface पर कभी नहीं।

<a id="in-game-code-execution-withscriptexecute"></a>
## In-game code execution (`WithScriptExecute`)

`builder.WithScriptExecute()` game में `script-execute` register करता है: connected client C# भेजता है, game
उसे Roslyn से compile करके अपने main thread पर चलाता है। यह **game process में arbitrary code execution** है,
player के file system और network access के साथ। यह default रूप से OFF है और इसके लिए build opt-in चाहिए:
export किए game में Code-Exec और Roslyn सिर्फ़ तब होते हैं जब उसकी `.csproj` `GodotMcpCodeExecInGame=true` सेट करे
और `GODOT_MCP_CODE_EXEC_IN_GAME` define करे ([Game में C# चलाना](runtime.hi.md#running-c-in-the-game))। Opt-in के बिना
call warning log करती है और कुछ register नहीं करती।

> **Editor से शुरू किए games।** इनका build editor build (`TOOLS`) होता है, इसलिए addon की editor tool
> families भी game assembly में compile हो जाती हैं। तब उस assembly पर `WithToolsFromAssembly(...)` उन्हें
> register कर देता है, editor वाले `script-execute` समेत, `WithScriptExecute()` हो या न हो। Export build में
> ये मौजूद नहीं होतीं। अगर editor से चलाए game को इन्हें offer नहीं करना चाहिए, तो ख़ास types register करें
> (`WithTools(...)`)।

- **सिर्फ़ debug / dev builds।** Call (और csproj opt-in) को अपने dev switch के पीछे रखें। इसे public release build
  में enabled करके कभी ship न करें।
- **हमेशा loopback + token।** Loopback host और environment से असली token के साथ `AuthOption = token` इस्तेमाल करें।
  Connection पर tools call कर सकने वाले का process पर पूरा नियंत्रण होता है।
- **सिर्फ़ Desktop CoreCLR।** NativeAOT और iOS run time पर emit किया code नहीं चला सकते; mobile test नहीं हुआ है।
- **आकार।** Opt-in export में Roslyn (`Microsoft.CodeAnalysis*.dll`, कई MB) जोड़ता है। RQL कभी ship नहीं
  होता।

<a id="environment-variables"></a>
## Environment variables

Runtime config (`GodotMcpConfig`) ये `GODOT_MCP_*` variables live पढ़ता है (process env या प्रोजेक्ट की
`.env`)। Resolution के समय ये code में सेट values को override करते हैं।

| Variable | Values | विवरण |
| -------- | ------ | ----------- |
| `GODOT_MCP_CONNECTION_MODE` | `Cloud` / `Custom` | Connection mode (loopback host का मतलब `Custom`)। |
| `GODOT_MCP_CLOUD_URL` | URL | Cloud base URL override करें (default `https://ai-game.dev`)। |
| `GODOT_MCP_HOST` | URL | Custom-mode server host (default `http://localhost:8080`)। |
| `GODOT_MCP_AUTH_OPTION` | `none` / `oauth` / `token` | Custom-mode auth: anonymous / account (oauth) / offline bearer (`token`)। सेट न हो तो `GODOT_MCP_TOKEN` `token` चुनता है। |
| `GODOT_MCP_TOKEN` | string | Bearer token (active mode के हिसाब से Cloud या Custom को route होता है)। |
| `GODOT_MCP_LOG_LEVEL` | `Trace` / `Debug` / `Info` / `Warning` / `Error` / `None` | Log-verbosity threshold। |
| `GODOT_MCP_EDITOR_PID` | process id | Editor plugin इसे editor process में सेट करता है, ताकि editor द्वारा शुरू किया हर process इसे inherit करे। जब तक वह editor चल रहा है, इसे रखने वाले process में runtime का `Connect()` offline रहता है (जब तक नीचे वाले से opt in न किया हो)। |
| `GODOT_MCP_RUNTIME_FROM_EDITOR` | `1` / `true` | सिर्फ़ process env। Godot editor से शुरू किए game में runtime को connect होने देता है (default: वहाँ यह offline रहता है, क्योंकि game editor की `GODOT_MCP_*` settings inherit करता है); `builder.ConnectWhenRunFromEditor()` के समान। |

<a id="editor-side-surfaces-accepted-posture"></a>
## Editor-side surfaces (स्वीकृत posture)

ऊपर का contract **game build** के लिए है। पूर्णता के लिए दो editor-side surfaces यहाँ document किए गए हैं;
दोनों आज **by design** हैं (अभी कोई encryption / shared-secret implement नहीं है — ये टाले गए, parity से बंधे
फ़ैसले हैं जिन्हें Unity / Godot / Unreal siblings के बीच coordinate करना है)।

<a id="editor-token-storage-is-plaintext-at-rest"></a>
### Editor token storage at rest plaintext है

जब आप **editor plugin** को connect करते हैं (Cloud device-auth, या Custom-mode token), तो वह आपका connection
config **`user://godot-mcp-config.json`** में persist करता है (Godot `ProjectSettings.GlobalizePath` के ज़रिए `user://`
को platform के हिसाब से user data directory में resolve करता है)। Bearer token (`token`) और Cloud token
(`cloudToken`) **plaintext JSON** के रूप में लिखे जाते हैं — ये **encrypted नहीं** हैं और OS keystore में **stored
नहीं** हैं।

- **भरोसे की धारणा: लोकल user account।** जो कोई भी आपकी user data directory पढ़ सकता है, वह token पढ़ सकता है।
  `user://godot-mcp-config.json` को secret मानें — इसे commit न करें, shared drive पर sync न करें, या paste न
  करें।
- **Rotation।** Dock में saved token clear करें (या file delete करें) और फिर से connect करें।
- **Precedence।** `GODOT_MCP_TOKEN` process-env value, या Custom-mode `.env` value, resolution के समय persisted
  token को ढक देती है और इस file में वापस **नहीं** लिखी जाती — इसलिए CI / shared मशीन disk पर कभी token persist
  किए बिना चल सकती है। Cloud mode में पढ़ा गया `.env` token dock save पर वापस लिखा जाता है।
- **सेट किया गया `GODOT_MCP_TOKEN` Custom host को भेजा जाता है।** `GODOT_MCP_AUTH_OPTION` न होने पर यह configured
  `AuthOption = none` को override करता है, code में सेट किए को भी। Connection को anonymous रखने के लिए `GODOT_MCP_AUTH_OPTION=none` सेट करें।

<a id="the-dev-control-bridge-is-unauthenticated-but-gated-off"></a>
### Dev-control bridge unauthenticated है लेकिन gated OFF है

Tests / terminal से editor dock चलाने के लिए सिर्फ़ development वाला inject/control HTTP bridge (`DevControlServer`)
मौजूद है। यह **unauthenticated** है, लेकिन इसकी security boundary तीन-स्तरीय है:

1. **सिर्फ़ editor।** यह `#if TOOLS` के पीछे compile होता है, इसलिए export किए game build में मौजूद नहीं होता।
2. **सिर्फ़ loopback।** यह विशेष रूप से **`127.0.0.1`** पर bind होता है — कभी किसी routable interface पर नहीं।
3. **Env से gated OFF।** यह **सिर्फ़ तब बनता और शुरू होता है जब `GODOT_MCP_DEV_CONTROL=1`** हो (ठीक `1`)।
   सेट न होने / किसी भी दूसरी value का मतलब है bridge कभी listen नहीं करता — shipped addon, और कोई भी सामान्य editor
   session, यह surface कभी नहीं खोलता।

वह env gate **load-bearing** है: "dev scaffolding" और "live dock पर एक unauthenticated control surface" के बीच
यही एकमात्र चीज़ है। इसे एक pure-managed predicate (`DevControlGate.IsEnabled`) और unit test (`DevControlGateTests`)
**और** construction site पर boot-time assertion (`DevControlGate.AssertEnabledOrThrow`) से लागू किया जाता है, ताकि
env var के बिना bridge को चालू करने वाला regression चुपचाप enabled ship होने की बजाय CI और runtime में तुरंत fail हो।
