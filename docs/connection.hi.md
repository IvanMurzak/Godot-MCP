[English](connection.md) · [中文](connection.zh-CN.md) · [日本語](connection.ja.md) · [Español](connection.es.md) · [فارسی](connection.fa.md) · **हिन्दी** · [العربية](connection.ar.md)

# Connection और server

Editor plugin दो में से किसी एक mode में MCP server से connect होता है। Mode editor dock में सेट करें, या process शुरू होते समय `GODOT_MCP_*` environment variables से override करें (CI, headless runs और लोकल development के लिए उपयोगी)। Environment हमेशा saved config पर भारी पड़ता है। `godot-cli open` इन्हें आपके लिए `--mode`, `--url`, `--cloud-url` और `--token` के ज़रिए पास करता है।

<a id="cloud-mode-default-ai-gamedev"></a>
## Cloud mode (default): ai-game.dev

Plugin `https://ai-game.dev` से connect होता है; आपको कोई server नहीं चलाना पड़ता।

`godot-cli login` से एक बार sign in करें। यह OAuth 2.1 device flow (RFC 8628) इस्तेमाल करता है: एक छोटा code print करता है, आपका browser खोलता है और, आपके approve करने के बाद, credential को machine store (`~/.ai-game-dev/credentials.json`) में save करता है। Editor plugin उसे ले लेता है, इसलिए कोई token copy नहीं करना पड़ता।

| Variable | उद्देश्य | Default |
| --- | --- | --- |
| `GODOT_MCP_CONNECTION_MODE` | `Cloud` या `Custom` (case-insensitive)। | `Cloud` |
| `GODOT_MCP_CLOUD_URL` | Cloud base URL। आख़िर का `/mcp` हटा दिया जाता है। | `https://ai-game.dev` |
| `GODOT_MCP_CREDENTIALS_DIR` | Isolated testing के लिए absolute credential-store directory। प्रोजेक्ट की `.env` से भी पढ़ा जाता है। | `~/.ai-game-dev` |
| `GODOT_MCP_TOKEN` | Active mode के लिए bearer token; CI और headless runs के लिए manual override। | कोई नहीं |

<a id="custom-mode-your-own-server"></a>
## Custom mode: आपका अपना server

Plugin आपके चुने server URL से connect होता है: एक लोकल server, या self-hosted।

| Variable | उद्देश्य | Default |
| --- | --- | --- |
| `GODOT_MCP_CONNECTION_MODE` | `Custom` पर सेट करें। | `Cloud` |
| `GODOT_MCP_HOST` | Server URL, एक absolute http(s) URL। | `http://localhost:8080` |
| `GODOT_MCP_TOKEN` | उस server को भेजा जाने वाला bearer token। इसे सेट करने से `token` authorization चुना जाता है। | कोई नहीं |
| `GODOT_MCP_AUTH_OPTION` | `none` / `oauth` / `token`; ऊपर वाले नियम को override करता है। | saved choice, नहीं तो `none` |

```bash
export GODOT_MCP_CONNECTION_MODE=Custom
export GODOT_MCP_HOST=http://localhost:8080
# export GODOT_MCP_TOKEN=...   # सिर्फ़ तब जब server को auth चाहिए
```

- Environment में (या Custom mode में प्रोजेक्ट की `.env` में) सेट किया token editor द्वारा शुरू किए गए लोकल server पर और **Configure** से लिखे गए एजेंट configs पर भी लागू होता है। यह config file में save नहीं होता।
- `oauth` के साथ, आपका ai-game.dev account token **सिर्फ़ localhost server** (`localhost`, `127.0.0.0/8`, `::1`) को भेजा जाता है। किसी दूसरे host के लिए `GODOT_MCP_TOKEN` सेट करें।
- `token` mode वाला server बिना token के connection को नामंज़ूर कर देता है; plugin log करता है कि क्या सेट करना है।

