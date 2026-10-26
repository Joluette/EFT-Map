// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Diagnostics;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Text;
using System.Windows.Forms;

namespace EftPosition
{
    public sealed class MainForm : Form
    {
        readonly Color muted = Color.FromArgb(151,169,175);
        readonly Color accent = Color.FromArgb(74,231,206);
        readonly MapCanvas canvas;
        readonly TextBox folderBox;
        readonly Label statusLabel, timeLabel, ageLabel, coordsLabel, headingLabel, fileLabel, countsLabel;
        readonly Timer timer = new Timer();
        readonly Stopwatch elapsed = Stopwatch.StartNew();
        readonly StreamWriter telemetry;
        readonly Process ownProcess = Process.GetCurrentProcess();
        readonly int exitAfter;
        readonly bool exercise;
        readonly bool cycleMaps;
        readonly bool isDemo;
        readonly string assetsDirectory,cacheDirectory;
        readonly MapDefinition[] maps;
        readonly ComboBox mapBox,layerBox;
        readonly Label mapSubtitle;
        readonly Panel header,sidebar;
        readonly FlowLayoutPanel selectors,toolbar;
        readonly Label mapHint,navigationLabel;
        readonly CheckBox autoNavigation;
        readonly Button fullScreenButton;
        AutoNavigator navigator;
        string navigatorMapId;
        WorldBounds pendingFocus,visualFocus;
        bool pendingFit,fullScreen;
        Rectangle savedBounds;
        Size savedMinimum;
        FormWindowState savedWindowState;
        public bool IsFullScreen { get { return fullScreen; } }
        readonly BackgroundWorker mapWorker=new BackgroundWorker { WorkerSupportsCancellation=true };
        volatile string mapProgress;
        MapDefinition requestedMap;
        MapLayer requestedLayer;
        int mapGeneration,completedMapLoads;
        double nextMapCycle=3;
        readonly string previewPath;
        bool previewWritten;
        readonly CheckBox follow;
        readonly Font titleFont = new Font("Segoe UI",18,FontStyle.Bold);
        readonly Font valueFont = new Font("Segoe UI",15,FontStyle.Bold);
        FolderMonitor monitor;
        Snapshot displayed;
        DateTime nextTelemetry = DateTime.MinValue;
        int viewStep;
        bool oldStale;

        public MainForm(string folder, string telemetryPath, int duration, bool exerciseView, bool demo, string preview, string cache=null, bool cycle=false, bool startFullScreen=false)
        {
            Text = "EFT Position · 13 map" + (demo ? " · DANE PRZYKŁADOWE" : "");
            MinimumSize = new Size(950,650);
            ClientSize = new Size(1250,760);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = Color.FromArgb(15,23,27);
            ForeColor = Color.FromArgb(232,239,238);
            Font = new Font("Segoe UI",10);
            AutoScaleMode = AutoScaleMode.Dpi;
            exitAfter = duration;
            exercise = exerciseView;
            cycleMaps=cycle; isDemo=demo;
            assetsDirectory=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets");
            cacheDirectory=cache??Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"cache");
            maps=MapCatalog.Load(assetsDirectory);
            previewPath = preview;
            if (!String.IsNullOrEmpty(telemetryPath))
            {
                telemetry = new StreamWriter(telemetryPath,false,new UTF8Encoding(false));
                telemetry.AutoFlush = true;
                telemetry.WriteLine("elapsedSeconds,workingSetBytes,privateBytes,peakWorkingSetBytes,handles,accepted,ignored,cacheEntries,latestX,latestZ,latestName,mapId,layerId,mapLoads,fullscreen,vector");
            }

