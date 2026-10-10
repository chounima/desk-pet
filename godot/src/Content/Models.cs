// 與網頁預覽共用的資料格式（art/characters/*/rig.json、data/animations/*.json、art/props/props.json）
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeskPet.Content;

public sealed class RigData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public float RootScale { get; set; } = 1;
    public float[] HeadTop { get; set; } = { 6, -110 };
    public float Height { get; set; } = 270;
    /// <summary>體型：humanoid（人形）或 quadruped（四足），決定用哪一套動作</summary>
    public string Body { get; set; } = "humanoid";
    /// <summary>可切換的型態（只有預設型態的 rig.json 有）</summary>
    public Dictionary<string, FormData>? Forms { get; set; }
    /// <summary>依型態調整的排球參數</summary>
    public VolleyBody? Volley { get; set; }
    public Dictionary<string, float> TrackScale { get; set; } = new();
    /// <summary>個別道具的位置／大小修正（Q 版單位）</summary>
    public Dictionary<string, PropOverride> PropOverrides { get; set; } = new();
    /// <summary>配件錨點：hat、eyes、neck、back</summary>
    public Dictionary<string, AnchorData> Anchors { get; set; } = new();
    public List<BoneData> Bones { get; set; } = new();
    public List<SlotData> Slots { get; set; } = new();

    /// <summary>角色資料夾路徑（載入後設定），部件圖檔以此為基準。</summary>
    public string BasePath { get; set; } = "";

    /// <summary>自訂角色：換成自己圖片的部件（同衣服的格式，當作這個角色的基本外觀）</summary>
    [JsonIgnore] public Dictionary<string, OutfitPart>? DefaultOutfit { get; set; }
    /// <summary>自訂角色的台詞（沒有的 key 用 default.json）</summary>
    [JsonIgnore] public Dictionary<string, List<string>>? CustomLines { get; set; }

    /// <summary>部件圖的完整路徑：絕對路徑（自訂角色的表情）直接用，否則接在角色資料夾後面</summary>
    public string Resolve(string file) =>
        System.IO.Path.IsPathRooted(file) || file.Contains("://") ? file : BasePath + file;
}

public sealed class AnchorData
{
    public string Bone { get; set; } = "head";
    public float X { get; set; }
    public float Y { get; set; }
    public float Scale { get; set; } = 1;
}

/// <summary>art/accessories/accessories.json</summary>
public sealed class AccessoryData
{
    public string File { get; set; } = "";
    public float[] Box { get; set; } = { 0, 0, 1, 1 };
    public string Slot { get; set; } = "";
    public string Anchor { get; set; } = "";
    public float Z { get; set; }
    public string Path { get; set; } = "";
}

public sealed class PropOverride
{
    public float Dx { get; set; }
    public float Dy { get; set; }
    public float Scale { get; set; } = 1;
}

public sealed class FormData
{
    public string Label { get; set; } = "";
    /// <summary>型態資料夾（相對角色資料夾），空字串 = 預設型態本身</summary>
    public string Rig { get; set; } = "";
    [JsonIgnore] public RigData Data { get; set; } = null!;
}

public sealed class VolleyBody
{
    public float? Reach { get; set; }
    public float? HitHeight { get; set; }
    public bool? Spike { get; set; }
    public bool? Dive { get; set; }
}

/// <summary>data/animations/&lt;體型&gt;/index.json</summary>
public sealed class BodyIndex
{
    public List<string> Clips { get; set; } = new();
    /// <summary>沒有專屬版的動作借用哪一個：{ 動作名: 借用的動作名 }</summary>
    public Dictionary<string, string> Alias { get; set; } = new();
}

public sealed class BoneData
{
    public string Name { get; set; } = "";
    public string? Parent { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
}

public sealed class SlotData
{
    public string Name { get; set; } = "";
    public string Bone { get; set; } = "";
    public float Z { get; set; }
    public string? File { get; set; }
    public float[] Box { get; set; } = { 0, 0, 1, 1 };
    public bool Far { get; set; }
    /// <summary>表情插槽：{ 表情名: 檔案 }</summary>
    public Dictionary<string, string>? Variants { get; set; }
    /// <summary>部件替換圖：{ 替換名: { file, box } }</summary>
    public Dictionary<string, AltData>? Alt { get; set; }
}

public sealed class AltData
{
    public string File { get; set; } = "";
    public float[] Box { get; set; } = { 0, 0, 1, 1 };
}

public sealed class PropData
{
    public string File { get; set; } = "";
    public string Bone { get; set; } = "";
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float[] Box { get; set; } = { 0, 0, 1, 1 };
    public string Path { get; set; } = "";
}

public sealed class ClipData
{
    public string Name { get; set; } = "";
    public string Label { get; set; } = "";
    public bool Hidden { get; set; }
    public float Duration { get; set; } = 1;
    public bool Loop { get; set; }
    public string? Next { get; set; }
    /// <summary>字串，或 [[時間, 表情], ...]</summary>
    public JsonElement Face { get; set; }
    public List<string>? Props { get; set; }
    public Dictionary<string, string>? Alt { get; set; }
    public string? Fx { get; set; }
    public float Move { get; set; }
    public Dictionary<string, List<float[]>> Tracks { get; set; } = new();

    List<(float t, string name)>? faceKeys;

    /// <summary>借用這個動作的姿勢，但名稱、標籤、next 用原本動作的</summary>
    public ClipData AliasAs(string name, ClipData? own)
    {
        var c = (ClipData)MemberwiseClone();
        c.Name = name;
        c.Label = own?.Label ?? Label;
        c.Hidden = own?.Hidden ?? Hidden;
        c.Next = own?.Next;
        return c;
    }

    public string FaceAt(float t)
    {
        if (Face.ValueKind == JsonValueKind.String) return Face.GetString()!;
        if (Face.ValueKind != JsonValueKind.Array) return "idle";
        faceKeys ??= Face.EnumerateArray().Select(k => (k[0].GetSingle(), k[1].GetString()!)).ToList();
        var value = faceKeys[0].name;
        foreach (var (k, name) in faceKeys)
            if (t >= k) value = name;
        return value;
    }
}
