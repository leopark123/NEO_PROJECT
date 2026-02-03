// ResourceCache.cs
// 渲染资源缓存 - 来源: ARCHITECTURE.md §5, 铁律6

using System.Collections.Concurrent;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;

namespace Neo.Rendering.Resources;

/// <summary>
/// 渲染资源缓存。
/// 管理 Brush、TextFormat 等 GPU 资源的缓存，避免每帧分配。
/// </summary>
/// <remarks>
/// 依据: ARCHITECTURE.md §5, 铁律6 (渲染线程不分配大对象)
///
/// 设计原则:
/// - 预创建常用资源
/// - 按需创建并缓存
/// - 设备丢失时清理
/// - 线程安全（用于资源查找）
/// </remarks>
public sealed class ResourceCache : IDisposable
{
    private ID2D1DeviceContext? _context;
    private IDWriteFactory? _writeFactory;
    private bool _disposed;

    // 画刷缓存：按颜色键缓存
    private readonly ConcurrentDictionary<Color4, ID2D1SolidColorBrush> _solidBrushes = new();

    // 文本格式缓存：按键缓存
    private readonly ConcurrentDictionary<TextFormatKey, IDWriteTextFormat> _textFormats = new();

    // 预定义颜色
    private static readonly Color4 ColorBlack = new(0, 0, 0, 1);
    private static readonly Color4 ColorWhite = new(1, 1, 1, 1);
    private static readonly Color4 ColorRed = new(1, 0, 0, 1);
    private static readonly Color4 ColorGreen = new(0, 1, 0, 1);
    private static readonly Color4 ColorBlue = new(0, 0, 1, 1);
    private static readonly Color4 ColorGray = new(0.5f, 0.5f, 0.5f, 1);
    private static readonly Color4 ColorLightGray = new(0.75f, 0.75f, 0.75f, 1);
    private static readonly Color4 ColorDarkGray = new(0.25f, 0.25f, 0.25f, 1);

    /// <summary>
    /// 缓存是否已初始化。
    /// </summary>
    public bool IsInitialized => _context != null && _writeFactory != null && !_disposed;

    /// <summary>
    /// 初始化资源缓存。
    /// </summary>
    /// <param name="context">D2D 设备上下文。</param>
    public void Initialize(ID2D1DeviceContext context)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        _context = context ?? throw new ArgumentNullException(nameof(context));
        _writeFactory = DWrite.DWriteCreateFactory<IDWriteFactory>(Vortice.DirectWrite.FactoryType.Shared);

