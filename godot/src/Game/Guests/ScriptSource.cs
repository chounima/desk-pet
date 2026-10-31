// 測試用的假來源：--guests=join:alice@sera,speak:alice,chat:alice:嗨,quiet:alice,mute:alice,react:alice:🎉,leave:alice
// 每一步間隔 1 秒（wait 空一拍）。join 後面的 @角色 指定要用哪個角色。
using System.Threading;
using System.Threading.Tasks;

namespace DeskPet.Game.Guests;

public sealed class ScriptSource : PresenceSource
{
    readonly string[] steps;
    CancellationTokenSource? cts;

    public ScriptSource(string spec) => steps = spec.Split(',');

    public override string Id => "script";

    public override void Start()
    {
        cts = new CancellationTokenSource();
        var token = cts.Token;
        Task.Run(async () =>
        {
            await Task.Delay(2000, token);
            foreach (var step in steps)
            {
                if (Parse(step) is { } e) Emit(e);
                Status = step;
                await Task.Delay(1000, token);
            }
            Status = "done";
        }, token);
    }

    public override void Stop() => cts?.Cancel();

    PresenceEvent? Parse(string step)
    {
        var parts = step.Split(':', 3);
        if (parts.Length < 2) return null;
        var (verb, who) = (parts[0], parts[1]);
        var text = parts.Length > 2 ? parts[2] : "";
        var hint = "";
        if (who.Split('@') is [var n, var c]) (who, hint) = (n, c);
        return verb switch
        {
            "join" => new(Id, PresenceKind.Join, who, who, hint),
            "leave" => new(Id, PresenceKind.Leave, who),
            "speak" => new(Id, PresenceKind.Speaking, who),
            "quiet" => new(Id, PresenceKind.Silent, who),
            "mute" => new(Id, PresenceKind.Mute, who),
            "unmute" => new(Id, PresenceKind.Unmute, who),
            "chat" => new(Id, PresenceKind.Chat, who, who, text),
            "react" => new(Id, PresenceKind.Reaction, who, who, text),
            _ => null,
        };
    }
}
