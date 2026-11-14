[English](runtime.md) · [中文](runtime.zh-CN.md) · [日本語](runtime.ja.md) · **Español** · [فارسی](runtime.fa.md) · [हिन्दी](runtime.hi.md) · [العربية](runtime.ar.md)

# Uso en runtime (dentro del juego)

El plugin del editor conecta el **editor de Godot**. Godot-MCP también puede ejecutarse dentro de un **juego en marcha o exportado** (debug o release), de modo que un LLM puede leer y controlar el estado del juego en vivo: por ejemplo, un juego de ajedrez cuyos movimientos del bot vienen de un LLM a través de dos herramientas que escribes tú.

El modo runtime se diferencia del editor a propósito:

- **Nunca se conecta por sí solo.** Tu código decide si se llama a `Connect()` y cuándo.
- **No expone nada por defecto.** Registras tú mismo cada herramienta (`[AiToolType]`), prompt (`[AiPromptType]`) y recurso (`[AiResourceType]`); cada tipo es opcional. Las herramientas de editor del addon están detrás de `#if TOOLS` y no se compilan en un juego exportado.

Lee [runtime-security.md](runtime-security.es.md) antes de publicar una compilación que llame a `GodotMcpRuntime.Initialize`.

<a id="set-it-up"></a>
## Configuración

Llama a `GodotMcpRuntime.Initialize(...)` una vez que exista un `SceneTree`, por ejemplo desde el `_Ready()` de un autoload:

```csharp
using System.Reflection;
using com.IvanMurzak.Godot.MCP.Connection;   // GodotMcpConnectionMode
using com.IvanMurzak.Godot.MCP.Runtime;      // GodotMcpRuntime
using McpServerConsts = com.IvanMurzak.McpPlugin.Common.Consts.MCP.Server;   // AuthOption
using Godot;

public partial class GameMcp : Node
{
    private GodotMcpRuntimeHandle? _mcp;

    public override async void _Ready()
    {
        _mcp = GodotMcpRuntime.Initialize(builder =>
        {
            builder.WithConfig(config =>
            {
                config.ConnectionMode = GodotMcpConnectionMode.Custom;     // tu propio servidor
                config.Host           = "http://localhost:8080";           // mejor loopback
                config.AuthOption     = McpServerConsts.AuthOption.token;  // exigir un token
                // Token: mejor GODOT_MCP_TOKEN del entorno que un valor en el código.
            });

            builder.WithToolsFromAssembly(Assembly.GetExecutingAssembly());     // clases [AiToolType]
            builder.WithPromptsFromAssembly(Assembly.GetExecutingAssembly());   // clases [AiPromptType]
            builder.WithResourcesFromAssembly(Assembly.GetExecutingAssembly()); // clases [AiResourceType]
        }).Build();

        await _mcp.Connect();   // activación explícita; reintenta en segundo plano mientras KeepConnected sea true
    }

    public override async void _ExitTree()
    {
        if (_mcp is not null)
            await _mcp.Disconnect();
    }
}
```

| Llamada | Qué hace |
| --- | --- |
| `GodotMcpRuntime.Initialize(configure)` | Empieza la configuración; `configure` puede ser `null` para el valor predeterminado sin herramientas, configurado por el entorno. |
| `WithConfig(Action<GodotMcpConfig>)` | Define `Host`, `Token`, `ConnectionMode` y `AuthOption` en el código. Las llamadas se componen en orden. |
| `WithToolsFromAssembly(Assembly)` / `WithTools(params Type[])` | Registra clases `[AiToolType]`: un ensamblado entero o tipos concretos. |
| `WithPromptsFromAssembly` / `WithPrompts` | Lo mismo para clases `[AiPromptType]`. |
| `WithResourcesFromAssembly` / `WithResources` | Lo mismo para clases `[AiResourceType]`. |
| `WithoutMainThreadDispatcher()` | Omite el despachador automático del hilo principal (solo si instalas el tuyo). |
| `ConnectWhenRunFromEditor()` | Conecta también en un juego iniciado desde el editor (desactivado por defecto, ver abajo). |
| `WithRuntimeErrorCapture()` | Captura errores en el juego y registra `runtime-errors-*`. Desactivado por defecto. |
| `WithScriptExecute()` | Registra `script-execute` en el juego. Desactivado por defecto; requiere activarlo en el csproj. |
| `Build()` | Devuelve un `GodotMcpRuntimeHandle` sin conectar. |
| `handle.Connect()` / `Disconnect()` / `Dispose()` | Abre, cierra o destruye la conexión. |

`Build()` también añade un nodo despachador del hilo principal al `SceneTree` en ejecución (salvo que lo desactives), para que los manejadores de herramientas puedan usar `MainThread.Instance.Run(...)`.

<a id="server-url-and-token"></a>
## URL del servidor y token