            header = new Panel { Dock=DockStyle.Top, Height=78, BackColor=Color.FromArgb(22,32,37) };
            Label brand = new Label { Text="EFT  /  POSITION", Font=titleFont, Location=new Point(24,15), AutoSize=true };
            mapSubtitle = new Label { Text="13 MAP   ·   WYBÓR Z MENU", ForeColor=muted, Location=new Point(28,49), AutoSize=true, Font=new Font("Segoe UI",8,FontStyle.Bold) };
            Label badge = new Label { Text= demo ? "DANE PRZYKŁADOWE" : "POZYCJA Z OSTATNIEGO ZRZUTU", ForeColor=accent, AutoSize=true, Anchor=AnchorStyles.Top|AnchorStyles.Right, Location=new Point(910,53), Font=new Font("Segoe UI",9,FontStyle.Bold) };
            fullScreenButton=MakeButton("Pełny ekran (F11)",1050,10,165);
            fullScreenButton.AccessibleName="Pełny ekran";
            fullScreenButton.Click+=delegate { SetFullScreen(!fullScreen); };
            header.Controls.AddRange(new Control[]{brand,mapSubtitle,badge,fullScreenButton});
            header.Resize+=delegate { badge.Left=Math.Max(340,header.Width-badge.Width-24); fullScreenButton.Left=header.Width-fullScreenButton.Width-24; };

            sidebar = new Panel { Dock=DockStyle.Left, Width=306, BackColor=Color.FromArgb(22,32,37), Padding=new Padding(18) };
            Label folderTitle = MakeLabel("01   FOLDER ZRZUTÓW",20,9,266,23,muted);
            folderBox = new TextBox { Location=new Point(20,41), Size=new Size(266,28), BackColor=Color.FromArgb(34,47,53), ForeColor=ForeColor, BorderStyle=BorderStyle.FixedSingle, Text=folder??"", AccessibleName="Ścieżka folderu zrzutów" };
            Button browse = MakeButton("Wybierz folder…",20,80,266);
            browse.Click += delegate
            {
                using(FolderBrowserDialog dialog=new FolderBrowserDialog())
                {
                    dialog.Description="Wybierz folder zrzutów zapisanych przez EFT";
                    dialog.ShowNewFolderButton=false;
                    if(Directory.Exists(folderBox.Text)) dialog.SelectedPath=folderBox.Text;
                    if(dialog.ShowDialog(this)==DialogResult.OK) { folderBox.Text=dialog.SelectedPath; StartWatching(); }
                }
            };
            Button start=MakeButton("Obserwuj",20,122,130), stop=MakeButton("Zatrzymaj",156,122,130);
            start.ForeColor=accent;
            start.Click+=delegate { StartWatching(); };
            stop.Click+=delegate { StopWatching(); };
            statusLabel=MakeLabel("Wybierz folder, aby rozpocząć.",20,166,266,42,muted);
            Label positionTitle=MakeLabel("02   OSTATNIA POZYCJA",20,226,266,24,muted);
            timeLabel=MakeLabel("Jeszcze brak zrzutu",20,260,266,30,ForeColor); timeLabel.Font=valueFont;
            ageLabel=MakeLabel("Oczekiwanie na plik PNG…",20,298,266,48,muted);
            coordsLabel=MakeLabel("X  —     Y  —     Z  —",20,358,266,60,ForeColor);
            headingLabel=MakeLabel("Kierunek: —",20,426,266,24,accent);
            fileLabel=MakeLabel("Nazwa ostatniego pliku pojawi się tutaj.",20,472,266,87,muted); fileLabel.Font=new Font("Segoe UI",8);
            countsLabel=MakeLabel("",20,566,266,38,muted); countsLabel.Font=new Font("Segoe UI",8);
            navigationLabel=MakeLabel("Auto wnętrza: oczekuję na zrzut.\nF11: pełny ekran • Esc: powrót.\nMapę nadal wybierasz ręcznie.",20,615,266,85,muted); navigationLabel.Font=new Font("Segoe UI",8);
            sidebar.Controls.AddRange(new Control[]{folderTitle,folderBox,browse,start,stop,statusLabel,positionTitle,timeLabel,ageLabel,coordsLabel,headingLabel,fileLabel,countsLabel,navigationLabel});
            sidebar.AutoScroll=true;

