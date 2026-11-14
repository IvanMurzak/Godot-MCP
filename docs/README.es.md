<div align="center">

<p><img src="img/hero.svg" alt="Godot MCP: los agentes de IA trabajan en el editor de Godot a través del addon godot_mcp" width="100%"/></p>

[![CI](https://img.shields.io/github/actions/workflow/status/IvanMurzak/Godot-MCP/ci.yml?branch=main&label=CI&color=15803D&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=github)](https://github.com/IvanMurzak/Godot-MCP/actions/workflows/ci.yml) [![Release](https://img.shields.io/github/v/release/IvanMurzak/Godot-MCP?label=release&color=2F6F9F&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=github)](https://github.com/IvanMurzak/Godot-MCP/releases/latest) [![Asset Library](https://img.shields.io/badge/AssetLib-Godot--MCP-2F6F9F?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=godotengine)](https://godotengine.org/asset-library/asset/5245) [![CLI](https://img.shields.io/npm/v/godot-cli?label=CLI&color=BE185D&style=for-the-badge&labelColor=0F172A&logoColor=white&logo=npm)](https://www.npmjs.com/package/godot-cli) \
[![Godot](https://img.shields.io/badge/Godot-4.3%2B%20.NET-0E7490?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=godotengine)](https://godotengine.org/) [![.NET 8](https://img.shields.io/badge/.NET-8%20SDK-0E7490?style=for-the-badge&labelColor=0F172A&logoColor=white)](https://dotnet.microsoft.com/) [![License](https://img.shields.io/badge/license-Apache--2.0-047857?style=for-the-badge&labelColor=0F172A&logoColor=white)](../LICENSE) [![Discord](https://img.shields.io/badge/Discord-join-5865F2?style=for-the-badge&labelColor=0F172A&logoColor=white&logo=discord)](https://discord.gg/cfbdMZX99G) [![Stand with Ukraine](https://raw.githubusercontent.com/IvanMurzak/IvanMurzak/main/images/ukraine/flag-badge.svg)](https://stand-with-ukraine.pp.ua)

[English](../README.md) · [中文](README.zh-CN.md) · [日本語](README.ja.md) · **Español** · [فارسی](README.fa.md) · [हिन्दी](README.hi.md) · [العربية](README.ar.md)

</div>

**Godot-MCP** conecta agentes de IA (Claude Code, Cursor, Codex, GitHub Copilot, Gemini o cualquier otro cliente [MCP](https://modelcontextprotocol.io/)) con el **editor de Godot**. El agente puede leer y modificar tus escenas, nodos, recursos y scripts, ejecutar C# en el editor y ver capturas de pantalla. Es un addon de editor en C# para Godot 4.3+ (.NET) con una CLI complementaria, [`godot-cli`](../cli/docs/README.es.md), y el hermano para Godot de [Unity-MCP](https://github.com/IvanMurzak/Unity-MCP).

<p align="center">
  <img src="img/terminal.svg" alt="Una sesión de terminal: godot-cli instala el addon, inicia sesión, configura Claude Code y abre el editor" width="100%"/>
</p>

<a id="how-it-works"></a>
## Cómo funciona

<p align="center">
  <img src="img/architecture.svg" alt="Un agente de IA habla MCP con un servidor (ai-game.dev o un gamedev-mcp-server local); el servidor llega al addon de editor godot_mcp por SignalR; el addon ejecuta las herramientas en el hilo principal de Godot" width="100%"/>
</p>

El addon se conecta a un servidor MCP: por defecto el alojado en [ai-game.dev](https://ai-game.dev), o tu propio [GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server), que el addon puede descargar y ejecutar por ti. Tu agente se conecta al mismo servidor y llama a las herramientas del addon.

<a id="features"></a>
## Funciones

✅ **Un solo lenguaje de consulta** — [RQL](rql.md) lee y edita nodos, escenas, recursos y ajustes\
✅ **Ediciones reversibles** — los cambios de RQL son todo o nada y se deshacen con Ctrl+Z\
✅ **C# y GDScript** — crea, actualiza, valida y adjunta scripts `.cs` / `.gd`\
✅ **C# en el editor** — `script-execute` ejecuta un fragmento con los tipos de tu juego\
✅ **Respuesta visual** — capturas del viewport, de una cámara o de un solo nodo\
✅ **Nube o autoalojado** — inicio de sesión en ai-game.dev, o un servidor local que ejecuta el addon\
✅ **Extensiones** — módulos RQL opcionales para animación, navegación, tilemaps y addons\
✅ **Runtime en el juego** — expón tus propias herramientas desde un juego en marcha (desactivado)\
✅ **Tus propias herramientas** — un método C# con atributo por herramienta ([guía](custom-tools.es.md))

<a id="quick-start"></a>
## Inicio rápido

Requisitos: Godot 4.3 o posterior, **edición .NET** (la versión solo GDScript no puede compilar el addon), el SDK de .NET 8 y Node.js 22.14+ para `godot-cli`.

```bash
npm install -g godot-cli

godot-cli create-project --dotnet ./MyGodotProject    # opcional: un nuevo proyecto Godot en C#
godot-cli install-plugin ./MyGodotProject             # addon + referencias NuGet + activar el plugin
godot-cli login                                       # iniciar sesión en ai-game.dev (una vez por equipo)
godot-cli setup-mcp claude-code ./MyGodotProject      # escribir la configuración MCP del agente
godot-cli open ./MyGodotProject                       # compilar C#, iniciar el editor, conectar
```

Después pídele a tu agente: *"Crea 3 cubos en un círculo de radio 2."*

`godot-cli setup-mcp --list` muestra todos los agentes compatibles. Para instalar desde la pestaña **AssetLib** del editor, desde un [zip de release](https://github.com/IvanMurzak/Godot-MCP/releases/latest) o a mano, sigue [install.es.md](install.es.md): el `.csproj` del proyecto necesita entonces estas referencias NuGet.

| Paquete | Versión |
| --- | --- |
| `com.IvanMurzak.ReflectorNet` | `5.5.2` |
| `com.IvanMurzak.McpPlugin` | `8.22.2` |
| `com.IvanMurzak.Rql` | `0.3.0` (solo editor) |
| `com.IvanMurzak.CodeExec`, `com.IvanMurzak.CodeExec.Roslyn` | `0.4.1` (solo editor) |
| `Microsoft.CodeAnalysis.CSharp` | `4.12.0` (solo editor) |

<a id="usage"></a>
## Uso

Habla con tu agente en lenguaje natural; él elige las herramientas. Desde la carpeta del proyecto puedes llamar a las mismas herramientas en una terminal:

```bash
godot-cli rql 'nodes | where class == "OmniLight3D" | select name props["light_energy"]'
godot-cli rql 'nodes | where name starts-with "Enemy" | set Visible = false'
godot-cli exec '1 + 2'        # → System.Int32: 3
godot-cli tools               # listar las herramientas del editor
```

Si no hay ningún editor en marcha, estos comandos inician uno headless para el proyecto. Todos los comandos están en la [referencia de la CLI](../cli/docs/README.es.md).

<a id="tools"></a>
### Herramientas

Godot-MCP incluye **24 herramientas integradas** en **9 familias**:

| Familia | Herramientas |
| --- | --- |
| **rql** | `rql-query`, `rql-mutate`, `rql-schema` |
| **script** | `script-read`, `script-create`, `script-update`, `script-delete`, `script-attach-to-node`, `script-validate`, `script-build`, `script-execute` |
| **screenshot** | `screenshot-viewport`, `screenshot-camera`, `screenshot-isolated` |
| **console**, **reflection**, **runtime-errors** | logs, reflexión de C# y captura de errores dentro de un juego en marcha |
| **ping**, **session**, **skills** | herramientas de sistema para `godot-cli` y la configuración, no visibles para los agentes |

Todas las herramientas y argumentos: [tools.es.md](tools.es.md).

<a id="extensions"></a>
### Extensiones

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

[Apache-2.0](../LICENSE) © Ivan Murzak. El addon también usa [`com.IvanMurzak.Rql`](https://www.nuget.org/packages/com.IvanMurzak.Rql) y Code-Exec, que tienen su propia licencia.

Esa licencia es la [PolyForm Shield License 1.0.0](https://polyformproject.org/licenses/shield/1.0.0/): de uso gratuito, también en productos comerciales, salvo para crear un producto que compita con los del licenciante.
