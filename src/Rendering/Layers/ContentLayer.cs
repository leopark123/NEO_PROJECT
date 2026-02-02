// ContentLayer.cs
// 内容层 - 来源: ARCHITECTURE.md §5, ADR-008 (Layer 2)

using System.Numerics;
using Neo.Rendering.Core;
using Neo.Rendering.EEG;
using Neo.Rendering.Resources;
using Vortice.Direct2D1;
using Vortice.Mathematics;

namespace Neo.Rendering.Layers;

/// <summary>
/// 内容层（Layer 2 - 波形层）。
/// 占位渲染，用于验证渲染通道。
/// </summary>
/// <remarks>
/// 依据: ARCHITECTURE.md §5, ADR-008
///
/// S1-04 范围（占位实现）:
/// - 绘制占位矩形/波形框架
/// - 验证渲染管线正常工作
/// - 当无数据时绘制占位波形框架
/// - 当有数据时使用 EegPolylineRenderer 绘制真实波形
///
/// S2-05 架构（替代 S1-05）:
/// - EEG 波形（4通道）使用 PolylineBuilder + EegPolylineRenderer
/// - 预处理阶段: PolylineBuilder.Build() 构建折线数据 (O(N) 允许)
/// - 渲染阶段: EegPolylineRenderer.Render() 只做 Draw 调用
/// - 严格遵循铁律6: 渲染只做 Draw，无 O(N) 计算
/// - 无 DSP/滤波/LOD
/// - 无包络/RMS/统计
///
/// 未来扩展（S3+）:
/// - aEEG 灰度图（4通道）
/// - NIRS 趋势图（6通道）
///
/// 铁律约束（铁律6）:
/// - Render() 只做 Draw 调用
/// - 预处理在 Draw 之前完成
/// </remarks>
public sealed class ContentLayer : LayerBase
{
    // 占位配置
    private const float ChannelPadding = 10.0f;
    private const float PlaceholderLineWidth = 2.0f;
    private const int EegChannelCount = 4;

    // 占位颜色
    private static readonly Color4 PlaceholderColor = new(0.4f, 0.6f, 0.8f, 0.5f);
    private static readonly Color4 PlaceholderBorderColor = new(0.5f, 0.7f, 0.9f, 1.0f);

    // S2-05: 预处理构建器（复用以减少分配）
    private readonly PolylineBuilder _polylineBuilder = new();

    // S2-05: 折线渲染器（只做 Draw）
    private readonly EegPolylineRenderer _eegPolylineRenderer = new();

    // 通道视图配置缓存
    private EegChannelView[] _channelViews = new EegChannelView[EegChannelCount];
    private bool _channelViewsValid;
    private int _lastViewportWidth;
    private int _lastViewportHeight;

    /// <inheritdoc/>
    public override string Name => "Content";

    /// <inheritdoc/>
    public override int Order => 1;  // 中间层

    /// <summary>
    /// 占位通道数量（仅用于视觉验证）。
    /// </summary>
    public int PlaceholderChannelCount { get; set; } = 4;

    /// <inheritdoc/>
    protected override void OnRender(ID2D1DeviceContext context, ResourceCache resources, RenderContext renderContext)
    {
        // 检查是否有真实 EEG 数据
        bool hasRealData = renderContext.Channels != null && renderContext.Channels.Count > 0;

        if (hasRealData)
        {
            // S2-05: 使用 PolylineBuilder + EegPolylineRenderer
            RenderEegWaveforms(context, resources, renderContext);
        }
        else
        {
            // 无数据时绘制占位波形
            DrawPlaceholderContent(context, resources, renderContext);
        }

        NeedsRedraw = false;
    }