<a id="the-server"></a>
## Server

Server साझा [GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server) (`gamedev-mcp-server`) है, Unity-MCP, Godot-MCP और Unreal-MCP के लिए एक ही binary, जो अपनी अलग repository से release होता है। Addon वह version pin करता है जो वह इस्तेमाल करता है (`addons/godot_mcp/Runtime/Connection/GodotMcpServerView.cs` में `ServerVersion`)।

<a id="let-the-addon-run-it"></a>
### Addon को चलाने दें

Custom mode में dock का **Server** card खोलें और **Start Server** दबाएँ। Addon:

- pinned version के लिए `gamedev-mcp-server-<rid>.zip` को GameDev-MCP-Server GitHub release से HTTPS पर download करता है (`win-x64`, `win-x86`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`);
- उसे `.godot/mcp-server/<rid>/` में cache करता है और pinned version बदलने पर ही दोबारा download करता है;
- उसे आपके **Server URL** के port (default `http://localhost:8080`) पर शुरू करता है और उससे connect होता है।

**Stop Server** उसे रोकता है; editor बंद करने पर भी वह रुक जाता है। Download URL सिर्फ़ pinned version और आपके platform से बनता है। Offline होने पर addon warning log करता है और server शुरू नहीं होता। CI (`CI` / `GITHUB_ACTIONS`) में download skip हो जाता है।

Tool calls 5 मिनट तक चल सकती हैं। `script-execute` snippet को खुद रोक देता है जब उसका `timeout` (default 30 s) ख़त्म हो जाता है।

Development के लिए, `GODOT_MCP_SERVER_PATH=<absolute path>` editor से pinned release की जगह वह मौजूदा `gamedev-mcp-server` binary शुरू करवाता है (कोई download नहीं, कोई version check नहीं, कोई orphan cleanup नहीं)। इसे हर plugin assembly load पर एक बार, process environment या प्रोजेक्ट की `.env` से पढ़ा जाता है; ऐसा path जो किसी file का नाम नहीं है, warning के साथ ignore हो जाता है। Linux और macOS पर file पहले से executable होनी चाहिए।

<a id="run-it-yourself"></a>
### इसे खुद चलाएँ

[GameDev-MCP-Server release](https://github.com/IvanMurzak/GameDev-MCP-Server/releases) download करें या [`aigamedeveloper/mcp-server`](https://hub.docker.com/r/aigamedeveloper/mcp-server) Docker image इस्तेमाल करें:

```bash
./gamedev-mcp-server --client-transport streamableHttp --port 8080   # HTTP
./gamedev-mcp-server --client-transport stdio                        # MCP client इसे सीधे launch करता है
```

फिर Custom mode में plugin को उसकी ओर point करें (`GODOT_MCP_HOST=http://localhost:8080`)। हर argument और environment variable [GameDev-MCP-Server README](https://github.com/IvanMurzak/GameDev-MCP-Server#readme) में है।

<a id="editor-side-security-notes"></a>
## Editor-side security notes

- **Editor token plaintext में stored है।** Plugin अपना connection config, `token` और `cloudToken` समेत, `user://godot-mcp-config.json` में सादे JSON के रूप में save करता है। उस file को secret मानें: उसे commit या share न करें। Rotate करने के लिए dock में token clear करें (या file delete करें) और फिर से connect करें। Process environment से, या Custom mode में `.env` से आया token इस file में नहीं लिखा जाता।
- **Dev-control bridge तब तक बंद है जब तक `GODOT_MCP_DEV_CONTROL=1` न हो।** यह tests में dock चलाने के लिए सिर्फ़ development वाला HTTP bridge है: सिर्फ़ editor, `127.0.0.1` पर bound, unauthenticated, और उस variable के बिना कभी शुरू नहीं होता।

विवरण: [runtime-security.md](runtime-security.hi.md#editor-side-surfaces-accepted-posture)।
