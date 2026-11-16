[English](runtime-security.md) · [中文](runtime-security.zh-CN.md) · [日本語](runtime-security.ja.md) · [Español](runtime-security.es.md) · **Português** · [فارسی](runtime-security.fa.md) · [हिन्दी](runtime-security.hi.md) · [العربية](runtime-security.ar.md)

# Contrato de segurança do runtime (dentro do jogo)

O Godot-MCP pode rodar **dentro de uma build de jogo em execução / exportada** (debug ou release), não apenas no editor —
veja [Uso em runtime (dentro do jogo)](runtime.pt-BR.md) para o guia completo. Executar uma conexão MCP
dentro de um jogo publicado abre uma **superfície de controle remoto**: tudo o que suas ferramentas
`[AiToolType]` registradas podem fazer, um cliente MCP conectado pode conduzir. Este documento é o contrato de segurança
desse modo. Leia-o antes de publicar uma build que chama `GodotMcpRuntime.Initialize(...)`.

<a id="guarantees-the-runtime-gives-you"></a>
## Garantias que o runtime oferece

- **Somente opt-in — nunca conecta automaticamente.** O plugin do editor conecta na inicialização; uma build de jogo **não**.
  `GodotMcpRuntime.Initialize(...).Build()` devolve um `GodotMcpRuntimeHandle` **desligado por padrão**. Nada
  conecta até que o *seu* código chame explicitamente `handle.Connect()`. Não há caminho de código de autoconexão em
  uma build que não é de editor.
- **Zero ferramentas por padrão — estritamente manual.** Um runtime recém-construído registra **nenhuma** ferramenta MCP. A
  superfície de ataque é exatamente o conjunto de classes `[AiToolType]` que você opta por incluir via
  `builder.WithToolsFromAssembly(...)` / `builder.WithTools(...)` — nada mais. Sem nenhuma registrada, a
  conexão ainda é construída e conecta com um conjunto de ferramentas vazio (exatamente o modelo do Unity-MCP).
- **As famílias de ferramentas do editor não podem vazar para dentro.** As famílias de ferramentas de editor do addon (`Tool_Rql`, `Tool_Script`,
  `Tool_Screenshot`, `Tool_Session`) são protegidas por `#if TOOLS` e **não compilam** em uma build de jogo exportada. Mesmo um
  `WithToolsFromAssembly(...)` sobre o próprio assembly do addon não pode registrá-las.
- **Nenhum carregamento automático de config persistida.** Uma build de jogo nunca lê silenciosamente um arquivo de config salvo (`user://`) —
  isso é uma conveniência exclusiva do editor. Host e token vêm apenas do seu código (`WithConfig`) ou do
  ambiente de processo `GODOT_MCP_*` / de um `.env` do projeto, que o `GodotMcpConfig` lê ao vivo.

<a id="what-you-must-do-when-enabling-it"></a>
## O que você deve fazer ao habilitá-lo

- **Conecte deliberadamente.** Só chame `handle.Connect()` quando você realmente pretende expor ferramentas (uma build
  de desenvolvimento, um harness de QA, um recurso de agente dentro do jogo explicitamente projetado). Chame `handle.Disconnect()` /
  `handle.Dispose()` para parar de expô-las.
- **Registre o conjunto mínimo de ferramentas.** Prefira `WithTools(typeof(MyToolFamily))` a um amplo
  `WithToolsFromAssembly(...)` quando você só precisa de algumas ferramentas. Toda ferramenta registrada é alcançável por um
  cliente conectado.
- **Prefira loopback + um token.** Para ferramentas locais, vincule o modo Custom a um host de loopback
  (`http://localhost:…` / `127.0.0.1`) e defina `AuthOption = Consts.MCP.Server.AuthOption.token` com um
  `Token` de verdade:

  ```csharp
  // using McpServerConsts = com.IvanMurzak.McpPlugin.Common.Consts.MCP.Server;
  builder.WithConfig(config =>
  {
      config.ConnectionMode = GodotMcpConnectionMode.Custom;
      config.Host           = "http://localhost:8080";   // loopback
      config.AuthOption     = McpServerConsts.AuthOption.token;
      config.Token          = "your-secret-token";  // discouraged: see the env-var form below — don't ship a hard-coded secret
  });
  ```

  Prefira a forma fora de banda abaixo, para que a build não carregue segredo embutido:

  ```bash
  export GODOT_MCP_CONNECTION_MODE=Custom
  export GODOT_MCP_HOST=http://localhost:8080
  export GODOT_MCP_AUTH_OPTION=token
  export GODOT_MCP_TOKEN=your-secret-token
  ```