    /// <summary>
    /// 渲染 EEG 波形（S2-05 架构）。
    /// </summary>
    /// <remarks>
    /// 铁律6: 预处理 + 渲染分离
    /// - 预处理: PolylineBuilder.Build() 构建折线数据
    /// - 渲染: EegPolylineRenderer.Render() 只做 Draw 调用
    /// </remarks>
    private void RenderEegWaveforms(ID2D1DeviceContext context, ResourceCache resources, RenderContext renderContext)
    {
        var channels = renderContext.Channels;
        if (channels == null || channels.Count == 0)
            return;

        // 更新通道视图配置
        UpdateChannelViews(renderContext);

        // === 预处理阶段: 构建渲染数据 ===
        var channelRenderDataList = new EegChannelRenderData[Math.Min(channels.Count, EegChannelCount)];

        for (int i = 0; i < channelRenderDataList.Length; i++)
        {
            var channelData = channels[i];
            var channelView = _channelViews[i];

            if (!channelView.IsVisible)
                continue;

            // 构建折线数据（预处理，可以 O(N)）
            var buildResult = _polylineBuilder.Build(
                channelData.DataPoints.Span,
                channelData.QualityFlags.Span,
                channelData.StartTimestampUs,
                channelData.SampleIntervalUs,
                ts => (float)renderContext.TimestampToX(ts),
                uv => channelView.UvToY(uv),
                renderContext.VisibleRange.StartUs,
                renderContext.VisibleRange.EndUs);

            // 封装为渲染数据
            channelRenderDataList[i] = new EegChannelRenderData
            {
                ChannelIndex = i,
                Points = buildResult.Points,
                Segments = buildResult.Segments,
                Gaps = buildResult.Gaps,
                SaturationIndices = buildResult.SaturationIndices,
                ChannelArea = new Rect(0, channelView.YOffset, renderContext.ViewportWidth, channelView.Height),
                Color = channelView.Color,
                LineWidth = channelView.LineWidth,
                BaselineY = channelView.BaselineY
            };
        }

        var renderData = new EegWaveformRenderData
        {
            Channels = channelRenderDataList
        };

        // === 渲染阶段: 只做 Draw 调用 ===
        _eegPolylineRenderer.Render(context, resources, renderData);
    }

    /// <summary>
    /// 更新通道视图配置。
    /// </summary>
    private void UpdateChannelViews(RenderContext renderContext)
    {
        int width = renderContext.ViewportWidth;
        int height = renderContext.ViewportHeight;

        // 仅在视口变化时重新计算
        if (_channelViewsValid && _lastViewportWidth == width && _lastViewportHeight == height)
            return;

        _lastViewportWidth = width;
        _lastViewportHeight = height;

        float dpiScale = (float)renderContext.DpiScale;
        float padding = ChannelPadding * dpiScale;
        float totalPadding = padding * (EegChannelCount + 1);
        float channelHeight = (height - totalPadding) / EegChannelCount;

        for (int i = 0; i < EegChannelCount; i++)
        {
            float yOffset = padding + i * (channelHeight + padding);
            _channelViews[i] = EegChannelView.CreateDefault(i, yOffset, channelHeight, dpiScale);
        }

        _channelViewsValid = true;
    }

    /// <summary>
    /// 绘制占位内容（无真实数据时）。
    /// </summary>
    private void DrawPlaceholderContent(ID2D1DeviceContext context, ResourceCache resources, RenderContext renderContext)
    {
        float width = renderContext.ViewportWidth;
        float height = renderContext.ViewportHeight;
        float dpiScale = (float)renderContext.DpiScale;

        // 计算通道高度
        int channelCount = Math.Max(1, PlaceholderChannelCount);
        float padding = ChannelPadding * dpiScale;
        float totalPadding = padding * (channelCount + 1);
        float channelHeight = (height - totalPadding) / channelCount;

        if (channelHeight < 10)
            return;  // 空间不足，不绘制

        var fillBrush = resources.GetSolidBrush(PlaceholderColor);
        var borderBrush = resources.GetSolidBrush(PlaceholderBorderColor);

        // 绘制每个占位通道
        for (int i = 0; i < channelCount; i++)
        {
            float y = padding + i * (channelHeight + padding);

            // 绘制通道区域（半透明填充）
            var rect = new Rect(padding, y, width - 2 * padding, channelHeight);
            context.FillRectangle(rect, fillBrush);
            context.DrawRectangle(rect, borderBrush, PlaceholderLineWidth);

            // 绘制占位波形线（简单正弦曲线占位）
            DrawPlaceholderWaveform(context, borderBrush, padding, y, width - 2 * padding, channelHeight, i);
        }
    }

    /// <summary>
    /// 绘制占位波形。
    /// </summary>
    /// <remarks>
    /// 仅用于视觉验证渲染管线。
    /// 使用固定数量的线段，不依赖实际数据。
    /// </remarks>
    private static void DrawPlaceholderWaveform(
        ID2D1DeviceContext context,
        ID2D1SolidColorBrush brush,
        float x,
        float y,
        float width,
        float height,
        int channelIndex)
    {
        // 固定使用 100 个线段（不依赖数据）
        const int segments = 100;
        float segmentWidth = width / segments;
        float centerY = y + height / 2;
        float amplitude = height * 0.3f;

        // 每个通道使用不同频率以区分
        float frequency = 2.0f + channelIndex * 0.5f;

        Vector2? lastPoint = null;

        for (int i = 0; i <= segments; i++)
        {
            float px = x + i * segmentWidth;
            // 简单正弦波占位
            float py = centerY + amplitude * MathF.Sin(frequency * MathF.PI * i / segments);

            if (lastPoint.HasValue)
            {
                context.DrawLine(lastPoint.Value, new Vector2(px, py), brush, 1.5f);
            }

            lastPoint = new Vector2(px, py);
        }
    }
}
