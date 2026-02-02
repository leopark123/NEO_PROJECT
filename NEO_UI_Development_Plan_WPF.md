# NEO UI 开发方案 (WPF + D2D)

> **版本**: 2.0  
> **日期**: 2026-01-30  
> **技术栈**: WPF + Vortice D2D + MVVM  
> **定位**: 设计与实现规范（How）  

---

## ⚠️ 文档关系

| 文件 | 定位 | 关系 |
|------|------|------|
| `UI_SPEC.md` | 功能规格（What） | 本文件必须对齐 |
| `本文件` | 设计实现（How） | 视觉细节权威 |
| `CHARTER.md` | 铁律约束 | 不可违反 |
| `PROJECT_STATEUI.md` | 进度锚点 | 唯一进度记录 |

**重要**：功能验收以 `UI_SPEC.md` 为准，视觉验收以本文件为准。

---

## 一、技术栈

| 层级 | 技术 | 版本 |
|------|------|------|
| 平台 | Windows .NET | 9.0 |
| UI 框架 | WPF | - |
| 架构 | MVVM | - |
| MVVM 框架 | CommunityToolkit.Mvvm | 8.2.2 |
| 渲染 | Vortice.Direct2D1 | 3.8.1 |
| D3D | Vortice.Direct3D11 | 3.8.1 |
| WPF 集成 | D3DImage | - |

---

## 二、项目结构

```
src/Neo.UI/
├── Neo.UI.csproj
├── App.xaml / App.xaml.cs
│
├── Views/
│   ├── MainWindow.xaml
│   ├── Controls/
│   │   ├── WaveformPanel.xaml       # D3DImage 宿主
│   │   ├── NavPanel.xaml            # 导航面板
│   │   ├── ToolbarPanel.xaml        # 工具栏
│   │   ├── StatusBarPanel.xaml      # 状态栏
│   │   ├── ChannelControlPanel.xaml # 参数面板
│   │   ├── NirsPanel.xaml           # NIRS 面板
│   │   ├── VideoPanel.xaml          # 视频面板
│   │   ├── SeekBar.xaml             # 时间轴
│   │   └── ToggleSwitch.xaml        # 开关控件
│   └── Dialogs/
│       ├── LoginDialog.xaml
│       ├── PatientDialog.xaml
│       ├── FilterDialog.xaml
│       ├── DisplayDialog.xaml
│       ├── UserManagementDialog.xaml
│       ├── PasswordDialog.xaml
│       └── HistoryDialog.xaml
│
├── ViewModels/
│   ├── ViewModelBase.cs
│   ├── MainViewModel.cs
│   ├── WaveformViewModel.cs
│   ├── NirsViewModel.cs
│   ├── PlaybackViewModel.cs
│   ├── PatientViewModel.cs
│   └── StatusViewModel.cs
│
├── Services/
│   ├── INavigationService.cs
│   ├── NavigationService.cs
│   ├── IDialogService.cs
│   ├── DialogService.cs
│   └── AuditServiceAdapter.cs
│
├── Rendering/
│   ├── D3DImageRenderer.cs
│   ├── WaveformRenderHost.cs
│   └── QualityIndicatorRenderer.cs
│
├── Converters/
│   └── *.cs
│
└── Styles/
    ├── Colors.xaml
    ├── Fonts.xaml
    ├── Buttons.xaml
    ├── TextBoxes.xaml
    ├── ComboBoxes.xaml
    └── Toggles.xaml
```

---

## 三、颜色定义（冻结）

### 3.1 主题色

| 名称 | 色值 | 用途 |
|------|------|------|
| Primary | `#D81B60` | 主色调（品红） |
| PrimaryLight | `#E91E63` | 悬停状态 |
| PrimaryDark | `#AD1457` | 按下状态 |

### 3.2 背景色

| 名称 | 色值 | 用途 |
|------|------|------|
| BackgroundDark | `#1A1A1A` | 波形区背景 |
| Background | `#2D2D2D` | 面板背景 |
| Surface | `#F5F5F5` | 对话框背景 |
| SurfaceDark | `#E0E0E0` | 对话框次背景 |

### 3.3 功能色

| 名称 | 色值 | 用途 |
|------|------|------|
| Success | `#4CAF50` | 成功/已连接 |
| Warning | `#FF9800` | 警告/导联脱落 |
| Error | `#F44336` | 错误/饱和 |
| Info | `#2196F3` | 信息提示 |

### 3.4 波形色

| 名称 | 色值 | 用途 |
|------|------|------|
| EegChannel1 | `#00E676` | EEG 通道1（绿） |
| EegChannel2 | `#FFD54F` | EEG 通道2（黄） |
| AeegFill | `#00E676` 40% | aEEG 包络填充 |
| NirsTrend | `#29B6F6` | NIRS 趋势（蓝） |

