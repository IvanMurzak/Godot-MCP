[English](tools.md) · **中文** · [日本語](tools.ja.md) · [Español](tools.es.md) · [فارسی](tools.fa.md) · [हिन्दी](tools.hi.md) · [العربية](tools.ar.md)

# 工具参考

Godot-MCP 内置 **24 个工具**，分为 **9 个系列**。编辑器的数据层是 [RQL](rql.md)（Reflection Query Language）：`rql-query`、`rql-mutate` 和 `rql-schema` 用一条查询读取和修改场景节点、Godot 属性、资源、文件、场景和编辑器状态，而不是每种操作一个工具。

在默认的 `toolFormat`（`Rql`）下，其他工具以 RQL 值作为参数并返回紧凑的文本；`Json` 则保留 ReflectorNet 的 JSON schema 和结果。截图返回 PNG 图像。插件启用后，所有编辑器工具即可使用。

**console**、**reflection** 和 **runtime-errors** 系列服务于*运行中的游戏*（见[运行时用法](runtime.zh-CN.md)）。在编辑器中，请改用 RQL 的 `console` 数据源以及 RQL 的 `call` / `CallMethod`。

| 系列 | 工具 | 作用 |
| --- | --- | --- |
| **rql** | `rql-query`, `rql-mutate`, `rql-schema` | 查询和修改实时编辑器。要么全部生效、要么全不生效，且可撤销。见 [rql.md](rql.md)。 |
| **script** | `script-read`, `script-create`, `script-update`, `script-delete`, `script-attach-to-node`, `script-validate`, `script-build`, `script-execute` | C#（`.cs`）和 GDScript（`.gd`）文件、将脚本挂载到节点、GDScript 校验、构建项目的 C#，以及在编辑器中运行 C#。 |
| **screenshot** | `screenshot-viewport`, `screenshot-camera`, `screenshot-isolated` | 截取编辑器视口、某个相机或单独渲染的单个节点，生成 LLM 可以查看的 PNG。 |
| **console** | `console-get-logs`, `console-clear-logs` | 在运行中的游戏内：读取和清除收集到的日志。 |
| **reflection** | `reflection-method-find`, `reflection-method-call` | 在运行中的游戏内：通过 ReflectorNet 在任意已加载程序集中查找并调用 C# 方法。 |
| **runtime-errors** | `runtime-errors-get`, `runtime-errors-clear` | 在运行中的游戏内：轮询 GDScript 运行时错误、`push_error` / `push_warning`、着色器错误和 C# 异常。**默认关闭。** |
| **ping** | `ping` | 就绪探测。系统工具。 |
| **session** | `session-close` | 保存并退出由 `godot-cli` 启动的编辑器。系统工具。 |
| **skills** | `godot-skill-create`, `godot-skill-generate` | 向项目写入新的 C# 工具文件；重新生成 `SKILL.md` 文件。系统工具。 |

**系统工具**通过 MCP 服务器的 `/api/system-tools/` HTTP 端点提供（例如 `godot-cli run-system-tool ping`）。它们不会在 `tools/list` 中列给 AI 智能体。

<a id="rql"></a>
## rql

- `rql-query`：只读查询，以紧凑的行返回。`nodes | where class == "OmniLight3D" | select name props["light_energy"]`
- `rql-mutate`：以 `set` 或 `call` 结尾的查询。要么全部生效、要么全不生效，可撤销（Ctrl+Z），`dry_run` 可预览。场景编辑会将场景标记为未保存；被编辑的资源文件和 `project.godot` 会被保存。
  - `nodes | where name starts-with "Enemy" | set Visible = false`
  - `root | call AddChild("Node3D", "Props")`
  - `nodes | where name == "Button" | call Connect("pressed", godot.find("UI"), "_on_pressed")`
  - `project | call AddInputAction("jump", ["Key:Space"])`
  - `editor | call SaveScene()`
- `rql-schema`：当前编辑场景中的数据源、函数和节点类；给出类型名时，返回其成员、方法和 Godot 属性。`rql-schema "<extension>"` 输出已安装扩展的指南。

数据源包括 `nodes`、`root`、`selection`、`scenes`、`files`、`folders`、`classes`、`console`、`editor`、`project`、`settings`、`inputActions`、`autoloads`、`groups`、`layers` 和 `tools`。完整语言（包括 TileSet、Theme、ShaderMaterial 参数和 MeshLibrary）见 [rql.md](rql.md) 和 `godot-rql` skill。

<a id="script"></a>
## script

