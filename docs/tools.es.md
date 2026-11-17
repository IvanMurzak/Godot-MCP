[English](tools.md) · [中文](tools.zh-CN.md) · [日本語](tools.ja.md) · **Español** · [فارسی](tools.fa.md) · [हिन्दी](tools.hi.md) · [العربية](tools.ar.md)

# Referencia de herramientas

Las herramientas integradas de Godot-MCP cubren prácticamente todo lo que puedes hacer en el editor de Godot, agrupadas en las familias que aparecen a continuación. Son compactas, así que un agente necesita menos llamadas y menos tokens ([medido en la evaluación de agentes de RQL](https://github.com/IvanMurzak/RQL/blob/main/RESULTS.md)). La capa de datos del editor es [RQL](rql.md) (Reflection Query Language): `rql-query`, `rql-mutate` y `rql-schema` leen y modifican nodos de escena, propiedades de Godot, recursos, archivos, escenas y el estado del editor con una sola consulta, en lugar de una herramienta por operación.

Con el `toolFormat` predeterminado (`Rql`), las demás herramientas reciben valores RQL como argumentos y devuelven texto compacto; `Json` conserva los esquemas y resultados JSON de ReflectorNet. Las capturas devuelven una imagen PNG. Todas las herramientas del editor están disponibles en cuanto se activa el addon.

Las familias **console**, **reflection** y **runtime-errors** sirven a un *juego en marcha* (consulta [Uso en runtime](runtime.es.md)). En el editor, usa en su lugar el origen RQL `console` y `call` / `CallMethod` de RQL.

| Familia | Herramientas | Qué hace |
| --- | --- | --- |
| **rql** | `rql-query`, `rql-mutate`, `rql-schema` | Consultar y modificar el editor en vivo. Todo o nada, y reversible; varias sentencias por llamada, separadas por `;`. Consulta [rql.md](rql.md). |
| **script** | `script-read`, `script-create`, `script-update`, `script-delete`, `script-attach-to-node`, `script-validate`, `script-build`, `script-execute` | Archivos C# (`.cs`) y GDScript (`.gd`), adjuntar un script a un nodo, validación de GDScript, compilar el C# del proyecto y ejecutar C# en el editor. |
| **screenshot** | `screenshot-viewport`, `screenshot-camera`, `screenshot-isolated` | Capturar el viewport del editor, una cámara o un nodo renderizado por separado, como un PNG que el LLM puede ver. |
| **console** | `console-get-logs`, `console-clear-logs` | En un juego en marcha: leer y borrar los logs recopilados. |
| **reflection** | `reflection-method-find`, `reflection-method-call` | En un juego en marcha: buscar y llamar métodos C# de cualquier ensamblado cargado mediante ReflectorNet. |
| **runtime-errors** | `runtime-errors-get`, `runtime-errors-clear` | En un juego en marcha: consultar errores de runtime de GDScript, `push_error` / `push_warning`, errores de shader y excepciones de C#. **Desactivado por defecto.** |
| **ping** | `ping` | Sonda de disponibilidad. Herramienta de sistema. |
| **session** | `session-close` | Guardar y cerrar un editor que inició `godot-cli`. Herramienta de sistema. |
| **skills** | `godot-skill-create`, `godot-skill-generate` | Escribir un nuevo archivo de herramienta C# en el proyecto; regenerar los archivos `SKILL.md`. Herramientas de sistema. |

Las **herramientas de sistema** se sirven en el endpoint HTTP `/api/system-tools/` del servidor MCP (por ejemplo `godot-cli run-system-tool ping`). No aparecen para los agentes de IA en `tools/list`.

<a id="rql"></a>
## rql

- `rql-query`: una consulta de solo lectura, devuelta como filas compactas. `nodes | where class == "OmniLight3D" | select name props["light_energy"]`
- `rql-mutate`: una consulta que termina en `set` o `call`. Todo o nada, reversible (Ctrl+Z), y `dry_run` muestra una vista previa. Las ediciones de escena marcan la escena como no guardada; los archivos de recursos editados y `project.godot` se guardan.
  - `nodes | where name starts-with "Enemy" | set Visible = false`
  - `root | call AddChild("Node3D", "Props")`
  - `nodes | where name == "Button" | call Connect("pressed", godot.find("UI"), "_on_pressed")`
  - `project | call AddInputAction("jump", ["Key:Space"])`
  - `editor | call SaveScene()`
- `rql-schema`: los orígenes, funciones y clases de nodo de la escena editada; con un nombre de tipo, sus miembros, métodos y propiedades de Godot. `rql-schema "<extension>"` muestra la guía de una extensión instalada.

Los orígenes incluyen `nodes`, `root`, `selection`, `scenes`, `files`, `folders`, `classes`, `console`, `editor`, `project`, `settings`, `inputActions`, `autoloads`, `groups`, `layers` y `tools`. El lenguaje completo, incluidos TileSets, Themes, parámetros de ShaderMaterial y MeshLibraries, está en [rql.md](rql.md) y en el skill `godot-rql`.

<a id="script"></a>
## script

- `script-read`, `script-create`, `script-update`, `script-delete`: CRUD sobre archivos `.cs` y `.gd`.
- `script-attach-to-node`: adjuntar un script a un nodo.
- `script-validate`: analizar y compilar archivos GDScript y devolver diagnósticos estructurados.
- `script-build`: compilar el C# del proyecto (`dotnet build`, Debug) para que se carguen los scripts nuevos. La compilación se ejecuta en segundo plano; cuando termina bien, Godot recarga el ensamblado C#, el plugin se reconecta y el resultado (advertencias o errores) llega tras la recarga. Consulta [Recargas del ensamblado C#](custom-tools.es.md#c-assembly-reloads).
- `script-execute`: compilar y ejecutar C# en el editor con Roslyn, mediante [Code-Exec](https://github.com/IvanMurzak/Code-Exec). Sentencias simples con la última expresión como resultado (`using` propios, clases locales, `await`), o una clase con un método estático. Los tipos compilados del juego son visibles; `include` añade archivos `.cs` auxiliares de `<project>/.ai-game-dev/snippets`. Devuelve el valor y lo que imprimió el código (`Console`; `GD.Print`, push_warning y push_error cuando el proyecto compila con Godot.NET.Sdk 4.5+). Desde una terminal: `godot-cli exec '1 + 2'`.

<a id="screenshot"></a>
## screenshot

- `screenshot-viewport`: el viewport del editor.
- `screenshot-camera`: la vista desde una cámara concreta.
- `screenshot-isolated`: un nodo renderizado por separado, desde el ángulo elegido.

Las tres devuelven la imagen en la conversación. Pasa `savePath` para escribir además un archivo: `res://Screenshots/shot.png`, `Screenshots/shot.png` (relativo a la raíz del proyecto) o una ruta absoluta dentro del proyecto. Las carpetas que faltan se crean y se añade `.png` si no hay extensión. Un `savePath` se rechaza (sin imagen ni archivo) si está fuera del proyecto (`..`, `user://`), pasa por un enlace simbólico o junction, apunta dentro de `res://.godot/` o de este addon, no es un `.png` o nombra un archivo existente: nunca sobrescribe. Godot importa el PNG como cualquier archivo del proyecto; una carpeta con un archivo `.gdignore` lo evita.

<a id="console-reflection-runtime-errors-running-game"></a>
## console, reflection, runtime-errors (juego en marcha)

- `console-get-logs` / `console-clear-logs`: leer (con filtrado) y borrar los logs que recopiló el plugin, incluidos sus propios diagnósticos de conexión.
- `reflection-method-find` / `reflection-method-call`: buscar métodos C# (también privados) en todos los ensamblados cargados y llamar a uno con argumentos.
- `runtime-errors-get`: errores capturados en el juego, del más antiguo al más reciente, conservando los más nuevos. Pasa el `highestSequence` anterior como `sinceSequence` para obtener solo los errores nuevos. Devuelve `available:false` si la captura nunca se activó.
- `runtime-errors-clear`: vaciar el búfer; el contador de secuencia sigue contando.

La captura de errores de runtime está desactivada hasta que un juego llama a `builder.WithRuntimeErrorCapture()`. Detalles: [Captura de errores de runtime en el juego](runtime.es.md#capturing-in-game-runtime-errors).

<a id="ping-session-skills-system-tools"></a>
## ping, session, skills (herramientas de sistema)

- `ping`: devuelve el mensaje recibido, o `pong`. `curl -X POST <host>/api/system-tools/ping -d '{}'`.
- `session-close`: guardar y cerrar un editor iniciado por `godot-cli exec` / `open --headless` (las escenas abiertas que tienen archivo y los recursos que modificaron). Necesita el nonce de esa sesión de `.ai-game-dev/session.json`, así que nunca cierra tu propio editor. `godot-cli close --owned-only` la llama.
- `godot-skill-create`: escribir un nuevo archivo de herramienta C# en el proyecto. Se puede llamar tras recompilar el proyecto.
- `godot-skill-generate`: regenerar todos los `SKILL.md` a partir de las herramientas que ha registrado el editor, en la carpeta de skills del agente de IA seleccionado.

<a id="visibility"></a>
## Visibilidad

Cada herramienta, prompt y recurso tiene un interruptor **Visible** en las ventanas Tools / Prompts / Resources del dock (`godot-cli configure` hace lo mismo desde una terminal). Solo decide si los clientes MCP ven el elemento en `tools/list`, lo que mantiene su contexto pequeño. No es control de acceso: una herramienta oculta sigue ejecutándose si un cliente la llama por su nombre.

<a id="extensions"></a>
## Extensiones

Las extensiones añaden orígenes RQL, adaptadores y una guía para el agente a las herramientas `rql-*` y al skill `godot-rql`. No añaden herramientas MCP propias. La lista está en el [README](README.es.md#extensions).

```bash
godot-cli install-extension com.IvanMurzak.Godot.MCP.CSG ./MyGodotProject
godot-cli build ./MyGodotProject
```

El instalador solo añade el paquete NuGet de la extensión; instala tú mismo en el proyecto el addon de terceros que se requiera. La sección **Extensions** del dock tiene **Install All**. `godot-cli update` actualiza el addon y vuelve a resolver las extensiones flotantes (`Version="*"`) a su última versión; una extensión que fijaste a una versión se mantiene. Si una extensión necesita un McpPlugin más nuevo que el que fija el addon, la restauración falla con `NU1605` hasta que actualices el addon o fijes la versión de la extensión.
