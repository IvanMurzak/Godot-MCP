[English](custom-tools.md) · [中文](custom-tools.zh-CN.md) · [日本語](custom-tools.ja.md) · **Español** · [فارسی](custom-tools.fa.md) · [हिन्दी](custom-tools.hi.md) · [العربية](custom-tools.ar.md)

# Herramientas propias

Añade tus propias herramientas MCP en el código C# del proyecto. Una familia de herramientas es una `partial class` marcada con `[AiToolType]`; cada herramienta es un método marcado con `[AiTool("tool-name", …)]`, con un `[Description]` en el método y en cada parámetro para que el LLM sepa cuándo y cómo llamarla.

Cualquier llamada a la API de Godot (`Node`, `Resource`, `EditorInterface`, …) **debe** ejecutarse en el hilo principal. Envuélvela en `MainThread.Instance.Run(...)`; el plugin respalda el `MainThread` de ReflectorNet con el despachador del hilo principal de Godot.

```csharp
[AiToolType]
public partial class Tool_MyFeature
{
    [AiTool("my-custom-task", Title = "Do a custom task")]
    [Description("Explain to the LLM what this does and when to call it.")]
    public string CustomTask
    (
        [Description("Explain to the LLM what this parameter is.")]
        string inputData
    )
    {
        // El trabajo que no toca la API de Godot puede ejecutarse aquí, en un hilo en segundo plano.

        return MainThread.Instance.Run(() =>
        {
            // Usa EditorInterface / Node / Resource aquí, en el hilo principal.
            return "[Success] Operation completed.";
        });
    }
}
```

- Devuelve un modelo estructurado (ReflectorNet lo serializa) o `void`, no cadenas formateadas a mano.
- Haz opcional un parámetro con un tipo anulable y un valor por defecto: `string? optional = null`.
- `[AiToolType(Target = ToolTarget.Runtime)]` hace que una familia sea solo del juego, `ToolTarget.Editor` solo del editor; el valor predeterminado, `Both`, la registra en todas partes. Herramientas del juego: [runtime.md](runtime.es.md).
- La herramienta de sistema `godot-skill-create` escribe ese archivo por ti; se puede llamar tras la siguiente compilación.

<a id="c-assembly-reloads"></a>
## Recargas del ensamblado C#

Godot recarga el ensamblado C# del proyecto alrededor de medio segundo después de que cambie la DLL compilada (`script-build`, el botón Build del editor o `dotnet build` en una terminal). El plugin forma parte de ese ensamblado, así que se descarga, se recompila y se reconecta en pocos segundos. Una llamada a herramienta que siga en curso en ese momento bloquearía la descarga, por lo que el plugin controla las llamadas en torno a una recarga:

- en cuanto la nueva DLL está en disco, las llamadas nuevas se rechazan con un error que admite reintento ("Godot is about to reload the project's C# assembly … Retry in a few seconds"); las que ya están en curso terminan primero (Godot espera hasta 10 s);
- cuando empieza la descarga, el plugin deja de aceptar llamadas y descarta el trabajo del hilo principal que siga en cola para una llamada; el servidor informa de esa llamada como perdida junto con la conexión, así que reinténtala también;
- tras la recarga el plugin se reconecta por sí mismo y `script-build` entrega su resultado.

<a id="known-issue-crash-or-hang-after-a-reload-on-net-10"></a>
## Problema conocido: cierre inesperado o bloqueo tras una recarga en .NET 10

En .NET 10 el editor puede cerrarse inesperadamente o bloquearse poco después de una recarga del ensamblado C#. Es un error del runtime de .NET, [dotnet/runtime#135441](https://github.com/dotnet/runtime/issues/135441), corregido en la rama principal del runtime ([#134983](https://github.com/dotnet/runtime/pull/134983)); la corrección de mantenimiento de .NET 10 es [#136481](https://github.com/dotnet/runtime/pull/136481).

Hasta que tu runtime incluya la corrección, define esta variable para el proceso del editor:

```
DOTNET_ThreadPool_ThreadsToKeepAlive=-1
```

Los editores iniciados por `godot-cli` (`open`, `open --headless`, `exec`) la reciben automáticamente. Si inicias Godot tú mismo, defínela para todo el sistema (Windows: `setx DOTNET_ThreadPool_ThreadsToKeepAlive -1`; macOS / Linux: expórtala en el shell o lanzador que inicia Godot) y reinicia el editor. Elimínala cuando tu runtime de .NET 10 incluya la corrección.