### 3.5 质量指示色

| 名称 | 色值 | 用途 |
|------|------|------|
| Missing | `#9E9E9E` 50% | 缺失数据背景 |
| Saturated | `#F44336` 50% | 信号饱和高亮 |
| LeadOff | `#FF9800` 50% | 导联脱落背景 |

### 3.6 文字色

| 名称 | 色值 | 用途 |
|------|------|------|
| TextPrimary | `#212121` | 主文字 |
| TextSecondary | `#757575` | 次文字 |
| TextOnPrimary | `#FFFFFF` | 主色按钮文字 |
| TextOnDark | `#FFFFFF` | 深色背景文字 |

---

## 四、字体定义

| 用途 | 字体 | 大小 | 字重 |
|------|------|------|------|
| 标题 | Microsoft YaHei UI | 24px | Bold |
| 副标题 | Microsoft YaHei UI | 18px | Medium |
| 正文 | Microsoft YaHei UI | 14px | Regular |
| 小字 | Microsoft YaHei UI | 12px | Regular |
| 数值 | Consolas | 16px | Regular |
| 波形标签 | Consolas | 12px | Regular |

---

## 五、控件样式

### 5.1 按钮

#### 主要按钮 (PrimaryButtonStyle)

```xml
<Style x:Key="PrimaryButtonStyle" TargetType="Button">
    <Setter Property="Background" Value="#D81B60"/>
    <Setter Property="Foreground" Value="White"/>
    <Setter Property="FontSize" Value="14"/>
    <Setter Property="FontWeight" Value="Medium"/>
    <Setter Property="Padding" Value="24,12"/>
    <Setter Property="MinWidth" Value="120"/>
    <Setter Property="MinHeight" Value="44"/>
    <Setter Property="Cursor" Value="Hand"/>
    <!-- 悬停: #E91E63, 按下: #AD1457 -->
</Style>
```

#### 次要按钮 (SecondaryButtonStyle)

```xml
<Style x:Key="SecondaryButtonStyle" TargetType="Button">
    <Setter Property="Background" Value="Transparent"/>
    <Setter Property="Foreground" Value="#D81B60"/>
    <Setter Property="BorderBrush" Value="#D81B60"/>
    <Setter Property="BorderThickness" Value="1"/>
    <Setter Property="MinHeight" Value="44"/>
</Style>
```

#### 导航按钮 (NavButtonStyle)

```xml
<Style x:Key="NavButtonStyle" TargetType="Button">
    <Setter Property="Width" Value="60"/>
    <Setter Property="Height" Value="60"/>
    <Setter Property="Background" Value="Transparent"/>
    <!-- 悬停: #E0E0E0 -->
</Style>
```

### 5.2 Toggle 开关 (ToggleSwitchStyle)

```xml
<Style x:Key="ToggleSwitchStyle" TargetType="CheckBox">
    <Setter Property="Width" Value="50"/>
    <Setter Property="Height" Value="26"/>
    <!-- 轨道: 关闭 #E0E0E0, 开启 #4CAF50 -->
    <!-- 滑块: 白色圆形 22x22 -->
</Style>
```

### 5.3 输入框

| 属性 | 值 |
|------|-----|
| 高度 | 44px |
| 圆角 | 4px |
| 边框 | #E0E0E0, 聚焦时 #D81B60 |
| 内边距 | 12px |

### 5.4 下拉框

| 属性 | 值 |
|------|-----|
| 高度 | 44px |
| 圆角 | 4px |
| 下拉项高度 | 40px |

---

## 六、对话框详细规格

### 6.1 登录对话框 (LoginDialog)

| 属性 | 规格 |
|------|------|
| 窗口尺寸 | 1000 × 600 px |
| 布局 | 左右各 50% |

**左侧品牌区**：
- 背景：渐变 `#D81B60` → `#E91E63`
- Logo：100 × 100 px，居中
- 标题："NEO" 48px Bold 白色
- 副标题："aEEG + NIRS" 36px Light 白色
- 公司名：14px，白色 80% 透明

**右侧登录区**：
- 背景：白色
- 标题："登录" 28px Medium
- 用户名输入框：宽度 300px
- 密码输入框：宽度 300px
- 登录按钮：PrimaryButtonStyle，宽度 200px

### 6.2 患者信息对话框 (PatientDialog)

| 属性 | 规格 |
|------|------|
| 窗口尺寸 | 900 × 700 px |
| 布局 | 双列表单 |
| 标题栏 | 蓝色背景 `#2196F3` |

