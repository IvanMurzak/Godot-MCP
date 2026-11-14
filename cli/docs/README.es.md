<div align="center">

<p><img src="https://raw.githubusercontent.com/IvanMurzak/Godot-MCP/docs/readme-refresh/docs/img/cli-hero.svg" alt="godot-cli: instala el addon Godot MCP, inicia sesión, configura tu agente de IA y controla el editor de Godot desde una terminal" width="100%"/></p>

[![CLI](https://img.shields.io/npm/v/godot-cli?label=CLI&color=BE185D&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=npm)](https://www.npmjs.com/package/godot-cli) [![Node](https://img.shields.io/badge/Node-22.14%2B-0E7490?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=nodedotjs)](https://nodejs.org/) [![License](https://img.shields.io/badge/license-Apache--2.0-047857?style=for-the-badge&labelColor=0F172A&logoColor=white)](https://github.com/IvanMurzak/Godot-MCP/blob/main/LICENSE) [![Discord](https://img.shields.io/badge/Discord-join-5865F2?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=discord)](https://discord.gg/cfbdMZX99G) [![Stand with Ukraine](https://raw.githubusercontent.com/IvanMurzak/IvanMurzak/main/images/ukraine/flag-badge.svg)](https://stand-with-ukraine.pp.ua)

[English](../README.md) · [中文](README.zh-CN.md) · [日本語](README.ja.md) · **Español** · [فارسی](README.fa.md) · [हिन्दी](README.hi.md) · [العربية](README.ar.md)

</div>

**`godot-cli`** es la línea de comandos de [Godot-MCP](https://github.com/IvanMurzak/Godot-MCP), el addon del editor de Godot que conecta agentes de IA (Claude Code, Cursor, Codex, GitHub Copilot, …) con el editor de Godot mediante el [Model Context Protocol](https://modelcontextprotocol.io/). Es hermana de [`unity-mcp-cli`](https://www.npmjs.com/package/unity-mcp-cli) y [`unreal-mcp-cli`](https://www.npmjs.com/package/unreal-mcp-cli), respaldada por [ai-game.dev](https://ai-game.dev).

<p align="center"><img src="https://raw.githubusercontent.com/IvanMurzak/Godot-MCP/docs/readme-refresh/docs/img/terminal.svg" alt="Una sesión de terminal: godot-cli instala el addon, inicia sesión, configura Claude Code y abre el editor" width="100%"/></p>

<a id="features"></a>
## Funciones

✅ **Instalación en un comando** — `install-plugin` añade el addon y las referencias NuGet y lo activa\
✅ **Inicia sesión una vez** — `login` usa el flujo de dispositivo OAuth; el editor toma la credencial\
✅ **Configura el agente** — `setup-mcp` escribe la config MCP para Claude Code, Cursor, Codex y más\
✅ **Control del editor** — `open`, `close`, `status` y `wait-for-ready` para el editor del proyecto\
✅ **Ejecuta herramientas** — `rql`, `exec`, `run-tool` inician un editor headless si no hay ninguno\
✅ **CI headless** — `check`, `import`, `test`, `export` y `run-script` sin un editor abierto\
✅ **Extensiones** — `install-extension` añade módulos RQL opcionales; `update` lo actualiza todo

<a id="quick-start"></a>
## Inicio rápido

Requiere [Node.js](https://nodejs.org/) 22.14 o posterior, y un editor Godot 4.3+ .NET con el SDK de .NET 8. Funciona en Windows, macOS y Linux.

```bash
npm install -g godot-cli

godot-cli install-plugin ./MyGodotProject          # addon + referencias NuGet + activar el plugin
godot-cli login                                    # iniciar sesión en ai-game.dev, una vez por equipo
godot-cli setup-mcp claude-code ./MyGodotProject   # escribir la configuración MCP del agente
godot-cli open ./MyGodotProject                    # compilar C#, iniciar el editor, conectar
godot-cli wait-for-ready ./MyGodotProject          # esperar hasta que el plugin responda
```

Ejecuta C# o RQL en el editor desde la carpeta del proyecto; si no hay ningún editor abierto se inicia uno headless:

```bash
godot-cli exec 'EditorInterface.Singleton.GetEditedSceneRoot()?.Name'
godot-cli rql 'nodes | where name starts-with "Tree" | count'
```

O ejecuta cualquier comando sin instalar: `npx godot-cli install-plugin ./MyGodotProject`.

<a id="commands"></a>
## Comandos

- **`open [path]`** — Compila el ensamblado C# del proyecto (para que el addon cargue en la primera apertura; ver abajo), localiza el binario del editor de Godot y lanza `--editor --path <project>` con las variables de entorno de conexión `GODOT_MCP_*`. `--no-build` omite la compilación. `--headless` inicia en su lugar una sesión de editor sin ventana (ver [`exec`](#exec)).
- **`exec [code]`** — Ejecuta C# en el editor mediante `script-execute` (ver [`exec`](#exec)): reutiliza un editor en marcha o, si no, inicia uno headless y lo mantiene activo.
- **`create-project [path]`** — Crea la estructura de un nuevo proyecto Godot; `--dotnet` también escribe un `.csproj` de Godot.NET.Sdk, `--name` define su nombre.
- **`build [path]`** — Compila el ensamblado C# del proyecto (`dotnet build`) para que el addon `godot_mcp` cargue en la próxima apertura del editor. En proyectos solo GDScript (sin `.csproj`) no hace nada. Es la misma compilación que `open` ejecuta antes de lanzar.
- **`rql [query]`** — Una consulta RQL desde la línea de comandos, sin JSON: `godot-cli rql 'nodes | where name starts-with "Tree" | count'`. Una consulta cuya última etapa es `set` o `call` va a `rql-mutate` (`--dry-run`, `--max-targets <n>`), cualquier otra a `rql-query`; `--schema [type]` ejecuta `rql-schema`; `-f <file>` (`-f -`: stdin) lee la consulta cuando un shell elimina sus comillas. Inicia un editor headless como `run-tool`. Tras `SaveScene()`, `NewScene(...)`, `CreateResource(...)` o una edición `godot.resource(...)` muestra los archivos escritos (`Saved to disk: res://scenes/forest.tscn`); un operador escrito como en otro lenguaje (`startsWith`, `like`) recibe una pista con la forma de RQL. `rql --help` lista recetas.
- **`run-tool <tool> [path]`** — POST a `<url>/api/tools/<tool>` con entrada JSON. El modo texto muestra una respuesta de texto como su texto (`--raw`: el JSON de la respuesta; `--verbose`: también el endpoint). Si ningún editor responde en el servidor local del proyecto, inicia uno headless como `exec` (`--no-start` lo desactiva; `--once` lo cierra tras la llamada).
- **`run-system-tool <tool> [path]`** — POST a `<url>/api/system-tools/<tool>` (herramientas no expuestas a los clientes MCP). Mismo inicio automático que `run-tool`.
- **`check [path]`** — Puerta de CI sin un editor en marcha: importación, `dotnet build`, arranque del editor y una comprobación de carga de GDScript, evaluados a partir de los logs (ver [comandos headless](#headless-commands-check-import-test-export-run-script)).
- **`import [path]`** — `godot --headless --import`, que falla ante errores de importación y de script en el log.
- **`test [path]`** — Ejecuta las pruebas GUT o gdUnit4 del proyecto en modo headless con un tiempo límite estricto; veredicto a partir del informe JUnit y el log.
- **`export <preset> <output>`** — Exporta un preset en modo headless (`--debug`, `--pack`); comprueba antes las plantillas de exportación y después la salida.
- **`run-script <script> [args...]`** — Ejecuta un GDScript `SceneTree` de un solo uso (`godot --headless -s`); sale con 0 solo tras `quit(0)`.
- **`commands`** — Manifiesto legible por máquina de todos los comandos, sus argumentos y opciones, y los códigos de salida (`--format json`).
- **`tools [name]`** — Las herramientas de IA del editor (MCP `tools/list`; inicia un editor headless si ninguno responde, guarda la lista en caché en `.ai-game-dev/tools.json`, ignorado por git). Con un nombre: su descripción, argumentos y un ejemplo de llamada `run-tool`. `--refresh` vuelve a preguntar al editor; `--no-start` usa la caché o la lista integrada.
- **`status [path]`** — Detecta un editor de Godot en marcha para el proyecto y comprueba el estado del servidor MCP.
- **`wait-for-ready [path]`** — Sondea el servidor MCP hasta que responde a `ping`.
- **`login [path]`** — Se autentica en la nube de ai-game.dev mediante el flujo de autorización de dispositivo de OAuth 2.1 (RFC 8628): abre un navegador y luego guarda una credencial de nube en el almacén compartido del equipo (`~/.ai-game-dev/credentials.json`), que el plugin del editor adopta automáticamente. Consulta la sección `login` más abajo para sus flags `--project` / `--base-url` / `--force` / `--tools-only` / `--yes`.
- **`setup-mcp <agent> [path]`** — Escribe la configuración del cliente MCP del agente apuntando a la URL `<host>/mcp/p/<pin>` fijada al proyecto (para que el agente llegue al editor de *este* proyecto). En modo Cloud la configuración lleva la clave de este proyecto (`Authorization: Bearer agd_pk_…`). Añade `--no-pin` para la URL simple `<host>/mcp`, `--oauth` para una configuración solo con URL, `--regenerate-key` para sustituir la clave.
- **`setup-skills <agent> [path]`** — Escribe el skill de agente `godot-cli` (flujo de arranque en frío, recetas RQL, códigos de salida) y `ai-game-dev-app` en la ruta de skills del agente, sin conexión. `--list` muestra la compatibilidad con skills de cada agente.
- **`configure [path]`** — Lista / muestra / oculta herramientas, prompts y recursos en el `.godot-mcp/features.json` local del proyecto (`--enable-*` muestra, `--disable-*` oculta; una herramienta oculta sigue ejecutándose si se llama por su nombre).
- **`close [path]`** — Detiene de forma ordenada el editor de Godot en marcha para un proyecto (`--force` para matarlo). También detiene el servidor MCP local que ejecutaba ese editor, si le sobrevivió. `--owned-only` cierra solo un editor que inició `godot-cli` (`exec` / `open --headless`).
- **`install-plugin [path]`** — Instala el addon `godot_mcp` de principio a fin: materializa `res://addons/godot_mcp/` (descarga la release de GitHub correspondiente, o `--source <path>` una copia local), añade al `.csproj` del proyecto los `PackageReference` de NuGet que necesita el addon instalado + el `<EmbeddedResource>` del catálogo de extensiones, y activa el plugin. Idempotente.
- **`install-extension <id> [path]`** — Instala una **extensión** de Godot-MCP (un paquete opcional de módulo RQL) en el proyecto: resuelve `<id>` en el catálogo compartido, añade/actualiza su `<PackageReference>` en el `.csproj` del proyecto y luego recompila para restaurar. Idempotente; con un comportamiento idéntico al del dock del editor.
- **`remove-plugin [path]`** — Desactiva el addon `godot_mcp` en `[editor_plugins]` de `project.godot` (no borra los archivos del addon).
- **`update [path]`** — Lo actualiza todo: el addon `godot_mcp` del proyecto y las extensiones instaladas a sus últimas versiones, y luego el propio `godot-cli`. Fuera de un proyecto solo actualiza la CLI. `--check` informa (sale con 2 si algo está desactualizado); `--no-self`, `--no-project`, `--no-extensions` omiten un paso.

<a id="build-before-open-open--build"></a>
### Compilar antes de abrir (`open` / `build`)

`open` **compila el ensamblado C# del proyecto antes de lanzar el editor**. Godot instancia el `EditorPlugin` de un addon activado en cuanto carga el editor, así que en la *primera* apertura de un proyecto C# que nunca se compiló todavía no existe ningún ensamblado y Godot falla con `Unable to load addon script 'res://addons/godot_mcp/Editor/GodotMcpPlugin.cs' … Disabling the addon`. Compilar primero garantiza que el ensamblado exista cuando se instancia el addon.

- La compilación es la misma que ejecuta `godot-cli build`: `dotnet build <project>.csproj --configuration Debug`.
- Los **proyectos solo GDScript** (sin `.csproj` en la raíz) se omiten automáticamente: no hay nada que compilar.
- La compilación se ejecuta **siempre** en proyectos C#; `dotnet build` es incremental, así que en un proyecto al día no hace nada y es rápida. Pasa `--no-build` a `open` para omitirla (p. ej., si ya compilaste o quieres abrir lo más rápido posible).
- Si la compilación **falla**, `open` **no** lanza el editor (lanzarlo solo reproduciría el fallo que desactiva el addon); en su lugar muestra el error.

<a id="editor-resolution-open"></a>
### Localización del editor (`open`)

`open` localiza el binario del editor de Godot en este orden:

1. `--editor-path <path>` (explícito).
2. Las variables de entorno `GODOT_BIN` / `GODOT4_BIN`.
3. El primer binario de Godot que coincida en el `PATH` (en Windows se prefiere la versión mono).
4. Directorios de instalación habituales de cada SO: Windows prefiere la versión mono; macOS resuelve el binario `.app/Contents/MacOS/Godot`; Linux examina las ubicaciones habituales de binarios extraídos.

<a id="connection-env-vars"></a>
### Variables de entorno de conexión

`open` reenvía estas al proceso del editor (los nombres coinciden con el `GodotMcpConfig` del addon):

| Flag | Variable de entorno |
| --- | --- |
| `--url <host>` | `GODOT_MCP_HOST` |
| `--cloud-url <url>` | `GODOT_MCP_CLOUD_URL` |
| `--token <token>` | `GODOT_MCP_TOKEN` |
| `--auth None\|Required` | `GODOT_MCP_AUTH_OPTION` |
| `--mode Cloud\|Custom` | `GODOT_MCP_CONNECTION_MODE` |
| `--log-level <level>` | `GODOT_MCP_LOG_LEVEL` |

El modo predeterminado del addon es Cloud (ai-game.dev): no necesita puertos abiertos, evita conflictos de puertos con otros programas y es gratuito. Para un servidor local que exija un token elegido por ti (trabajo sin conexión o en entornos aislados), abre el editor en modo Custom con ese token y luego pulsa **Start Server** en el dock (`Required` selecciona la autorización `token`):

```bash
godot-cli open ./MyGodotProject --mode Custom --auth Required --token <token>
```

<a id="server-url-resolution-run-tool--status--wait-for-ready"></a>
### Resolución de la URL del servidor (`run-tool` / `status` / `wait-for-ready`)

La URL base del servidor a la que una llamada a herramienta hace POST (`<base>/api/tools/<name>`, o `<base>/api/system-tools/<name>` para herramientas de sistema; `status` / `wait-for-ready` sondean la herramienta de sistema `ping`) se resuelve así:

1. `--url <url>` (sustitución explícita).
2. La variable de entorno `GODOT_MCP_HOST` (host del modo Custom).
3. La variable de entorno `GODOT_MCP_CLOUD_URL` → la URL del **hub `/mcp`** de la nube (se añade `/mcp` si falta).
4. `GODOT_MCP_CONNECTION_MODE=Cloud` → el hub de nube predeterminado `https://ai-game.dev/mcp`.
5. El **marcador de proyecto inscrito** (`serverTarget` de `.ai-game-dev/project.json`, escrito por `install-plugin --enroll`): un destino alojado se dirige a su hub `/mcp`, un destino `localhost` se usa tal cual. Así, **un proyecto de nube inscrito es accesible sin ninguna configuración de entorno**: la credencial de nube persistida (almacén por proyecto `<project>/.ai-game-dev/`, luego el destino de proyecto heredado y luego `~/.ai-game-dev/credentials.json`) se usa automáticamente y se renueva al caducar.
6. Alternativa local: `http://localhost:<derived-port>`, el puerto determinista v2 derivado de la ruta del proyecto que el addon del editor usa localmente (un `portOverride` del marcador prevalece sobre el puerto derivado del hash).

Las rutas de nube llevan el segmento de hub `/mcp` para que la petición llegue al hub y no al backend de la nube (que no sirve `/api/tools/*`). Pasa `--url http://localhost:<port>` para apuntar explícitamente a un servidor local/autoalojado.

`run-tool`, `run-system-tool` y `exec` sustituyen el paso 6 por el **modo de conexión del proyecto**, leído igual que lo lee el addon (`GODOT_MCP_CONNECTION_MODE`, el `.env` del proyecto, el `user://godot-mcp-config.json` persistido y, si no, Cloud, el predeterminado del addon). El modo Custom usa el servidor local anterior. El modo Cloud usa el hub de nube con el inicio de sesión del equipo (no se inicia ningún editor: solo responde un editor conectado a ai-game.dev). Si este equipo **nunca** ha iniciado sesión en ai-game.dev, se usa en su lugar el servidor local del proyecto, con una advertencia (lo sirve un editor iniciado en ese momento; no se persiste nada). Un inicio de sesión que existe pero no se puede usar (caducado, revocado, ilegible) es un error (salida 6, `NOT_AUTHENTICATED`): un equipo con sesión iniciada nunca se cambia a un servidor local. Lo mismo vale para `open`, `tools` y `open --headless`: en un proyecto Cloud de un equipo con sesión iniciada, `open --headless` no inicia ningún editor y sale con 6 (usa `open`, `run-tool` y `exec`, que pasan por ai-game.dev). Un `--url` / `--token` explícito se usa tal cual; un `--url` / `GODOT_MCP_HOST` no local lo llama `exec` directamente y `open --headless` lo rechaza (un editor headless es local).


<a id="exec"></a>
## `exec`

Ejecuta C# en el editor de Godot del proyecto (la herramienta `script-execute` del addon, sobre [Code-Exec](https://github.com/IvanMurzak/Code-Exec)):

```bash
godot-cli exec '1 + 2'                                   # → System.Int32: 3
godot-cli exec -f snippet.cs --using MyGame.Gameplay      # código desde un archivo ("-" o una tubería: stdin)
godot-cli exec 'Helper.Twice(21)' --include helpers       # archivos/carpetas .cs en <project>/.ai-game-dev/snippets
```

El código son sentencias simples; la última expresión es el resultado. La salida es el valor (`Type: value`) y, bajo `logs:`, lo que imprimió el código (`Console`; `GD.Print` / push_warning / push_error con Godot.NET.Sdk 4.5+). Un error de compilación señala la línea del fragmento (`snippet.cs(2,12)`) y sale con 6.

- **Un editor del proyecto en marcha** (el tuyo, con ventana, o uno que inició `godot-cli`) se reutiliza y nunca se cierra. El tuyo debe servir el servidor **local** del proyecto (dock: modo Custom, servidor iniciado).
- **Ninguno en marcha:** se compila el ensamblado C# y luego se inicia un editor **headless** (`--headless --editor --path`, o con ventana usando `--window`) conectado al servidor local del proyecto (`http://localhost:<derived port>`, o `--url`), que su plugin inicia por sí mismo. Uno headless se cierra tras `--idle <seconds>` (300 por defecto, `0` = nunca) sin llamadas a herramientas, guardando las escenas abiertas que tienen archivo (uno con ventana sigue abierto). Las llamadas en paralelo compilan una vez e inician un solo editor; si el servidor local del editor de un usuario no responde, se espera hasta 60 s y luego se informa.
- `open --headless [--idle-timeout <s>]` inicia la misma sesión sin ejecutar código; `close --owned-only` la guarda y la cierra (mediante la herramienta `session-close` del plugin; nunca el editor de un usuario).

Las sesiones usan `EditorSession` de [`@baizor/gamedev-cli-core`](https://www.npmjs.com/package/@baizor/gamedev-cli-core): la concesión `<project>/.ai-game-dev/session.json` marca un editor como iniciado por la CLI (solo un editor así se cierra alguna vez) y registra el servidor en el que se ejecuta: mientras ese editor siga vivo de forma demostrable, las llamadas posteriores a `exec` / `open` / `close` usan ese servidor, diga lo que diga `--url`. `<project>/.ai-game-dev/session.lock` hace que las llamadas concurrentes inicien un solo editor: la compilación de C# se ejecuta bajo ese bloqueo, solo en la llamada que inicia el editor. Ambos archivos están en `<project>/.ai-game-dev/.gitignore`. El editor recibe `AIGD_SESSION_IDLE_SECONDS`, `AIGD_SESSION_OWNER=cli` y `AIGD_SESSION_NONCE`, además de `GODOT_MCP_CONNECTION_MODE=Custom`, `GODOT_MCP_HOST` y `GODOT_MCP_START_SERVER=true`. Opciones: `--ready-timeout <ms>`, `--timeout <ms>` (llamada a herramienta, 310000 por defecto: algo más que el `tool-timeout` de 5 minutos del servidor), `--no-build`, `--editor-path`, `--token`, `--raw`.

`run-tool` / `run-system-tool` siguen la misma política cuando la llamada se rechaza en una URL **local** de un proyecto Godot: inician un editor headless (compilando antes C# salvo con `--no-build`) y reintentan. Un editor en marcha se reutiliza y nunca se cierra; `--once` cierra el editor después solo si lo inició esta llamada. `--no-start` lo desactiva, y una URL de nube o remota nunca inicia nada. Opciones: `--editor-path`, `--ready-timeout <ms>`.

<a id="exit-codes-and-output-formats"></a>
## Códigos de salida y formatos de salida

Los códigos de salida se comparten con `unity-mcp-cli` y `unreal-mcp-cli` (`godot-cli commands --format json` los lista):

| Código | Significado |
| --- | --- |
| 0 | Éxito |
| 1 | Error genérico o sin clasificar; también una ejecución interrumpida con Ctrl+C / SIGTERM (se mata el árbol de procesos de Godot) |
| 2 | Error de uso: comando u opción desconocidos, valor de flag o argumento incorrecto, una herramienta desconocida (`NOT_FOUND`) (`--help` / `--version` salen con 0) |
| 6 | El comando se ejecutó y falló: la herramienta devolvió un error, errores de script o de compilación, sin veredicto de pruebas, credenciales rechazadas |
| 7 | El editor o el servidor MCP no es accesible (también HTTP 502) |
| 8 | Las pruebas se ejecutaron y al menos una falló (cuenta un fallo de setup/teardown a nivel de suite) |
| 14 | Tiempo agotado (también HTTP 408 / 504) |
| 17 | El servidor está ocupado (HTTP 503 / 429); la llamada no se ejecutó |
| 18 | El editor está bloqueado por un diálogo modal (reservado: no detectable en un Godot headless) |

`update --check` sigue saliendo con 2 si algo está desactualizado.

**Una llamada a herramienta que no tuvo éxito** (`run-tool`, `run-system-tool`, `exec`, `rql`, `tools`) se asigna al mismo código de salida y código de error en las tres CLI (la tabla vive en `@baizor/gamedev-cli-core`; `commands --format json` la lista como `errorCodes`). Cuando el servidor MCP respondió, decide su estado HTTP, y el error del sobre lo incluye: `errors[{code, message, httpStatus}]`.

| Condición | Estado HTTP | Salida | `errors[].code` |
|---|---|---|---|
| Argumentos incorrectos: un flag erróneo, argumentos de herramienta que la CLI o el servidor rechazaron (cualquier 4xx no listado aquí) | 400 (un 422 `[invalid_params]`) | `2` | `USAGE` |
| No hay ninguna herramienta con ese nombre (lístalas con `tools`), o no hay proyecto en la ruta | 404 | `2` | `NOT_FOUND` |
| No hay credenciales utilizables, o el servidor las rechazó: inicia sesión con `login` | 401, 403 | `6` | `NOT_AUTHENTICATED` |
| La herramienta se ejecutó e informó de un error (`status: "error"`, cualquier 5xx no listado aquí) | 409, 422, 500 | `6` | `TOOL_FAILED` |
| El código del proyecto no compiló, así que no se inició ningún editor | — | `6` | `COMPILATION_FAILED` |
| Conexión rechazada o reiniciada, ningún plugin de motor conectado al servidor, plugin perdido tras enviar la llamada | 502 | `7` | `UNREACHABLE` |
| Sin respuesta a tiempo: `--timeout`, o el tiempo límite de herramienta del propio servidor | 408, 504 | `14` | `TIMEOUT` |
| Ocupado y no ejecutó la llamada (recargando, con límite de tasa, otra llamada de CLI ocupa el editor); reintenta más tarde | 503, 429 | `17` | `BUSY` |
| Cualquier otra cosa (la llamada se abortó, un error inesperado) | — | `1` | `ERROR` |

`--format text|json|ndjson` (por defecto `text`) lo aceptan `run-tool`, `run-system-tool`, `exec`, `status`, `wait-for-ready`, `check`, `import`, `test`, `export`, `run-script` y `commands`. `json` imprime exactamente un sobre en stdout:

```json
{ "success": false, "command": "check", "data": { "...": "..." },
  "errors": [{ "code": "SCRIPT_ERRORS", "message": "res://player.gd:12: error: Expected expression." }],
  "warnings": [] }
```

`ndjson` imprime eventos en stderr, un objeto JSON por línea (`{"type":"progress","command":…,"message":…}`, y cada otra línea como `{"type":"log","command":…,"level":"info|error","message":…}`), y el sobre como una línea en stdout. En ambos, los logs, spinners y encabezados van a stderr sin color. Códigos de error: `USAGE`, `TOOL_FAILED`, `UNREACHABLE`, `TIMEOUT`, `BUSY`, `AWAITING_INPUT`, `TESTS_FAILED`, `TEST_RUN_ERROR`, `COMPILATION_FAILED`, `SCRIPT_ERRORS`, `IMPORT_FAILED`, `PROJECT_OPEN`, `NOT_FOUND`, `EXPORT_FAILED`, `NOT_AUTHENTICATED`, `CLOUD_PROJECT`, `ERROR`. `--raw` sigue funcionando como antes.

<a id="headless-commands-check-import-test-export-run-script"></a>
## Comandos headless (`check`, `import`, `test`, `export`, `run-script`)

Ejecutan Godot sin una sesión de editor y no necesitan servidor MCP. En modo headless el código de salida de Godot no es un veredicto (un `--import` con un error de análisis de GDScript, o un addon que no carga, sigue saliendo con 0), así que cada comando evalúa la ejecución a partir de su log (`SCRIPT ERROR`, `ERROR:`, `Parse Error`), de un marcador que demuestra que el paso se ejecutó y de los archivos que debe producir. Cada proceso tiene un `--timeout` estricto (se mata todo el árbol de procesos de Godot: salida 14). Los logs y los scripts temporales van a `<project>/.godot/godot-cli/`. `check`, `import`, `test` y `export` rechazan un proyecto abierto en un editor (salida 6, `PROJECT_OPEN`: una importación entra en conflicto con ese editor por `.godot/`); `--allow-open` lo anula y `close` detiene un editor de `godot-cli`. El binario se obtiene de `--editor-path`, `GODOT_BIN`, `PATH` o las carpetas de instalación habituales.

```bash
godot-cli check                     # 0 limpio, 6 SCRIPT_ERRORS / COMPILATION_FAILED, 14 tiempo agotado
godot-cli check --strict            # las advertencias del compilador de GDScript y C# también fallan
godot-cli test --format github      # anotaciones ::error; 0 aprobado, 8 fallos, 6 sin veredicto / sin runner
godot-cli export "Windows Desktop" build/game.exe
godot-cli run-script tools/migrate.gd -- --dry-run
```

- **`check [path] [--strict] [--include-addons]`** ejecuta cuatro procesos separados (un único `--build-solutions --quit` en frío se cierra con fallo con los plugins C# `[Tool]`): `--headless --import`; `dotnet build` para un proyecto C# (errores como `file(line,col): error CS…`); `--headless --editor --quit`, que debe imprimir `[Godot-MCP] plugin loaded` si el addon está activado; y una pasada de GDScript que carga cada `.gd` bajo `res://` (`res://addons/` solo con `--include-addons`) e informa de los errores de análisis como `res://file:line`. `--strict` también falla con advertencias de GDScript (las advertencias de nivel Warn del proyecto elevadas a errores en memoria, nunca guardadas) y advertencias `CS` de C#. Los mensajes del motor (por ejemplo, advertencias de runtime de un plugin) se listan como advertencias y nunca hacen fallar la comprobación.
- **`import [path]`** falla ante errores de script (`SCRIPT_ERRORS`) y ante recursos que no se importan (`IMPORT_FAILED`).
- **`test [path]`** encuentra GUT (`addons/gut`) o gdUnit4 (`addons/gdUnit4`), importa el proyecto (`--no-import` lo omite) y ejecuta el script de línea de comandos del runner con un informe JUnit (`-gjunit_xml_file`, o gdUnit4 `-rd`; gdUnit4 se ejecuta con `--ignoreHeadlessMode -c`). Sale con 0 si pasan todas las pruebas, 8 si hay fallos, 6 (`TEST_RUN_ERROR`) si no hay informe, no se ejecutó ninguna prueba o un script de prueba no cargó (sus pruebas faltan del informe, así que no debe aprobarse), 6 (`NOT_FOUND`) con una pista de instalación si no hay ningún runner instalado. `--runner gut|gdunit4`, `--dir <dir>` (repetible; por defecto `res://test`, `res://tests` o el `.gutconfig.json` de GUT), `--filter <text>` (scripts de prueba cuyo nombre de archivo lo contiene), `--output <file>` (copia el XML JUnit), `--format text|json|ndjson|github`.
- **`export <preset> <output> [--debug|--pack]`** comprueba que `export_presets.cfg` tenga el preset y que estén instaladas las plantillas de exportación de la versión del editor (salvo con `--pack` o si el preset indica su propia plantilla), ejecuta `--export-release|--export-debug|--export-pack` y falla (`EXPORT_FAILED`) salvo que el archivo de salida exista, no esté vacío y lo haya escrito esta ejecución, y que el log no tenga errores de exportación (Godot escribe el ejecutable y sale con 0 incluso cuando, por ejemplo, la publicación de C# falló por falta de un `.sln`).
- **`run-script <script> [args...]`** ejecuta `godot --headless --path <project> -s <script> -- <args>`: el script extiende `SceneTree` o `MainLoop` y termina con `quit(code)`. Sale con 0 para `quit(0)`; 6 para otro código (`data.scriptExitCode`) o un script que no cargó; 14 con `--timeout` (300000 ms por defecto). No importa, así que también funciona mientras un editor tiene el proyecto abierto.

<a id="login"></a>
## `login`

`godot-cli login` autentica la conexión de **nube** del plugin del editor con [ai-game.dev](https://ai-game.dev) usando el **flujo de autorización de dispositivo de OAuth 2.1** (RFC 8628): muestra un código de usuario corto + la URL de verificación, abre tu navegador y sondea hasta que apruebas:

```bash
godot-cli login                       # iniciar sesión una vez por equipo (predeterminado)
godot-cli login --project ./MyGame    # guardar en su lugar una credencial por proyecto
godot-cli login --base-url <url>      # autenticarse con un servidor no predeterminado
godot-cli login --force               # volver a autenticarse sobre una credencial existente
godot-cli login --tools-only         # CI/automatización: solo credencial de herramientas (sin recogida por la App)
godot-cli login --yes                # no interactivo: confirmar la sustitución de la credencial de otra cuenta
```

- Por defecto la credencial se guarda en el **almacén compartido del equipo** `~/.ai-game-dev/credentials.json` (`0600` en POSIX / DPAPI en Windows): inicia sesión **una vez por equipo** y el plugin del editor de Godot la adopta automáticamente, así que `godot-cli open --mode Cloud` se conecta **sin `--token`**.
- El login predeterminado inicia sesión en todo el equipo: el flujo solicita una concesión de **ámbito de agente** y deriva de ella la **credencial de herramientas (plugin)**, más restringida, mediante intercambio de tokens RFC 8693, de modo que todas las herramientas de AI-Game-Dev del equipo (plugins de motor, CLI, la App de escritorio) recogen el inicio de sesión.
- `--tools-only` emite **solo** la credencial de herramientas: no se guarda nada con ámbito de agente, la App de escritorio no puede adoptar el inicio de sesión y el runner aparece como su propio dispositivo revocable en [ai-game.dev](https://ai-game.dev). Úsalo para CI y runners de automatización compartidos.
- Si el equipo ya tiene la credencial de **otra cuenta**, login pregunta antes de sustituirla (rechazar revoca la credencial recién emitida y deja el almacén intacto). Pasa `--yes` para confirmar de forma no interactiva.
- `--project <path>` (o el `[path]` posicional) guarda una credencial por proyecto (`<path>/.ai-game-dev/credentials.json`, ignorado por git) para cuentas por proyecto. El antiguo destino por proyecto `<path>/.godot-mcp/credentials.json` **ya no se escribe**; todavía se lee (y se migra al almacén del equipo en el primer uso) durante una release.
- El flujo persiste el conjunto **completo** de credenciales (token de acceso + token de actualización rotativo + caducidad): **nunca se emite un token de acceso personal (PAT)**. Ante cualquier fallo no se escribe nada, así que una credencial existente sobrevive intacta a un rechazo / caducidad / error de red. Los comandos renuevan automáticamente un token de acceso caducado (mediante `@baizor/gamedev-cli-core`), de modo que un equipo con sesión iniciada sigue con sesión iniciada sin volver a ejecutar `login`.

<a id="setup-skills"></a>
## `setup-skills`

`godot-cli setup-skills <agent> [path]` genera **archivos de skill** de agente de IA para un proyecto Godot en la ruta de skills del agente seleccionado (p. ej., `.claude/skills` de Claude Code). Usa `--list` para ver todos los agentes y su compatibilidad con skills.

```bash
godot-cli setup-skills claude-code            # generar en ./.claude/skills
godot-cli setup-skills cursor ./MyGame        # generar en MyGame/.cursor/skills
godot-cli setup-skills --list                 # listar agentes + sus rutas de skills
```

Escribe, sin conexión (sin editor, sin servidor, sin red):

- `godot-cli/SKILL.md`: cómo usa un agente esta CLI desde un arranque en frío: el primer comando de editor inicia un editor headless, `rql` con recetas para escenas, nodos y recursos, `check` / `test`, `--format json`, qué significa cada código de salida. Es breve a propósito (un agente mantiene en contexto cada byte que lee); `references/rql.md` y `references/install.md` contienen el detalle y solo se leen cuando hace falta. Las recetas vienen de la misma fuente que `godot-cli --help` y `rql --help`, así que el skill y la ayuda no pueden desincronizarse.
- `ai-game-dev-app/SKILL.md`: la app complementaria, para herramientas creativas que la CLI no tiene.

Es **idempotente**: volver a ejecutarlo reescribe los mismos bytes. Elimina los skills `godot-mcp` / `godot-mcp-<family>` que versiones anteriores generaban a partir de un catálogo escrito a mano. Los skills por herramienta son cosa del addon: los genera a partir de las herramientas que realmente registra cada vez que se ejecuta el editor (se activa en el panel Skills del addon), junto con su guía `godot-cli-install`.

<a id="install-plugin"></a>
## `install-plugin`

`godot-cli install-plugin [path]` es un **instalador real**: hace que una instalación desde cero en terminal produzca un proyecto funcional, con un solo comando idempotente:

```bash
godot-cli install-plugin ./MyGodotProject                      # descargar la release correspondiente + instalar
godot-cli install-plugin --version <x.y.z> ./MyGodotProject   # fijar una release concreta del addon
godot-cli install-plugin --source ./Godot-MCP/addons/godot_mcp ./MyGodotProject   # copia sin conexión / de desarrollo
```

Realiza cuatro pasos:

1. **Materializa `res://addons/godot_mcp/`.** Por defecto descarga `godot-mcp-addon-<version>.zip` por HTTPS **solo desde github.com** (la release `v<version>` de `IvanMurzak/Godot-MCP`; la versión es por defecto la de la propia CLI). Se rechazan los hosts que no son `github.com` y el `http` plano. Con `--source <path>` copia el addon desde un directorio local (sin red); `<path>` puede ser un directorio que *es* `addons/godot_mcp` o uno que lo *contiene*.
2. **Añade los paquetes NuGet** que necesita el addon (`com.IvanMurzak.ReflectorNet`, `com.IvanMurzak.McpPlugin`) al `.csproj` del proyecto, de forma idempotente: los añade si faltan, corrige una versión obsoleta y deja intacta una versión fijada correcta. Las versiones vienen del addon que se instala: su `nuget-deps.json` (generado a partir de `Godot-MCP.csproj` en cada zip de release por el workflow de release) o, con `--source` apuntando a un checkout de Godot-MCP, el `Godot-MCP.csproj` de ese checkout. Los zips antiguos del addon sin ese manifiesto recurren a las versiones integradas en la CLI, con una advertencia.
3. **Incrusta el catálogo de extensiones** (`<EmbeddedResource Include="addons/godot_mcp/extensions.catalog.json" LogicalName="Godot-MCP.extensions.catalog.json" />`) en el `.csproj` del proyecto, de forma idempotente. Sin él, el registro de extensiones del addon no lee ningún catálogo en tiempo de ejecución del editor y el **panel Extensions queda vacío**. Tiene una única fuente + pruebas de paridad contra el csproj del addon, junto con las versiones fijadas. También define `<EnableDynamicLoading>true</EnableDynamicLoading>` (la plantilla de Godot lo incluye; sin él, un juego ejecutado desde el editor no puede instanciar scripts C#).
4. **Activa el plugin** en `[editor_plugins]` de `project.godot`.

Es seguro como biblioteca (devuelve una unión `{ kind: 'success' | 'failure' }`; nunca lanza excepciones más allá de la frontera pública) e idempotente: volver a ejecutarlo en un proyecto ya instalado no informa de ningún cambio.

<a id="install-extension"></a>
## `install-extension`

`godot-cli install-extension <id> [path]` instala una **extensión** opcional de Godot-MCP (un paquete NuGet opcional que amplía RQL; no añade herramientas propias) en un proyecto Godot C#: es el canal de terminal/biblioteca para la misma instalación que realiza el dock **Extensions** del editor:

```bash
godot-cli install-extension com.IvanMurzak.Godot.MCP.CSG ./MyGodotProject   # añadir/actualizar el PackageReference
godot-cli install-extension "CSG Tools"                                      # resolver por nombre; cwd por defecto
godot-cli install-extension com.IvanMurzak.Godot.MCP.CSG --version <x.y.z>   # sustituir la versión fijada del catálogo
```

- Resuelve `<id>` en el **catálogo compartido de extensiones** (`addons/godot_mcp/extensions.catalog.json`, la única fuente de verdad que consumen el dock, la CLI y la app), buscando por id de paquete (luego por nombre), sin distinguir mayúsculas.
- Lee, modifica y escribe un `<PackageReference Include="<packageId>" Version="<version>" />` en el `.csproj` raíz del proyecto: **lo añade** si no existe, **sube** la versión solo cuando el catálogo (o `--version`) fija una versión más nueva, y **no hace nada** si ya está al día; luego te pide recompilar (`godot-cli build`) para que Godot restaure + compile el nuevo paquete.
- Comportamiento idéntico al `ExtensionInstaller` del dock (las mismas reglas de añadir / actualizar / no hacer nada + comparación numérica de versiones; verificado con un conjunto de escenarios compartido en ambos lados).

<a id="library-api"></a>
## API de biblioteca

El paquete también exporta una biblioteca sin efectos secundarios (la entrada `.`):

```ts
import { openProject, runTool, setupMcp, installPlugin, installExtension, execCode, ensureEditor } from 'godot-cli';

// También se exportan el catálogo compartido de extensiones + funciones de búsqueda, para que una GUI (la app)
// pueda mostrar la misma lista desde la que instalan el dock + la CLI:
import { EXTENSIONS_CATALOG, findExtension } from 'godot-cli';

// Los comandos headless, sus analizadores de log/JUnit y los códigos de salida:
import { checkProject, importProject, runTests, exportProject, runScript, parseGodotLog, parseJUnit, ExitCode } from 'godot-cli';
```

Cada función devuelve una unión discriminada (`{ kind: 'success', ... }` / `{ kind: 'failure', error }`) y nunca lanza excepciones más allá de la frontera pública.

<a id="development"></a>
## Desarrollo

```bash
npm install
npm run build   # tsc → dist/ (ESM)
npm test        # vitest
```


<a id="supported-ai-agents"></a>
## Agentes de IA compatibles

`godot-cli` escribe configuraciones de cliente MCP listas para usar para todos los agentes de programación de IA principales: ejecuta `godot-cli setup-mcp <agent> [path]` para conectar uno (y `godot-cli setup-skills <agent> [path]` para generar sus archivos de skill). Usa `--list` en cualquiera de los dos comandos para ver todos los agentes compatibles.

Los agentes compatibles incluyen Claude Code, Claude Desktop, Cursor, VS Code y Visual Studio (Copilot), GitHub Copilot CLI, Rider (Junie), Codex, Gemini, Antigravity, Cline, Open Code, Kilo Code y un cliente MCP genérico `custom`.

```bash
godot-cli setup-mcp --list                    # listar todos los ids de agente compatibles
godot-cli setup-mcp claude-code ./MyGame      # escribir la configuración de cliente MCP del agente (fijada a este proyecto)
godot-cli setup-mcp claude-code ./MyGame --no-pin   # escribir en su lugar la URL simple <host>/mcp, sin fijar
godot-cli setup-mcp claude-code ./MyGame --oauth    # solo URL: el agente inicia sesión con su propio OAuth
godot-cli setup-mcp claude-code ./MyGame --regenerate-key   # nueva clave de proyecto; la anterior se revoca
```

En modo Cloud, `setup-mcp` escribe una **clave de proyecto** en la configuración de cada agente: `Authorization: Bearer agd_pk_…` (Codex: `http_headers`). Una clave de proyecto nunca caduca, está ligada al pin de este proyecto (no puede llegar a ningún otro proyecto de tu cuenta) y se crea una vez por proyecto con el inicio de sesión del equipo (`godot-cli login`), y después se reutiliza desde `~/.ai-game-dev/project-keys.json`. `--regenerate-key` la sustituye y revoca la anterior. Sin inicio de sesión en el equipo (o mientras el servidor no pueda emitir claves), la configuración solo lleva la URL y el agente se autentica con su propio OAuth; `--oauth` pide explícitamente esa configuración solo con URL. Un `--token` explícito siempre prevalece. Una configuración de servidor local (`--url http://localhost:…`) nunca lleva clave de proyecto.

> Para la documentación completa del proyecto Godot-MCP, consulta el [README principal](../../docs/README.es.md). Respaldado por **[ai-game.dev](https://ai-game.dev)**.