            Panel main=new Panel { Dock=DockStyle.Fill };
            selectors=new FlowLayoutPanel { Dock=DockStyle.Top,Height=55,Padding=new Padding(12,12,0,0),BackColor=Color.FromArgb(25,37,42),WrapContents=false };
            mapBox=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList,Width=245,AccessibleName="Wybór mapy",BackColor=Color.FromArgb(34,47,53),ForeColor=ForeColor };
            layerBox=new ComboBox { DropDownStyle=ComboBoxStyle.DropDownList,Width=210,AccessibleName="Wybór piętra",BackColor=Color.FromArgb(34,47,53),ForeColor=ForeColor };
            selectors.Controls.Add(new Label {Text="Mapa",AutoSize=true,Margin=new Padding(0,5,8,0),ForeColor=muted});
            selectors.Controls.Add(mapBox);
            selectors.Controls.Add(new Label {Text="Poziom",AutoSize=true,Margin=new Padding(16,5,8,0),ForeColor=muted});
            selectors.Controls.Add(layerBox);
            foreach(MapDefinition m in maps) mapBox.Items.Add(m);
            for(int i=0;i<maps.Length;i++) if(maps[i].Id=="customs") mapBox.SelectedIndex=i;
            toolbar=new FlowLayoutPanel { Dock=DockStyle.Top, Height=54, Padding=new Padding(12,9,0,0), BackColor=Color.FromArgb(25,37,42), WrapContents=false };
            Button fit=MakeButton("Cała mapa",0,0,112), center=MakeButton("Do znacznika",0,0,125), plus=MakeButton("+",0,0,40), minus=MakeButton("−",0,0,40);
            fit.Click+=delegate { canvas.FitMap(); };
            center.Click+=delegate { canvas.CenterPosition(); };
            plus.Click+=delegate { canvas.Zoom(1.25f); };
            minus.Click+=delegate { canvas.Zoom(.8f); };
            follow=new CheckBox { Text="Śledź pozycję",AccessibleName="Śledź pozycję",AutoSize=true,Margin=new Padding(8,7,0,0),ForeColor=muted,Checked=false };
            follow.CheckedChanged+=delegate { canvas.Follow=follow.Checked; if(follow.Checked) canvas.CenterPosition(); };
            autoNavigation=new CheckBox { Text="Auto wnętrza",AccessibleName="Automatyczne wnętrza",AutoSize=true,Margin=new Padding(8,7,0,0),ForeColor=accent,Checked=true };
            autoNavigation.CheckedChanged+=delegate { if(navigator!=null) navigator.Reset(); visualFocus=null; pendingFocus=null; pendingFit=false; if(autoNavigation.Checked && displayed!=null) ApplyNavigation(displayed.Position); };
            toolbar.Controls.AddRange(new Control[]{fit,center,plus,minus,follow,autoNavigation});
            canvas=new MapCanvas() { Dock=DockStyle.Fill };
            mapBox.SelectedIndexChanged+=delegate { PopulateLayers(); };
            layerBox.SelectedIndexChanged+=delegate { RequestMap(); };
            layerBox.SelectionChangeCommitted+=delegate { autoNavigation.Checked=false; };
            mapWorker.DoWork+=LoadMapInBackground;
            mapWorker.RunWorkerCompleted+=MapLoaded;
            mapHint = new Label { Text="Kółko: zbliżenie • Przeciągnij: przesuwanie • F11: pełny ekran • Esc: powrót", Dock=DockStyle.Bottom, Height=29, Padding=new Padding(15,5,0,0), ForeColor=muted, Font=new Font("Segoe UI",8) };
            ContextMenuStrip mapMenu=new ContextMenuStrip();
            ToolStripMenuItem lastUpdate=new ToolStripMenuItem("Brak zrzutu") { Enabled=false };
            ToolStripMenuItem mapCredit=new ToolStripMenuItem("Mapa") { Enabled=false };
            mapMenu.Items.Add(lastUpdate); mapMenu.Items.Add(mapCredit); mapMenu.Items.Add(new ToolStripSeparator());
            mapMenu.Opening+=delegate
            {
                lastUpdate.Text=displayed==null?"Brak zrzutu":"Ostatni zrzut: "+displayed.FileTimeUtc.ToLocalTime().ToString("HH:mm:ss")+" • "+Math.Max(0,(int)(DateTime.UtcNow-displayed.FileTimeUtc).TotalSeconds)+" s temu";
                mapCredit.Text=canvas.Definition==null?"Mapa wczytuje się…":"Mapa: "+canvas.Definition.Author+" • "+(canvas.Layer.Remote?"prawa autorów":"CC BY-NC-SA 4.0");
            };
            mapMenu.Items.Add("Pełny ekran / powrót (F11)",null,delegate { SetFullScreen(!fullScreen); });
            mapMenu.Items.Add("Cała mapa",null,delegate { canvas.FitMap(); });
            mapMenu.Items.Add("Do znacznika",null,delegate { canvas.CenterPosition(); });
            mapMenu.Items.Add("Auto wnętrza: włącz / wyłącz",null,delegate { autoNavigation.Checked=!autoNavigation.Checked; });
            canvas.ContextMenuStrip=mapMenu;
            main.Controls.Add(canvas); main.Controls.Add(mapHint); main.Controls.Add(toolbar); main.Controls.Add(selectors);
            Controls.Add(main); Controls.Add(sidebar); Controls.Add(header);
            timer.Interval=100;
            timer.Tick+=Tick;
            timer.Start();
            Shown+=delegate { PopulateLayers(); if(!String.IsNullOrEmpty(folder)) StartWatching(); canvas.FitMap(); if(startFullScreen) SetFullScreen(true); };
            FormClosed+=delegate { timer.Stop(); mapWorker.CancelAsync(); if(monitor!=null) monitor.Dispose(); if(telemetry!=null) telemetry.Dispose(); ownProcess.Dispose(); };
        }
        sealed class MapJob
        { public MapDefinition Map; public MapLayer Layer; public int Generation; public string Path; }
        void PopulateLayers()
        {
            layerBox.Items.Clear();
            MapDefinition m=mapBox.SelectedItem as MapDefinition;
            if(m==null) return;
            if(navigatorMapId!=m.Id)
            {
                navigator=new AutoNavigator(m,Path.Combine(assetsDirectory,"maps.upstream.json"));
                navigatorMapId=m.Id; pendingFocus=null; pendingFit=false; visualFocus=null;
            }
            foreach(MapLayer l in m.Layers) layerBox.Items.Add(l);
            layerBox.SelectedIndex=0;
        }
        void ApplyNavigation(Position p)
        {
            if(!autoNavigation.Checked || navigator==null || p==null) return;
            MapLayer selected=layerBox.SelectedItem as MapLayer;
            if(selected==null) return;
            NavigationDecision decision=navigator.Update(p,selected.Id);
            navigationLabel.Text="Auto: "+decision.ContextName+"\n"+(decision.SourceLevelName??"Poziom bazowy")+" • zmiana po nowym SS\nF11: pełny ekran • Esc: powrót";
            if(decision.Changed)
            {
                pendingFocus=decision.FocusBounds; pendingFit=decision.ResetView; visualFocus=null;
                if(decision.LayerId!=selected.Id)
                {
                    for(int i=0;i<layerBox.Items.Count;i++) if(((MapLayer)layerBox.Items[i]).Id==decision.LayerId)
                    { layerBox.SelectedIndex=i; return; }
                }
                ApplyPendingView();
            }
            if(!decision.IsInterior && canvas.Definition!=null && canvas.Definition.Id==navigatorMapId)
            {
                WorldBounds building=visualFocus!=null && visualFocus.Contains(p.X,p.Z,.8)?visualFocus:canvas.BuildingAt(p);
                if(building!=null && building!=visualFocus)
                { visualFocus=building; canvas.FocusArea(building); navigationLabel.Text="Auto: obrys budynku na mapie\nZbliżenie po nowym zrzucie.\nF11: pełny ekran • Esc: powrót"; }
                else if(building==null && visualFocus!=null) { visualFocus=null; canvas.FitMap(); }
            }
        }
        void ApplyPendingView()
        {
            if(canvas.Layer==null || canvas.Definition==null || canvas.Definition.Id!=navigatorMapId) return;
            if(pendingFocus!=null) { canvas.FocusArea(pendingFocus); pendingFocus=null; }
            else if(pendingFit) canvas.FitMap();
            pendingFit=false;
        }
        public void SetFullScreen(bool enabled)
        {
            if(enabled==fullScreen) return;
            SuspendLayout();
            try
            {
                if(enabled)
                {
                    savedWindowState=WindowState; savedBounds=WindowState==FormWindowState.Normal?Bounds:RestoreBounds;
                    savedMinimum=MinimumSize; Rectangle screen=Screen.FromControl(this).Bounds;
                    fullScreen=true; MinimumSize=Size.Empty; WindowState=FormWindowState.Normal;
                    FormBorderStyle=FormBorderStyle.None;
                    header.Visible=false; sidebar.Visible=false; selectors.Visible=false; toolbar.Visible=false; mapHint.Visible=false;
                    canvas.MapOnly=true; Bounds=screen;
                }
                else
                {
                    fullScreen=false; WindowState=FormWindowState.Normal; FormBorderStyle=FormBorderStyle.Sizable;
                    header.Visible=true; sidebar.Visible=true; selectors.Visible=true; toolbar.Visible=true; mapHint.Visible=true;
                    canvas.MapOnly=false; Bounds=savedBounds; MinimumSize=savedMinimum; WindowState=savedWindowState;
                }
            }
            finally { ResumeLayout(true); }
            canvas.Focus(); canvas.Invalidate();
        }
        protected override bool ProcessCmdKey(ref Message msg,Keys keyData)
        {
            if(keyData==Keys.F11) { SetFullScreen(!fullScreen); return true; }
            if(keyData==Keys.Escape && fullScreen) { SetFullScreen(false); return true; }
            return base.ProcessCmdKey(ref msg,keyData);
        }
        void RequestMap()
        {
            MapDefinition selected=mapBox.SelectedItem as MapDefinition;
            MapLayer level=layerBox.SelectedItem as MapLayer;
            if(selected==null || level==null) return;
            requestedMap=selected; requestedLayer=level; mapGeneration++;
            canvas.ClearMap();
            mapProgress=level.Remote?"Wczytywanie mapy; pierwszy raz wymaga internetu…":"Wczytywanie mapy…";
            canvas.LoadingText=mapProgress;
            mapSubtitle.Text=selected.Name.ToUpperInvariant()+" · "+level.Name;
            Text="EFT Position · "+selected.Name+(isDemo?" · DANE PRZYKŁADOWE":"");
            if(mapWorker.IsBusy) mapWorker.CancelAsync(); else StartMapJob();
        }
        void StartMapJob()
        { activeGeneration=mapGeneration; mapWorker.RunWorkerAsync(new MapJob { Map=requestedMap,Layer=requestedLayer,Generation=mapGeneration }); }
        void LoadMapInBackground(object sender,DoWorkEventArgs e)
        {
            MapJob job=(MapJob)e.Argument;
            job.Path=!String.IsNullOrEmpty(job.Layer.VectorImage)?Path.Combine(assetsDirectory,job.Layer.VectorImage):
                MapAssets.Ensure(assetsDirectory,cacheDirectory,job.Layer,delegate { return mapWorker.CancellationPending; },delegate(string s) { mapProgress=s; });
            e.Result=job;
        }
        void MapLoaded(object sender,RunWorkerCompletedEventArgs e)
        {
            if(IsDisposed || Disposing) return;
            MapJob job=e.Error==null && !e.Cancelled ? e.Result as MapJob:null;
            if(activeGeneration!=mapGeneration) { StartMapJob(); return; }
            if(e.Error!=null)
            {
                canvas.LoadingText="Nie udało się wczytać mapy.\nSprawdź internet lub pliki programu i wybierz mapę ponownie.\n"+e.Error.GetBaseException().Message;
                canvas.Invalidate(); return;
            }
            try
            {
                canvas.LoadMap(job.Map,job.Layer,job.Path,cacheDirectory); completedMapLoads++;
                if(autoNavigation.Checked && displayed!=null) ApplyNavigation(displayed.Position);
                ApplyPendingView();
            }
            catch(Exception ex) { canvas.LoadingText="Nie mogę otworzyć mapy: "+ex.Message; canvas.Invalidate(); }
        }
        int activeGeneration;
        Label MakeLabel(string text,int x,int y,int w,int h,Color color)
        { return new Label { Text=text,Location=new Point(x,y),Size=new Size(w,h),ForeColor=color }; }
        Button MakeButton(string text,int x,int y,int width)
        {
            Button b=new Button { Text=text,Location=new Point(x,y),Size=new Size(width,34),FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(34,47,53),ForeColor=ForeColor,Cursor=Cursors.Hand };
            b.FlatAppearance.BorderColor=Color.FromArgb(54,72,78);
            return b;
        }
        void StartWatching()
        {
            try
            {
                string path=folderBox.Text.Trim().Trim('"');
                if (String.IsNullOrWhiteSpace(path)) throw new IOException("Wybierz folder zrzutów.");
                FolderMonitor next=new FolderMonitor(path);
                if(monitor!=null) monitor.Dispose();
                monitor=next;
                if(navigator!=null) navigator.Reset(); visualFocus=null; pendingFocus=null; pendingFit=false;
                folderBox.Text=next.Folder;
                displayed=null; canvas.SetPosition(null); canvas.FitMap();
                timeLabel.Text="Jeszcze brak zrzutu"; coordsLabel.Text="X  —     Y  —     Z  —";
                headingLabel.Text="Kierunek: —"; fileLabel.Text="Oczekiwanie na poprawną nazwę pliku…";
            }
            catch(Exception ex)
            {
                if(!(ex is IOException) && !(ex is UnauthorizedAccessException) && !(ex is ArgumentException)) throw;
                MessageBox.Show(this,ex.Message,"Nie mogę obserwować folderu",MessageBoxButtons.OK,MessageBoxIcon.Information);
            }
        }
        void StopWatching()
        {
            if(monitor!=null) { monitor.Dispose(); monitor=null; }
            statusLabel.Text="Obserwacja zatrzymana.\nOstatni znacznik pozostaje na mapie.";
        }
        void Tick(object sender,EventArgs e)
        {
            if(monitor!=null)
            {
                statusLabel.Text=monitor.Status;
                countsLabel.Text="Aktualizacje: "+monitor.Accepted+"  •  Pominięte nazwy: "+monitor.Ignored;
                Snapshot s=monitor.Latest;
                if(s!=null && s!=displayed)
                {
                    displayed=s;
                    canvas.SetPosition(s.Position);
                    ApplyNavigation(s.Position);
                    timeLabel.Text=s.FileTimeUtc.ToLocalTime().ToString("HH:mm:ss");
                    coordsLabel.Text=String.Format(CultureInfo.InvariantCulture,"X   {0:F2}\nY   {1:F2}      Z   {2:F2}",s.Position.X,s.Position.Y,s.Position.Z);
                    headingLabel.Text=s.Position.HasHeading ? String.Format(CultureInfo.InvariantCulture,"Obrót w grze: {0:F1}°",s.Position.Yaw) : "Kierunek pionowy • brak strzałki";
                    fileLabel.Text=s.Position.FileName;
                }
            }
            bool stale=true;
            if(displayed!=null)
            {
                double age=Math.Max(0,(DateTime.UtcNow-displayed.FileTimeUtc).TotalSeconds);
                stale=age>=30;
                string ageText=age<60 ? ((int)age)+" s temu" : age<3600 ? ((int)(age/60))+" min temu" : ((int)(age/3600))+" h temu";
                bool onMap=canvas.Layer!=null && canvas.Layer.Contains(displayed.Position);
                ageLabel.Text=canvas.Layer==null ? "Czas pliku: "+displayed.FileTimeUtc.ToLocalTime().ToString("HH:mm:ss")+"\nWczytywanie wybranej mapy…" : onMap ? "Czas pliku: "+displayed.FileTimeUtc.ToLocalTime().ToString("dd.MM.yyyy")+"\n"+ageText+" • pozycja z ostatniego zrzutu" : "Poza obszarem wybranej mapy.\nSprawdź wybraną lokację.";
                ageLabel.ForeColor=stale || !onMap ? Color.FromArgb(244,183,83) : accent;
            }
            else ageLabel.Text="Oczekiwanie na poprawny plik PNG…";
            if(stale!=oldStale) { canvas.Stale=stale; canvas.Invalidate(); oldStale=stale; }
            canvas.RefreshTiles();
            if(mapWorker.IsBusy) { canvas.LoadingText=mapProgress; canvas.Invalidate(); }
            if(cycleMaps && elapsed.Elapsed.TotalSeconds>=nextMapCycle && !mapWorker.IsBusy)
            { nextMapCycle=elapsed.Elapsed.TotalSeconds+1.5; mapBox.SelectedIndex=(mapBox.SelectedIndex+1)%maps.Length; }
            if(exercise && displayed!=null) canvas.ExerciseView(viewStep++);
            if(!previewWritten && !String.IsNullOrEmpty(previewPath) && elapsed.Elapsed.TotalSeconds>=2)
            {
                previewWritten=true;
                using(Bitmap image=new Bitmap(Width,Height))
                { DrawToBitmap(image,new Rectangle(0,0,Width,Height)); image.Save(previewPath,System.Drawing.Imaging.ImageFormat.Png); }
            }
            if(telemetry!=null && DateTime.UtcNow>=nextTelemetry)
            {
                nextTelemetry=DateTime.UtcNow.AddSeconds(1);
                ownProcess.Refresh();
                telemetry.WriteLine(String.Format(CultureInfo.InvariantCulture,"{0:F2},{1},{2},{3},{4},{5},{6},{7},{8},{9},\"{10}\",{11},{12},{13},{14},{15}",
                    elapsed.Elapsed.TotalSeconds,ownProcess.WorkingSet64,ownProcess.PrivateMemorySize64,ownProcess.PeakWorkingSet64,ownProcess.HandleCount,
                    monitor==null?0:monitor.Accepted,monitor==null?0:monitor.Ignored,monitor==null?0:monitor.CacheCount,
                    displayed==null?"":displayed.Position.X.ToString("F2",CultureInfo.InvariantCulture),
                    displayed==null?"":displayed.Position.Z.ToString("F2",CultureInfo.InvariantCulture),displayed==null?"":displayed.Position.FileName,
                    canvas.Definition==null?"":canvas.Definition.Id,canvas.Layer==null?"":canvas.Layer.Id,completedMapLoads,fullScreen?1:0,canvas.IsVector?1:0));
            }
            if(exitAfter>0 && elapsed.Elapsed.TotalSeconds>=exitAfter) Close();
        }
        protected override void Dispose(bool disposing)
        {
            if(disposing) { timer.Dispose(); titleFont.Dispose(); valueFont.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
