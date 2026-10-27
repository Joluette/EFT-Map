// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace EftPosition
{
    // Only snapshots explicitly acknowledged by the map are eligible. Never scans a folder.
    public sealed class ScreenshotCleaner : IDisposable
    {
        readonly object gate=new object();
        readonly string folder;
        readonly List<Entry> pending=new List<Entry>();
        readonly Timer timer;
        bool disposed;
        int deleted,failed;
        sealed class Entry { internal Snapshot Snapshot; internal DateTime Queued; }
        public int Deleted {get {lock(gate)return deleted;}}
        public int Failed {get {lock(gate)return failed;}}
        public ScreenshotCleaner(string path)
        {folder=Path.GetFullPath(path);timer=new Timer(Flush,null,250,250);}
        public void ConfirmDisplayed(Snapshot snapshot)
        {
            if(snapshot==null)return;
            lock(gate)
            {
                if(disposed)return;
                foreach(Entry e in pending)if(e.Snapshot==snapshot)return;
                if(pending.Count>=32){failed++;return;}
                pending.Add(new Entry {Snapshot=snapshot,Queued=DateTime.UtcNow});
            }
        }
        void Flush(object unused)
        {
            lock(gate)
            {
                if(disposed)return;
                for(int i=pending.Count-1;i>=0;i--)
                {
                    Entry e=pending[i];
                    // Created may arrive while EFT is still writing the PNG.
                    if((DateTime.UtcNow-e.Queued).TotalMilliseconds<500)continue;
                    bool removed;
                    int result=TryDelete(folder,e.Snapshot,out removed);
                    if(result==0 && (DateTime.UtcNow-e.Queued).TotalSeconds<15)continue;
                    pending.RemoveAt(i);
                    if(removed)deleted++;else if(result!=1)failed++;
                }
            }
        }
        // 1 = finished; 0 = temporarily locked; -1 = identity changed/unsafe.
        internal static int TryDelete(string directory,Snapshot snapshot,out bool removed)
        {
            removed=false;
            string name=snapshot.Position.FileName;
            Position parsed;
            if(Path.GetFileName(name)!=name || !ScreenshotParser.TryParse(name,out parsed))return -1;
            string path=Path.Combine(directory,name);
            // An exclusive handle prevents replacement between the identity check and deletion.
            // OPEN_REPARSE_POINT avoids following a link, and disposition targets this handle.
            using(SafeFileHandle handle=CreateFile(path,0x10000|0x80,0,IntPtr.Zero,3,0x00200000,IntPtr.Zero))
            {
                if(handle.IsInvalid)
                {
                    int error=Marshal.GetLastWin32Error();
                    return error==2 || error==3?1:error==32 || error==33?0:-1;
                }
                FileInfoByHandle info;
                if(!GetFileInformationByHandle(handle,out info))return -1;
                if((info.Attributes & (0x10|0x400))!=0 ||
                    Time(info.Written)!=snapshot.FileTimeUtc.ToFileTimeUtc() ||
                    Time(info.Created)!=snapshot.CreationTimeUtc.ToFileTimeUtc())return -1;
                Disposition disposition=new Disposition {Delete=true};
                if(!SetFileInformationByHandle(handle,4,ref disposition,1))return -1;
                removed=true;return 1;
            }
        }
        static long Time(System.Runtime.InteropServices.ComTypes.FILETIME time)
        {return ((long)(uint)time.dwHighDateTime<<32)|(uint)time.dwLowDateTime;}
        [StructLayout(LayoutKind.Sequential)]
        struct FileInfoByHandle
        {
            internal uint Attributes;
            internal System.Runtime.InteropServices.ComTypes.FILETIME Created,Accessed,Written;
            internal uint Volume,SizeHigh,SizeLow,Links,IndexHigh,IndexLow;
        }
        [StructLayout(LayoutKind.Sequential)]
        struct Disposition {[MarshalAs(UnmanagedType.U1)]internal bool Delete;}
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]
        static extern SafeFileHandle CreateFile(string name,uint access,uint share,IntPtr security,uint creation,uint flags,IntPtr template);
        [DllImport("kernel32.dll",SetLastError=true)]
        [return:MarshalAs(UnmanagedType.Bool)]
        static extern bool GetFileInformationByHandle(SafeFileHandle handle,out FileInfoByHandle info);
        [DllImport("kernel32.dll",SetLastError=true)]
        [return:MarshalAs(UnmanagedType.Bool)]
        static extern bool SetFileInformationByHandle(SafeFileHandle handle,int type,ref Disposition info,uint size);
        public void Dispose()
        {lock(gate){if(disposed)return;disposed=true;timer.Dispose();pending.Clear();}}
    }
}
