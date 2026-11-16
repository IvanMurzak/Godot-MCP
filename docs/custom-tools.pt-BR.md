[English](custom-tools.md) · [中文](custom-tools.zh-CN.md) · [日本語](custom-tools.ja.md) · [Español](custom-tools.es.md) · **Português** · [فارسی](custom-tools.fa.md) · [हिन्दी](custom-tools.hi.md) · [العربية](custom-tools.ar.md)

# Ferramentas personalizadas

Adicione suas próprias ferramentas MCP no código C# do projeto. Uma família de ferramentas é uma `partial class` marcada com `[AiToolType]`; cada ferramenta é um método marcado com `[AiTool("tool-name", …)]`, com uma `[Description]` no método e em cada parâmetro para que o LLM saiba quando e como chamá-la.

Qualquer chamada à API do Godot (`Node`, `Resource`, `EditorInterface`, …) **deve** rodar na thread principal. Envolva-a em `MainThread.Instance.Run(...)`; o plugin apoia o `MainThread` do ReflectorNet no dispatcher de thread principal do Godot.

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
        // Work that does not touch the Godot API can run here, on a background thread.

        return MainThread.Instance.Run(() =>
        {
            // Touch EditorInterface / Node / Resource here, on the main thread.
            return "[Success] Operation completed.";
        });
    }
}
```

- Devolva um modelo estruturado (o ReflectorNet o serializa) ou `void`, não strings formatadas à mão.
- Torne um parâmetro opcional com um tipo anulável e um padrão: `string? optional = null`.
- `[AiToolType(Target = ToolTarget.Runtime)]` torna uma família exclusiva do jogo, `ToolTarget.Editor` exclusiva do editor; o padrão, `Both`, a registra em todo lugar. Ferramentas de jogo: [runtime.md](runtime.pt-BR.md).
- A ferramenta de sistema `godot-skill-create` escreve tal arquivo para você; ele se torna chamável após a próxima build.

<a id="c-assembly-reloads"></a>
## Reloads de assembly C#

O Godot recarrega o assembly C# do projeto cerca de meio segundo depois que a DLL compilada muda (`script-build`, o botão Build do editor, ou `dotnet build` em um terminal). O plugin faz parte desse assembly, então ele é descarregado, recompilado e reconecta em poucos segundos. Uma chamada de ferramenta ainda em execução bloquearia o descarregamento, então o plugin controla as chamadas em torno de um reload:

- assim que a nova DLL está no disco, novas chamadas são recusadas com um erro retentável ("Godot is about to reload the project's C# assembly … Retry in a few seconds"); chamadas já em execução terminam primeiro (o Godot espera até 10 s);
- quando o descarregamento começa, o plugin para de aceitar chamadas e descarta trabalho de thread principal ainda enfileirado para uma chamada; o servidor reporta tal chamada como perdida junto com a conexão, então repita-a também;
- após o reload, o plugin reconecta sozinho, e o `script-build` entrega seu resultado.

<a id="known-issue-crash-or-hang-after-a-reload-on-net-10"></a>
## Problema conhecido: crash ou travamento após um reload no .NET 10

No .NET 10 o editor pode crashar ou travar pouco depois de um reload de assembly C#. É um bug do runtime do .NET, [dotnet/runtime#135441](https://github.com/dotnet/runtime/issues/135441), corrigido na branch main do runtime ([#134983](https://github.com/dotnet/runtime/pull/134983)); a correção de manutenção para o .NET 10 é [#136481](https://github.com/dotnet/runtime/pull/136481).

Até que seu runtime tenha a correção, defina esta variável para o processo do editor:

```
DOTNET_ThreadPool_ThreadsToKeepAlive=-1
```

Editores iniciados pelo `godot-cli` (`open`, `open --headless`, `exec`) a recebem automaticamente. Se você mesmo inicia o Godot, defina-a globalmente no sistema (Windows: `setx DOTNET_ThreadPool_ThreadsToKeepAlive -1`; macOS / Linux: exporte-a no shell ou launcher que inicia o Godot) e reinicie o editor. Remova-a quando seu runtime .NET 10 incluir a correção.
