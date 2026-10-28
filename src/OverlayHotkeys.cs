// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace EftPosition
{
    public sealed class OverlayHotkeys : NativeWindow,IDisposable
    {
        const int ToggleId=0x4546,EditId=0x4547,BackupId=0x4548;
        readonly Action toggle,edit;
        readonly Timer poll=new Timer {Interval=20};
        readonly Func<Keys,bool> keyDown;
        readonly Keys chordKey;
        bool mHeld,f8Held,disposed;
        DateTime nextRetry=DateTime.MinValue;
        public bool ToggleAvailable {get;private set;}
        public bool EditAvailable {get;private set;}
        public bool BackupAvailable {get;private set;}
        public int ToggleError {get;private set;}
        public int EditError {get;private set;}
        [DllImport("user32.dll",SetLastError=true)]static extern bool RegisterHotKey(IntPtr window,int id,uint modifiers,uint key);
        [DllImport("user32.dll")]static extern bool UnregisterHotKey(IntPtr window,int id);
        [DllImport("user32.dll")]static extern short GetAsyncKeyState(int key);
        public OverlayHotkeys(Action toggleAction,Action editAction):this(toggleAction,editAction,null,Keys.M){}
        internal OverlayHotkeys(Action toggleAction,Action editAction,Func<Keys,bool> reader,Keys mainKey)
        {
            toggle=toggleAction;edit=editAction;
            chordKey=mainKey;
            keyDown=reader??delegate(Keys key){return (GetAsyncKeyState((int)key)&0x8000)!=0;};
            CreateHandle(new CreateParams {Caption="EFT Position hotkeys",Parent=new IntPtr(-3)});
            RegisterMissing();poll.Tick+=delegate{PollKeys();};poll.Start();
        }
        void RegisterMissing()
        {
            if(!ToggleAvailable){ToggleAvailable=RegisterHotKey(Handle,ToggleId,0x4000|0x0002|0x0001,(uint)chordKey);ToggleError=ToggleAvailable?0:Marshal.GetLastWin32Error();}
            if(!EditAvailable){EditAvailable=RegisterHotKey(Handle,EditId,0x4000|0x0002|0x0001|0x0004,(uint)chordKey);EditError=EditAvailable?0:Marshal.GetLastWin32Error();}
            if(!BackupAvailable)BackupAvailable=RegisterHotKey(Handle,BackupId,0x4000,(uint)Keys.F8);
            nextRetry=DateTime.UtcNow.AddSeconds(5);
        }
        internal void PollKeys()
        {
            if(Handle==IntPtr.Zero)return;
            if(DateTime.UtcNow>=nextRetry)RegisterMissing();
            bool m=keyDown(chordKey),f8=keyDown(Keys.F8);
            bool control=keyDown(Keys.ControlKey),alt=keyDown(Keys.Menu),shift=keyDown(Keys.ShiftKey),win=keyDown(Keys.LWin)||keyDown(Keys.RWin);
            if(m && !mHeld && control && alt && !win)
            {mHeld=true;if(shift){if(EditAvailable)edit();}else if(ToggleAvailable)toggle();}
            mHeld=m;
            if(f8 && !f8Held && !control && !alt && !shift && !win && BackupAvailable){f8Held=true;toggle();}
            f8Held=f8;
        }
        public string Status
        {get{return "Ctrl+Alt+M: "+UiText.T(ToggleAvailable?"aktywny":"zajęty / niedostępny")+" · F8: "+UiText.T(BackupAvailable?"aktywny":"zajęty / niedostępny")+
            "\nCtrl+Alt+Shift+M: "+UiText.T(EditAvailable?"aktywny":"zajęty / niedostępny");}}
        protected override void WndProc(ref Message message)
        {
            if(message.Msg==0x312)
            {
                int id=message.WParam.ToInt32();
                if(id==ToggleId && ToggleAvailable && !mHeld){mHeld=true;toggle();}
                else if(id==EditId && EditAvailable && !mHeld){mHeld=true;edit();}
                else if(id==BackupId && BackupAvailable && !f8Held){f8Held=true;toggle();}
                message.Result=IntPtr.Zero;return;
            }
            base.WndProc(ref message);
        }
        public void Dispose()
        {
            if(disposed)return;disposed=true;
            poll.Stop();poll.Dispose();if(Handle==IntPtr.Zero)return;
            if(ToggleAvailable)UnregisterHotKey(Handle,ToggleId);if(EditAvailable)UnregisterHotKey(Handle,EditId);
            if(BackupAvailable)UnregisterHotKey(Handle,BackupId);
            ToggleAvailable=false;EditAvailable=false;BackupAvailable=false;DestroyHandle();
        }
    }
}
