// 對話泡泡的款式（商店商品）：classic 經典、cloud 雲朵、pixel 像素風、hand 手寫風
using Godot;

namespace DeskPet.View;

public static class BubbleStyles
{
    /// <summary>(外框樣式, 文字顏色, 尖角顏色, 旋轉角度)</summary>
    public static (StyleBoxFlat Box, Color Text, Color Tail, float Rotation) Make(string style, float u)
    {
        var box = new StyleBoxFlat
        {
            ContentMarginLeft = 12 * u, ContentMarginRight = 12 * u,
            ContentMarginTop = 6 * u, ContentMarginBottom = 7 * u,
        };
        Color bg, border, text = new("#2b2d3a");
        float rotation = 0;
        switch (style)
        {
            case "cloud":
                bg = new Color("#f4f8ff");
                border = new Color("#9fb8e8");
                box.SetCornerRadiusAll((int)(24 * u));
                box.SetBorderWidthAll((int)(3 * u));
                box.ContentMarginLeft = box.ContentMarginRight = 18 * u;
                break;
            case "pixel":
                bg = new Color("#e8f7d8");
                border = new Color("#1a1a2e");
                text = new Color("#1a1a2e");
                box.SetCornerRadiusAll(0);
                box.SetBorderWidthAll((int)(4 * u));
                break;
            case "hand":
                bg = new Color("#fff8e6");
                border = new Color("#b08a5a");
                text = new Color("#3a2a18");
                box.CornerRadiusTopLeft = (int)(18 * u);
                box.CornerRadiusTopRight = (int)(6 * u);
                box.CornerRadiusBottomRight = (int)(18 * u);
                box.CornerRadiusBottomLeft = (int)(8 * u);
                box.SetBorderWidthAll((int)(2 * u));
                rotation = -0.035f;
                break;
            default:
                bg = new Color(1, 1, 1, 0.96f);
                border = new Color("#3a3d4f");
                box.SetCornerRadiusAll((int)(12 * u));
                box.SetBorderWidthAll((int)(2 * u));
                break;
        }
        box.BgColor = bg;
        box.BorderColor = border;
        return (box, text, bg, rotation);
    }
}