- **Não exponha uma superfície MCP sem autenticação em uma interface pública em uma build de release**, a menos que você
  tenha explicitamente projetado e protegido essa superfície. Trate qualquer host que não seja loopback como uma decisão deliberada
  e revisada.
- **Mude o host ou o modo apenas enquanto desconectado.** A menos que você atribua seu próprio `CredentialProvider`, o
  bearer de cada conexão está vinculado ao modo e ao host que ela disca: depois de uma mudança, uma reconexão do link existente
  é enviada sem token, então chame `handle.Disconnect()` e `handle.Connect()` para trocar de servidor.
- **Não embuta segredos de longa duração no binário publicado.** Prefira `GODOT_MCP_TOKEN` do
  ambiente a um `config.Token` fixo no código, para que o token não seja extraível da build.
- **Habilite a captura de erros de runtime apenas em uma conexão confiável.** `builder.WithRuntimeErrorCapture()` fica
  **DESLIGADO por padrão**. Quando habilitado, os erros capturados encaminham a mensagem **completa** e (para falhas C#) o
  **stack trace gerenciado completo** ao agente conectado via a ferramenta `runtime-errors-get`. Essas strings podem
  embutir dados sensíveis de runtime — caminhos absolutos do sistema de arquivos, nomes de máquina/usuário, query strings, ou um
  segredo/token que apareceu em uma mensagem ou argumento de exceção. Esse é o valor diagnóstico pretendido,
  mas ele amplia os dados expostos pela conexão. Habilite-o apenas em um host de loopback com
  `AuthOption = Consts.MCP.Server.AuthOption.token` e um token de verdade — nunca em uma interface pública
  sem autenticação em uma build de release.

<a id="in-game-code-execution-withscriptexecute"></a>
## Execução de código dentro do jogo (`WithScriptExecute`)

`builder.WithScriptExecute()` registra `script-execute` no jogo: um cliente conectado envia C#, o jogo
o compila com Roslyn e o executa na sua thread principal. Isso é **execução arbitrária de código no processo do
jogo**, com o sistema de arquivos e o acesso à rede do jogador. Ele fica DESLIGADO por padrão e precisa de um opt-in
de build: um jogo exportado só tem Code-Exec e Roslyn quando seu `.csproj` define `GodotMcpCodeExecInGame=true`
e define `GODOT_MCP_CODE_EXEC_IN_GAME` ([Executando C# no jogo](runtime.pt-BR.md#running-c-in-the-game)). Sem o opt-in, a chamada registra
um aviso e não registra nada.

> **Jogos iniciados pelo editor.** A build deles é a build do editor (`TOOLS`), então as famílias de ferramentas de editor
> do addon também compilam no assembly do jogo. Um `WithToolsFromAssembly(...)` sobre esse assembly então
> as registra, incluindo o `script-execute` do editor, com ou sem `WithScriptExecute()`. Em uma build de
> exportação elas não existem. Registre tipos específicos (`WithTools(...)`) se um jogo executado pelo editor não deve
> oferecê-las.

- **Somente builds de debug / desenvolvimento.** Proteja a chamada (e o opt-in do csproj) atrás do seu próprio interruptor de desenvolvimento. Nunca a publique
  habilitada em uma build de release pública.
- **Loopback + token, sempre.** Use um host de loopback e `AuthOption = token` com um token de verdade do
  ambiente. Qualquer um que possa chamar ferramentas na conexão é dono do processo.
- **Somente CoreCLR de desktop.** NativeAOT e iOS não podem executar código emitido em tempo de execução; mobile não foi testado.
- **Tamanho.** O opt-in adiciona o Roslyn (`Microsoft.CodeAnalysis*.dll`, vários MB) à exportação. O RQL nunca é
  incluído.

<a id="environment-variables"></a>
## Variáveis de ambiente

A config de runtime (`GodotMcpConfig`) lê estas variáveis `GODOT_MCP_*` ao vivo (ambiente de processo ou `.env` do
projeto). Elas sobrescrevem valores definidos em código no momento da resolução.

| Variável | Valores | Descrição |
| -------- | ------ | ----------- |
| `GODOT_MCP_CONNECTION_MODE` | `Cloud` / `Custom` | Modo de conexão (um host de loopback implica `Custom`). |
| `GODOT_MCP_CLOUD_URL` | URL | Sobrescrever a URL base da nuvem (padrão `https://ai-game.dev`). |
| `GODOT_MCP_HOST` | URL | Host do servidor no modo Custom (padrão `http://localhost:8080`). |
| `GODOT_MCP_AUTH_OPTION` | `none` / `oauth` / `token` | Autenticação do modo Custom: anônima / conta (oauth) / bearer offline (`token`). Sem ela, um `GODOT_MCP_TOKEN` seleciona `token`. |
| `GODOT_MCP_TOKEN` | string | O token bearer (roteado para Cloud ou Custom pelo modo ativo). |
| `GODOT_MCP_LOG_LEVEL` | `Trace` / `Debug` / `Info` / `Warning` / `Error` / `None` | Limiar de verbosidade de log. |
| `GODOT_MCP_EDITOR_PID` | id do processo | Definido pelo plugin do editor no processo do editor, de modo que todo processo que o editor inicia o herda. Enquanto esse editor roda, o `Connect()` do runtime permanece offline em um processo que o carrega (a menos que haja o opt-in abaixo). |
| `GODOT_MCP_RUNTIME_FROM_EDITOR` | `1` / `true` | Somente ambiente de processo. Permite ao runtime conectar em um jogo iniciado pelo editor Godot (padrão: ele permanece offline nesse caso, porque o jogo herda as configurações `GODOT_MCP_*` do editor); o mesmo que `builder.ConnectWhenRunFromEditor()`. |

<a id="editor-side-surfaces-accepted-posture"></a>
## Superfícies do lado do editor (postura aceita)

O contrato acima é para uma **build de jogo**. Duas superfícies do lado do editor são documentadas aqui por completude;
ambas são **intencionais** hoje (nenhuma criptografia / nenhum segredo compartilhado está implementado ainda — essas são
decisões adiadas, restringidas por paridade, a coordenar entre os irmãos Unity / Godot / Unreal).

<a id="editor-token-storage-is-plaintext-at-rest"></a>
### O armazenamento do token do editor é texto puro em repouso

Quando você conecta o **plugin do editor** (device-auth da nuvem, ou um token do modo Custom), ele persiste sua
config de conexão em **`user://godot-mcp-config.json`** (o Godot resolve `user://` para o diretório de dados do usuário
de cada plataforma via `ProjectSettings.GlobalizePath`). O token bearer (`token`) e o token da nuvem
(`cloudToken`) são escritos como **JSON em texto puro** — eles **não** são criptografados e **não** ficam em um
keystore do sistema operacional.

- **Premissa de confiança: a conta de usuário local.** Qualquer um que possa ler seu diretório de dados de usuário pode ler o
  token. Trate `user://godot-mcp-config.json` como um segredo — não o commite, não o sincronize para um drive compartilhado e não o
  cole em lugar algum.
- **Rotação.** Limpe o token salvo no dock (ou apague o arquivo) e reconecte.
- **Precedência.** Um valor de `GODOT_MCP_TOKEN` do ambiente de processo, ou um valor do `.env` no modo Custom, sobrepõe o token
  persistido no momento da resolução e **não** é escrito de volta nesse arquivo — assim um CI / máquina compartilhada pode rodar
  sem jamais persistir um token em disco. Um token de `.env` lido no modo Cloud é escrito de volta em um salvamento do dock.
- **Um `GODOT_MCP_TOKEN` definido é enviado ao host Custom.** Sem um `GODOT_MCP_AUTH_OPTION`, ele sobrescreve um
  `AuthOption = none` configurado, inclusive um definido em código. Defina `GODOT_MCP_AUTH_OPTION=none` para manter uma conexão anônima.

<a id="the-dev-control-bridge-is-unauthenticated-but-gated-off"></a>
### A ponte dev-control é sem autenticação, mas desligada por um portão

Uma ponte HTTP de injeção/controle somente para desenvolvimento (`DevControlServer`) existe para operar o dock do editor a partir de
testes / um terminal. Ela é **sem autenticação**, mas sua fronteira de segurança é tripla:

1. **Somente editor.** Ela é compilada atrás de `#if TOOLS`, então não existe em uma build de jogo exportada.
2. **Somente loopback.** Ela vincula-se exclusivamente a **`127.0.0.1`** — nunca a uma interface roteável.
3. **Desligada por variável de ambiente.** Ela é construída e iniciada **somente quando `GODOT_MCP_DEV_CONTROL=1`** (exatamente `1`).
   Um valor não definido / qualquer outro valor significa que a ponte nunca escuta — um addon publicado, e qualquer sessão normal
   do editor, nunca abre a superfície.

Esse portão de ambiente é **estrutural**: é a única coisa entre "andaime de desenvolvimento" e "uma
superfície de controle sem autenticação no dock ao vivo". Ele é garantido por um predicado puramente gerenciado
(`DevControlGate.IsEnabled`) com um teste unitário (`DevControlGateTests`) **e** uma asserção em tempo de boot no
ponto de construção (`DevControlGate.AssertEnabledOrThrow`), de modo que uma regressão que ligue a ponte sem
a variável de ambiente falha rápido no CI e em runtime, em vez de ser publicada habilitada silenciosamente.
