[English](tools.md) · [中文](tools.zh-CN.md) · [日本語](tools.ja.md) · [Español](tools.es.md) · **Português** · [فارسی](tools.fa.md) · [हिन्दी](tools.hi.md) · [العربية](tools.ar.md)

# Referência de ferramentas

As ferramentas embutidas do Godot-MCP cobrem praticamente tudo o que você pode fazer no editor Godot, agrupadas nas famílias abaixo. Elas são compactas, então um agente precisa de menos chamadas e menos tokens ([medido na avaliação de agentes do RQL](https://github.com/IvanMurzak/RQL/blob/main/RESULTS.md)). A camada de dados do editor é o [RQL](rql.md) (Reflection Query Language): `rql-query`, `rql-mutate` e `rql-schema` leem e alteram nós de cena, propriedades Godot, recursos, arquivos, cenas e estado do editor em uma query, em vez de uma ferramenta por operação.

Com o `toolFormat` padrão (`Rql`), as demais ferramentas aceitam valores RQL como argumentos e devolvem texto compacto; `Json` mantém os esquemas e resultados JSON do ReflectorNet. Capturas de tela devolvem uma imagem PNG. Toda ferramenta de editor fica disponível assim que o addon é habilitado.

As famílias **console**, **reflection** e **runtime-errors** servem a um *jogo em execução* (veja [Uso em runtime](runtime.pt-BR.md)). No editor, use a fonte RQL `console` e o `call` / `CallMethod` do RQL.

| Família | Ferramentas | O que faz |
| --- | --- | --- |
| **rql** | `rql-query`, `rql-mutate`, `rql-schema` | Consulta e altera o editor ao vivo. Tudo ou nada e desfazível; várias instruções por chamada, separadas por `;`. Veja [rql.md](rql.md). |
| **script** | `script-read`, `script-create`, `script-update`, `script-delete`, `script-attach-to-node`, `script-validate`, `script-build`, `script-execute` | Arquivos C# (`.cs`) e GDScript (`.gd`), anexar um script a um nó, validação de GDScript, compilar o C# do projeto e executar C# no editor. |
| **screenshot** | `screenshot-viewport`, `screenshot-camera`, `screenshot-isolated` | Captura o viewport do editor, uma câmera, ou um nó renderizado isolado, como um PNG que o LLM pode ver. |
| **console** | `console-get-logs`, `console-clear-logs` | Em um jogo em execução: ler e limpar os logs coletados. |
| **reflection** | `reflection-method-find`, `reflection-method-call` | Em um jogo em execução: encontrar e chamar métodos C# em qualquer assembly carregado via ReflectorNet. |
| **runtime-errors** | `runtime-errors-get`, `runtime-errors-clear` | Em um jogo em execução: consultar erros de runtime de GDScript, `push_error` / `push_warning`, erros de shader e exceções C#. **Desligado por padrão.** |
| **ping** | `ping` | Sonda de prontidão. Ferramenta de sistema. |
| **session** | `session-close` | Salvar e sair de um editor que o `godot-cli` iniciou. Ferramenta de sistema. |
| **skills** | `godot-skill-create`, `godot-skill-generate` | Escrever um novo arquivo de ferramenta C# no projeto; regenerar os arquivos `SKILL.md`. Ferramentas de sistema. |

**Ferramentas de sistema** são servidas no endpoint HTTP `/api/system-tools/` do servidor MCP (por exemplo `godot-cli run-system-tool ping`). Elas não são listadas aos agentes de IA em `tools/list`.

<a id="rql"></a>
## rql

- `rql-query`: uma consulta somente leitura, devolvida como linhas compactas. `nodes | where class == "OmniLight3D" | select name props["light_energy"]`
- `rql-mutate`: uma query que termina em `set` ou `call`. Tudo ou nada, desfazível (Ctrl+Z), e `dry_run` a pré-visualiza. Edições de cena marcam a cena como não salva; arquivos de recursos editados e `project.godot` são salvos.
  - `nodes | where name starts-with "Enemy" | set Visible = false`
  - `root | call AddChild("Node3D", "Props")`
  - `nodes | where name == "Button" | call Connect("pressed", godot.find("UI"), "_on_pressed")`
  - `project | call AddInputAction("jump", ["Key:Space"])`
  - `editor | call SaveScene()`
- `rql-schema`: as fontes, funções e classes de nó da cena em edição; com um nome de tipo, seus membros, métodos e propriedades Godot. `rql-schema "<extension>"` imprime o guia de uma extensão instalada.

As fontes incluem `nodes`, `root`, `selection`, `scenes`, `files`, `folders`, `classes`, `console`, `editor`, `project`, `settings`, `inputActions`, `autoloads`, `groups`, `layers` e `tools`. A linguagem completa, incluindo TileSets, Themes, parâmetros de ShaderMaterial e MeshLibraries, está em [rql.md](rql.md) e na skill `godot-rql`.

<a id="script"></a>
## script

- `script-read`, `script-create`, `script-update`, `script-delete`: CRUD em arquivos `.cs` e `.gd`.
- `script-attach-to-node`: anexar um script a um nó.
- `script-validate`: analisar e compilar arquivos GDScript e devolver diagnósticos estruturados.
- `script-build`: compilar o C# do projeto (`dotnet build`, Debug) para que novos scripts carreguem. A build roda em segundo plano; quando ela tem sucesso o Godot recarrega o assembly C#, o plugin reconecta, e o resultado (avisos ou erros) chega após o reload. Veja [Reloads de assembly C#](custom-tools.pt-BR.md#c-assembly-reloads).
- `script-execute`: compilar e executar C# no editor com Roslyn, via [Code-Exec](https://github.com/IvanMurzak/Code-Exec). Instruções simples com a última expressão como resultado (`using`s próprios, classes locais, `await`), ou uma classe com um método estático. Os tipos compilados do jogo ficam visíveis; `include` adiciona arquivos `.cs` auxiliares de `<project>/.ai-game-dev/snippets`. Devolve o valor e o que o código imprimiu (`Console`; `GD.Print`, push_warning e push_error quando o projeto compila com Godot.NET.Sdk 4.5+). De um terminal: `godot-cli exec '1 + 2'`.

<a id="screenshot"></a>
## screenshot

- `screenshot-viewport`: o viewport do editor.
- `screenshot-camera`: a visão de uma câmera específica.
- `screenshot-isolated`: um nó renderizado isolado, de um ângulo escolhido.

As três devolvem a imagem na conversa. Passe `savePath` para também escrever um arquivo: `res://Screenshots/shot.png`, `Screenshots/shot.png` (relativo à raiz do projeto) ou um caminho absoluto dentro do projeto. Pastas inexistentes são criadas e `.png` é adicionado quando não há extensão. Um `savePath` é recusado (sem imagem, sem arquivo) quando está fora do projeto (`..`, `user://`), passa por um symlink ou junction, aponta para dentro de `res://.godot/` ou deste addon, não é um `.png`, ou nomeia um arquivo existente: ele nunca sobrescreve. O Godot importa o PNG como qualquer arquivo do projeto; uma pasta com um arquivo `.gdignore` evita isso.

<a id="console-reflection-runtime-errors-running-game"></a>
## console, reflection, runtime-errors (jogo em execução)

- `console-get-logs` / `console-clear-logs`: ler (com filtragem) e limpar os logs que o plugin coletou, incluindo seus próprios diagnósticos de conexão.
- `reflection-method-find` / `reflection-method-call`: encontrar métodos C# (inclusive privados) em todos os assemblies carregados, e chamar um deles com argumentos.
- `runtime-errors-get`: erros capturados dentro do jogo, mais antigos primeiro, mais novos mantidos. Passe o `highestSequence` anterior como `sinceSequence` para receber apenas os erros novos. Devolve `available:false` quando a captura nunca foi habilitada.
- `runtime-errors-clear`: limpar o buffer; o contador de sequência continua contando.

A captura de erros de runtime fica desligada até um jogo chamar `builder.WithRuntimeErrorCapture()`. Detalhes: [Capturando erros de runtime dentro do jogo](runtime.pt-BR.md#capturing-in-game-runtime-errors).

<a id="ping-session-skills-system-tools"></a>
## ping, session, skills (ferramentas de sistema)

- `ping`: ecoa uma mensagem de volta, ou devolve `pong`. `curl -X POST <host>/api/system-tools/ping -d '{}'`.
- `session-close`: salvar e sair de um editor iniciado por `godot-cli exec` / `open --headless` (cenas abertas com arquivo e os recursos que elas alteraram). Ele precisa do nonce dessa sessão em `.ai-game-dev/session.json`, então nunca fecha o seu próprio editor. `godot-cli close --owned-only` o chama.
- `godot-skill-create`: escrever um novo arquivo de ferramenta C# no projeto. Ele se torna chamável após o projeto ser recompilado.
- `godot-skill-generate`: regenerar todos os `SKILL.md` a partir das ferramentas que o editor registrou, na pasta de skills do agente de IA selecionado.

<a id="visibility"></a>
## Visibilidade

Cada ferramenta, prompt e recurso tem um interruptor **Visible** nas janelas Tools / Prompts / Resources do dock (`godot-cli configure` faz o mesmo de um terminal). Ele só decide se os clientes MCP veem o item em `tools/list`, o que mantém o contexto deles pequeno. Não é controle de acesso: uma ferramenta oculta ainda executa quando um cliente a chama pelo nome.

<a id="extensions"></a>
## Extensões

Extensões adicionam fontes RQL, adaptadores e um guia para o agente às ferramentas `rql-*` e à skill `godot-rql`. Elas não adicionam ferramentas MCP próprias. A lista está no [README](README.pt-BR.md#extensions).

```bash
godot-cli install-extension com.IvanMurzak.Godot.MCP.CSG ./MyGodotProject
godot-cli build ./MyGodotProject
```

O instalador adiciona apenas o pacote NuGet da extensão; instale você mesmo no projeto o addon de terceiros exigido. A seção **Extensions** do dock tem **Install All**. `godot-cli update` atualiza o addon e re-resolve extensões flutuantes (`Version="*"`) para suas versões mais recentes; uma extensão que você fixou em uma versão é mantida. Se uma extensão precisa de um McpPlugin mais novo que o fixado pelo addon, o restore falha com `NU1605` até você atualizar o addon ou fixar a extensão.
