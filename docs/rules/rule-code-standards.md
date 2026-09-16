# rule-code-standards: C# 12 / Avalonia 11 现代编码与注释规范

## Metadata

- **Rule ID**: rule-code-standards
- **Category**: architecture | process
- **Severity**: BLOCK (Hard Error)
- **Status**: active
- **Created Date**: 2026-09-14
- **Related Incident / SPEC**: SPEC-001, adr-technology-stack

---

## 1. Why (The Expensive Lesson)

在 .NET 跨平台与 XAML 项目中，常见的三大恶性痛点是：
1. **垃圾代码膨胀**：过度手写 `INotifyPropertyChanged` 样板代码，导致几百行代码充斥毫无逻辑的 setter/getter。
2. **多视窗内存泄漏**：在多窗口（如主窗体与快捷弹窗）交互中，强引用事件监听或未解绑的委托直接导致 Window 句柄和 ViewModel 无法被 GC 回收。
3. **无用注释泛滥与关键意图缺失**：大量无意义注释（如 `// 获取名称`）掩盖了真实的业务边界约束，同时忽略了“为什么这样做”的架构意图。

因此，本项目严格执行**微软官方最新 C# 规范 + CommunityToolkit.Mvvm 最佳实践 + 清晰的意图导向注释规范**。

---

## 2. Mandates (Imperative Rules)

### 2.1 语言与语法特性
1. **必须全面启用可空引用类型**（`<Nullable>enable</Nullable>`），禁止在无保护情况下抑制空引用警告。
2. **ViewModel 必须使用 CommunityToolkit.Mvvm 源生成器**：
   - 必须使用 `[ObservableProperty]` 标注字段，禁止手写 `RaisePropertyChanged`；
   - 必须使用 `[RelayCommand]` 标注命令方法，禁止手写 `ICommand` 实现类。
3. **窗口间通信必须使用 `WeakReferenceMessenger`**：
   - 禁止让 ViewModel 持有其他 Window/ViewModel 的强引用。
4. **命名约定**：
   - 接口以 `I` 开头（如 `ITaskRepository`）；
   - 异步方法必须以 `Async` 结尾，并合理支持 `CancellationToken`；
   - XAML 命名空间与类名严格保持对齐。

### 2.2 注释与文档标准
1. **只在必要时注释（Explain WHY, not WHAT）**：
   - 杜绝描述表面语法的无意义废话（禁止：`// 将完成状态设置为 true`）；
   - 注释必须阐述：**为什么选择此方案、边界防护原因、平台兼容性注意事项**。
2. **公共 API 与领域实体必须具备标准 XML 文档注释**（`<summary>`, `<param>`, `<returns>`）。
3. **严禁在代码中与用户对话**，严禁签入包含 `TODO: AI generated` 这类临时污染标记。

---

## 3. Concrete Examples (真实案例)

### ❌ Anti-Pattern (手写冗余样板代码与强引用内存泄漏)

```csharp
// 违规案例：冗余样板代码、强引用监听导致内存泄漏、无意义注释
public class BadTodoViewModel : INotifyPropertyChanged
{
    private string _title;
    // 获取或设置标题 (无用注释)
    public string Title
    {
        get => _title;
        set
        {
            _title = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RegisterWindow(MainWindow window) 
    {
        // 强引用持有外部窗口，导致 window 永远无法被 GC 回收
        window.Closed += (s, e) => SaveData();
    }
}
```

### ✅ Approved Pattern (现代 Source Generator + WeakReference 解耦)

```csharp
/// <summary>
/// 待办项视图模型，支持响应式数据绑定与状态流转
/// </summary>
public partial class TodoItemViewModel : ObservableObject
{
    /// <summary>
    /// 任务标题（由 Source Generator 自动生成 Title 属性与变更广播）
    /// </summary>
    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private bool _isCompleted;

    /// <summary>
    /// 切换完成状态并发布解耦事件
    /// </summary>
    [RelayCommand]
    private void ToggleComplete()
    {
        IsCompleted = !IsCompleted;
        // 使用弱引用消息总线解耦，跨窗口安全通信，杜绝循环强引用
        WeakReferenceMessenger.Default.Send(new TaskStatusChangedMessage(this));
    }
}
```

---

## 4. Automated Enforcement (机器检查命令)

在 CI 或本地构建阶段，必须通过以下命令执行硬性编译检查（零警告或警告视同错误）：

```bash
# 执行强类型与分析器检查
dotnet build --configuration Release /p:TreatWarningsAsErrors=true
```

---

## 5. Exceptions & Escape Hatch

- 涉及 P/Invoke 底层系统 API 互操作（如全局快捷键注册）时，允许局部使用 `unsafe` 或忽略平台特定分析警告，但必须显式标注 `[SupportedOSPlatform]`。
