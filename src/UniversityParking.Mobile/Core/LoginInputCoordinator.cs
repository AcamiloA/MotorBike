namespace UniversityParking.Mobile.Core;

public interface ILoginInputLifecycle { Task PrepareAsync(); }

// Page-owned focus handling, independent of authentication and testable without an IME.
public sealed class LoginInputCoordinator : ILoginInputLifecycle
{
    private object? owner;
    private Func<Task>? release;
    public void Attach(object page, Func<Task> action) { owner = page; release = action; }
    public void Detach(object page) { if (ReferenceEquals(owner, page)) { owner = null; release = null; } }
    public Task PrepareAsync() => release?.Invoke() ?? Task.CompletedTask;
}
