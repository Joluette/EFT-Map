// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace EftPosition
{
    // One current snapshot + a bounded duplicate cache. No images or history are read.
    public sealed class FolderMonitor : IDisposable
    {
        readonly object gate = new object();
        readonly string folder;
        readonly Dictionary<string, long> seen = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
        readonly Queue<string> seenOrder = new Queue<string>();
        readonly Dictionary<string, DateTime> pending = new Dictionary<string, DateTime>(StringComparer.OrdinalIgnoreCase);
        FileSystemWatcher watcher;
        Timer recovery, eventTimer;
        Snapshot latest;
        bool disposed, scanning, rescanRequested;
        long accepted, ignored, notifications;
        string status = "Łączenie z folderem…";
        public FolderMonitor(string path)
        {
            folder = Path.GetFullPath(path);
            if (!Directory.Exists(folder)) throw new DirectoryNotFoundException("Folder nie istnieje.");
            try { lock (gate) AttachWatcher(); }
            catch { if (watcher != null) watcher.Dispose(); throw; }
            recovery = new Timer(Recover, null, 2000, 2000);
            eventTimer = new Timer(FlushPending, null, 100, 100);
            RequestRescan();
        }
        public string Folder { get { return folder; } }
        public Snapshot Latest { get { lock(gate) return latest; } }
        public string Status { get { lock(gate) return status; } }
        public long Accepted { get { lock(gate) return accepted; } }
        public long Ignored { get { lock(gate) return ignored; } }
        public long Notifications { get { lock(gate) return notifications; } }
        public int CacheCount { get { lock(gate) return seen.Count; } }

        void AttachWatcher()
        {
            if (watcher != null) watcher.Dispose();
            watcher = new FileSystemWatcher(folder, "*.png");
            watcher.IncludeSubdirectories = false;
            watcher.NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite;
            watcher.InternalBufferSize = 16384;
            watcher.Created += OnFile;
            watcher.Changed += OnFile;
            watcher.Renamed += OnRenamed;
            watcher.Error += OnError;
            watcher.EnableRaisingEvents = true;
            status = "Obserwuję folder • tylko PNG";
        }
        void OnFile(object sender, FileSystemEventArgs e) { QueueFile(e.FullPath); }
        void OnRenamed(object sender, RenamedEventArgs e) { QueueFile(e.FullPath); }
        void QueueFile(string path)
        {
            lock(gate)
            {
                if (disposed) return;
                // Briefly debounce metadata updates (Created can precede final LastWrite).
                if (pending.Count < 256 || pending.ContainsKey(path)) pending[path] = DateTime.UtcNow.AddMilliseconds(150);
                else rescanRequested = true;
            }
        }
        void FlushPending(object unused)
        {
            List<string> ready = new List<string>();
            lock(gate)
            {
                if (disposed) return;
                DateTime now = DateTime.UtcNow;
                foreach(var item in pending) if (item.Value <= now) ready.Add(item.Key);
                foreach(string path in ready) pending.Remove(path);
            }
            foreach(string path in ready) Consider(path);
        }
        void OnError(object sender, ErrorEventArgs e)
        {
            lock(gate)
            {
                if (disposed) return;
                status = "Przerwa w obserwacji • ponawiam połączenie…";
                if (watcher != null) { watcher.Dispose(); watcher = null; }
                rescanRequested = true;
            }
        }
        void Consider(string path)
        {
            try
            {
                FileInfo f = new FileInfo(path);
                if (!String.Equals(f.Extension, ".png", StringComparison.OrdinalIgnoreCase) || !f.Exists ||
                    (f.Attributes & FileAttributes.Directory) != 0) return;
                string name = f.Name;
                DateTime fileTime = f.LastWriteTimeUtc;
                Position parsed;
                bool valid = ScreenshotParser.TryParse(name, out parsed);
                lock(gate)
                {
                    if (disposed) return;
                    notifications++;
                    long old;
                    if (seen.TryGetValue(name, out old) && old == fileTime.Ticks) return;
                    if (!seen.ContainsKey(name))
                    {
                        if (seen.Count >= 256) seen.Remove(seenOrder.Dequeue());
                        seenOrder.Enqueue(name);
                    }
                    seen[name] = fileTime.Ticks;
                    if (!valid) { ignored++; return; }
                    Snapshot candidate = new Snapshot(parsed, fileTime, f.CreationTimeUtc, DateTime.UtcNow);
                    if (candidate.IsNewerThan(latest)) { latest = candidate; accepted++; }
                }
            }
            catch (IOException) { /* A file may disappear between notification and metadata read. */ }
            catch (UnauthorizedAccessException) { /* Inaccessible individual files do not stop the monitor. */ }
            catch (ArgumentException) { }
        }
        public void RequestRescan()
        {
            lock(gate)
            {
                if (disposed) return;
                if (scanning) { rescanRequested = true; return; }
                scanning = true;
            }
            ThreadPool.QueueUserWorkItem(delegate
            {
                try
                {
                    // EnumerateFiles streams names, unlike GetFiles/ToList/OrderBy.
                    foreach(string file in Directory.EnumerateFiles(folder, "*.png", SearchOption.TopDirectoryOnly))
                    {
                        lock(gate) { if (disposed) return; }
                        Consider(file);
                    }
                }
                catch (IOException) { lock(gate) { status = "Folder niedostępny • ponawiam…"; rescanRequested = true; } }
                catch (UnauthorizedAccessException) { lock(gate) { status = "Brak dostępu do folderu • sprawdź uprawnienia."; rescanRequested = true; } }
                finally { lock(gate) scanning = false; }
            });
        }
        void Recover(object unused)
        {
            bool scan = false;
            lock(gate)
            {
                if (disposed) return;
                if (!Directory.Exists(folder))
                {
                    if (watcher != null) { watcher.Dispose(); watcher = null; }
                    status = "Folder zniknął • czekam na jego powrót…";
                    return;
                }
                if (watcher == null)
                {
                    try { AttachWatcher(); scan = true; }
                    catch (IOException) { status = "Nie mogę otworzyć folderu • ponawiam…"; return; }
                    catch (UnauthorizedAccessException) { status = "Brak dostępu do folderu • ponawiam…"; return; }
                    catch (ArgumentException) { status = "Nie mogę obserwować folderu • ponawiam…"; return; }
                }
                if (rescanRequested) { scan = true; rescanRequested = false; }
            }
            if (scan) RequestRescan();
        }
        public void Dispose()
        {
            lock(gate)
            {
                if (disposed) return;
                disposed = true;
                if (watcher != null) { watcher.Dispose(); watcher = null; }
                if (recovery != null) { recovery.Dispose(); recovery = null; }
                if (eventTimer != null) { eventTimer.Dispose(); eventTimer = null; }
                pending.Clear();
            }
        }
    }
}
