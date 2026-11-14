[English](connection.md) · [中文](connection.zh-CN.md) · [日本語](connection.ja.md) · **Español** · [فارسی](connection.fa.md) · [हिन्दी](connection.hi.md) · [العربية](connection.ar.md)

# Conexión y servidor

El plugin del editor se conecta a un servidor MCP en uno de dos modos. Elige el modo en el dock del editor, o sustitúyelo al arrancar el proceso con las variables de entorno `GODOT_MCP_*` (útil para CI, ejecuciones headless y desarrollo local). El entorno siempre prevalece sobre la configuración guardada. `godot-cli open` las pasa por ti mediante `--mode`, `--url`, `--cloud-url` y `--token`.

<a id="cloud-mode-default-ai-gamedev"></a>
## Modo Cloud (predeterminado): ai-game.dev

El plugin se conecta a `https://ai-game.dev`; no ejecutas ningún servidor. Cloud es el modo predeterminado porque no necesita puertos abiertos y evita conflictos de puertos con otros programas; es gratuito. El relay no almacena el contenido de las peticiones ni de las respuestas y solo conserva metadatos, durante 90 días.

Inicia sesión una vez con `godot-cli login`. Usa el flujo de dispositivo de OAuth 2.1 (RFC 8628): muestra un código corto, abre tu navegador y, cuando apruebas, guarda una credencial en el almacén del equipo (`~/.ai-game-dev/credentials.json`). El plugin del editor la recoge, así que no hay ningún token que copiar.

| Variable | Propósito | Predeterminado |
| --- | --- | --- |
| `GODOT_MCP_CONNECTION_MODE` | `Cloud` o `Custom` (sin distinguir mayúsculas). | `Cloud` |
| `GODOT_MCP_CLOUD_URL` | URL base de la nube. Se elimina un `/mcp` final. | `https://ai-game.dev` |
| `GODOT_MCP_CREDENTIALS_DIR` | Directorio absoluto del almacén de credenciales, para pruebas aisladas. También se lee del `.env` del proyecto. | `~/.ai-game-dev` |
| `GODOT_MCP_TOKEN` | Token bearer del modo activo; una sustitución manual para CI y ejecuciones headless. | ninguno |

<a id="custom-mode-your-own-server"></a>
## Modo Custom: tu propio servidor

El plugin se conecta a la URL de servidor que elijas: un servidor local o uno autoalojado. Úsalo para trabajar sin conexión o en entornos aislados (air-gapped).

| Variable | Propósito | Predeterminado |
| --- | --- | --- |
| `GODOT_MCP_CONNECTION_MODE` | Establécela en `Custom`. | `Cloud` |
| `GODOT_MCP_HOST` | URL del servidor, una URL http(s) absoluta. | `http://localhost:<port>`, un puerto derivado de la ruta del proyecto |
| `GODOT_MCP_TOKEN` | Token bearer enviado a ese servidor. Si se define, selecciona la autorización `token`. | ninguno |
| `GODOT_MCP_AUTH_OPTION` | `none` / `oauth` / `token`; sustituye la regla anterior. | opción guardada, si no `none` |

```bash
export GODOT_MCP_CONNECTION_MODE=Custom
export GODOT_MCP_HOST=http://localhost:<port>
# export GODOT_MCP_TOKEN=...   # solo si el servidor requiere autenticación
```

`godot-cli open` las define por ti. Para un servidor local que exija un token elegido por ti, abre el editor con ese token y luego pulsa **Start Server** en el dock (`--auth Required` selecciona la autorización `token`):

```bash
godot-cli open ./MyGodotProject --mode Custom --auth Required --token <token>
```

- Un token definido en el entorno (o en el `.env` del proyecto en modo Custom) también se aplica a un servidor local que inicie el editor y a las configuraciones de agente escritas con **Configure**. No se guarda en el archivo de configuración.
- Con `oauth`, el token de tu cuenta de ai-game.dev se envía **solo a un servidor localhost** (`localhost`, `127.0.0.0/8`, `::1`). Para cualquier otro host define `GODOT_MCP_TOKEN`.
- Un servidor en modo `token` rechaza las conexiones sin token; el plugin registra en el log qué hay que definir.

