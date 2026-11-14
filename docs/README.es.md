<div align="center">

<p><img src="img/hero.svg" alt="Godot MCP: los agentes de IA trabajan en el editor de Godot a través del addon godot_mcp" width="100%"/></p>

[![CI](https://img.shields.io/github/actions/workflow/status/IvanMurzak/Godot-MCP/ci.yml?branch=main&label=CI&color=15803D&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=github)](https://github.com/IvanMurzak/Godot-MCP/actions/workflows/ci.yml) [![Release](https://img.shields.io/github/v/release/IvanMurzak/Godot-MCP?label=release&color=2F6F9F&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=github)](https://github.com/IvanMurzak/Godot-MCP/releases/latest) [![Asset Library](https://img.shields.io/badge/AssetLib-Godot--MCP-2F6F9F?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=godotengine)](https://godotengine.org/asset-library/asset/5245) [![CLI](https://img.shields.io/npm/v/godot-cli?label=CLI&color=BE185D&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=npm)](https://www.npmjs.com/package/godot-cli) \
[![Godot](https://img.shields.io/badge/Godot-4.3%2B%20.NET-0E7490?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=godotengine)](https://godotengine.org/) [![.NET 8](https://img.shields.io/badge/.NET-8%20SDK-0E7490?style=for-the-badge&labelColor=0F172A&logoColor=white)](https://dotnet.microsoft.com/) [![License](https://img.shields.io/badge/license-Apache--2.0-047857?style=for-the-badge&labelColor=0F172A&logoColor=white)](../LICENSE) [![Discord](https://img.shields.io/badge/Discord-join-5865F2?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=discord)](https://discord.gg/cfbdMZX99G) [![Stand with Ukraine](https://raw.githubusercontent.com/IvanMurzak/IvanMurzak/main/images/ukraine/flag-badge.svg)](https://stand-with-ukraine.pp.ua)

[English](../README.md) · [中文](README.zh-CN.md) · [日本語](README.ja.md) · **Español** · [فارسی](README.fa.md) · [हिन्दी](README.hi.md) · [العربية](README.ar.md)

</div>

**Godot-MCP** conecta agentes de IA (Claude Code, Cursor, Codex, GitHub Copilot, Gemini o cualquier otro cliente [MCP](https://modelcontextprotocol.io/)) con el **editor de Godot**. El agente puede leer y modificar tus escenas, nodos, recursos y scripts, compilar y ejecutar C# y ver capturas de pantalla. Es un addon de editor con una CLI complementaria, [`godot-cli`](../cli/docs/README.es.md), y el hermano para Godot de [Unity-MCP](https://github.com/IvanMurzak/Unity-MCP).

**Requisitos:** la **versión .NET** del editor de Godot 4.3+ y el **SDK de .NET 8**, que se instala una sola vez. Tus scripts pueden seguir en GDScript. La `godot-cli` opcional necesita Node.js 22.14+.

<a id="why"></a>
## Por qué Godot-MCP

✅ **De Godot 4.3 a 4.7** — cada release se prueba en CI con 4.3, 4.4, 4.5, 4.6 y 4.7\
✅ **Compilaciones C# reales** — `script-build` ejecuta `dotnet build` y devuelve los errores del compilador\
✅ **Comprobaciones de GDScript** — `script-validate` devuelve los diagnósticos de análisis y compilación de los archivos `.gd`\
✅ **Ediciones atómicas y reversibles** — los cambios de una consulta se aplican todos o ninguno, cada uno con un Ctrl+Z\
✅ **Capturas de pantalla** — el viewport, una cámara o un solo nodo renderizado aparte, devueltos al agente\
✅ **Herramientas de runtime** — logs, reflexión de C# y errores de runtime de un juego en marcha (opcional)\
✅ **Nube gratuita o totalmente local** — ai-game.dev por defecto (sin puertos abiertos), o un servidor local sin cuenta\
✅ **Sin telemetría** — ni el addon ni `godot-cli` envían datos de uso o de analítica

<a id="gdscript"></a>
## Funciona con proyectos GDScript

El código de tu juego sigue en GDScript. El addon en sí es C#, así que el proyecto necesita unos pocos pasos que se hacen una sola vez:

✅ **Editor .NET de Godot** — abre el proyecto con la versión .NET de Godot 4.3+ (el mismo proyecto, las mismas escenas)\
✅ **SDK de .NET 8** — se instala una vez por equipo\
✅ **Solución C#** — proyecto existente: **Project → Tools → C# → Create C# solution**; proyecto nuevo: `create-project --dotnet`\
✅ **El addon** — `godot-cli install-plugin` lo añade junto con sus referencias NuGet y lo activa

**¿Un proyecto solo con GDScript necesita una solución C#?** Sí, una vez: el addon es C# y se compila dentro del ensamblado C# del proyecto. **Create C# solution** añade un archivo `.csproj` y otro `.sln` en la raíz del proyecto. Tus scripts `.gd`, escenas y recursos no cambian y funcionan como antes; el agente también lee, escribe y valida GDScript. Después, las exportaciones usan las plantillas de exportación .NET de Godot e incluyen los pequeños ensamblados de runtime del addon, que permanecen inactivos salvo que tu código llame a `GodotMcpRuntime.Initialize()` ([cómo excluirlos](ARCHITECTURE.md#optional-strictly-zero-bytes-export-exclude-for-dev-only-users)).

<p align="center">
  <img src="img/terminal.svg" alt="Una sesión de terminal: godot-cli instala el addon, inicia sesión, configura Claude Code y abre el editor" width="100%"/>
</p>

<a id="how-it-works"></a>
## Cómo funciona

<p align="center">
  <img src="img/architecture.svg" alt="Un agente de IA habla MCP con un servidor (ai-game.dev o un gamedev-mcp-server local); el servidor llega al addon de editor godot_mcp por SignalR; el addon ejecuta las herramientas en el hilo principal de Godot" width="100%"/>
</p>

El addon se conecta a un servidor MCP, y tu agente se conecta al mismo servidor y llama a las herramientas del addon. El servidor es un [GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server) local que el addon descarga y ejecuta por ti, o el relay alojado de [ai-game.dev](https://ai-game.dev). El addon no abre ningún puerto propio.

Cloud es el modo predeterminado porque no necesita puertos abiertos y evita conflictos de puertos con otros programas; es gratuito. El modo local es una alternativa completa para trabajar sin conexión o en entornos aislados (air-gapped).

<a id="stability"></a>
### Estabilidad

- **Versiones:** el addon y `godot-cli` siempre se publican con el mismo número de versión; lee las [notas de la release](https://github.com/IvanMurzak/Godot-MCP/releases/latest) antes de actualizar.
- **Qué cambia entre versiones:** una release puede cambiar las versiones NuGet que necesita el addon y la versión de GameDev-MCP-Server que descarga. `godot-cli update` (o `install-plugin`) escribe las versiones NuGet que corresponden al addon que instalas, y el addon obtiene por sí mismo el servidor correspondiente.
- **Condición de publicación:** una release solo se publica después de que pasen las pruebas de .NET, las de la CLI y las del editor en Godot 4.3–4.7.
- **Historial de cambios:** cada [release de GitHub](https://github.com/IvanMurzak/Godot-MCP/releases) lista los cambios fusionados; `godot-cli` también mantiene un [changelog](../cli/CHANGELOG.md).

<a id="quick-start"></a>
## Inicio rápido

Instala `godot-cli` (Node.js 22.14+) y añade el addon a tu proyecto:

```bash
npm install -g godot-cli
godot-cli install-plugin ./MyGodotProject             # addon + referencias NuGet + activar el plugin
```

Después elige cómo llega el agente al editor. Las dos opciones son configuraciones completas. Cloud es el modo predeterminado del addon: sin puertos abiertos, sin conflictos de puertos con otros programas y gratuito. Local lo mantiene todo en tu equipo, para trabajar sin conexión o en entornos aislados.

**Opción A: local, sin cuenta.** Las llamadas a herramientas no salen de tu equipo.

1. **Define un token.** Abre el proyecto en el editor .NET de Godot. En el dock **AI Game Developer** elige **Custom**, configura **Authorization → token**, pulsa **Generate** y luego **Start Server**. Anota la **Server URL** (`http://localhost:<port>`, un puerto derivado de la ruta del proyecto).
   O elige tú mismo un token, abre el editor con él desde una terminal y luego pulsa **Start Server**:
   `godot-cli open ./MyGodotProject --mode Custom --auth Required --token <token>`
2. **Conecta tu agente** con el mismo token:

```bash
godot-cli setup-mcp claude-code ./MyGodotProject --url http://localhost:<port> --token <token>
```

> **Sin token**, el servidor local usa la autorización `none`. Solo escucha en loopback, así que solo los programas de tu propio equipo pueden llegar a él.
> En ese caso, `godot-cli open ./MyGodotProject` inicia el editor y su servidor local en un solo paso y muestra la URL para `setup-mcp --url`.

**Opción B: la nube de ai-game.dev.** Inicia sesión una vez por equipo; las configuraciones de agente llevan entonces una clave vinculada a este proyecto.

```bash
godot-cli login                                       # inicio de sesión OAuth por dispositivo en el navegador
godot-cli setup-mcp claude-code ./MyGodotProject      # escribir la configuración MCP del agente
godot-cli open ./MyGodotProject                       # compilar C#, iniciar el editor, conectar
```

Después pídele a tu agente: *"Crea 3 cubos en un círculo de radio 2."*

Para instalar desde la pestaña **AssetLib** del editor, desde un [zip de release](https://github.com/IvanMurzak/Godot-MCP/releases/latest) o a mano, sigue [install.es.md](install.es.md): el `.csproj` del proyecto necesita entonces las referencias NuGet del addon (`com.IvanMurzak.ReflectorNet`, `com.IvanMurzak.McpPlugin` y, solo para el editor, `com.IvanMurzak.Rql`, `com.IvanMurzak.CodeExec`, `com.IvanMurzak.CodeExec.Roslyn` y `Microsoft.CodeAnalysis.CSharp`) en las versiones de la release que instalas.

<a id="relay"></a>
### Qué pasa por ai-game.dev

Solo en modo Cloud. El relay enruta las llamadas a herramientas entre tu agente y el editor. No almacena lo que envías: ni contenido de peticiones o respuestas, ni conversaciones, código fuente, archivos del proyecto o assets. Solo conserva metadatos, durante 90 días: dirección IP, hora y duración, nombre de la herramienta y número de llamadas, tamaño de la petición y de la respuesta, y eventos MCP (tipo, estado, nombre de la herramienta, errores). En modo local, el editor y el servidor solo se comunican a través de localhost.

<a id="clients"></a>
## Conecta tu cliente de IA

`godot-cli setup-mcp <agent> ./MyGodotProject` escribe la configuración por ti (`godot-cli setup-mcp --list` muestra todos los ids de agente). La entrada del servidor se llama `ai-game-developer`; `<url>` es la URL que muestra `setup-mcp`. En modo Cloud la entrada también lleva `Authorization: Bearer agd_pk_…`, una clave que solo da acceso a este proyecto.

<details>
<summary><b>Claude Code</b> — <code>.mcp.json</code> en el proyecto</summary>

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

En modo Cloud la clave va en `http_headers`.
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

También compatibles: Claude Desktop, Visual Studio (Copilot), GitHub Copilot CLI, Rider (Junie), Antigravity, Cline, Open Code, Kilo Code y un cliente genérico `custom` ([referencia de la CLI](../cli/docs/README.es.md#supported-ai-agents)).

<a id="what-the-agent-can-do"></a>
## Qué puede hacer el agente

Godot-MCP incluye **24 herramientas integradas** en **9 familias**. No escribes consultas: el agente lee la guía de RQL que acompaña a las herramientas (`rql-schema`, el skill `godot-rql`) y las escribe él mismo.

| Capacidad | Herramientas | Pídele a tu agente |
| --- | --- | --- |
| Escenas y nodos | `rql-query`, `rql-mutate` | *"Añade un DirectionalLight3D a la escena principal y guárdala."* |
| Ediciones masivas | `rql-mutate` | *"Oculta todos los nodos cuyo nombre empiece por Enemy."* |
| Recursos y ajustes del proyecto | `rql-query`, `rql-mutate` | *"Añade una acción de entrada jump asignada a Espacio."* |
| Consultar la API | `rql-schema` | *"¿Qué propiedades de Godot tiene un CharacterBody3D?"* |
| Scripts (GDScript y C#) | `script-create`, `script-update`, `script-attach-to-node`, `script-validate`, `script-read`, `script-delete` | *"Escribe un GDScript que rote este nodo y adjúntaselo."* |
| Compilar C# | `script-build` | *"Compila el proyecto C# y corrige los errores."* |
| Ejecutar C# en el editor | `script-execute` | *"Cuenta los nodos MeshInstance3D de la escena abierta."* |
| Ver el resultado | `screenshot-viewport`, `screenshot-camera`, `screenshot-isolated` | *"Haz una captura del viewport y revisa la iluminación."* |
| Juego en marcha (opcional) | `console-*`, `reflection-*`, `runtime-errors-*` | *"Comprueba si el juego en marcha tiene errores de runtime nuevos."* |

`ping`, `session-close`, `godot-skill-create` y `godot-skill-generate` son herramientas de sistema para `godot-cli` y la configuración; los agentes no las ven. Todas las herramientas y argumentos: [tools.es.md](tools.es.md).

Las mismas herramientas funcionan desde una terminal, en la carpeta del proyecto (se inicia un editor headless si no hay ninguno en marcha):

```bash
godot-cli rql 'nodes | where name starts-with "Enemy" | set Visible = false'
godot-cli exec '1 + 2'        # → System.Int32: 3
godot-cli tools               # listar las herramientas del editor
```

<a id="security"></a>
## Seguridad

| Área | Qué se aplica |
| --- | --- |
| **Servidor local** | Solo escucha en loopback. Su autorización predeterminada es `none`, así que cualquier proceso de tu equipo puede llamarlo. Elige `token` en el dock, define `GODOT_MCP_TOKEN` o abre el editor con `godot-cli open <path> --mode Custom --auth Required --token <token>` para exigir un token bearer. |
| **Comprobación de Origin** | El servidor responde `403` a una petición con una cabecera `Origin` no permitida, en cualquier modo de autorización (defensa contra DNS rebinding). |
| **Addon del editor** | No abre ningún puerto; se conecta hacia fuera al servidor. Un puente de control solo para desarrollo en `127.0.0.1` solo se inicia con `GODOT_MCP_DEV_CONTROL=1`. |
| **Nube** | Las configuraciones de agente llevan una clave vinculada a un único proyecto; el editor usa el inicio de sesión de tu equipo. El token de tu cuenta solo va a ai-game.dev o a un servidor localhost. El relay no almacena contenido de peticiones ni de respuestas y solo conserva metadatos, durante 90 días. |
| **Tokens guardados** | El editor guarda su token como JSON plano en `user://godot-mcp-config.json`; trata ese archivo como un secreto. |
| **Juegos exportados** | No se ejecuta nada en un juego salvo que tu código llame a `GodotMcpRuntime.Initialize(...)` y a `Connect()`, y solo con las herramientas que registres. La captura de errores y el C# dentro del juego se activan por separado ([seguridad en runtime](runtime-security.es.md)). |
| **Sin protección** | Las llamadas a herramientas modifican tu proyecto y ejecutan C# en el editor: usa control de versiones y nunca expongas a una red un servidor con autorización `none`. |

Detalles: [Conexión y servidor](connection.es.md) y el [README de GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server#readme).

<a id="extensions"></a>
## Extensiones

Las extensiones son paquetes NuGet opcionales que amplían RQL; no añaden herramientas propias.

```bash
godot-cli install-extension com.IvanMurzak.Godot.MCP.CSG ./MyGodotProject
```

| Extensión | Añade | Requiere |
| --- | --- | --- |
| [Animation](../extensions/Godot-AI-Animation/README.md) | Bibliotecas, pistas y claves de AnimationPlayer | |
| [Beehave](../extensions/Godot-AI-Beehave/README.md) | Árboles de comportamiento y sus nodos | Addon [Beehave](https://github.com/bitbrain/beehave) |
| [CSG](../extensions/Godot-AI-CSG/README.md) | Formas CSG con dimensiones validadas | |
| [Dialogic](../extensions/Godot-AI-Dialogic/README.md) | Líneas de tiempo y personajes | Addon [Dialogic](https://github.com/dialogic-godot/dialogic), Godot 4.4+ |
| [GridMap](../extensions/Godot-AI-GridMap/README.md) | Ajustes de GridMap, filas de celdas y edición de celdas | |
| [Navigation](../extensions/Godot-AI-Navigation/README.md) | Agentes, regiones y enlaces de navegación, horneado | |
| [Particles](../extensions/Godot-AI-Particles/README.md) | Partículas GPU en 2D y 3D | |
| [PhantomCamera](../extensions/Godot-AI-PhantomCamera/README.md) | Cámaras virtuales, seguimiento y look-at | Addon [Phantom Camera](https://github.com/ramokz/phantom-camera), Godot 4.4+ |
| [Terrain3D](../extensions/Godot-AI-Terrain3D/README.md) | Origen del terreno, tamaño de región y materiales | Addon [Terrain3D](https://github.com/TokisanGames/Terrain3D), Godot 4.4+ |
| [Tilemap](../extensions/Godot-AI-Tilemap/README.md) | Filas de celdas y edición de celdas de TileMapLayer | |

Instala tú mismo en el proyecto el addon de terceros que se requiera; la sección **Extensions** del dock puede instalarlos todos.

<a id="documentation"></a>
## Documentación

| Página | Qué contiene |
| --- | --- |
| [Instalación manual](install.es.md) | AssetLib, zip de release, referencias NuGet, configuración del agente |
| [Conexión y servidor](connection.es.md) | Modos Cloud y Custom, variables, el servidor |
| [Referencia de herramientas](tools.es.md) | Cada herramienta y sus argumentos, capturas, extensiones |
| [RQL en Godot-MCP](rql.md) | Orígenes, funciones, escrituras, deshacer y guardar |
| [Herramientas propias](custom-tools.es.md) | Escribir herramientas, recargas del ensamblado C#, el problema conocido de .NET 10 |
| [Uso en runtime](runtime.es.md) | MCP dentro de un juego en marcha: configuración, captura de errores, C# en el juego |
| [Seguridad en runtime](runtime-security.es.md) | Qué garantiza el runtime en el juego y qué debes hacer tú |
| [Referencia de la CLI](../cli/docs/README.es.md) | Cada comando de `godot-cli`, códigos de salida y formatos de salida |
| [Arquitectura](ARCHITECTURE.md) · [Publicación](RELEASING.md) · [Extensiones](EXTENSION-MONOREPO.md) | Para colaboradores |

<a id="contributing"></a>
## Contribuir

No hace falta ningún binario de Godot para compilar y ejecutar las pruebas unitarias:

```bash
dotnet restore Godot-MCP.sln
dotnet build  Godot-MCP.sln --configuration Debug --no-restore
dotnet test   Godot-MCP.Tests/Godot-MCP.Tests.csproj --configuration Debug --no-build
```

Las convenciones, la prueba de humo del editor headless y los principales escollos están en [AGENTS.md](../AGENTS.md). Haz un fork del repositorio y abre un pull request contra `main`. Preguntas y proyectos para mostrar: [Discord](https://discord.gg/cfbdMZX99G).

Relacionados: [GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server) · [Unity-MCP](https://github.com/IvanMurzak/Unity-MCP) · [RQL](https://github.com/IvanMurzak/RQL) · [Code-Exec](https://github.com/IvanMurzak/Code-Exec) · [ai-game.dev](https://ai-game.dev)

<a id="license"></a>
## Licencia

**Los desarrolladores de juegos pueden usarlo todo libremente, también en juegos comerciales.** Godot-MCP usa la licencia [Apache-2.0](../LICENSE). Sus dependencias del lado del editor, RQL y Code-Exec, usan la [PolyForm Shield License 1.0.0](https://polyformproject.org/licenses/shield/1.0.0/), que solo restringe a las empresas que crean herramientas de IA competidoras para el desarrollo de juegos.
