// 共用測試（tests/golden/pose.json）：角色＋動作＋時間 → 各骨骼世界座標與表情，和網頁版（preview/tests/golden.html）比對同一份答案
//
// --selftest=golden          比對 expected，全部通過結束碼 0
// --selftest=golden --update 用目前的計算結果重寫 expected（改了動作或骨架、確認畫面正確之後才用）
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using DeskPet.Content;
using DeskPet.Core;
using Godot;

namespace DeskPet.Tests;

public static class Golden
{
    const float PosTolerance = 0.01f, AngleTolerance = 0.01f;

    static string FilePath => $"{ContentLoader.Root}/tests/golden/pose.json";

    public static int Run(bool update)
    {
        var doc = JsonNode.Parse(File.ReadAllText(ProjectSettings.GlobalizePath(FilePath)))!;
        var (_, rigs) = ContentLoader.LoadCharacters();
        var bodies = rigs.Values.SelectMany(r => (r.Forms?.Values.Select(f => f.Data) ?? new[] { r }).Append(r)).Select(r => r.Body);
        var (_, sets) = ContentLoader.LoadAnimations(bodies);

        var results = doc["cases"]!.AsArray().Select(c => Compute(c!, rigs, sets)).ToList();
        if (update)
        {
            doc["expected"] = new JsonArray(results.Select(r => (JsonNode)r).ToArray());
            File.WriteAllText(ProjectSettings.GlobalizePath(FilePath), doc.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
            GD.Print($"[golden] 已更新 {results.Count} 個案例");
            return 0;
        }

        var expected = doc["expected"]!.AsArray();
        if (expected.Count != results.Count)
        {
            GD.Print($"[golden] ✗ expected 有 {expected.Count} 個，cases 有 {results.Count} 個（先跑 --update）");
            return 1;
        }
        var failures = 0;
        for (var i = 0; i < results.Count; i++)
            foreach (var problem in Compare(expected[i]!, results[i]))
            {
                if (failures++ < 20) GD.Print($"[golden] ✗ {Describe(doc["cases"]![i]!)}：{problem}");
            }
        GD.Print(failures == 0 ? $"[golden] 全部通過（{results.Count} 個案例）" : $"[golden] ✗ {failures} 個不一致");
        return failures == 0 ? 0 : 1;
    }

    static string Describe(JsonNode c) => $"{c["char"]} {c["clip"]} frac={c["frac"]}";

    // 和 Pet.Probe 一樣：直接取樣關鍵影格（不含交叉淡化、視線），再算骨架
    static JsonObject Compute(JsonNode c, Dictionary<string, RigData> rigs, Dictionary<string, Dictionary<string, ClipData>> sets)
    {
        var spec = c["char"]!.GetValue<string>().Split('@');
        var rig = ContentLoader.FormRig(rigs[spec[0]], spec.Length > 1 ? spec[1] : null);
        var clips = sets.GetValueOrDefault(rig.Body) ?? sets["humanoid"];
        var clip = clips[c["clip"]!.GetValue<string>()];
        var t = c["frac"]!.GetValue<float>() * clip.Duration;
        var mods = c["mods"]?.AsObject().ToDictionary(kv => kv.Key, kv => kv.Value!.GetValue<float>());

        var pose = new Dictionary<string, float>();
        foreach (var (ch, keys) in clip.Tracks)
            pose[ch] = Animator.SampleTrack(keys, t) * (mods != null && mods.TryGetValue(ch, out var m) ? m : 1);
        var world = new Skeleton(rig.Bones, rig.RootScale, rig.TrackScale).Compute(pose);

        var bones = new JsonObject();
        foreach (var b in rig.Bones)
        {
            var w = world[b.Name];
            bones[b.Name] = new JsonArray(Round(w.Origin.X), Round(w.Origin.Y), Round(Mathf.RadToDeg(w.Rotation)));
        }
        return new JsonObject { ["face"] = clip.FaceAt(t), ["bones"] = bones };
    }

    static float Round(float v) => MathF.Round(v, 4);

    static IEnumerable<string> Compare(JsonNode expected, JsonObject actual)
    {
        if (expected["face"]!.GetValue<string>() != actual["face"]!.GetValue<string>())
            yield return $"表情 {expected["face"]} ≠ {actual["face"]}";
        var eb = expected["bones"]!.AsObject();
        var ab = actual["bones"]!.AsObject();
        foreach (var (name, e) in eb)
        {
            if (ab[name] is not JsonArray a)
            {
                yield return $"少了骨骼 {name}";
                continue;
            }
            var ev = e!.AsArray().Select(v => v!.GetValue<float>()).ToArray();
            var av = a.Select(v => v!.GetValue<float>()).ToArray();
            if (MathF.Abs(ev[0] - av[0]) > PosTolerance || MathF.Abs(ev[1] - av[1]) > PosTolerance)
                yield return $"{name} 位置 ({ev[0]}, {ev[1]}) ≠ ({av[0]}, {av[1]})";
            if (AngleDiff(ev[2], av[2]) > AngleTolerance)
                yield return $"{name} 角度 {ev[2]} ≠ {av[2]}";
        }
    }

    static float AngleDiff(float a, float b)
    {
        var d = MathF.Abs(a - b) % 360;
        return d > 180 ? 360 - d : d;
    }
}
