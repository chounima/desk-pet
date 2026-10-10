// 執行時把 SVG 轉成貼圖（依顯示大小決定解析度，放大也清楚），並保留 Image 供像素點擊判定
using System.Collections.Generic;
using DeskPet.Content;
using Godot;

namespace DeskPet.View;

public static class SvgTextures
{
    public sealed record Entry(ImageTexture Texture, Image Image);

    static readonly Dictionary<(string, float), Entry> Cache = new();

    static readonly string[] Bitmaps = { ".png", ".jpg", ".jpeg", ".webp" };

    /// <param name="box">點陣圖（自訂配件、衣服的 PNG）要拉伸成的範圍；SVG 用不到</param>
    public static Entry Load(string path, float scale, float[]? box = null)
    {
        if (Cache.TryGetValue((path, scale), out var hit)) return hit;
        var image = new Image();
        if (System.Array.IndexOf(Bitmaps, System.IO.Path.GetExtension(path).ToLowerInvariant()) >= 0)
        {
            // 點陣圖：縮放成「範圍 × 顯示倍率」的大小，和 SVG 的貼圖規格一致
            var err = image.Load(path);
            if (err != Error.Ok) { GD.PushError($"圖片讀取失敗 {path}: {err}"); image = Image.CreateEmpty(2, 2, false, Image.Format.Rgba8); }
            else if (box != null)
                image.Resize(System.Math.Max(1, (int)((box[2] - box[0]) * scale)), System.Math.Max(1, (int)((box[3] - box[1]) * scale)), Image.Interpolation.Lanczos);
            if (image.GetFormat() != Image.Format.Rgba8) image.Convert(Image.Format.Rgba8);
        }
        else
        {
            var err = image.LoadSvgFromString(ContentLoader.ReadText(path), scale);
            if (err != Error.Ok) GD.PushError($"SVG 轉換失敗 {path}: {err}");
        }
        var entry = new Entry(ImageTexture.CreateFromImage(image), image);
        Cache[(path, scale)] = entry;
        return entry;
    }
}
