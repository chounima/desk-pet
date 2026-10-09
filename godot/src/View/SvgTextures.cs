// 執行時把 SVG 轉成貼圖（依顯示大小決定解析度，放大也清楚），並保留 Image 供像素點擊判定
using System.Collections.Generic;
using DeskPet.Content;
using Godot;

namespace DeskPet.View;

public static class SvgTextures
{
    public sealed record Entry(ImageTexture Texture, Image Image);

    static readonly Dictionary<(string, float), Entry> Cache = new();

    public static Entry Load(string path, float scale)
    {
        if (Cache.TryGetValue((path, scale), out var hit)) return hit;
        var image = new Image();
        var err = image.LoadSvgFromString(ContentLoader.ReadText(path), scale);
        if (err != Error.Ok) GD.PushError($"SVG 轉換失敗 {path}: {err}");
        var entry = new Entry(ImageTexture.CreateFromImage(image), image);
        Cache[(path, scale)] = entry;
        return entry;
    }
}
