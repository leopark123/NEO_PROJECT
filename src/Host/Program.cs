// Program.cs
// NEO Host 应用程序入口点 - S1-06 系统集成

namespace Neo.Host;

/// <summary>
/// 应用程序入口点。
/// </summary>
/// <remarks>
/// S1-06 系统集成：
/// - 初始化顺序: Clock → DataSource → Buffer → Renderer → Window
/// - 只做"接线"，不新增功能
/// </remarks>
internal static class Program
{
    /// <summary>
    /// 应用程序主入口点。
    /// </summary>
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }
}
