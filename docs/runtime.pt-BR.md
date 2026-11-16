[English](runtime.md) · [中文](runtime.zh-CN.md) · [日本語](runtime.ja.md) · [Español](runtime.es.md) · **Português** · [فارسی](runtime.fa.md) · [हिन्दी](runtime.hi.md) · [العربية](runtime.ar.md)

# Uso em runtime (dentro do jogo)

O plugin do editor conecta o **editor Godot**. O Godot-MCP também pode rodar dentro de um **jogo em execução ou exportado** (debug ou release), para que um LLM leia e conduza o estado do jogo ao vivo: por exemplo, um jogo de xadrez cujos movimentos do bot vêm de um LLM através de duas ferramentas que você escreve.

O modo runtime difere do editor de propósito:

- **Ele nunca conecta sozinho.** Seu código decide se e quando chamar `Connect()`.
- **Ele não expõe nada por padrão.** Você mesmo registra cada ferramenta (`[AiToolType]`), prompt (`[AiPromptType]`) e recurso (`[AiResourceType]`); cada tipo é opcional. As ferramentas de editor do addon ficam atrás de `#if TOOLS` e não compilam em um jogo exportado.

Leia [runtime-security.md](runtime-security.pt-BR.md) antes de publicar uma build que chama `GodotMcpRuntime.Initialize`.

<a id="set-it-up"></a>
## Configuração

