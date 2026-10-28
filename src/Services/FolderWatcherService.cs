using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.UI.Dispatching;
using Resona.Models;

namespace Resona.Services;

/// <summary>
/// Surveille en arrière-plan les dossiers musicaux de l'utilisateur (FileSystemWatcher).
/// Ajoute, met à jour ou supprime automatiquement les pistes dans la bibliothèque et le cache SQLite
/// dès qu'un fichier audio est ajouté, modifié, renommé ou supprimé, sans devoir rescanner toute la bibliothèque.
/// </summary>
public sealed class FolderWatcherService : IDisposable
{
    private readonly List<FileSystemWatcher> _watchers = new();
    private readonly object _lock = new();
    private readonly ConcurrentDictionary<string, PendingChange> _pendingChanges = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _coverDir;
    private Timer? _debounceTimer;
    private bool _isPaused;
    private bool _isDisposed;

    private sealed class PendingChange
    {
        public string Path { get; set; }
        public string? OldPath { get; set; }
        public WatcherChangeTypes ChangeType { get; set; }
        public bool IsDirectory { get; set; }
        public DateTime Timestamp { get; set; }
        public int RetryCount { get; set; }

        public PendingChange(string path, string? oldPath, WatcherChangeTypes changeType, bool isDirectory)
        {
            Path = path;
            OldPath = oldPath;
            ChangeType = changeType;
            IsDirectory = isDirectory;
            Timestamp = DateTime.UtcNow;
        }
    }

    public FolderWatcherService()
    {
        _coverDir = AppPaths.CoversDir;
        try { Directory.CreateDirectory(_coverDir); } catch { }
    }

    /// <summary>Démarre la surveillance sur tous les dossiers configurés.</summary>
    public void Start()
    {
        lock (_lock)
        {
            if (_isDisposed) return;
            StopWatchersLocked();

            if (App.Settings?.Current == null || !App.Settings.Current.WatchFoldersEnabled)
                return;

            List<string> folders = GetDistinctFoldersToWatch();
            foreach (string folder in folders)
            {
                try
                {
                    if (!Directory.Exists(folder)) continue;

                    var watcher = new FileSystemWatcher(folder)
                    {
                        IncludeSubdirectories = true,
                        NotifyFilter = NotifyFilters.FileName | NotifyFilters.DirectoryName | NotifyFilters.LastWrite | NotifyFilters.Size,
                        InternalBufferSize = 65536 // 64 Ko pour résister aux copies massives
                    };

                    watcher.Created += (s, e) => EnqueueChange(e.FullPath, null, WatcherChangeTypes.Created);
                    watcher.Changed += (s, e) => EnqueueChange(e.FullPath, null, WatcherChangeTypes.Changed);
                    watcher.Deleted += (s, e) => EnqueueChange(e.FullPath, null, WatcherChangeTypes.Deleted);
                    watcher.Renamed += (s, e) => EnqueueChange(e.FullPath, e.OldFullPath, WatcherChangeTypes.Renamed);
                    watcher.Error += (s, e) => LogService.Write("FolderWatcher", e.GetException(), $"Erreur watcher sur {folder}");

                    watcher.EnableRaisingEvents = true;
                    _watchers.Add(watcher);
                }
                catch (Exception ex)
                {
                    LogService.Write("FolderWatcher", ex, $"Impossible de surveiller {folder}");
                }
            }
        }
    }

    /// <summary>Arrête tous les watchers actifs.</summary>
    public void Stop()
    {
        lock (_lock)
        {
            StopWatchersLocked();
            _debounceTimer?.Dispose();
            _debounceTimer = null;
            _pendingChanges.Clear();
        }
    }

    /// <summary>Rafraîchit la liste des dossiers surveillés (ex: ajout/suppression de dossier dans les Paramètres).</summary>
    public void RefreshWatchedFolders()
    {
        Start();
    }

    /// <summary>Suspend temporairement les notifications (ex: pendant un scan complet explicite).</summary>
    public void Pause()
    {
        _isPaused = true;
    }

    /// <summary>Reprend la surveillance après une pause.</summary>
    public void Resume()
    {
        _isPaused = false;
    }

    private void StopWatchersLocked()
    {
        foreach (var watcher in _watchers)
        {
            try
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            catch { }
        }
        _watchers.Clear();
    }

