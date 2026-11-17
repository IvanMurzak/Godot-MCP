[English](custom-tools.md) · **中文** · [日本語](custom-tools.ja.md) · [Español](custom-tools.es.md) · [فارسی](custom-tools.fa.md) · [हिन्दी](custom-tools.hi.md) · [العربية](custom-tools.ar.md)

# 自定义工具

在项目的 C# 代码中添加你自己的 MCP 工具。一个工具系列是一个标有 `[AiToolType]` 的 `partial class`；每个工具是一个标有 `[AiTool("tool-name", …)]` 的方法，方法和每个参数上都要加 `[Description]`，让 LLM 知道何时以及如何调用它。

任何 Godot API 调用（`Node`、`Resource`、`EditorInterface` 等）**必须**在主线程上运行。请用 `MainThread.Instance.Run(...)` 包裹；插件以 Godot 的主线程调度器作为 ReflectorNet `MainThread` 的底层实现。

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
        // 不涉及 Godot API 的工作可以在这里、在后台线程上运行。

        return MainThread.Instance.Run(() =>
        {
            // 在这里、在主线程上访问 EditorInterface / Node / Resource。
            return "[Success] Operation completed.";
        });
    }
}
```

- 返回结构化模型（由 ReflectorNet 序列化）或 `void`，不要返回手工拼接的字符串。
- 用可空类型加默认值让参数变为可选：`string? optional = null`。
- `[AiToolType(Target = ToolTarget.Runtime)]` 使某个系列仅用于游戏，`ToolTarget.Editor` 则仅用于编辑器；默认值 `Both` 会在所有地方注册。游戏工具：[runtime.md](runtime.zh-CN.md)。
- `godot-skill-create` 系统工具会替你写出这样的文件；下次构建后即可调用。

<a id="c-assembly-reloads"></a>
## C# 程序集重载

构建出的 DLL 发生变化（`script-build`、编辑器的 Build 按钮，或在终端中运行 `dotnet build`）约半秒后，Godot 会重新加载项目的 C# 程序集。插件属于该程序集，因此会被卸载、重建，并在几秒内重新连接。此时仍在运行的工具调用会阻塞卸载，所以插件会在重载前后对调用进行管控：

- 新 DLL 写入磁盘后，新的调用会被拒绝并返回可重试的错误（"Godot is about to reload the project's C# assembly … Retry in a few seconds"）；已在运行的调用会先完成（Godot 最多等待 10 秒）；
- 卸载开始时，插件停止接收调用，并丢弃仍在排队的主线程任务；服务器会将这类调用报告为随连接丢失，同样请重试；
- 重载完成后插件会自动重新连接，`script-build` 返回其结果。

<a id="known-issue-crash-or-hang-after-a-reload-on-net-10"></a>
## 已知问题：.NET 10 上重载后崩溃或卡死

在 .NET 10 上，编辑器可能在 C# 程序集重载后不久崩溃或卡死。这是一个 .NET 运行时 bug，[dotnet/runtime#135441](https://github.com/dotnet/runtime/issues/135441)，已在运行时的主分支中修复（[#134983](https://github.com/dotnet/runtime/pull/134983)）；.NET 10 的服务更新修复是 [#136481](https://github.com/dotnet/runtime/pull/136481)。

在你的运行时包含该修复之前，请为编辑器进程设置此变量：

```
DOTNET_ThreadPool_ThreadsToKeepAlive=-1
```

由 `godot-cli` 启动的编辑器（`open`、`open --headless`、`exec`）会自动获得该变量。如果你自己启动 Godot，请在系统范围内设置它（Windows：`setx DOTNET_ThreadPool_ThreadsToKeepAlive -1`；macOS / Linux：在启动 Godot 的 shell 或启动器中 export 它）并重启编辑器。一旦你的 .NET 10 运行时包含该修复，就移除它。
