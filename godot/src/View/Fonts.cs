// 介面字型：Godot 內建字型沒有中文字，改用系統字型（正黑體優先，符號與表情符號備援）
using Godot;

namespace DeskPet.View;

public static class Fonts
{
    public static readonly Font Ui = new SystemFont
    {
        FontNames = new[]
        {
            "Microsoft JhengHei UI", "Microsoft JhengHei", "Microsoft YaHei UI",
            "Segoe UI", "Segoe UI Symbol", "Segoe UI Emoji",
        },
        FontWeight = 600,
    };
}