    private static List<string> GetDistinctFoldersToWatch()
    {
        var raw = new List<string>(App.Settings?.Current?.MusicFolders ?? new List<string>());
        string? dl = App.Settings?.Current?.DownloadFolder;
        if (!string.IsNullOrWhiteSpace(dl) && Directory.Exists(dl))
        {
            raw.Add(dl);
        }

        var valid = raw
            .Where(f => !string.IsNullOrWhiteSpace(f) && Directory.Exists(f))
            .Select(f => Path.GetFullPath(f).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(f => f.Length)
            .ToList();

        var result = new List<string>();
        foreach (string folder in valid)
        {
            bool coveredByParent = result.Any(parent =>
                folder.StartsWith(parent + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
            if (!coveredByParent)
            {
                result.Add(folder);
            }
        }
        return result;
    }

    private void EnqueueChange(string fullPath, string? oldPath, WatcherChangeTypes changeType)
    {
        if (_isPaused || _isDisposed || (App.Settings?.Current != null && !App.Settings.Current.WatchFoldersEnabled))
            return;

        bool isDir = Directory.Exists(fullPath) || (changeType == WatcherChangeTypes.Deleted && !Path.HasExtension(fullPath));

        if (!isDir)
        {
            bool isAudio = LibraryScannerService.IsSupportedAudio(fullPath);
            bool oldIsAudio = oldPath != null && LibraryScannerService.IsSupportedAudio(oldPath);
            if (!isAudio && !oldIsAudio) return;

            var excluded = App.Settings?.Current?.ExcludedFilePaths;
            if (excluded != null && excluded.Contains(fullPath, StringComparer.OrdinalIgnoreCase)) return;
        }

        var change = new PendingChange(fullPath, oldPath, changeType, isDir);
        _pendingChanges[fullPath] = change;

        lock (_lock)
        {
            _debounceTimer?.Dispose();
            _debounceTimer = new Timer(OnDebounceTimerFired, null, 800, Timeout.Infinite);
        }
    }

    private void OnDebounceTimerFired(object? state)
    {
        if (_isPaused || _isDisposed) return;

        var changes = new List<PendingChange>();
        foreach (string key in _pendingChanges.Keys.ToList())
        {
            if (_pendingChanges.TryRemove(key, out var change))
            {
                changes.Add(change);
            }
        }

        if (changes.Count == 0) return;

        _ = Task.Run(async () =>
        {
            try
            {
                await ProcessChangesAsync(changes);
            }
            catch (Exception ex)
            {
                LogService.Write("FolderWatcher", ex, "Erreur traitement modifications dossiers");
            }
        });
    }

    private async Task ProcessChangesAsync(List<PendingChange> changes)
    {
        var deletedFilePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var filesToProcess = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var retriesNeeded = new List<PendingChange>();

        foreach (var change in changes)
        {
            if (change.ChangeType == WatcherChangeTypes.Deleted)
            {
                if (change.IsDirectory)
                {
                    string dirPrefix = change.Path.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                    var tracks = App.MainWindowInstance?.GetLibrarySnapshot() ?? new List<Track>();
                    foreach (var t in tracks)
                    {
                        if (t.FilePath.StartsWith(dirPrefix, StringComparison.OrdinalIgnoreCase))
                        {
                            deletedFilePaths.Add(t.FilePath);
                        }
                    }
                }
                else
                {
                    deletedFilePaths.Add(change.Path);
                }
            }
            else if (change.ChangeType == WatcherChangeTypes.Renamed)
            {
                if (change.OldPath != null)
                {
                    if (change.IsDirectory)
                    {
                        string oldPrefix = change.OldPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                        var tracks = App.MainWindowInstance?.GetLibrarySnapshot() ?? new List<Track>();
                        foreach (var t in tracks)
                        {
                            if (t.FilePath.StartsWith(oldPrefix, StringComparison.OrdinalIgnoreCase))
                            {
                                deletedFilePaths.Add(t.FilePath);
                            }
                        }
                    }
                    else
                    {
                        deletedFilePaths.Add(change.OldPath);
                    }
                }

                if (change.IsDirectory)
                {
                    if (Directory.Exists(change.Path))
                    {
                        foreach (string file in App.Scanner.EnumerateAudioFiles(change.Path))
                        {
                            filesToProcess.Add(file);
                        }
                    }
                }
                else
                {
                    if (File.Exists(change.Path)) filesToProcess.Add(change.Path);
                }
            }
            else // Created ou Changed
            {
                if (change.IsDirectory)
                {
                    if (Directory.Exists(change.Path))
                    {
                        foreach (string file in App.Scanner.EnumerateAudioFiles(change.Path))
                        {
                            filesToProcess.Add(file);
                        }
                    }
                }
                else
                {
                    if (File.Exists(change.Path)) filesToProcess.Add(change.Path);
                }
            }
        }

        // Pour les fichiers ajoutés / modifiés, on extrait les métadonnées dès qu'ils sont prêts
        var newOrUpdatedTracks = new List<Track>();
        foreach (string filePath in filesToProcess)
        {
            if (deletedFilePaths.Contains(filePath)) continue;

            if (!WaitForFileReady(filePath, 2500))
            {
                // Fichier encore verrouillé (ex: gros fichier encore en cours d'écriture)
                retriesNeeded.Add(new PendingChange(filePath, null, WatcherChangeTypes.Changed, false)
                {
                    RetryCount = 1
                });
                continue;
            }

            try
            {
                Track? track = App.Scanner.ExtractMetadata(filePath, out byte[] embeddedCover);
                if (track != null)
                {
                    if (embeddedCover != null && embeddedCover.Length > 0)
                    {
                        string cacheKey = CoverArtService.GetAlbumCacheKey(
                            !string.IsNullOrWhiteSpace(track.AlbumArtist) ? track.AlbumArtist : track.Artist,
                            track.Album,
                            track.Id);
                        string coverPath = Path.Combine(_coverDir, cacheKey + ".jpg");
                        if (!File.Exists(coverPath))
                        {
                            File.WriteAllBytes(coverPath, embeddedCover);
                        }
                        track.CoverArtPath = coverPath;
                    }
                    newOrUpdatedTracks.Add(track);
                }
            }
            catch (Exception ex)
            {
                LogService.Write("FolderWatcher", ex, $"Erreur extraction {filePath}");
            }
        }

        // Re-planifier les retries si nécessaire
        if (retriesNeeded.Count > 0)
        {
            foreach (var r in retriesNeeded)
            {
                if (r.RetryCount <= 4)
                {
                    r.RetryCount++;
                    _pendingChanges[r.Path] = r;
                }
            }
            lock (_lock)
            {
                _debounceTimer?.Dispose();
                _debounceTimer = new Timer(OnDebounceTimerFired, null, 1500, Timeout.Infinite);
            }
        }

        if (deletedFilePaths.Count == 0 && newOrUpdatedTracks.Count == 0)
            return;

        // 1) Mise à jour de la base de données SQLite en arrière-plan
        try
        {
            if (deletedFilePaths.Count > 0)
            {
                await App.Cache.DeleteTracksByPathsAsync(deletedFilePaths);
            }
            if (newOrUpdatedTracks.Count > 0)
            {
                await App.Cache.UpsertTracksBatchedAsync(newOrUpdatedTracks);
            }
        }
        catch (Exception ex)
        {
            LogService.Write("FolderWatcher", ex, "Erreur mise à jour cache SQLite");
        }

        // 2) Mise à jour de la bibliothèque en mémoire et de l'interface graphique sur le thread UI
        var mainWindow = App.MainWindowInstance;
        if (mainWindow != null)
        {
            mainWindow.DispatcherQueue.TryEnqueue(() =>
            {
                try
                {
                    if (deletedFilePaths.Count > 0)
                    {
                        mainWindow.RemoveTracksFromLibrary(deletedFilePaths);
                    }
                    if (newOrUpdatedTracks.Count > 0)
                    {
                        mainWindow.AddTracksToLibrary(newOrUpdatedTracks);
                    }
                }
                catch (Exception ex)
                {
                    LogService.Write("FolderWatcher", ex, "Erreur application interface UI");
                }
            });
        }
    }

    private static bool WaitForFileReady(string filePath, int timeoutMs = 2500)
    {
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            try
            {
                if (!File.Exists(filePath)) return false;
                using var fs = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (fs.Length > 0) return true;
            }
            catch (IOException)
            {
                // Fichier encore verrouillé par le processus d'écriture
            }
            catch
            {
                return false;
            }
            Thread.Sleep(120);
        }
        return false;
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _isDisposed = true;
            StopWatchersLocked();
            _debounceTimer?.Dispose();
            _debounceTimer = null;
            _pendingChanges.Clear();
        }
    }
}
