// 單一實例：已經有一隻桌寵在跑時，新啟動的就直接結束（避免桌面上出現兩隻）
using System.Threading;

namespace DeskPet.Platform;

public static class SingleInstance
{
    const string Name = @"Local\DeskPet.SingleInstance";

    static Mutex? mutex;     // 保持參考，程式結束前都持有

    /// <summary>取得單一實例鎖；回傳 false 表示已經有另一隻在跑</summary>
    public static bool TryAcquire()
    {
        mutex = new Mutex(initiallyOwned: true, Name, out var createdNew);
        if (createdNew) return true;
        mutex.Dispose();
        mutex = null;
        return false;
    }

    public static void Release()
    {
        mutex?.ReleaseMutex();
        mutex?.Dispose();
        mutex = null;
    }
}
