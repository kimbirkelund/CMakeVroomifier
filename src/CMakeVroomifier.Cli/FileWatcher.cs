using System.Reactive.Disposables;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Text.RegularExpressions;

namespace CMakeVroomifier.Cli;

public class FileWatcher : IDisposable
{
    private readonly string _basePath;
    private readonly Subject<string> _changesSubject = new();
    private readonly CompositeDisposable _disposables;
    private readonly Regex[] _excludeRegexes;
    private readonly Regex[] _includeRegexes;
    private readonly FileSystemWatcher _watcher;

    public IObservable<string> Changes => _changesSubject.AsObservable();

    public FileWatcher(string path, string[]? include = null, string[]? exclude = null)
    {
        _basePath = Path.GetFullPath(path);
        _includeRegexes = [..include?.Select(GlobRegexFactory.Create) ?? []];
        _excludeRegexes = [..exclude?.Select(GlobRegexFactory.Create) ?? []];

        _watcher = new FileSystemWatcher(_basePath)
                   {
                       IncludeSubdirectories = true,
                       EnableRaisingEvents = true
                   };

        _watcher.Changed += OnChanged;
        _watcher.Created += OnChanged;
        _watcher.Deleted += OnChanged;
        _watcher.Renamed += OnChanged;

        _disposables = [_watcher, _changesSubject];
    }

    public void Dispose()
    {
        _disposables.Dispose();
    }

    private void OnChanged(object sender, FileSystemEventArgs e)
    {
        var rel = Path.GetRelativePath(_basePath, e.FullPath);

        if (_excludeRegexes.Any(r => r.IsMatch(rel)))
            return;
        if (!_includeRegexes.Any(r => r.IsMatch(rel)))
            return;

        _changesSubject.OnNext(rel);
    }
}
