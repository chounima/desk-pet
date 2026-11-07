// 與網頁預覽共用的資料格式（art/characters/*/rig.json、data/animations/*.json、art/props/props.json）
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DeskPet.Content;

public sealed class RigData
{
    /// <summary>這個程式認得的最新角色格式版本（schemas/rig.schema.json）</summary>
    public const int FormatVersion = 1;

    /// <summary>角色格式版本；沒有 = 1。比 FormatVersion 新的檔案照樣讀，但會提醒</summary>
    public int Version { get; set; } = 1;
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
    /// <summary>錨點：配件（hat、eyes、neck、back）、嘴巴（mouth，講話的聲波）…，開放的字典</summary>
    public Dictionary<string, AnchorData> Anchors { get; set; } = new();
    /// <summary>次級物理 { 骨骼: 參數 }（尾巴、披布、頭髮…，見 Core/Spring.cs）</summary>
    public Dictionary<string, PhysicsParams>? Physics { get; set; }
    /// <summary>分層臉（選配）：眼眶、瞳孔、眼線、腮紅、嘴…分開，由 Game/FaceDriver 決定每層顯示什麼；沒有就只用整張臉</summary>
    public FaceRig? Face { get; set; }
    /// <summary>（第 6 階段）其他視角（bust = 正面半身）與半身取景</summary>
    public Dictionary<string, JsonElement>? Views { get; set; }
    public JsonElement? Bust { get; set; }
    /// <summary>還不認得的欄位：讀進來保留，工具重寫檔案時不會弄丟（之後版本的欄位、AI 部件包先放的資料）</summary>
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
    public List<BoneData> Bones { get; set; } = new();
    public List<SlotData> Slots { get; set; } = new();

    /// <summary>用哪個角色的專屬動作（部件包、自訂角色沿用範本的；預設 = Id）</summary>
    [JsonIgnore] public string? AnimSource { get; set; }
    /// <summary>這是哪個型態（有型態的角色才有；角色＋型態的專屬動作用）</summary>
    [JsonIgnore] public string? FormName { get; set; }

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

/// <summary>次級物理參數（rig.json 的 physics）。數值意義見 Core/Spring.cs</summary>
public sealed class PhysicsParams
{
    /// <summary>彈簧強度（1/秒²）：越大越快回到動畫的角度</summary>
    public double Stiffness { get; set; } = 120;
    /// <summary>阻尼（1/秒）：越大越快停下來、甩的次數越少</summary>
    public double Damping { get; set; } = 8;
    /// <summary>重力（弧度/秒²）：往下垂的程度，0 = 不受重力</summary>
    public double Gravity { get; set; }
    /// <summary>慣性倍率：身體加速、急停時甩動的程度</summary>
    public double Inertia { get; set; } = 1;
    /// <summary>空氣阻力：移動時往後拖的程度（拖的角度和速度成正比），停下時會彈回並甩過頭</summary>
    public double Drag { get; set; }
    /// <summary>最多偏離動畫幾度</summary>
    public double Limit { get; set; } = 35;
    /// <summary>骨頭長度（Q 版單位）：越長慣性造成的轉動越小</summary>
    public double Length { get; set; } = 60;
    /// <summary>部件在骨頭座標裡往哪個方向延伸（度，0 = 往右、90 = 往下）：決定重力、慣性從哪個方向推</summary>
    public double Axis { get; set; } = 90;
}

/// <summary>rig.json 的 face（分層臉）：圖層照順序疊；表情決定每層用哪張圖（tools/artgen/faces.py 產生）</summary>
public sealed class FaceRig
{
    /// <summary>所有圖層共用的範圍（頭部座標）</summary>
    public float[] Box { get; set; } = { 0, 0, 1, 1 };
    public List<FaceLayer> Layers { get; set; } = new();
    /// <summary>{ 表情: { 圖層: 圖名 } }；沒列的圖層不顯示</summary>
    public Dictionary<string, Dictionary<string, string>> Expressions { get; set; } = new();
    /// <summary>{ 表情: { 圖層: 不透明度 } }（腮紅深淺）</summary>
    public Dictionary<string, Dictionary<string, float>>? Alpha { get; set; }
    /// <summary>講話時嘴巴管道的值（half、open、a、i、u、e、o）→ 嘴型圖名</summary>
    public Dictionary<string, string>? Talk { get; set; }
}

public sealed class FaceLayer
{
    public string Name { get; set; } = "";
    /// <summary>{ 圖名: 檔案（相對角色資料夾）}</summary>
    public Dictionary<string, string> Variants { get; set; } = new();
    /// <summary>跟著哪一層換圖（瞳孔、眼線跟著 eyes）：用同名的圖，沒有就不顯示</summary>
    public string? Follow { get; set; }
    /// <summary>視線位移範圍 [左, 上, 右, 下]（瞳孔）</summary>
    public float[]? Move { get; set; }
}

public sealed class AnchorData
{
    public string Bone { get; set; } = "head";
    public float X { get; set; }
    public float Y { get; set; }
    public float Scale { get; set; } = 1;
    /// <summary>轉頭視差的深度（掛在這裡的配件跟著移動，見 SlotData.Depth）</summary>
    public float Depth { get; set; }
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
    /// <summary>轉頭視差的深度：看游標時位移 = 臉的位移 × Depth（臉 = 1、前髮 &gt; 1、後髮 &lt; 0）；沒寫時表情插槽算 1，其他不動</summary>
    public float? Depth { get; set; }
    /// <summary>表情插槽：{ 表情名: 檔案 }</summary>
    public Dictionary<string, string>? Variants { get; set; }
    /// <summary>部件替換圖：{ 替換名: { file, box } }</summary>
    public Dictionary<string, AltData>? Alt { get; set; }
    /// <summary>網格變形（選配，見 Core/Mesh.cs）：關節附近跟著兩根骨頭彎</summary>
    public MeshData? Mesh { get; set; }
    [JsonExtensionData] public Dictionary<string, JsonElement>? Extra { get; set; }
}

/// <summary>插槽的網格：部件範圍鋪 Cols × Rows 格，頂點依距離自動分配給 Bones 的權重</summary>
public sealed class MeshData
{
    public int Cols { get; set; } = 2;
    public int Rows { get; set; } = 4;
    public List<string> Bones { get; set; } = new();
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
    /// <summary>分層臉的眼睛、嘴巴圖（選配，格式同 Face）：表情沒被暫時蓋掉時才用</summary>
    public JsonElement? Eyes { get; set; }
    public JsonElement? Mouth { get; set; }
    public List<string>? Props { get; set; }
    public Dictionary<string, string>? Alt { get; set; }
    public string? Fx { get; set; }
    public float Move { get; set; }
    public Dictionary<string, List<Keyframe>> Tracks { get; set; } = new();

