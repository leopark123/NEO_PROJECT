// MainWindow.xaml.cs
// Sprint 2.5: Clean shell — all regions extracted to UserControls.
// Sprint 1.4 render test code removed (validated and no longer needed).
//
// Layout per UI_SPEC.md §4:
// - Top toolbar (60px) → ToolbarPanel
// - Left navigation (60px) → NavPanel
// - Bottom status bar (30px) → StatusBarPanel
// - Right parameter panel (150px) → ChannelControlPanel
// - Right video + NIRS panel (300px) → inline (Phase 6)
// - Center waveform area (6 sub-regions per §4.1) → inline (Phase 3)

using System.Windows;

namespace Neo.UI;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }
}
