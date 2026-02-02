# 📋 TASK-S1-03: Vortice 渲染底座

> **Sprint**: 1  
> **负责方**: Codex  
> **优先级**: 🔴 P0  
> **预估工时**: 8h  
> **状态**: ⏳ 待开始

---

## 1. 目标

使用 Vortice 封装 Direct3D 11 渲染底座，实现设备管理、DPI 感知、DeviceLost 恢复。

---

## 2. 输入（必读文件）

| 文件 | 重点章节 |
|------|----------|
| `spec/00_CONSTITUTION.md` | 铁律6（渲染线程只Draw） |
| `spec/ARCHITECTURE.md` | §5（渲染层）、ADR-002、ADR-008 |
| `spec/DECISIONS.md` | ADR-002（Vortice选型）、ADR-008（三层架构） |
| `spec/ACCEPTANCE_TESTS.md` | AT-07（DPI）、AT-08（DeviceLost） |

---

## 3. 输出

### 3.1 代码文件

```
src/Rendering/
├── Device/
│   ├── DeviceManager.cs          # 设备生命周期管理
│   ├── SwapChainManager.cs       # 交换链管理
│   └── DpiHelper.cs              # DPI 工具类
├── Resources/
│   ├── ResourceCache.cs          # GPU资源缓存
│   └── ShaderManager.cs          # 着色器管理
└── Core/
    └── RenderContext.cs          # 渲染上下文

tests/Rendering.Tests/Device/
├── DeviceManagerTests.cs
├── DpiTests.cs
└── DeviceLostTests.cs
```

### 3.2 交接文档

```
handoff/renderer-device-api.md
```

---

## 4. 设计规格

### 4.1 DeviceManager

```csharp
/// <summary>
/// Direct3D 11 设备管理器
/// </summary>
/// <remarks>
/// <para><b>线程模型</b>: 仅渲染线程访问</para>
/// <para><b>生命周期</b>: 应用程序级单例</para>
/// </remarks>
public class DeviceManager : IDisposable
{
    /// <summary>D3D11 设备</summary>
    public ID3D11Device Device { get; }
    
    /// <summary>设备上下文</summary>
    public ID3D11DeviceContext Context { get; }
    
    /// <summary>当前 DPI 缩放因子</summary>
    public float DpiScale { get; private set; }
    
    /// <summary>设备是否有效</summary>
    public bool IsDeviceValid { get; }
    
    /// <summary>初始化设备</summary>
    public void Initialize(IntPtr hwnd);
    
    /// <summary>处理 DPI 变化</summary>
    public void OnDpiChanged(float newDpi);
    
    /// <summary>检查并恢复设备</summary>
    public bool CheckAndRecoverDevice();
    
    /// <summary>设备丢失事件</summary>
    public event EventHandler DeviceLost;
    
    /// <summary>设备恢复事件</summary>
    public event EventHandler DeviceRecovered;
}
```

### 4.2 SwapChainManager

```csharp
public class SwapChainManager : IDisposable
{
    /// <summary>交换链</summary>
    public IDXGISwapChain1 SwapChain { get; }
    
    /// <summary>后缓冲区渲染目标视图</summary>
    public ID3D11RenderTargetView RenderTargetView { get; }
    
    /// <summary>调整大小</summary>
    public void Resize(int width, int height);
    
    /// <summary>呈现</summary>
    public void Present(int syncInterval = 1);
}
```

### 4.3 DPI 处理

```csharp
public static class DpiHelper
{
    /// <summary>获取窗口 DPI</summary>
    public static float GetDpiForWindow(IntPtr hwnd);
    
    /// <summary>逻辑像素转物理像素</summary>
    public static int LogicalToPhysical(int logical, float dpiScale);
    
    /// <summary>物理像素转逻辑像素</summary>
    public static float PhysicalToLogical(int physical, float dpiScale);
}
```

---

## 5. DeviceLost 恢复流程

```
检测到 DeviceLost
        │
        ▼
  ┌─────────────────┐
  │ 1. 释放所有资源  │
  │    (RTV, Buffer) │
  └────────┬────────┘
           │
           ▼
  ┌─────────────────┐
  │ 2. 释放设备     │
  │    Device.Dispose│
  └────────┬────────┘
           │
           ▼
  ┌─────────────────┐
  │ 3. 重新创建设备  │
  │    D3D11.CreateDevice│
  └────────┬────────┘
           │
           ▼
  ┌─────────────────┐
  │ 4. 重新创建交换链│
  │    SwapChain     │
  └────────┬────────┘
           │
           ▼
  ┌─────────────────┐
  │ 5. 重新创建资源  │
  │    从 ResourceCache│
  └────────┬────────┘
           │
           ▼
  ┌─────────────────┐
  │ 6. 触发 Recovered│
  │    事件          │
  └─────────────────┘
```

---

## 6. 验收标准

### 6.1 功能验收

- [ ] 设备正常创建和销毁
- [ ] 交换链正确调整大小
- [ ] DPI 变化正确处理

### 6.2 AT-07: DPI 切换

```
测试步骤：
1. 在 100% DPI 下启动
2. 切换到 150% DPI
3. 切换到 200% DPI
4. 切回 100% DPI

验收标准：
- [ ] 切换过程无崩溃
- [ ] 切换时间 < 500ms
- [ ] 渲染内容正确缩放
```

### 6.3 AT-08: DeviceLost 恢复

```
测试步骤：
1. 正常渲染中
2. 模拟 DeviceLost（Ctrl+Alt+Del / RDP断开）
3. 返回桌面

验收标准：
- [ ] 检测到 DeviceLost 事件
- [ ] 自动恢复，恢复时间 < 3秒
- [ ] 恢复后正常渲染
- [ ] 无内存泄漏
```

### 6.4 编译验收

- [ ] `dotnet build` 通过
- [ ] `dotnet test` 全部通过
- [ ] 正确引用 Vortice.Direct3D11 等包

---

## 7. NuGet 依赖

```xml
<PackageReference Include="Vortice.Direct3D11" Version="3.x.x" />
<PackageReference Include="Vortice.DXGI" Version="3.x.x" />
<PackageReference Include="Vortice.Mathematics" Version="1.x.x" />
```

---

## 8. 约束（不可违反）

```
❌ 禁止在渲染线程外访问 Device/Context
❌ 禁止忽略 DeviceLost 异常
❌ 禁止硬编码 DPI 值
✅ 必须实现资源重建机制
✅ 必须正确释放 COM 对象
```

---

## 9. 依赖与被依赖

### 依赖
- S1-01: 核心接口定义（IRenderTarget）

### 被依赖
- S1-04: 三层渲染框架（使用 DeviceManager）

---

## 10. 启动指令（给 Codex）

```
请先阅读以下文件：
1. spec/00_CONSTITUTION.md（铁律6）
2. spec/ARCHITECTURE.md §5
3. spec/DECISIONS.md ADR-002, ADR-008
4. spec/ACCEPTANCE_TESTS.md AT-07, AT-08

然后执行任务 TASK-S1-03：
- 使用 Vortice 实现 DeviceManager
- 实现 DPI 变化处理
- 实现 DeviceLost 恢复机制
- 完成后生成 handoff/renderer-device-api.md
```

---

**任务卡结束**