**字段布局（2列）**：

| 左列 | 右列 |
|------|------|
| 床位号 | 医院 |
| 母亲身份证 | 科室名称 |
| 姓 + 名 | 住院号* |
| 性别 | 出生日期 + 胎龄 |
| APGAR评分 | 出生体重 + 日龄 |

**疾病勾选（GroupBox）**：
- 宫内感染
- 慢性缺氧缺血
- 先天遗传代谢病
- 染色体基因异常
- 其他（文本输入）

**母亲孕期（GroupBox）**：
- 妊高症
- 糖尿病
- 甲状腺功能衰退
- 长期服用激素
- 胎膜早破
- 胎盘早剥
- 其他（文本输入）

**底部按钮**：确定 + 取消，居中

### 6.3 滤波设置对话框 (FilterDialog)

| 属性 | 规格 |
|------|------|
| 窗口尺寸 | 400 × 300 px |
| 布局 | 垂直表单 |

**内容**：
- 低通滤波：ComboBox (15/35/50/70 Hz)
- 高通滤波：ComboBox (0.3/0.5/1.5 Hz)
- 陷波器：ComboBox (50/60 Hz)
- 确定 + 取消按钮

### 6.4 显示设置对话框 (DisplayDialog)

| 属性 | 规格 |
|------|------|
| 窗口尺寸 | 400 × 350 px |

**内容**：
- 扫描速度：ComboBox
- 标尺显示：Toggle
- EEG 网格显示：Toggle
- 10 分钟 aEEG 网格：Toggle

### 6.5 用户管理对话框 (UserManagementDialog)

| 属性 | 规格 |
|------|------|
| 窗口尺寸 | 800 × 500 px |
| 前置条件 | 密码验证 |

**布局**：
- 顶部：添加按钮
- 中部：用户列表 DataGrid
  - 列：工号 | 姓名 | 电话 | 邮箱 | 操作
- 操作：编辑 | 删除

### 6.6 历史数据对话框 (HistoryDialog)

| 属性 | 规格 |
|------|------|
| 窗口尺寸 | 900 × 600 px |

**布局**：
- 顶部：查询区（姓名输入 + 住院号输入 + 查询按钮）
- 中部：结果列表 DataGrid
  - 列：住院号 | 姓名 | 开始时间 | 结束时间
- 底部：加载按钮

### 6.7 密码验证对话框 (PasswordDialog)

| 属性 | 规格 |
|------|------|
| 窗口尺寸 | 400 × 200 px |
| 内容 | 密码输入 + 确定/取消 |

---

## 七、触摸支持规格

### 7.1 尺寸要求

| 元素 | 最小尺寸 |
|------|----------|
| 按钮 | 44 × 44 px |
| 导航按钮 | 60 × 60 px |
| 下拉框 | 高度 44px |
| Toggle 开关 | 50 × 26 px |
| SeekBar 滑块 | 20 × 20 px |
| 列表项 | 高度 48px |

### 7.2 间距要求

| 元素 | 间距 |
|------|------|
| 按钮间距 | ≥8px |
| 表单项间距 | ≥16px |
| 面板内边距 | ≥12px |

### 7.3 交互反馈

- 点击反馈：背景色变化（<100ms）
- 拖动反馈：实时跟随
- 禁用状态：50% 透明度

---

## 八、D3DImage 集成

### 8.1 渲染器核心代码

```csharp
public class D3DImageRenderer : IDisposable
{
    private D3DImage _d3dImage;
    private ID3D11Device _device;
    private ID3D11Texture2D _texture;
    private ID2D1RenderTarget _d2dTarget;
    
    public ImageSource ImageSource => _d3dImage;
    public ID2D1RenderTarget RenderTarget => _d2dTarget;
    
    public void Resize(int width, int height) { ... }
    public void BeginRender() { ... }
    public void EndRender() { ... }
    public void Dispose() { ... }
}
```

### 8.2 渲染循环

```csharp
// 60fps 渲染
CompositionTarget.Rendering += (s, e) =>
{
    _renderer.BeginRender();
    _waveformRenderer.Render(context);
    _renderer.EndRender();
};
```

---

## 九、MVVM 架构

### 9.1 ViewModel 示例

```csharp
public partial class MainViewModel : ObservableObject
{
    [ObservableProperty] private WaveformViewModel _waveformVM;
    [ObservableProperty] private NirsViewModel _nirsVM;
    [ObservableProperty] private StatusViewModel _statusVM;
    
    [RelayCommand]
    private async Task Navigate(string target)
    {
        // 导航逻辑
    }
}
```

### 9.2 审计集成

