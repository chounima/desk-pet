// 與網頁預覽共用的資料格式（art/characters/*/rig.json、data/animations/*.json、art/props/props.json）
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace DeskPet.Content;

public sealed class RigData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public float RootScale { get; set; } = 1;
    public float[] HeadTop { get; set; } = { 6, -110 };
    public float Height { get; set; } = 270;
    public Dictionary<string, float> TrackScale { get; set; } = new();
    public List<BoneData> Bones { get; set; } = new();
    public List<SlotData> Slots { get; set; } = new();

    /// <summary>角色資料夾路徑（載入後設定），部件圖檔以此為基準。</summary>
    public string BasePath { get; set; } = "";
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