- `script-read`、`script-create`、`script-update`、`script-delete`：对 `.cs` 和 `.gd` 文件进行增删改查。
- `script-attach-to-node`：将脚本挂载到节点。
- `script-validate`：解析并编译 GDScript 文件，返回结构化诊断信息。
- `script-build`：构建项目的 C#（`dotnet build`，Debug），以便加载新脚本。构建在后台运行；成功后 Godot 重新加载 C# 程序集，插件重新连接，结果（警告或错误）在重载后返回。见 [C# 程序集重载](custom-tools.zh-CN.md#c-assembly-reloads)。
- `script-execute`：通过 [Code-Exec](https://github.com/IvanMurzak/Code-Exec) 用 Roslyn 在编辑器中编译并运行 C#。可以是普通语句，以最后一个表达式作为结果（可有自己的 `using`、局部类、`await`），也可以是带静态方法的类。游戏编译后的类型均可见；`include` 会添加来自 `<project>/.ai-game-dev/snippets` 的辅助 `.cs` 文件。返回值以及代码打印的内容（`Console`；当项目使用 Godot.NET.Sdk 4.5+ 构建时还包括 `GD.Print`、push_warning 和 push_error）。在终端中：`godot-cli exec '1 + 2'`。

<a id="screenshot"></a>
## screenshot

- `screenshot-viewport`：编辑器视口。
- `screenshot-camera`：指定相机的视图。
- `screenshot-isolated`：从选定角度单独渲染的单个节点。

三者都会在对话中返回图像。传入 `savePath` 还会写入文件：`res://Screenshots/shot.png`、`Screenshots/shot.png`（相对于项目根目录）或项目内的绝对路径。缺失的文件夹会被创建，没有扩展名时会添加 `.png`。以下情况会拒绝 `savePath`（不返回图像，也不写文件）：位于项目之外（`..`、`user://`）、经过符号链接或目录联接、指向 `res://.godot/` 或本插件内部、不是 `.png`，或指向已存在的文件：它从不覆盖。Godot 会像对待任何项目文件一样导入该 PNG；放在含 `.gdignore` 文件的文件夹中可避免导入。

<a id="console-reflection-runtime-errors-running-game"></a>
## console、reflection、runtime-errors（运行中的游戏）

- `console-get-logs` / `console-clear-logs`：读取（可过滤）和清除插件收集的日志，包括插件自身的连接诊断信息。
- `reflection-method-find` / `reflection-method-call`：在所有已加载程序集中查找 C# 方法（包括私有方法），并带参数调用其中一个。
- `runtime-errors-get`：捕获到的游戏内错误，按从旧到新排列，保留最新的。将上一次的 `highestSequence` 作为 `sinceSequence` 传入，即可只获取新错误。如果从未启用捕获，则返回 `available:false`。
- `runtime-errors-clear`：清空缓冲区；序号计数器继续递增。

在游戏调用 `builder.WithRuntimeErrorCapture()` 之前，运行时错误捕获处于关闭状态。详情：[捕获游戏内运行时错误](runtime.zh-CN.md#capturing-in-game-runtime-errors)。

<a id="ping-session-skills-system-tools"></a>
## ping、session、skills（系统工具）

- `ping`：回显一条消息，或返回 `pong`。`curl -X POST <host>/api/system-tools/ping -d '{}'`。
- `session-close`：保存并退出由 `godot-cli exec` / `open --headless` 启动的编辑器（保存有文件的已打开场景及其修改过的资源）。它需要 `.ai-game-dev/session.json` 中该会话的 nonce，因此绝不会关闭你自己的编辑器。`godot-cli close --owned-only` 会调用它。
- `godot-skill-create`：向项目写入一个新的 C# 工具文件。项目重新构建后即可调用。
- `godot-skill-generate`：根据编辑器已注册的工具重新生成所有 `SKILL.md`，写入所选 AI 智能体的 skills 文件夹。

<a id="visibility"></a>
## 可见性

每个工具、提示词和资源在停靠面板的 Tools / Prompts / Resources 窗口中都有一个 **Visible** 开关（`godot-cli configure` 可在终端中完成同样操作）。它只决定 MCP 客户端能否在 `tools/list` 中看到该项，从而让客户端的上下文保持精简。它不是访问控制：被隐藏的工具在客户端按名称调用时仍会运行。

<a id="extensions"></a>
## 扩展

扩展为 `rql-*` 工具和 `godot-rql` skill 添加 RQL 数据源、适配器和智能体指南。它们本身不添加 MCP 工具。列表见 [README](README.zh-CN.md#extensions)。

```bash
godot-cli install-extension com.IvanMurzak.Godot.MCP.CSG ./MyGodotProject
godot-cli build ./MyGodotProject
```

安装器只添加扩展的 NuGet 包；所需的第三方插件需由你自行安装到项目中。停靠面板的 **Extensions** 区域有 **Install All**。`godot-cli update` 会更新插件，并将浮动版本的扩展（`Version="*"`）重新解析为最新版本；你固定了版本的扩展保持不变。如果某个扩展需要比插件固定版本更新的 McpPlugin，还原会以 `NU1605` 失败，直到你更新插件或固定该扩展的版本。