<a id="the-server"></a>
## El servidor

El servidor es el [GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server) compartido (`gamedev-mcp-server`), un único binario para Unity-MCP, Godot-MCP y Unreal-MCP, publicado desde su propio repositorio. El addon fija la versión que usa (`ServerVersion` en `addons/godot_mcp/Runtime/Connection/GodotMcpServerView.cs`).

<a id="let-the-addon-run-it"></a>
### Dejar que lo ejecute el addon

En modo Custom, abre la tarjeta **Server** del dock y pulsa **Start Server**. El addon:

- descarga por HTTPS `gamedev-mcp-server-<rid>.zip` de la versión fijada desde la release de GitHub de GameDev-MCP-Server (`win-x64`, `win-x86`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`);
- lo guarda en caché en `.godot/mcp-server/<rid>/` y solo vuelve a descargarlo cuando cambia la versión fijada;
- lo inicia en el puerto de tu **Server URL** (por defecto, un puerto derivado de la ruta del proyecto) y se conecta a él.

El servidor escucha solo en loopback y responde `403` a una petición con una cabecera `Origin` no permitida (defensa contra DNS rebinding). Usa la autorización elegida en el dock: con `none` cualquier proceso de tu equipo puede llamarlo; con `token` exige el token bearer. Reglas de enlace, autorización y Origin: [README de GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server#readme).

**Stop Server** lo detiene; cerrar el editor también lo detiene. La URL de descarga depende solo de la versión fijada y de tu plataforma. Sin conexión, el addon registra una advertencia y el servidor no arranca. En CI (`CI` / `GITHUB_ACTIONS`) se omite la descarga.

Las llamadas a herramientas pueden durar hasta 5 minutos. `script-execute` detiene un fragmento por sí mismo cuando se agota su `timeout` (30 s por defecto).

Para desarrollo, `GODOT_MCP_SERVER_PATH=<ruta absoluta>` hace que el editor inicie ese binario `gamedev-mcp-server` existente en lugar de la release fijada (sin descarga, sin comprobación de versión, sin limpieza de procesos huérfanos). Se lee una vez por cada carga del ensamblado del plugin, desde el entorno del proceso o el `.env` del proyecto; una ruta que no apunta a ningún archivo se ignora con una advertencia. En Linux y macOS el archivo ya debe ser ejecutable.

<a id="run-it-yourself"></a>
### Ejecutarlo tú mismo

Descarga una [release de GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server/releases) o usa la imagen Docker [`aigamedeveloper/mcp-server`](https://hub.docker.com/r/aigamedeveloper/mcp-server):

```bash
./gamedev-mcp-server --client-transport streamableHttp --port 8080   # HTTP
./gamedev-mcp-server --client-transport stdio                        # un cliente MCP lo lanza directamente
```

Después apunta el plugin a él en modo Custom (`GODOT_MCP_HOST=http://localhost:8080`). Todos los argumentos y variables de entorno están en el [README de GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server#readme).

<a id="editor-side-security-notes"></a>
## Notas de seguridad del lado del editor

- **El token del editor se guarda en texto plano.** El plugin guarda su configuración de conexión, incluidos `token` y `cloudToken`, como JSON plano en `user://godot-mcp-config.json`. Trata ese archivo como un secreto: no lo subas al repositorio ni lo compartas. Para rotarlo, borra el token en el dock (o elimina el archivo) y vuelve a conectar. Un token del entorno del proceso, o del `.env` en modo Custom, no se escribe en este archivo.
- **El puente dev-control está desactivado salvo con `GODOT_MCP_DEV_CONTROL=1`.** Es un puente HTTP solo para desarrollo que controla el dock en las pruebas: solo en el editor, ligado a `127.0.0.1`, sin autenticación y nunca iniciado sin esa variable.

Detalles: [runtime-security.md](runtime-security.es.md#editor-side-surfaces-accepted-posture).
