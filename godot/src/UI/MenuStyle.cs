// 選單外觀：4K 高縮放時 Godot 的彈出選單字很小，依螢幕縮放放大字型與間距
using Godot;

namespace DeskPet.UI;

public static class MenuStyle
{
    public static void Apply(PopupMenu menu, float uiScale)
    {
        int Px(float v) => Mathf.RoundToInt(v * uiScale);
        menu.AddThemeFontSizeOverride("font_size", Px(15));
        menu.AddThemeFontSizeOverride("font_separator_size", Px(13));
        menu.AddThemeConstantOverride("v_separation", Px(6));
        menu.AddThemeConstantOverride("h_separation", Px(6));
        menu.AddThemeConstantOverride("item_start_padding", Px(8));
        menu.AddThemeConstantOverride("item_end_padding", Px(8));
        menu.AddThemeConstantOverride("icon_max_width", Px(16));
    }
}
