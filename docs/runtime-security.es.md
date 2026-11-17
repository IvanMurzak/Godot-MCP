[English](runtime-security.md) · [中文](runtime-security.zh-CN.md) · [日本語](runtime-security.ja.md) · **Español** · [فارسی](runtime-security.fa.md) · [हिन्दी](runtime-security.hi.md) · [العربية](runtime-security.ar.md)

# Contrato de seguridad del runtime (dentro del juego)

Godot-MCP puede ejecutarse **dentro de una compilación del juego en marcha / exportada** (debug o release), no solo en el editor;
consulta [Uso en runtime (dentro del juego)](runtime.es.md) para la guía completa. Ejecutar una conexión
MCP dentro de un juego publicado abre una **superficie de control remoto**: todo lo que puedan hacer tus herramientas
`[AiToolType]` registradas lo puede controlar un cliente MCP conectado. Este documento es el contrato de seguridad
de ese modo. Léelo antes de publicar una compilación que llame a `GodotMcpRuntime.Initialize(...)`.

<a id="guarantees-the-runtime-gives-you"></a>
## Garantías que te da el runtime

- **Solo por activación explícita: nunca se conecta solo.** El plugin del editor se conecta al arrancar; una compilación del juego **no**.
  `GodotMcpRuntime.Initialize(...).Build()` devuelve un `GodotMcpRuntimeHandle` **desactivado por defecto**. Nada
  se conecta hasta que *tu* código llama explícitamente a `handle.Connect()`. No existe ninguna ruta de código de conexión automática en
  una compilación que no sea del editor.
- **Cero herramientas por defecto: estrictamente manual.** Un runtime recién construido no registra **ninguna** herramienta MCP. La
  superficie de ataque es exactamente el conjunto de clases `[AiToolType]` que activas con
  `builder.WithToolsFromAssembly(...)` / `builder.WithTools(...)`, nada más. Sin ninguna registrada, la
  conexión igualmente se construye y se conecta con un conjunto de herramientas vacío (exactamente el modelo de Unity-MCP).
- **Las familias de herramientas del editor no pueden colarse.** Las familias de herramientas de editor del addon (`Tool_Rql`, `Tool_Script`,
  `Tool_Screenshot`, `Tool_Session`) están protegidas por `#if TOOLS` y **no se compilan** en una compilación exportada del juego. Ni siquiera un
  `WithToolsFromAssembly(...)` sobre el propio ensamblado del addon puede registrarlas.
- **Sin carga automática de configuración persistida.** Una compilación del juego nunca lee en silencio un archivo de configuración guardado (`user://`);
  eso es una comodidad solo del editor. El host y el token vienen únicamente de tu código (`WithConfig`) o del
  entorno del proceso `GODOT_MCP_*` / un `.env` del proyecto, que `GodotMcpConfig` lee en vivo.

<a id="what-you-must-do-when-enabling-it"></a>
## Qué debes hacer al activarlo

- **Conéctate de forma deliberada.** Llama a `handle.Connect()` solo cuando realmente quieras exponer herramientas (una
  compilación de desarrollo, un banco de pruebas de QA, una función de agente en el juego diseñada expresamente). Llama a `handle.Disconnect()` /
  `handle.Dispose()` para dejar de exponerlas.
- **Registra el conjunto mínimo de herramientas.** Prefiere `WithTools(typeof(MyToolFamily))` a un
  `WithToolsFromAssembly(...)` amplio cuando solo necesites unas pocas herramientas. Cada herramienta registrada es accesible para un
  cliente conectado.
- **Prefiere loopback + un token.** Para herramientas locales, vincula el modo Custom a un host loopback
  (`http://localhost:…` / `127.0.0.1`) y define `AuthOption = Consts.MCP.Server.AuthOption.token` con un
  `Token` real:

  ```csharp
  // using McpServerConsts = com.IvanMurzak.McpPlugin.Common.Consts.MCP.Server;
  builder.WithConfig(config =>
  {
      config.ConnectionMode = GodotMcpConnectionMode.Custom;
      config.Host           = "http://localhost:8080";   // loopback
      config.AuthOption     = McpServerConsts.AuthOption.token;
      config.Token          = "your-secret-token";  // desaconsejado: ver la forma con variables de entorno abajo; no publiques un secreto incrustado
  });
  ```

  Prefiere la forma fuera de banda de abajo, para que la compilación no lleve ningún secreto incrustado:

  ```bash
  export GODOT_MCP_CONNECTION_MODE=Custom
  export GODOT_MCP_HOST=http://localhost:8080
  export GODOT_MCP_AUTH_OPTION=token
  export GODOT_MCP_TOKEN=your-secret-token
  ```