Un juego nunca carga la configuración guardada del editor. Toma los ajustes de tu código (`WithConfig`) y de las variables de entorno del proceso `GODOT_MCP_*` o de un archivo `.env` del proyecto, que se leen en vivo y prevalecen sobre el código. Las variables se enumeran en [runtime-security.md](runtime-security.es.md#environment-variables). El modo Custom en un host loopback con token es la configuración recomendada para un juego.

<a id="games-run-from-the-editor"></a>
## Juegos ejecutados desde el editor

Un juego iniciado con Play (F5) hereda el entorno del editor, incluidos `GODOT_MCP_HOST` y `GODOT_MCP_TOKEN`. Por eso, por defecto, `handle.Connect()` no hace nada en ese juego y devuelve `false` (registra el motivo): el editor sigue siendo el único plugin con el que habla el agente. Los juegos exportados y los iniciados fuera del editor se conectan con normalidad. Para conectar también un juego ejecutado desde el editor, usa `builder.ConnectWhenRunFromEditor()` o `GODOT_MCP_RUNTIME_FROM_EDITOR=1`. Un GameDev-MCP-Server con roles de plugin mantiene entonces ambas conexiones y enruta las llamadas a herramientas al editor mientras esté conectado.

<a id="a-game-tool"></a>
## Una herramienta del juego

Una herramienta de runtime se escribe igual que una del editor ([custom-tools.md](custom-tools.es.md)). Marca las familias solo de juego con `Target = ToolTarget.Runtime` para que el editor no las registre y el agente llegue al juego incluso con el editor conectado.

```csharp
using System.ComponentModel;
using com.IvanMurzak.McpPlugin;              // [AiToolType], [AiTool]
using com.IvanMurzak.McpPlugin.Common;       // ToolTarget
using com.IvanMurzak.ReflectorNet.Utils;     // MainThread
using Godot;

[AiToolType(Target = ToolTarget.Runtime)]
public partial class GameMcpTools
{
    [AiTool("game-scene-tree-summary", Title = "Game Scene-Tree Summary", ReadOnlyHint = true)]
    [Description("Summary of the running game's SceneTree: current scene and root child names.")]
    public SceneTreeSummary GameSceneTreeSummary()
    {
        return MainThread.Instance.Run(() =>
        {
            var summary = new SceneTreeSummary();
            if (Engine.GetMainLoop() is not SceneTree tree || tree.Root == null)
                return summary;

            summary.CurrentSceneName = tree.CurrentScene?.Name ?? "<none>";
            foreach (var child in tree.Root.GetChildren())
                summary.RootChildNames.Add(child.Name);
            return summary;
        });
    }
}

public sealed class SceneTreeSummary
{
    public string CurrentSceneName { get; set; } = string.Empty;
    public System.Collections.Generic.List<string> RootChildNames { get; set; } = new();
}
```

Los prompts y recursos se registran igual: una `partial class` marcada con `[AiPromptType]` / `[AiResourceType]` con miembros marcados con `[AiPrompt(Name = …, Role = …)]` / `[AiResource(Name = …, Route = "game://…", MimeType = …)]`. Un método de recurso devuelve `ResponseResourceContent[]` (por ejemplo `ResponseResourceContent.CreateText(uri, json, "application/json")`). Todos estos tipos vienen del paquete `com.IvanMurzak.McpPlugin`.

<a id="capturing-in-game-runtime-errors"></a>
## Captura de errores de runtime en el juego

Las herramientas del editor no ven los errores que se producen dentro de un juego en marcha, así que un agente podría lanzar el juego, leer logs sin errores y concluir por error que funciona bien. `builder.WithRuntimeErrorCapture()` cierra ese hueco. Instala tres canales de mejor esfuerzo y registra `runtime-errors-get` / `runtime-errors-clear`:

1. **Errores del motor (Godot 4.5+)** mediante `OS.AddLogger`: errores de runtime de GDScript, `push_error` / `push_warning` y errores de shader, con `file`, `line` y `function`. En Godot 4.5+ un error de GDScript también incluye la pila de llamadas completa en `frames` (la más interna primero) y `stackTrace`.
2. **Excepciones no controladas de C#** (`AppDomain.CurrentDomain.UnhandledException`) con la traza de pila administrada completa.
3. **Excepciones de `Task` no observadas de C#** (`TaskScheduler.UnobservedTaskException`), marcadas como observadas solo para registrarlas.

En Godot anterior a 4.5 el canal del motor no está disponible; los canales de C# siguen funcionando. `handle.Dispose()` elimina los hooks. Los mensajes y trazas de pila capturados llegan al agente tal cual y pueden contener rutas, nombres de usuario o secretos, así que activa la captura solo en una conexión de confianza loopback + token.

<a id="running-c-in-the-game"></a>
## Ejecutar C# en el juego

`builder.WithScriptExecute()` permite al agente enviar C# que el juego en marcha compila con Roslyn y ejecuta en su hilo principal, con los tipos, autoloads y árbol de escena del propio juego. Las compilaciones de exportación excluyen Roslyn, así que el `.csproj` del juego debe activarlo:

```xml
<PropertyGroup>
  <GodotMcpCodeExecInGame>true</GodotMcpCodeExecInGame>
  <DefineConstants>$(DefineConstants);GODOT_MCP_CODE_EXEC_IN_GAME</DefineConstants>
</PropertyGroup>
```

Un `godot-cli install-plugin` actual escribe las referencias a los paquetes Code-Exec y Roslyn con una `Condition` que respeta esta propiedad; RQL nunca se incluye. Sin esta activación, `WithScriptExecute()` registra una advertencia y no registra nada.

> **Esto es ejecución de código arbitrario.** Cualquiera que pueda llamar a herramientas en la conexión puede ejecutar cualquier código en el proceso del juego. Úsalo solo en compilaciones de desarrollo, en un host loopback con token. Solo CoreCLR de escritorio: NativeAOT e iOS no pueden ejecutar código generado en tiempo de ejecución.
