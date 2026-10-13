namespace UniversityParking.Mobile.Core;

public sealed class PullRefreshRunner
{
    private int claimed;
    public bool IsRefreshing => Volatile.Read(ref claimed) != 0;
    public async Task<bool> RunAsync(Func<Task> load,Func<bool> busy)
    {
        if (Interlocked.CompareExchange(ref claimed,1,0) != 0) return false;
        try { if(busy())return false; await load();return true; }
        finally { Interlocked.Exchange(ref claimed,0); }
    }
}
public sealed class AutomaticPageLoadState
{
    public bool LoadedSuccessfully { get; private set; }
    public bool ShouldLoad(bool once,bool busy) => !busy && (!once || !LoadedSuccessfully);
    public void Complete(bool success) => LoadedSuccessfully=success;
}