Chame `GodotMcpRuntime.Initialize(...)` assim que uma `SceneTree` existir, por exemplo do `_Ready()` de um autoload:

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
                config.ConnectionMode = GodotMcpConnectionMode.Custom;     // your own server
                config.Host           = "http://localhost:8080";           // prefer loopback
                config.AuthOption     = McpServerConsts.AuthOption.token;  // require a token
                // Token: prefer GODOT_MCP_TOKEN from the environment over a value in code.
            });

            builder.WithToolsFromAssembly(Assembly.GetExecutingAssembly());     // [AiToolType] classes
            builder.WithPromptsFromAssembly(Assembly.GetExecutingAssembly());   // [AiPromptType] classes
            builder.WithResourcesFromAssembly(Assembly.GetExecutingAssembly()); // [AiResourceType] classes
        }).Build();

        await _mcp.Connect();   // explicit opt-in; retries in the background while KeepConnected is true
    }

    public override async void _ExitTree()
    {
        if (_mcp is not null)
            await _mcp.Disconnect();
    }
}
```

| Chamada | O que faz |
| --- | --- |
| `GodotMcpRuntime.Initialize(configure)` | Inicia a configuração; `configure` pode ser `null` para o padrão sem ferramentas, configurado pelo ambiente. |
| `WithConfig(Action<GodotMcpConfig>)` | Definir `Host`, `Token`, `ConnectionMode`, `AuthOption` em código. As chamadas compõem em ordem. |
| `WithToolsFromAssembly(Assembly)` / `WithTools(params Type[])` | Registrar classes `[AiToolType]`: um assembly inteiro, ou tipos específicos. |
| `WithPromptsFromAssembly` / `WithPrompts` | O mesmo para classes `[AiPromptType]`. |
| `WithResourcesFromAssembly` / `WithResources` | O mesmo para classes `[AiResourceType]`. |
| `WithoutMainThreadDispatcher()` | Pular o dispatcher automático de thread principal (apenas se você instalar o seu próprio). |
| `ConnectWhenRunFromEditor()` | Também conectar em um jogo iniciado pelo editor (desligado por padrão, veja abaixo). |
| `WithRuntimeErrorCapture()` | Capturar erros dentro do jogo e registrar `runtime-errors-*`. Desligado por padrão. |
| `WithScriptExecute()` | Registrar `script-execute` no jogo. Desligado por padrão; requer o opt-in no csproj. |
| `Build()` | Devolve um `GodotMcpRuntimeHandle` que não está conectado. |
| `handle.Connect()` / `Disconnect()` / `Dispose()` | Abrir, fechar ou desmontar a conexão. |

`Build()` também adiciona um nó dispatcher de thread principal à `SceneTree` em execução (a menos que você opte por não ter), de modo que handlers de ferramenta podem usar `MainThread.Instance.Run(...)`.

<a id="server-url-and-token"></a>
## URL do servidor e token

Um jogo nunca carrega a config salva do editor. Ele toma as configurações do seu código (`WithConfig`) e de variáveis de ambiente de processo `GODOT_MCP_*` ou de um arquivo `.env` do projeto, que são lidas ao vivo e vencem sobre o código. As variáveis estão listadas em [runtime-security.md](runtime-security.pt-BR.md#environment-variables). Modo Custom em um host de loopback com um token é a configuração recomendada para um jogo.

<a id="games-run-from-the-editor"></a>
## Jogos executados pelo editor

Um jogo iniciado com Play (F5) herda o ambiente do editor, incluindo `GODOT_MCP_HOST` e `GODOT_MCP_TOKEN`. Então, por padrão, `handle.Connect()` não faz nada em tal jogo e devolve `false` (ele registra o motivo): o editor permanece o único plugin com que o agente fala. Jogos exportados e jogos iniciados fora do editor conectam normalmente. Para também conectar um jogo executado pelo editor, use `builder.ConnectWhenRunFromEditor()` ou `GODOT_MCP_RUNTIME_FROM_EDITOR=1`. Um GameDev-MCP-Server com papéis de plugin mantém então as duas conexões e roteia chamadas de ferramenta para o editor enquanto ele estiver conectado.

<a id="a-game-tool"></a>
## Uma ferramenta de jogo

Uma ferramenta de runtime é escrita como uma ferramenta de editor ([custom-tools.md](custom-tools.pt-BR.md)). Marque as famílias exclusivas do jogo com `Target = ToolTarget.Runtime`, para que o editor não as registre e o agente alcance o jogo mesmo enquanto o editor está conectado.

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

Prompts e recursos registram-se da mesma forma: uma `partial class` marcada com `[AiPromptType]` / `[AiResourceType]` com membros marcados com `[AiPrompt(Name = …, Role = …)]` / `[AiResource(Name = …, Route = "game://…", MimeType = …)]`. Um método de recurso devolve `ResponseResourceContent[]` (por exemplo `ResponseResourceContent.CreateText(uri, json, "application/json")`). Todos esses tipos vêm do pacote `com.IvanMurzak.McpPlugin`.

<a id="capturing-in-game-runtime-errors"></a>
## Capturando erros de runtime dentro do jogo

As ferramentas de editor não veem erros levantados dentro de um jogo em execução, então um agente poderia iniciar o jogo, ler logs silenciosos e concluir erroneamente que ele está saudável. `builder.WithRuntimeErrorCapture()` fecha essa lacuna. Ele instala três canais de melhor esforço e registra `runtime-errors-get` / `runtime-errors-clear`:

1. **Erros do engine (Godot 4.5+)** através de `OS.AddLogger`: erros de runtime de GDScript, `push_error` / `push_warning` e erros de shader, com `file`, `line` e `function`. No Godot 4.5+ um erro de GDScript também carrega a pilha de chamadas completa em `frames` (mais interno primeiro) e `stackTrace`.
2. **Exceções C# não tratadas** (`AppDomain.CurrentDomain.UnhandledException`) com o stack trace gerenciado completo.
3. **Exceções de `Task` não observadas em C#** (`TaskScheduler.UnobservedTaskException`), observadas apenas para log.

Em Godot abaixo de 4.5 o canal do engine não está disponível; os canais C# continuam funcionando. `handle.Dispose()` remove os hooks. As mensagens e stack traces capturados vão ao agente literalmente e podem conter caminhos, nomes de usuário ou segredos, então habilite a captura somente em uma conexão confiável de loopback + token.

<a id="running-c-in-the-game"></a>
## Executando C# no jogo

`builder.WithScriptExecute()` permite que o agente envie C# que o jogo em execução compila com Roslyn e executa na sua thread principal, com os tipos, autoloads e árvore de cena do próprio jogo. Builds de exportação deixam o Roslyn de fora, então o `.csproj` do jogo precisa do opt-in:

```xml
<PropertyGroup>
  <GodotMcpCodeExecInGame>true</GodotMcpCodeExecInGame>
  <DefineConstants>$(DefineConstants);GODOT_MCP_CODE_EXEC_IN_GAME</DefineConstants>
</PropertyGroup>
```

Um `godot-cli install-plugin` atual escreve as referências dos pacotes Code-Exec e Roslyn com uma `Condition` que honra essa propriedade; o RQL nunca é incluído. Sem o opt-in, `WithScriptExecute()` registra um aviso e não registra nada.

> **Isto é execução arbitrária de código.** Qualquer um que possa chamar ferramentas na conexão pode executar qualquer código no processo do jogo. Use-o apenas em builds de desenvolvimento, em um host de loopback com um token. Somente CoreCLR de desktop: NativeAOT e iOS não podem executar código gerado em tempo de execução.
