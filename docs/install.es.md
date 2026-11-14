[English](install.md) · [中文](install.zh-CN.md) · [日本語](install.ja.md) · **Español** · [فارسی](install.fa.md) · [हिन्दी](install.hi.md) · [العربية](install.ar.md)

# Instalación manual

`godot-cli install-plugin ./MyGodotProject` realiza todos los pasos de esta página en un solo comando idempotente (consulta el [README](README.es.md#quick-start)). Usa esta página para una instalación desde la Asset Library o para un proyecto gestionado a mano.

<a id="requirements"></a>
## Requisitos

- **Godot 4.3 o posterior, edición .NET (mono).** El código de tu juego puede seguir en GDScript, pero el addon es C#: abre el proyecto con la versión .NET del editor y dale un archivo de proyecto C# (`.csproj`). En un proyecto GDScript existente usa una vez **Project → Tools → C# → Create C# solution**. El csproj del addon fija `Godot.NET.Sdk/4.3.0` como mínimo; la CI prueba Godot 4.3, 4.4, 4.5, 4.6 y 4.7.
- **SDK de .NET 8** (`net8.0`).
- **Windows, macOS o Linux.** Godot-MCP funciona en los tres.

<a id="1-add-the-addon"></a>
## 1. Añadir el addon

Elige una opción:

- **Godot Asset Library.** En el editor abre la pestaña **AssetLib**, busca **Godot-MCP** y pulsa **Download** e **Install**. Los archivos quedan en `res://addons/godot_mcp/`. Página web: [godotengine.org/asset-library/asset/5245](https://godotengine.org/asset-library/asset/5245).
- **GitHub Release.** Descarga `godot-mcp-addon-<version>.zip` de la [última release](https://github.com/IvanMurzak/Godot-MCP/releases/latest) y extráelo en la raíz del proyecto (el archivo ya contiene `addons/godot_mcp/`).
- **Desde el código fuente.** Copia `addons/godot_mcp/` de este repositorio en la carpeta `addons/` del proyecto.

<a id="2-add-the-nuget-packages-and-the-extension-catalog"></a>
## 2. Añadir los paquetes NuGet y el catálogo de extensiones

Godot compila todos los archivos `.cs` del proyecto en un único ensamblado, así que el `.csproj` del proyecto debe referenciar los paquetes que usa el addon. Usa exactamente las versiones de la release que instalas: `godot-cli install-plugin` las escribe por ti, un zip de release las lista en `addons/godot_mcp/nuget-deps.json` y cada tag de release las tiene en su `Godot-MCP.csproj` ([última release](https://github.com/IvanMurzak/Godot-MCP/releases/latest)):

```xml
<ItemGroup>
  <PackageReference Include="com.IvanMurzak.ReflectorNet" Version="<version>" />
  <PackageReference Include="com.IvanMurzak.McpPlugin"   Version="<version>" />
</ItemGroup>

<!-- Solo editor: los juegos exportados (ExportDebug / ExportRelease) no incluyen RQL ni Roslyn. -->
<ItemGroup>
  <PackageReference Include="com.IvanMurzak.Rql"         Version="<version>" Condition="'$(Configuration)' != 'ExportDebug' and '$(Configuration)' != 'ExportRelease'" />
  <PackageReference Include="com.IvanMurzak.CodeExec"        Version="<version>" Condition="'$(Configuration)' != 'ExportDebug' and '$(Configuration)' != 'ExportRelease'" />
  <PackageReference Include="com.IvanMurzak.CodeExec.Roslyn" Version="<version>" Condition="'$(Configuration)' != 'ExportDebug' and '$(Configuration)' != 'ExportRelease'" />
  <PackageReference Include="Microsoft.CodeAnalysis.CSharp"  Version="<version>" Condition="'$(Configuration)' != 'ExportDebug' and '$(Configuration)' != 'ExportRelease'" />
</ItemGroup>

<!-- Incrusta el catálogo de extensiones; si no, la lista Extensions del dock queda vacía. -->
<ItemGroup>
  <EmbeddedResource Include="addons/godot_mcp/extensions.catalog.json" LogicalName="Godot-MCP.extensions.catalog.json" />
</ItemGroup>
```

| Paquete | Función |
| --- | --- |
| [`com.IvanMurzak.ReflectorNet`](https://www.nuget.org/packages/com.IvanMurzak.ReflectorNet) | Núcleo de reflexión y serialización |
| [`com.IvanMurzak.McpPlugin`](https://www.nuget.org/packages/com.IvanMurzak.McpPlugin) | Cliente del plugin MCP |
| [`com.IvanMurzak.Rql`](https://www.nuget.org/packages/com.IvanMurzak.Rql) | RQL, la capa de datos de las herramientas del editor (PolyForm Shield) |
| [`com.IvanMurzak.CodeExec`](https://www.nuget.org/packages/com.IvanMurzak.CodeExec) + [`.Roslyn`](https://www.nuget.org/packages/com.IvanMurzak.CodeExec.Roslyn) | Code-Exec, detrás de `script-execute` (PolyForm Shield) |
| [`Microsoft.CodeAnalysis.CSharp`](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp) | Roslyn, el compilador de C# que ejecuta `script-execute` |

- Los cuatro paquetes solo de editor llevan una `Condition` que los excluye de las compilaciones de exportación, de modo que un juego publicado no contiene ni RQL ni Roslyn ([ARCHITECTURE.md](ARCHITECTURE.md#editor-only-nuget-packages-no-roslyn-in-an-exported-game)).
- El `<EmbeddedResource>` es obligatorio: el addon lee el catálogo desde su ensamblado, y su `LogicalName` debe ser exactamente `Godot-MCP.extensions.catalog.json`.
- Mantén `<EnableDynamicLoading>true</EnableDynamicLoading>` en el `<PropertyGroup>` (la plantilla C# de Godot lo incluye). Sin él, los ensamblados de los paquetes no se copian junto al ensamblado del juego, y un juego iniciado desde el editor no logra crear ningún script C# ("Cannot instantiate C# script because the associated class could not be found").

Ejecuta `dotnet restore` y luego compila. No hace falta copiar DLL: en tiempo de ejecución del editor, el resolvedor de ensamblados del addon las encuentra en la carpeta global de paquetes NuGet a través del `*.deps.json` de la compilación.

<a id="3-enable-the-plugin"></a>
## 3. Activar el plugin

**Project → Project Settings → Plugins → Godot-MCP → Enable.** El panel Output muestra:

```
[Godot-MCP] plugin loaded
```

<a id="4-connect-an-ai-agent"></a>
## 4. Conectar un agente de IA

Instala cualquier cliente MCP (Claude Code, Claude Desktop, Cursor, VS Code con Copilot, Codex, Gemini, …) y escribe su configuración:

```bash
godot-cli setup-mcp claude-code ./MyGodotProject   # godot-cli setup-mcp --list muestra todos los ids de agente
```

El botón **Configure** del dock del editor escribe la misma configuración. Por defecto usa la URL de nube fijada al proyecto `<host>/mcp/p/<pin>`, de modo que un agente iniciado en esta carpeta llega al editor de este proyecto aunque haya varios editores conectados; pasa `--no-pin` para la URL simple `<host>/mcp`. En modo Cloud la configuración lleva la clave de este proyecto (`Authorization: Bearer agd_pk_…`); sin inicio de sesión, o con `--oauth`, solo lleva la URL y el agente inicia sesión con su propio OAuth. Modos de conexión y variables de entorno: [connection.md](connection.es.md).
