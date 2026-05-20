using System.Collections.Concurrent;

namespace AIRecruitment.Api.Services;

public interface IFileWatcherService
{
    Task<string?> WaitForFileAsync(string directory, string pattern, int timeoutSeconds = 60);
    void StartWatching(string directory, string pattern, Action<string> onFileDetected);
    void StopWatching(string directory);
}

public class FileWatcherService : IFileWatcherService, IDisposable
{
    private readonly ConcurrentDictionary<string, FileSystemWatcher> _watchers = new();
    private readonly ISignalRService _signalR;

    public FileWatcherService(ISignalRService signalR)
    {
        _signalR = signalR;
    }

    public Task<string?> WaitForFileAsync(string directory, string pattern, int timeoutSeconds = 60)
    {
        var tcs = new TaskCompletionSource<string?>();
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var watcher = new FileSystemWatcher(directory, pattern)
        {
            EnableRaisingEvents = false,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
        };

        FileSystemEventHandler handler = null!;
        handler = (_, e) =>
        {
            tcs.TrySetResult(e.FullPath);
            watcher.Created -= handler;
            watcher.Changed -= handler;
            watcher.Dispose();
            cts.Dispose();
        };

        watcher.Created += handler;
        watcher.Changed += handler;
        watcher.EnableRaisingEvents = true;

        cts.Token.Register(() =>
        {
            tcs.TrySetResult(null);
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
        });

        _ = _signalR.SendToAllAsync(NotificationEvents.FileDetected, new { directory, pattern, status = "watching" });

        return tcs.Task;
    }

    public void StartWatching(string directory, string pattern, Action<string> onFileDetected)
    {
        if (!Directory.Exists(directory))
            Directory.CreateDirectory(directory);

        var key = $"{directory}|{pattern}";
        if (_watchers.ContainsKey(key)) return;

        var watcher = new FileSystemWatcher(directory, pattern)
        {
            EnableRaisingEvents = true,
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite
        };

        watcher.Created += (_, e) =>
        {
            onFileDetected(e.FullPath);
            _ = _signalR.SendToAllAsync(NotificationEvents.FileDetected, new { directory, pattern, file = e.FullPath });
        };

        _watchers[key] = watcher;
    }

    public void StopWatching(string directory)
    {
        var keys = _watchers.Keys.Where(k => k.StartsWith(directory)).ToList();
        foreach (var key in keys)
        {
            if (_watchers.TryRemove(key, out var watcher))
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
        }
    }

    public void Dispose()
    {
        foreach (var w in _watchers.Values)
        {
            w.EnableRaisingEvents = false;
            w.Dispose();
        }
        _watchers.Clear();
    }
}
