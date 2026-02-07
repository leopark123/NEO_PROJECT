// WaveformLayout.cs
// Phase 3: Defines the 5-region layout for waveform rendering.
//
// Regions (based on reference design):
// 1) aEEG Ch1 - 25%
// 2) EEG Preview Ch1 - 5% (narrow strip showing ±range lines)
// 3) aEEG Ch2 - 25%
// 4) EEG Preview Ch2 - 5% (narrow strip showing ±range lines)
// 5) NIRS trend - 40%

using Vortice.Mathematics;

namespace Neo.UI.Rendering;

public readonly struct WaveformLayout
{
    public Rect Aeeg1 { get; init; }
    public Rect EegPreview1 { get; init; }
    public Rect Aeeg2 { get; init; }
    public Rect EegPreview2 { get; init; }
    public Rect Nirs { get; init; }

    public static WaveformLayout Create(int width, int height)
    {
        // Add top padding to prevent 200μV label from appearing too close to toolbar
        const float topPadding = 8f;

        // Ratios based on reference design:
        // aEEG1: 25% + EEG Preview1: 5% + aEEG2: 25% + EEG Preview2: 5% + NIRS: 40%
        float total = 100f;
        float aeeg = 25f;
        float eegPreview = 5f;
        float nirs = 40f;

        float availableHeight = Math.Max(0f, height - topPadding);
        float y = topPadding;  // Start from topPadding instead of 0
        float h1 = availableHeight * (aeeg / total);
        float h2 = availableHeight * (eegPreview / total);
        float h3 = availableHeight * (aeeg / total);
        float h4 = availableHeight * (eegPreview / total);
        float h5 = availableHeight * (nirs / total);

        var layout = new WaveformLayout
        {
            Aeeg1 = new Rect(0, y, width, h1),
            EegPreview1 = new Rect(0, y += h1, width, h2),
            Aeeg2 = new Rect(0, y += h2, width, h3),
            EegPreview2 = new Rect(0, y += h3, width, h4),
            Nirs = new Rect(0, y += h4, width, h5)
        };

        return layout;
    }
}