    List<(float t, string name)>? faceKeys, eyesKeys, mouthKeys;

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

    public string FaceAt(float t) => Discrete(Face, t, ref faceKeys) ?? "idle";
    public string? EyesAt(float t) => Eyes is { } e ? Discrete(e, t, ref eyesKeys) : null;
    public string? MouthAt(float t) => Mouth is { } m ? Discrete(m, t, ref mouthKeys) : null;

    // 不連續的軌道：字串，或 [[時間, 值], ...]（取 t 之前最後一個）
    static string? Discrete(JsonElement track, float t, ref List<(float t, string name)>? cache)
    {
        if (track.ValueKind == JsonValueKind.String) return track.GetString()!;
        if (track.ValueKind != JsonValueKind.Array || track.GetArrayLength() == 0) return null;
        cache ??= track.EnumerateArray().Select(k => (k[0].GetSingle(), k[1].GetString()!)).ToList();
        var value = cache[0].name;
        foreach (var (k, name) in cache)
            if (t >= k) value = name;
        return value;
    }
}

/// <summary>關鍵影格曲線：從這個關鍵影格到下一個怎麼變化</summary>
public enum Curve { Smooth, Linear, EaseIn, EaseOut, Back, Hold }

/// <summary>關鍵影格 [時間, 數值] 或 [時間, 數值, "曲線"]（曲線：smooth、linear、easeIn、easeOut、back、hold）</summary>
[JsonConverter(typeof(KeyframeConverter))]
public readonly record struct Keyframe(float T, float V, Curve Curve = Curve.Smooth);

public sealed class KeyframeConverter : JsonConverter<Keyframe>
{
    public override Keyframe Read(ref Utf8JsonReader reader, System.Type type, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("關鍵影格要是陣列 [時間, 數值, 曲線]");
        reader.Read();
        var t = reader.GetSingle();
        reader.Read();
        var v = reader.GetSingle();
        reader.Read();
        var curve = Curve.Smooth;
        if (reader.TokenType == JsonTokenType.String)
        {
            var name = reader.GetString();
            curve = name switch
            {
                "smooth" => Curve.Smooth, "linear" => Curve.Linear, "easeIn" => Curve.EaseIn, "easeOut" => Curve.EaseOut,
                "back" => Curve.Back, "hold" => Curve.Hold, _ => throw new JsonException($"不認識的曲線 {name}"),
            };
            reader.Read();
        }
        if (reader.TokenType != JsonTokenType.EndArray) throw new JsonException("關鍵影格最多三個值");
        return new Keyframe(t, v, curve);
    }

    public override void Write(Utf8JsonWriter writer, Keyframe k, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteNumberValue(k.T);
        writer.WriteNumberValue(k.V);
        if (k.Curve != Curve.Smooth) writer.WriteStringValue(char.ToLowerInvariant(k.Curve.ToString()[0]) + k.Curve.ToString()[1..]);
        writer.WriteEndArray();
    }
}
