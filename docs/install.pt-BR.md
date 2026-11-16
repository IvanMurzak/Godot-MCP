[English](install.md) · [中文](install.zh-CN.md) · [日本語](install.ja.md) · [Español](install.es.md) · **Português** · [فارسی](install.fa.md) · [हिन्दी](install.hi.md) · [العربية](install.ar.md)

# Instalação manual

`godot-cli install-plugin ./MyGodotProject` executa todos os passos desta página em um comando idempotente (veja o [README](README.pt-BR.md#quick-start)). Use esta página para uma instalação pela Asset Library ou para um projeto gerenciado manualmente.

<a id="requirements"></a>
## Requisitos

- **Godot 4.3 ou posterior, edição .NET (mono).** O código do seu jogo pode continuar em GDScript, mas o addon é C#: abra o projeto na versão .NET do editor e dê a ele um arquivo de projeto C# (`.csproj`). Para um projeto GDScript existente use **Project → Tools → C# → Create C# solution** uma vez. O csproj do addon fixa `Godot.NET.Sdk/4.3.0` como piso; o CI testa Godot 4.3, 4.4, 4.5, 4.6 e 4.7.
- **SDK do .NET 8** (`net8.0`).
- **Windows, macOS ou Linux.** O Godot-MCP funciona nos três.

<a id="1-add-the-addon"></a>
## 1. Adicione o addon

Escolha uma opção:

- **Godot Asset Library.** No editor, abra a aba **AssetLib**, procure por **Godot-MCP**, depois **Download** e **Install**. Os arquivos vão para `res://addons/godot_mcp/`. Página web: [godotengine.org/asset-library/asset/5245](https://godotengine.org/asset-library/asset/5245).
- **GitHub Release.** Baixe `godot-mcp-addon-<version>.zip` da [release mais recente](https://github.com/IvanMurzak/Godot-MCP/releases/latest) e extraia-o na raiz do projeto (o arquivo já contém `addons/godot_mcp/`).
- **A partir do código-fonte.** Copie `addons/godot_mcp/` deste repositório para a pasta `addons/` do projeto.

<a id="2-add-the-nuget-packages-and-the-extension-catalog"></a>
## 2. Adicione os pacotes NuGet e o catálogo de extensões

O Godot compila todo arquivo `.cs` do projeto em um único assembly, então o `.csproj` do projeto deve referenciar os pacotes que o addon usa. Use exatamente as versões da release que você instalar: `godot-cli install-plugin` as escreve para você, um zip de release as lista em `addons/godot_mcp/nuget-deps.json`, e cada tag de release as tem no seu `Godot-MCP.csproj` ([release mais recente](https://github.com/IvanMurzak/Godot-MCP/releases/latest)):

```xml
<ItemGroup>
  <PackageReference Include="com.IvanMurzak.ReflectorNet" Version="<version>" />
  <PackageReference Include="com.IvanMurzak.McpPlugin"   Version="<version>" />
</ItemGroup>

<!-- Somente editor: jogos exportados (ExportDebug / ExportRelease) não incluem RQL nem Roslyn. -->
<ItemGroup>
  <PackageReference Include="com.IvanMurzak.Rql"         Version="<version>" Condition="'$(Configuration)' != 'ExportDebug' and '$(Configuration)' != 'ExportRelease'" />
  <PackageReference Include="com.IvanMurzak.CodeExec"        Version="<version>" Condition="'$(Configuration)' != 'ExportDebug' and '$(Configuration)' != 'ExportRelease'" />
  <PackageReference Include="com.IvanMurzak.CodeExec.Roslyn" Version="<version>" Condition="'$(Configuration)' != 'ExportDebug' and '$(Configuration)' != 'ExportRelease'" />
  <PackageReference Include="Microsoft.CodeAnalysis.CSharp"  Version="<version>" Condition="'$(Configuration)' != 'ExportDebug' and '$(Configuration)' != 'ExportRelease'" />
</ItemGroup>

<!-- Incorpore o catálogo de extensões, ou a lista Extensions do dock ficará vazia. -->
<ItemGroup>
  <EmbeddedResource Include="addons/godot_mcp/extensions.catalog.json" LogicalName="Godot-MCP.extensions.catalog.json" />
</ItemGroup>
```

| Pacote | Papel |
| --- | --- |
| [`com.IvanMurzak.ReflectorNet`](https://www.nuget.org/packages/com.IvanMurzak.ReflectorNet) | Núcleo de reflexão e serialização |
| [`com.IvanMurzak.McpPlugin`](https://www.nuget.org/packages/com.IvanMurzak.McpPlugin) | Cliente do plugin MCP |
| [`com.IvanMurzak.Rql`](https://www.nuget.org/packages/com.IvanMurzak.Rql) | RQL, a camada de dados das ferramentas do editor (PolyForm Shield) |
| [`com.IvanMurzak.CodeExec`](https://www.nuget.org/packages/com.IvanMurzak.CodeExec) + [`.Roslyn`](https://www.nuget.org/packages/com.IvanMurzak.CodeExec.Roslyn) | Code-Exec, por trás de `script-execute` (PolyForm Shield) |
| [`Microsoft.CodeAnalysis.CSharp`](https://www.nuget.org/packages/Microsoft.CodeAnalysis.CSharp) | Roslyn, o compilador C# que o `script-execute` executa |

- Os quatro pacotes somente de editor carregam uma `Condition` que os deixa fora das builds de exportação, de modo que um jogo publicado não contém RQL nem Roslyn ([ARCHITECTURE.md](ARCHITECTURE.md#editor-only-nuget-packages-no-roslyn-in-an-exported-game)).
- O `<EmbeddedResource>` é obrigatório: o addon lê o catálogo do seu assembly, e seu `LogicalName` deve ser exatamente `Godot-MCP.extensions.catalog.json`.
- Mantenha `<EnableDynamicLoading>true</EnableDynamicLoading>` no `<PropertyGroup>` (o template C# do Godot já o traz). Sem ele, os assemblies dos pacotes não são copiados para junto do assembly do jogo, e um jogo iniciado pelo editor falha ao criar todos os scripts C# ("Cannot instantiate C# script because the associated class could not be found").

Execute `dotnet restore` e depois compile. Nenhuma DLL precisa ser copiada: em tempo de execução do editor, o resolvedor de assemblies do addon as encontra na pasta global de pacotes NuGet através do `*.deps.json` da build.

<a id="3-enable-the-plugin"></a>
## 3. Habilite o plugin

**Project → Project Settings → Plugins → Godot-MCP → Enable.** O painel Output imprime:

```
[Godot-MCP] plugin loaded
```

<a id="4-connect-an-ai-agent"></a>
## 4. Conecte um agente de IA

Instale qualquer cliente MCP (Claude Code, Claude Desktop, Cursor, VS Code com Copilot, Codex, Gemini, …) e escreva sua config:

```bash
godot-cli setup-mcp claude-code ./MyGodotProject   # godot-cli setup-mcp --list mostra todos os ids de agente
```

O botão **Configure** do dock do editor escreve a mesma config. Por padrão ele usa a URL de nuvem fixada ao projeto `<host>/mcp/p/<pin>`, de modo que um agente iniciado nesta pasta alcança o editor deste projeto mesmo quando vários editores estão conectados; passe `--no-pin` para a URL simples `<host>/mcp`. No modo Cloud a config carrega a chave deste projeto (`Authorization: Bearer agd_pk_…`); sem login, ou com `--oauth`, ela fica apenas com a URL e o agente faz login com seu próprio OAuth. Modos de conexão e variáveis de ambiente: [connection.md](connection.pt-BR.md).