- **No expongas una superficie MCP sin autenticación en una interfaz pública en una compilación release**, salvo que
  hayas diseñado y protegido esa superficie de forma explícita. Trata cualquier host que no sea loopback como una decisión deliberada
  y revisada.
- **Cambia el host o el modo solo con la conexión cerrada.** Salvo que asignes tu propio `CredentialProvider`, el
  bearer de cada conexión está ligado al modo y al host al que llama: tras un cambio, una reconexión del enlace existente
  se envía sin token, así que llama a `handle.Disconnect()` y `handle.Connect()` para cambiar de servidor.
- **No incrustes secretos de larga duración en el binario publicado.** Prefiere `GODOT_MCP_TOKEN` del
  entorno a un `config.Token` escrito en el código, para que el token no pueda extraerse de la compilación.
- **Activa la captura de errores de runtime solo en una conexión de confianza.** `builder.WithRuntimeErrorCapture()` está
  **DESACTIVADO por defecto**. Cuando se activa, los errores capturados reenvían el mensaje **completo** y (para fallos de C#) la
  **traza de pila administrada completa** al agente conectado mediante la herramienta `runtime-errors-get`. Esas cadenas pueden
  contener datos sensibles del runtime: rutas absolutas del sistema de archivos, nombres de equipo/usuario, query strings, o un
  secreto/token que apareció en el mensaje o argumento de una excepción. Ese es el valor diagnóstico buscado,
  pero amplía los datos expuestos por la conexión. Actívala solo en un host loopback con
  `AuthOption = Consts.MCP.Server.AuthOption.token` y un token real; nunca en una interfaz pública sin autenticación
  en una compilación release.

<a id="in-game-code-execution-withscriptexecute"></a>
## Ejecución de código en el juego (`WithScriptExecute`)

`builder.WithScriptExecute()` registra `script-execute` en el juego: un cliente conectado envía C#, el juego
lo compila con Roslyn y lo ejecuta en su hilo principal. Eso es **ejecución de código arbitrario en el proceso
del juego**, con el acceso al sistema de archivos y a la red del jugador. Está DESACTIVADO por defecto y requiere activarlo en la
compilación: un juego exportado solo tiene Code-Exec y Roslyn cuando su `.csproj` define `GodotMcpCodeExecInGame=true`
y define `GODOT_MCP_CODE_EXEC_IN_GAME` ([Ejecutar C# en el juego](runtime.es.md#running-c-in-the-game)). Sin esa activación, la llamada registra
una advertencia y no registra nada.

> **Juegos iniciados desde el editor.** Su compilación es la del editor (`TOOLS`), así que las familias de herramientas
> de editor del addon también se compilan en el ensamblado del juego. Un `WithToolsFromAssembly(...)` sobre ese ensamblado
> las registra entonces, incluido el `script-execute` del editor, con o sin `WithScriptExecute()`. En una compilación
> de exportación no existen. Registra tipos concretos (`WithTools(...)`) si un juego ejecutado desde el editor no debe
> ofrecerlas.

- **Solo compilaciones debug / de desarrollo.** Condiciona la llamada (y la activación en el csproj) a tu propio interruptor de desarrollo. Nunca la publiques
  activada en una compilación release pública.
- **Loopback + token, siempre.** Usa un host loopback y `AuthOption = token` con un token real tomado del
  entorno. Quien pueda llamar a herramientas en la conexión controla el proceso.
- **Solo CoreCLR de escritorio.** NativeAOT e iOS no pueden ejecutar código emitido en tiempo de ejecución; en móviles no está probado.
- **Tamaño.** La activación añade Roslyn (`Microsoft.CodeAnalysis*.dll`, varios MB) a la exportación. RQL nunca se
  incluye.

<a id="environment-variables"></a>
## Variables de entorno

La configuración del runtime (`GodotMcpConfig`) lee estas variables `GODOT_MCP_*` en vivo (entorno del proceso o `.env`
del proyecto). Sustituyen los valores definidos en el código en el momento de resolverlos.

| Variable | Valores | Descripción |
| -------- | ------ | ----------- |
| `GODOT_MCP_CONNECTION_MODE` | `Cloud` / `Custom` | Modo de conexión (un host loopback implica `Custom`). |
| `GODOT_MCP_CLOUD_URL` | URL | Sustituye la URL base de Cloud (por defecto `https://ai-game.dev`). |
| `GODOT_MCP_HOST` | URL | Host del servidor en modo Custom (por defecto `http://localhost:8080`). |
| `GODOT_MCP_AUTH_OPTION` | `none` / `oauth` / `token` | Autenticación en modo Custom: anónima / cuenta (oauth) / bearer sin conexión (`token`). Si no se define, un `GODOT_MCP_TOKEN` selecciona `token`. |
| `GODOT_MCP_TOKEN` | cadena | El token bearer (dirigido a Cloud o Custom según el modo activo). |
| `GODOT_MCP_LOG_LEVEL` | `Trace` / `Debug` / `Info` / `Warning` / `Error` / `None` | Umbral de detalle del log. |
| `GODOT_MCP_EDITOR_PID` | id de proceso | Lo define el plugin del editor en el proceso del editor, así que todo proceso que inicie el editor lo hereda. Mientras ese editor está en marcha, el `Connect()` del runtime permanece sin conexión en un proceso que la lleve (salvo que se active abajo). |
| `GODOT_MCP_RUNTIME_FROM_EDITOR` | `1` / `true` | Solo entorno del proceso. Permite al runtime conectarse en un juego iniciado desde el editor de Godot (por defecto permanece sin conexión ahí, porque el juego hereda los ajustes `GODOT_MCP_*` del editor); equivale a `builder.ConnectWhenRunFromEditor()`. |

<a id="editor-side-surfaces-accepted-posture"></a>
## Superficies del lado del editor (postura aceptada)

El contrato anterior es para una **compilación del juego**. Aquí se documentan dos superficies del lado del editor para que esté completo;
ambas son **así por diseño** hoy (aún no se implementa cifrado ni secreto compartido; son decisiones aplazadas,
condicionadas por la paridad, que deben coordinarse entre los proyectos hermanos de Unity / Godot / Unreal).

<a id="editor-token-storage-is-plaintext-at-rest"></a>
### El token del editor se almacena en texto plano

Cuando conectas el **plugin del editor** (autenticación de dispositivo Cloud, o un token en modo Custom), este guarda tu
configuración de conexión en **`user://godot-mcp-config.json`** (Godot resuelve `user://` al directorio de datos de usuario
de cada plataforma mediante `ProjectSettings.GlobalizePath`). El token bearer (`token`) y el token de Cloud
(`cloudToken`) se escriben como **JSON en texto plano**: **no** están cifrados y **no** se guardan en un almacén de
claves del sistema operativo.

- **Supuesto de confianza: la cuenta de usuario local.** Cualquiera que pueda leer tu directorio de datos de usuario puede leer el
  token. Trata `user://godot-mcp-config.json` como un secreto: no lo subas al repositorio, no lo sincronices con una unidad compartida ni lo
  pegues en ningún sitio.
- **Rotación.** Borra el token guardado en el dock (o elimina el archivo) y vuelve a conectar.
- **Precedencia.** Un valor `GODOT_MCP_TOKEN` del entorno del proceso, o un valor del `.env` en modo Custom, oculta el token
  persistido en el momento de resolverlo y **no** se vuelve a escribir en este archivo; así, una máquina de CI / compartida puede funcionar
  sin persistir nunca un token en disco. Un token del `.env` leído en modo Cloud sí se escribe al guardar desde el dock.
- **Un `GODOT_MCP_TOKEN` definido se envía al host Custom.** Sin `GODOT_MCP_AUTH_OPTION`, sustituye un
  `AuthOption = none` configurado, incluso uno definido en el código. Define `GODOT_MCP_AUTH_OPTION=none` para mantener anónima una conexión.

<a id="the-dev-control-bridge-is-unauthenticated-but-gated-off"></a>
### El puente dev-control no tiene autenticación, pero está desactivado

Existe un puente HTTP de inyección/control solo para desarrollo (`DevControlServer`) para controlar el dock del editor desde
pruebas / una terminal. **No tiene autenticación**, pero su límite de seguridad es triple:

1. **Solo editor.** Se compila detrás de `#if TOOLS`, así que no existe en una compilación exportada del juego.
2. **Solo loopback.** Se vincula **exclusivamente a `127.0.0.1`**, nunca a una interfaz enrutable.
3. **DESACTIVADO por variable de entorno.** Se construye e inicia **solo cuando `GODOT_MCP_DEV_CONTROL=1`** (exactamente `1`).
   Sin definir o con cualquier otro valor, el puente nunca escucha: un addon publicado, y cualquier sesión normal
   del editor, nunca abre esa superficie.

Esa condición de entorno es **crítica**: es lo único que separa el "andamiaje de desarrollo" de "una
superficie de control sin autenticación sobre el dock en vivo". Se hace cumplir con un predicado puramente administrado
(`DevControlGate.IsEnabled`) con una prueba unitaria (`DevControlGateTests`) **y** una aserción al arrancar en el
punto de construcción (`DevControlGate.AssertEnabledOrThrow`), de modo que una regresión que active el puente sin
la variable de entorno falle de inmediato en CI y en tiempo de ejecución, en lugar de publicarse activada en silencio.