        // 预创建常用画刷
        PrecreateBrushes();
    }

    /// <summary>
    /// 获取或创建纯色画刷。
    /// </summary>
    /// <param name="color">颜色。</param>
    /// <returns>纯色画刷。</returns>
    public ID2D1SolidColorBrush GetSolidBrush(Color4 color)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_context == null)
            throw new InvalidOperationException("ResourceCache not initialized.");

        return _solidBrushes.GetOrAdd(color, c => _context.CreateSolidColorBrush(c));
    }

    /// <summary>
    /// 获取或创建纯色画刷（使用 RGBA 值）。
    /// </summary>
    /// <param name="r">红色分量 (0-1)。</param>
    /// <param name="g">绿色分量 (0-1)。</param>
    /// <param name="b">蓝色分量 (0-1)。</param>
    /// <param name="a">透明度 (0-1)。</param>
    /// <returns>纯色画刷。</returns>
    public ID2D1SolidColorBrush GetSolidBrush(float r, float g, float b, float a = 1.0f)
    {
        return GetSolidBrush(new Color4(r, g, b, a));
    }

    /// <summary>
    /// 获取黑色画刷。
    /// </summary>
    public ID2D1SolidColorBrush BlackBrush => GetSolidBrush(ColorBlack);

    /// <summary>
    /// 获取白色画刷。
    /// </summary>
    public ID2D1SolidColorBrush WhiteBrush => GetSolidBrush(ColorWhite);

    /// <summary>
    /// 获取红色画刷。
    /// </summary>
    public ID2D1SolidColorBrush RedBrush => GetSolidBrush(ColorRed);

    /// <summary>
    /// 获取绿色画刷。
    /// </summary>
    public ID2D1SolidColorBrush GreenBrush => GetSolidBrush(ColorGreen);

    /// <summary>
    /// 获取蓝色画刷。
    /// </summary>
    public ID2D1SolidColorBrush BlueBrush => GetSolidBrush(ColorBlue);

    /// <summary>
    /// 获取灰色画刷。
    /// </summary>
    public ID2D1SolidColorBrush GrayBrush => GetSolidBrush(ColorGray);

    /// <summary>
    /// 获取浅灰色画刷。
    /// </summary>
    public ID2D1SolidColorBrush LightGrayBrush => GetSolidBrush(ColorLightGray);

    /// <summary>
    /// 获取深灰色画刷。
    /// </summary>
    public ID2D1SolidColorBrush DarkGrayBrush => GetSolidBrush(ColorDarkGray);

    /// <summary>
    /// 获取或创建文本格式。
    /// </summary>
    /// <param name="fontFamily">字体家族。</param>
    /// <param name="fontSize">字体大小（DIP）。</param>
    /// <param name="weight">字体粗细。</param>
    /// <param name="style">字体样式。</param>
    /// <returns>文本格式。</returns>
    public IDWriteTextFormat GetTextFormat(
        string fontFamily,
        float fontSize,
        FontWeight weight = FontWeight.Normal,
        Vortice.DirectWrite.FontStyle style = Vortice.DirectWrite.FontStyle.Normal)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_writeFactory == null)
            throw new InvalidOperationException("ResourceCache not initialized.");

        var key = new TextFormatKey(fontFamily, fontSize, weight, style);

        return _textFormats.GetOrAdd(key, k =>
            _writeFactory.CreateTextFormat(
                k.FontFamily,
                fontCollection: null!,
                k.Weight,
                k.Style,
                FontStretch.Normal,
                k.FontSize));
    }

    /// <summary>
    /// 获取默认文本格式（Segoe UI, 12pt）。
    /// </summary>
    public IDWriteTextFormat DefaultTextFormat => GetTextFormat("Segoe UI", 12.0f);

    /// <summary>
    /// 获取小字体文本格式（Segoe UI, 10pt）。
    /// </summary>
    public IDWriteTextFormat SmallTextFormat => GetTextFormat("Segoe UI", 10.0f);

    /// <summary>
    /// 获取大字体文本格式（Segoe UI, 16pt）。
    /// </summary>
    public IDWriteTextFormat LargeTextFormat => GetTextFormat("Segoe UI", 16.0f);

    /// <summary>
    /// 清理所有缓存的资源（设备丢失时调用）。
    /// </summary>
    public void Clear()
    {
        // 释放所有画刷
        foreach (var brush in _solidBrushes.Values)
        {
            brush.Dispose();
        }
        _solidBrushes.Clear();

        // 释放所有文本格式
        foreach (var format in _textFormats.Values)
        {
            format.Dispose();
        }
        _textFormats.Clear();

        // 释放 WriteFactory
        _writeFactory?.Dispose();
        _writeFactory = null;

        _context = null;
    }

    /// <summary>
    /// 预创建常用画刷。
    /// </summary>
    private void PrecreateBrushes()
    {
        if (_context == null)
            return;

        // 预创建常用颜色的画刷
        GetSolidBrush(ColorBlack);
        GetSolidBrush(ColorWhite);
        GetSolidBrush(ColorRed);
        GetSolidBrush(ColorGreen);
        GetSolidBrush(ColorBlue);
        GetSolidBrush(ColorGray);
        GetSolidBrush(ColorLightGray);
        GetSolidBrush(ColorDarkGray);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        Clear();
    }

    /// <summary>
    /// 文本格式缓存键。
    /// </summary>
    private readonly record struct TextFormatKey(
        string FontFamily,
        float FontSize,
        FontWeight Weight,
        Vortice.DirectWrite.FontStyle Style);
}