```csharp
partial void OnGainChanged(GainLevel value)
{
    _auditService.Log(AuditEventType.GAIN_CHANGE, $"Gain: {value}");
}
```

---

## 十、开发阶段

| Phase | 名称 | 工期 |
|-------|------|------|
| 1 | 项目框架搭建 | 1 周 |
| 2 | 主窗口框架 | 1 周 |
| 3 | 波形渲染集成 | 1.5 周 |
| 4 | 对话框系统 | 1.5 周 |
| 5 | 高级功能 | 1.5 周 |
| 6 | NIRS + 质量指示 | 1 周 |
| 7 | 测试优化 | 1 周 |
| **合计** | | **8.5 周** |

---

## 十一、csproj 配置

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0-windows</TargetFramework>
    <UseWPF>true</UseWPF>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  
  <ItemGroup>
    <PackageReference Include="CommunityToolkit.Mvvm" Version="8.2.2" />
    <PackageReference Include="Vortice.Direct2D1" Version="3.8.1" />
    <PackageReference Include="Vortice.Direct3D11" Version="3.8.1" />
    <PackageReference Include="Vortice.DXGI" Version="3.8.1" />
  </ItemGroup>
  
  <ItemGroup>
    <ProjectReference Include="..\Neo.Core\Neo.Core.csproj" />
    <ProjectReference Include="..\Neo.Rendering\Neo.Rendering.csproj" />
    <ProjectReference Include="..\Neo.DSP\Neo.DSP.csproj" />
    <ProjectReference Include="..\Neo.Storage\Neo.Storage.csproj" />
    <ProjectReference Include="..\Neo.Playback\Neo.Playback.csproj" />
    <ProjectReference Include="..\Neo.Video\Neo.Video.csproj" />
  </ItemGroup>
</Project>
```

---

## 十二、XAML 样式资源

### Colors.xaml

```xml
<ResourceDictionary>
    <!-- 主题色 -->
    <SolidColorBrush x:Key="PrimaryBrush" Color="#D81B60"/>
    <SolidColorBrush x:Key="PrimaryLightBrush" Color="#E91E63"/>
    <SolidColorBrush x:Key="PrimaryDarkBrush" Color="#AD1457"/>
    
    <!-- 背景色 -->
    <SolidColorBrush x:Key="BackgroundDarkBrush" Color="#1A1A1A"/>
    <SolidColorBrush x:Key="BackgroundBrush" Color="#2D2D2D"/>
    <SolidColorBrush x:Key="SurfaceBrush" Color="#F5F5F5"/>
    
    <!-- 功能色 -->
    <SolidColorBrush x:Key="SuccessBrush" Color="#4CAF50"/>
    <SolidColorBrush x:Key="WarningBrush" Color="#FF9800"/>
    <SolidColorBrush x:Key="ErrorBrush" Color="#F44336"/>
    <SolidColorBrush x:Key="InfoBrush" Color="#2196F3"/>
    
    <!-- 波形色 -->
    <SolidColorBrush x:Key="EegChannel1Brush" Color="#00E676"/>
    <SolidColorBrush x:Key="EegChannel2Brush" Color="#FFD54F"/>
    <SolidColorBrush x:Key="NirsTrendBrush" Color="#29B6F6"/>
    
    <!-- 质量指示色 -->
    <SolidColorBrush x:Key="MissingBrush" Color="#809E9E9E"/>
    <SolidColorBrush x:Key="SaturatedBrush" Color="#80F44336"/>
    <SolidColorBrush x:Key="LeadOffBrush" Color="#80FF9800"/>
</ResourceDictionary>
```

---

## 附录：文档关系声明

```
┌─────────────────────────────────────────────────────────────────┐
│                     文档职责分工                                │
├─────────────────────────────────────────────────────────────────┤
│                                                                 │
│  UI_SPEC.md (What)                                              │
│  • 功能需求                                                     │
│  • 布局尺寸                                                     │
│  • 稳定性/性能要求                                              │
│  • 审计要求                                                     │
│                                                                 │
│  NEO_UI_Development_Plan_WPF.md (How) [本文件]                  │
│  • 颜色定义                                                     │
│  • 字体规范                                                     │
│  • 控件样式                                                     │
│  • 对话框布局                                                   │
│  • 触摸尺寸                                                     │
│  • 技术实现                                                     │
│                                                                 │
│  验收规则:                                                      │
│  • 功能验收 → UI_SPEC.md                                        │
│  • 视觉验收 → 本文件                                            │
│                                                                 │
└─────────────────────────────────────────────────────────────────┘
```

---

*NEO_UI_Development_Plan_WPF v2.0*
