[English](connection.md) · [中文](connection.zh-CN.md) · [日本語](connection.ja.md) · [Español](connection.es.md) · **Português** · [فارسی](connection.fa.md) · [हिन्दी](connection.hi.md) · [العربية](connection.ar.md)

# Conexão e servidor

O plugin do editor conecta-se a um servidor MCP em um de dois modos. Defina o modo no dock do editor, ou sobrescreva-o no início do processo com variáveis de ambiente `GODOT_MCP_*` (útil para CI, execuções headless e desenvolvimento local). O ambiente sempre vence sobre a config salva. `godot-cli open` as passa para você através de `--mode`, `--url`, `--cloud-url` e `--token`.

<a id="cloud-mode-default-ai-gamedev"></a>
## Modo Cloud (padrão): ai-game.dev

O plugin conecta-se a `https://ai-game.dev`; você não executa servidor algum. A nuvem é o padrão porque não precisa de portas abertas e evita conflitos de porta com outros programas; é gratuita. O relay é sem estado: ele roteia chamadas de ferramenta sem inspecionar nem persistir seus conteúdos, e registra metadados de requisições e de eventos MCP ([Política de Privacidade](https://ai-game.dev/legal/privacy)).

Faça login uma vez com `godot-cli login`. Ele usa o fluxo de dispositivo do OAuth 2.1 (RFC 8628): imprime um código curto, abre seu navegador e, depois que você aprova, salva uma credencial no armazenamento da máquina (`~/.ai-game-dev/credentials.json`). O plugin do editor a adota automaticamente, então não há token para copiar.

| Variável | Propósito | Padrão |
| --- | --- | --- |
| `GODOT_MCP_CONNECTION_MODE` | `Cloud` ou `Custom` (sem distinção de maiúsculas). | `Cloud` |
| `GODOT_MCP_CLOUD_URL` | URL base da nuvem. Um `/mcp` final é removido. | `https://ai-game.dev` |
| `GODOT_MCP_CREDENTIALS_DIR` | Diretório absoluto do armazenamento de credenciais, para testes isolados. Também lido do `.env` do projeto. | `~/.ai-game-dev` |
| `GODOT_MCP_TOKEN` | Token bearer para o modo ativo; uma sobrescrita manual para CI e execuções headless. | nenhum |

<a id="custom-mode-your-own-server"></a>
## Modo Custom: seu próprio servidor

O plugin conecta-se a uma URL de servidor que você escolhe: um servidor local, ou um auto-hospedado. Use-o para trabalho offline ou isolado da rede.

| Variável | Propósito | Padrão |
| --- | --- | --- |
| `GODOT_MCP_CONNECTION_MODE` | Defina como `Custom`. | `Cloud` |
| `GODOT_MCP_HOST` | URL do servidor, uma URL http(s) absoluta. | `http://localhost:<port>`, uma porta derivada do caminho do projeto |
| `GODOT_MCP_TOKEN` | Token bearer enviado a esse servidor. Defini-lo seleciona a autorização `token`. | nenhum |
| `GODOT_MCP_AUTH_OPTION` | `none` / `oauth` / `token`; sobrescreve a regra acima. | escolha salva, senão `none` |

```bash
export GODOT_MCP_CONNECTION_MODE=Custom
export GODOT_MCP_HOST=http://localhost:<port>
# export GODOT_MCP_TOKEN=...   # apenas se o servidor exigir autenticação
```

`godot-cli open` as define para você. Para um servidor local que exige um token escolhido por você, abra o editor com ele e pressione **Start Server** no dock (`--auth Required` seleciona a autorização `token`):

```bash
godot-cli open ./MyGodotProject --mode Custom --auth Required --token <token>
```

- Um token definido no ambiente (ou no `.env` do projeto no modo Custom) também se aplica a um servidor local que o editor inicia e às configs de agente escritas com **Configure**. Ele não é salvo no arquivo de config.
- Com `oauth`, o token da sua conta ai-game.dev é enviado **somente a um servidor em localhost** (`localhost`, `127.0.0.0/8`, `::1`). Para qualquer outro host, defina `GODOT_MCP_TOKEN`.
- Um servidor em modo `token` rejeita uma conexão sem token; o plugin registra no log o que definir.

<a id="the-server"></a>
## O servidor

O servidor é o [GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server) compartilhado (`gamedev-mcp-server`), um único binário para Unity-MCP, Godot-MCP e Unreal-MCP, publicado a partir do seu próprio repositório. O addon fixa a versão que usa (`ServerVersion` em `addons/godot_mcp/Runtime/Connection/GodotMcpServerView.cs`).

<a id="let-the-addon-run-it"></a>
### Deixe o addon executá-lo

No modo Custom, abra o cartão **Server** do dock e pressione **Start Server**. O addon:

- baixa `gamedev-mcp-server-<rid>.zip` da versão fixada via HTTPS da release do GameDev-MCP-Server no GitHub (`win-x64`, `win-x86`, `win-arm64`, `linux-x64`, `linux-arm64`, `osx-x64`, `osx-arm64`);
- o armazena em cache em `.godot/mcp-server/<rid>/` e baixa de novo somente quando a versão fixada muda;
- o inicia na porta da sua **Server URL** (por padrão uma porta derivada do caminho do projeto) e conecta-se a ele.

O servidor escuta somente no loopback e responde `403` a uma requisição com um header `Origin` não permitido (defesa contra DNS rebinding). Ele usa a autorização escolhida no dock: com `none` qualquer processo na sua máquina pode chamá-lo, com `token` ele exige o token bearer. Regras de bind, autorização e Origin: [README do GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server#readme).

**Stop Server** o para; fechar o editor também o para. A URL de download vem apenas da versão fixada e da sua plataforma. Offline, o addon registra um aviso e o servidor não inicia. Em CI (`CI` / `GITHUB_ACTIONS`) o download é pulado.

Chamadas de ferramenta podem rodar por até 5 minutos. O `script-execute` interrompe um snippet por conta própria quando seu `timeout` expira (30 s por padrão).

Para desenvolvimento, `GODOT_MCP_SERVER_PATH=<caminho absoluto>` faz o editor iniciar esse binário `gamedev-mcp-server` existente em vez da release fixada (sem download, sem verificação de versão, sem limpeza de órfãos). Ela é lida uma vez por carga do assembly do plugin, do ambiente do processo ou do `.env` do projeto; um caminho que não nomeia arquivo algum é ignorado com um aviso. No Linux e no macOS o arquivo já deve ser executável.

<a id="run-it-yourself"></a>
### Execute-o você mesmo

Baixe uma [release do GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server/releases) ou use a imagem Docker [`aigamedeveloper/mcp-server`](https://hub.docker.com/r/aigamedeveloper/mcp-server):

```bash
./gamedev-mcp-server --client-transport streamableHttp --port 8080   # HTTP
./gamedev-mcp-server --client-transport stdio                        # um cliente MCP o inicia diretamente
```

Depois aponte o plugin para ele no modo Custom (`GODOT_MCP_HOST=http://localhost:8080`). Todos os argumentos e variáveis de ambiente estão no [README do GameDev-MCP-Server](https://github.com/IvanMurzak/GameDev-MCP-Server#readme).

<a id="editor-side-security-notes"></a>
## Notas de segurança do lado do editor

- **O token do editor é armazenado em texto puro.** O plugin salva sua config de conexão, incluindo `token` e `cloudToken`, como JSON puro em `user://godot-mcp-config.json`. Trate esse arquivo como um segredo: não o commite nem compartilhe. Para rotacionar, limpe o token no dock (ou apague o arquivo) e reconecte. Um token do ambiente do processo, ou do `.env` no modo Custom, não é escrito nesse arquivo.
- **A ponte dev-control fica desligada a menos que `GODOT_MCP_DEV_CONTROL=1`.** É uma ponte HTTP somente para desenvolvimento, para operar o dock em testes: somente no editor, vinculada a `127.0.0.1`, sem autenticação, e nunca iniciada sem essa variável.

Detalhes: [runtime-security.md](runtime-security.pt-BR.md#editor-side-surfaces-accepted-posture).
