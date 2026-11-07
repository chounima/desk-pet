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
    static string PhysicsPath => $"{ContentLoader.Root}/tests/golden/physics.json";
    const float PhysicsTolerance = 0.05f;

    public static int Run(bool update) => RunPose(update) + RunPhysics(update) + RunCurves(update) + RunOverlay(update) + RunFace(update) + RunMesh(update) == 0 ? 0 : 1;

    // ---------- 網格（tests/golden/mesh.json）：角色＋動作＋時間（＋額外角度）＋插槽 → 頂點位置 ----------

    static int RunMesh(bool update)
    {
        var path = ProjectSettings.GlobalizePath($"{ContentLoader.Root}/tests/golden/mesh.json");
        var doc = JsonNode.Parse(File.ReadAllText(path))!;
        var (_, rigs) = ContentLoader.LoadCharacters();
        var bodies = rigs.Values.Select(r => r.Body);
        var (_, sets) = ContentLoader.LoadAnimations(bodies);
        var results = doc["cases"]!.AsArray().Select(c =>
        {
            var rig = rigs[c!["char"]!.GetValue<string>()];
            var clip = sets.For(rig)[c["clip"]!.GetValue<string>()];
            var t = c["frac"]!.GetValue<float>() * clip.Duration;
            var pose = new Dictionary<string, float>();
            foreach (var (ch, keys) in clip.Tracks) pose[ch] = Animator.SampleTrack(keys, t);
            foreach (var (ch, v) in c["add"]?.AsObject() ?? new JsonObject()) pose[ch] = pose.GetValueOrDefault(ch) + v!.GetValue<float>();
            var skeleton = new Skeleton(rig.Bones, rig.RootScale, rig.TrackScale);
            var rest = new Dictionary<string, Transform2D>(skeleton.Compute(new Dictionary<string, float>()));
            var world = skeleton.Compute(pose);
            var slot = rig.Slots.First(s => s.Name == c["slot"]!.GetValue<string>());
            var spec = c["mesh"] is { } m ? m.Deserialize<MeshData>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })! : slot.Mesh!;
            var mesh = new MeshDeform(slot, spec, rest, rig.Bones, rig.Slots, rig.RootScale);
            return new JsonArray(mesh.Compute(world).Select(v => (JsonNode)new JsonArray(MathF.Round(v.X, 3), MathF.Round(v.Y, 3))).ToArray());
        }).ToList();
        if (update)
        {
            doc["expected"] = new JsonArray(results.Select(r => (JsonNode)r).ToArray());
            File.WriteAllText(path, doc.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
            GD.Print($"[golden] 網格：已更新 {results.Count} 個案例");
            return 0;
        }
        var expected = doc["expected"]!.AsArray();
        var failures = 0;
        for (var i = 0; i < results.Count; i++)
        {
            var e = expected.Count > i ? expected[i]!.AsArray() : new JsonArray();
            var ok = e.Count == results[i].Count && e.Zip(results[i]).All(p =>
                MathF.Abs(p.First![0]!.GetValue<float>() - p.Second![0]!.GetValue<float>()) <= 0.01f &&
                MathF.Abs(p.First![1]!.GetValue<float>() - p.Second![1]!.GetValue<float>()) <= 0.01f);
            if (!ok && failures++ < 10) GD.Print($"[golden] ✗ 網格 {doc["cases"]![i]!.ToJsonString()}");
        }
        GD.Print(failures == 0 ? $"[golden] 網格全部通過（{results.Count} 個案例）" : $"[golden] ✗ 網格 {failures} 個不一致");
        return failures == 0 ? 0 : 1;
    }

    // ---------- 分層臉（tests/golden/face.json）：角色＋表情＋眨眼／嘴型／視線 → 每層 [名稱, 檔案, 不透明度, dx, dy] ----------

    static int RunFace(bool update)
    {
        var path = ProjectSettings.GlobalizePath($"{ContentLoader.Root}/tests/golden/face.json");
        var doc = JsonNode.Parse(File.ReadAllText(path))!;
        var (_, rigs) = ContentLoader.LoadCharacters();
        var results = doc["cases"]!.AsArray().Select(c =>
        {
            var look = c!["look"]?.AsArray();
            var input = new Game.Face.FaceInput(c["expression"]!.GetValue<string>(), c["blink"]?.GetValue<bool>() ?? false,
                c["mouth"]?.GetValue<string>(), c["eyes"]?.GetValue<string>(),
                look?[0]!.GetValue<float>() ?? 0, look?[1]!.GetValue<float>() ?? 0, c["mouthArt"]?.GetValue<string>());
            var layers = Game.Face.FaceDriver.Layers(rigs[c["char"]!.GetValue<string>()].Face!, input);
            return new JsonArray(layers.Select(l => (JsonNode)new JsonArray(l.Name, l.File, MathF.Round(l.Alpha, 4), MathF.Round(l.Dx, 4), MathF.Round(l.Dy, 4))).ToArray());
        }).ToList();
        if (update)
        {
            doc["expected"] = new JsonArray(results.Select(r => (JsonNode)r).ToArray());
            File.WriteAllText(path, doc.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
            GD.Print($"[golden] 分層臉：已更新 {results.Count} 個案例");
            return 0;
        }
        var expected = doc["expected"]!.AsArray();
        var failures = 0;
        for (var i = 0; i < results.Count; i++)
        {
            var want = expected.Count > i ? expected[i]!.ToJsonString() : "";
            var got = results[i].ToJsonString();
            if (want != got && failures++ < 10) GD.Print($"[golden] ✗ 分層臉 {doc["cases"]![i]!.ToJsonString()}：{want} ≠ {got}");
        }
        GD.Print(failures == 0 ? $"[golden] 分層臉全部通過（{results.Count} 個案例）" : $"[golden] ✗ 分層臉 {failures} 個不一致");
        return failures == 0 ? 0 : 1;
    }

    // ---------- 疊加層（tests/golden/overlay.json） ----------

    static int RunOverlay(bool update)
    {
        var path = ProjectSettings.GlobalizePath($"{ContentLoader.Root}/tests/golden/overlay.json");
        var doc = JsonNode.Parse(File.ReadAllText(path))!;
        var config = MotionConfig.Load();
        var results = doc["cases"]!.AsArray().Select(c => new JsonObject(
            Overlay.Deltas(config, c!["action"]!.GetValue<string>(), c["time"]!.GetValue<double>())
                   .Select(kv => new KeyValuePair<string, JsonNode?>(kv.Key, MathF.Round(kv.Value, 5))))).ToList();
        if (update)
        {
            doc["expected"] = new JsonArray(results.Select(r => (JsonNode)r).ToArray());
            File.WriteAllText(path, doc.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
            GD.Print($"[golden] 疊加層：已更新 {results.Count} 個案例");
            return 0;
        }
        var expected = doc["expected"]!.AsArray();
        var failures = 0;
        for (var i = 0; i < results.Count; i++)
        {
            var e = expected.Count > i ? expected[i]!.AsObject() : new JsonObject();
            var keys = e.Select(kv => kv.Key).Union(results[i].Select(kv => kv.Key)).ToList();
            foreach (var k in keys)
            {
                var want = e[k]?.GetValue<float>() ?? float.NaN;
                var got = results[i][k]?.GetValue<float>() ?? float.NaN;
                if (!(MathF.Abs(want - got) <= 0.0005f) && failures++ < 10)
                    GD.Print($"[golden] ✗ 疊加層 {doc["cases"]![i]!.ToJsonString()} {k}：{want} ≠ {got}");
            }
        }
        GD.Print(failures == 0 ? $"[golden] 疊加層全部通過（{results.Count} 個案例）" : $"[golden] ✗ 疊加層 {failures} 個不一致");
        return failures == 0 ? 0 : 1;
    }

    // ---------- 關鍵影格曲線（tests/golden/curves.json） ----------

    static int RunCurves(bool update)
    {
        var path = ProjectSettings.GlobalizePath($"{ContentLoader.Root}/tests/golden/curves.json");
        var doc = JsonNode.Parse(File.ReadAllText(path))!;
        var keys = JsonSerializer.Deserialize<List<Keyframe>>(doc["keys"]!.ToJsonString())!;
        var values = doc["samples"]!.AsArray().Select(t => MathF.Round(Animator.SampleTrack(keys, t!.GetValue<float>()), 4)).ToList();
        if (update)
        {
            doc["expected"] = new JsonArray(values.Select(v => (JsonNode)v).ToArray());
            File.WriteAllText(path, doc.ToJsonString(new JsonSerializerOptions { Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
            GD.Print($"[golden] 曲線：已更新 {values.Count} 個取樣");
            return 0;
        }
        var expected = doc["expected"]!.AsArray().Select(v => v!.GetValue<float>()).ToList();
        var bad = Enumerable.Range(0, values.Count).Where(i => i >= expected.Count || MathF.Abs(values[i] - expected[i]) > 0.001f).ToList();
        foreach (var i in bad.Take(10)) GD.Print($"[golden] ✗ 曲線 t={doc["samples"]![i]}：{(i < expected.Count ? expected[i] : float.NaN)} ≠ {values[i]}");
        GD.Print(bad.Count == 0 ? $"[golden] 曲線全部通過（{values.Count} 個取樣）" : $"[golden] ✗ 曲線 {bad.Count} 個不一致");
        return bad.Count == 0 ? 0 : 1;
    }

    static int RunPose(bool update)
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
    static JsonObject Compute(JsonNode c, Dictionary<string, RigData> rigs, ClipLibrary sets)
    {
        var spec = c["char"]!.GetValue<string>().Split('@');
        var rig = ContentLoader.FormRig(rigs[spec[0]], spec.Length > 1 ? spec[1] : null);
        var clips = sets.For(rig);
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

    // ---------- 次級物理（tests/golden/physics.json） ----------

    static int RunPhysics(bool update)
    {
        var doc = JsonNode.Parse(File.ReadAllText(ProjectSettings.GlobalizePath(PhysicsPath)))!;
        var (_, rigs) = ContentLoader.LoadCharacters();
        var bodies = rigs.Values.SelectMany(r => (r.Forms?.Values.Select(f => f.Data) ?? new[] { r }).Append(r)).Select(r => r.Body);
        var (_, sets) = ContentLoader.LoadAnimations(bodies);
        var results = doc["cases"]!.AsArray().Select(c => SimulatePhysics(c!, rigs, sets)).ToList();
        if (update)
        {
            doc["expected"] = new JsonArray(results.Select(r => (JsonNode)r).ToArray());
            File.WriteAllText(ProjectSettings.GlobalizePath(PhysicsPath), doc.ToJsonString(new JsonSerializerOptions { WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping }) + "\n");
            GD.Print($"[golden] 物理：已更新 {results.Count} 個案例");
            return 0;
        }
        var expected = doc["expected"]!.AsArray();
        if (expected.Count != results.Count)
        {
            GD.Print($"[golden] ✗ 物理 expected 有 {expected.Count} 個，cases 有 {results.Count} 個（先跑 --update）");
            return 1;
        }
        var failures = 0;
        for (var i = 0; i < results.Count; i++)
            foreach (var (frame, bones) in expected[i]!.AsObject())
                foreach (var (bone, value) in bones!.AsObject())
                {
                    var want = value!.GetValue<float>();
                    var got = results[i][frame]?[bone]?.GetValue<float>() ?? float.NaN;
                    if (!(MathF.Abs(want - got) <= PhysicsTolerance) && failures++ < 20)
                        GD.Print($"[golden] ✗ 物理 {Describe(doc["cases"]![i]!)} 第 {frame} 幀 {bone}：{want} ≠ {got}");
                }
        GD.Print(failures == 0 ? $"[golden] 物理全部通過（{results.Count} 個案例）" : $"[golden] ✗ 物理 {failures} 個不一致");
        return failures == 0 ? 0 : 1;
    }

    // 和 Pet.Update 一樣：取樣動作 → 骨架 → 次級物理；身體位置用 Q 版單位（縮放 1），每幀 1/60 秒
    static JsonObject SimulatePhysics(JsonNode c, Dictionary<string, RigData> rigs, ClipLibrary sets)
    {
        var spec = c["char"]!.GetValue<string>().Split('@');
        var rig = ContentLoader.FormRig(rigs[spec[0]], spec.Length > 1 ? spec[1] : null);
        var clip = sets.For(rig)[c["clip"]!.GetValue<string>()];
        var skeleton = new Skeleton(rig.Bones, rig.RootScale, rig.TrackScale);
        var spring = new Spring(rig.Bones, rig.Physics);
        int frames = c["frames"]!.GetValue<int>(), moveUntil = c["moveUntil"]!.GetValue<int>(), flipAt = c["flipAt"]!.GetValue<int>();
        var vx = c["vx"]!.GetValue<double>();
        var samples = c["samples"]!.AsArray().Select(v => v!.GetValue<int>()).ToHashSet();
        const double dt = 1.0 / 60;
        double x = 0;
        var facing = 1;
        var result = new JsonObject();
        for (var n = 0; n < frames; n++)
        {
            if (n < moveUntil) x += vx * dt;
            if (n == flipAt) facing = -facing;
            var t = (float)(n * dt % clip.Duration);
            var pose = new Dictionary<string, float>();
            foreach (var (ch, keys) in clip.Tracks) pose[ch] = Animator.SampleTrack(keys, t);
            var offsets = spring.Update(dt, skeleton.Compute(pose), x, 0, 1, facing);
            if (samples.Contains(n))
                result[n.ToString()] = new JsonObject(offsets.Select(kv => new KeyValuePair<string, JsonNode?>(kv.Key, MathF.Round(kv.Value, 4))));
        }
        return result;
    }
}
