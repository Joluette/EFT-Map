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
        readonly Color muted = UiTheme.Muted;
        readonly Color accent = UiTheme.Accent;
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
        readonly Button startButton,stopButton;
        readonly UiButton browseButton;
        readonly MarkerPanel markerPanel;
        readonly TableLayoutPanel positionStack;
        readonly UiButton markerButton;
        readonly AppPreferences preferences;
        readonly string preferencesFile;
        readonly CheckBox deleteScreenshots;
        ScreenshotCleaner cleaner;
        Snapshot cleanupQueued;
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
        readonly Font titleFont = new Font("Segoe UI Semibold",16);
        readonly Font valueFont = new Font("Segoe UI Semibold",24);
        readonly ToolTip detailsTip=new ToolTip { AutoPopDelay=12000,InitialDelay=450,ReshowDelay=150 };
        FolderMonitor monitor;
        Snapshot displayed;
        DateTime nextTelemetry = DateTime.MinValue;
        int viewStep;
        bool oldStale;

        public MainForm(string folder, string telemetryPath, int duration, bool exerciseView, bool demo, string preview, string cache=null, bool cycle=false, bool startFullScreen=false, string initialMap=null)
        {
            Text = "EFT Position · 13 map" + (demo ? " · DANE PRZYKŁADOWE" : "");
            MinimumSize = new Size(950,650);
            ClientSize = new Size(1250,760);
            StartPosition = FormStartPosition.CenterScreen;
            BackColor = UiTheme.Background;
            ForeColor = UiTheme.Text;
            Font = UiTheme.Body;
            AutoScaleMode = AutoScaleMode.Dpi;
            exitAfter = duration;
            exercise = exerciseView;
            cycleMaps=cycle; isDemo=demo;
            assetsDirectory=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"assets");
            cacheDirectory=cache??Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"cache");
            preferencesFile=Path.Combine(cacheDirectory,"app-preferences.json");
            preferences=AppPreferences.Load(preferencesFile);
            string initialFolder=folder??(demo?"":preferences.ScreenshotFolder);
            string iconPath=Path.Combine(assetsDirectory,"app.ico");
            if(File.Exists(iconPath)) using(Icon appIcon=new Icon(iconPath)) Icon=(Icon)appIcon.Clone();
            maps=MapCatalog.Load(assetsDirectory);
            string firstMapId=initialMap??"customs";
            int firstMap=Array.FindIndex(maps,delegate(MapDefinition m) {return String.Equals(m.Id,firstMapId,StringComparison.OrdinalIgnoreCase);});
            if(firstMap<0) throw new ArgumentException("Nieznana mapa: "+firstMapId);
            previewPath = preview;
            if (!String.IsNullOrEmpty(telemetryPath))
            {
                telemetry = new StreamWriter(telemetryPath,false,new UTF8Encoding(false));
                telemetry.AutoFlush = true;
                telemetry.WriteLine("elapsedSeconds,workingSetBytes,privateBytes,peakWorkingSetBytes,handles,accepted,ignored,cacheEntries,latestX,latestZ,latestName,mapId,layerId,mapLoads,fullscreen,vector");
            }

            header = new Panel { Dock=DockStyle.Top, Height=64, BackColor=UiTheme.Surface };
            Label brand = new Label { Text="EFT  /  POSITION", Font=titleFont, Location=new Point(20,8), AutoSize=true };
            mapSubtitle = new Label { Text="WYBIERZ MAPĘ", ForeColor=ForeColor, Location=new Point(296,21), Size=new Size(680,24), AutoEllipsis=true, Font=UiTheme.Section };
            Label badge = new Label { Text= demo ? "DANE PRZYKŁADOWE" : "POZYCJA Z OSTATNIEGO ZRZUTU", ForeColor=accent, AutoSize=true, Location=new Point(23,39), Font=UiTheme.Caption };
            fullScreenButton=MakeButton("Pełny ekran  F11",1050,16,150);
            fullScreenButton.AccessibleName="Pełny ekran";
            fullScreenButton.Click+=delegate { SetFullScreen(!fullScreen); };
            header.Controls.AddRange(new Control[]{brand,mapSubtitle,badge,fullScreenButton});
            header.Resize+=delegate { fullScreenButton.Left=header.Width-fullScreenButton.Width-20;mapSubtitle.Width=Math.Max(40,fullScreenButton.Left-mapSubtitle.Left-20); };

            sidebar = new Panel { Dock=DockStyle.Left, Width=280, BackColor=UiTheme.Background, Padding=new Padding(16),AutoScroll=true };
            TableLayoutPanel stack=positionStack=new TableLayoutPanel { Dock=DockStyle.Top,Height=514,ColumnCount=1,RowCount=5,Margin=Padding.Empty,Padding=Padding.Empty,BackColor=UiTheme.Background };
            stack.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
            foreach(int height in new[]{214,12,184,12,92}) stack.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
            UiCard folderCard=new UiCard {Dock=DockStyle.Fill},positionCard=new UiCard {Dock=DockStyle.Fill},navigationCard=new UiCard {Dock=DockStyle.Fill};
            stack.Controls.Add(folderCard,0,0);stack.Controls.Add(positionCard,0,2);stack.Controls.Add(navigationCard,0,4);sidebar.Controls.Add(stack);
            Label folderTitle = MakeLabel("Zrzuty gry",14,12,220,23,ForeColor);folderTitle.Font=UiTheme.Section;
            Panel folderFrame=new Panel { Location=new Point(14,42),Size=new Size(220,28),Padding=new Padding(7,5,7,3),BackColor=UiTheme.Field,Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right };
            folderBox = new TextBox { Dock=DockStyle.Fill, BackColor=UiTheme.Field, ForeColor=ForeColor, BorderStyle=BorderStyle.None, Text=initialFolder??"", AccessibleName="Ścieżka folderu zrzutów",Font=UiTheme.Small };
            folderFrame.Controls.Add(folderBox);
            Button browse = browseButton=(UiButton)MakeButton("Wybierz folder…",14,78,220);browse.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;
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
            Button start=startButton=MakeButton("Obserwuj",14,118,106), stop=stopButton=MakeButton("Zatrzymaj",128,118,106);
            ((UiButton)start).Primary=true;
            start.Click+=delegate { StartWatching(); };
            stop.Click+=delegate { StopWatching(); };
            folderBox.TextChanged+=delegate { UpdateWatchButtons(); };
            UpdateWatchButtons();
            deleteScreenshots=new CheckBox {Text="Usuwaj zrzut po odczycie",AccessibleName="Usuwaj zrzut po odczycie",Location=new Point(14,155),Size=new Size(220,20),Font=UiTheme.Small,Checked=preferences.DeleteScreenshots,Enabled=!demo,ForeColor=muted};
            deleteScreenshots.CheckedChanged+=delegate {preferences.DeleteScreenshots=deleteScreenshots.Checked;SavePreferences();if(!deleteScreenshots.Checked && cleaner!=null){cleaner.Dispose();cleaner=null;}else if(monitor!=null && !isDemo)cleaner=new ScreenshotCleaner(monitor.Folder);};
            detailsTip.SetToolTip(deleteScreenshots,demo?"W trybie przykładowym pliki pozostają w folderze.":"Usuwa tylko rozpoznany PNG po pokazaniu pozycji na mapie. Inne pliki pozostają w folderze.");
            statusLabel=MakeLabel("Wybierz folder, aby rozpocząć.",14,181,220,26,muted);statusLabel.Font=UiTheme.Small;
            Label positionTitle=MakeLabel("OSTATNI ZRZUT",14,12,220,18,muted);positionTitle.Font=UiTheme.Caption;
            timeLabel=MakeLabel("— : — : —",10,33,228,43,ForeColor); timeLabel.Font=valueFont;
            ageLabel=MakeLabel("Brak pozycji\nWykonaj zrzut w grze.",14,80,220,34,muted);ageLabel.Font=UiTheme.Small;
            coordsLabel=MakeLabel("X  —       Z  —\nY  —",14,121,220,36,ForeColor);coordsLabel.Font=UiTheme.Numbers;
            headingLabel=MakeLabel("Kierunek · —",14,158,220,20,accent);headingLabel.Font=UiTheme.Small;
            fileLabel=MakeLabel("",0,0,1,1,muted);fileLabel.Visible=false;
            countsLabel=MakeLabel("",0,0,1,1,muted);countsLabel.Visible=false;
            Label navigationTitle=MakeLabel("KONTEKST MAPY",14,12,220,18,muted);navigationTitle.Font=UiTheme.Caption;
            navigationLabel=MakeLabel("Wybierz aktualną lokację.\nPiętro dopasuje się po zrzucie.",14,37,220,43,ForeColor);navigationLabel.Font=UiTheme.Small;navigationLabel.AutoEllipsis=true;
            folderCard.Controls.AddRange(new Control[]{folderTitle,folderFrame,browse,start,stop,deleteScreenshots,statusLabel});
            positionCard.Controls.AddRange(new Control[]{positionTitle,timeLabel,ageLabel,coordsLabel,headingLabel,fileLabel,countsLabel});
            navigationCard.Controls.AddRange(new Control[]{navigationTitle,navigationLabel});
            detailsTip.SetToolTip(folderBox,"Folder zrzutów zapisanych przez EFT. Możesz wkleić ścieżkę lub wybrać folder.");
            detailsTip.SetToolTip(timeLabel,"Czas ostatniego zrzutu. Najedź tutaj po odczytaniu pozycji, aby zobaczyć nazwę pliku.");

            Panel main=new Panel { Dock=DockStyle.Fill };
            selectors=new FlowLayoutPanel { Dock=DockStyle.Top,Height=62,Padding=new Padding(16,4,16,4),BackColor=UiTheme.Background,WrapContents=false };
            mapBox=new UiComboBox { Dock=DockStyle.Bottom,AccessibleName="Wybór mapy" };
            layerBox=new UiComboBox { Dock=DockStyle.Bottom,AccessibleName="Wybór piętra" };
            TableLayoutPanel fields=new TableLayoutPanel { ColumnCount=2,RowCount=1,Height=54,Margin=Padding.Empty,Padding=Padding.Empty };
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,55));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,45));
            fields.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            Panel mapField=new Panel {Dock=DockStyle.Fill,Margin=new Padding(0,0,12,0)},levelField=new Panel {Dock=DockStyle.Fill,Margin=Padding.Empty};
            Label mapCaption=new Label {Text="MAPA",Dock=DockStyle.Top,Height=18,Font=UiTheme.Caption,ForeColor=muted};
            Label levelCaption=new Label {Text="POZIOM",Dock=DockStyle.Top,Height=18,Font=UiTheme.Caption,ForeColor=muted};
            mapField.Controls.Add(mapBox);mapField.Controls.Add(mapCaption);levelField.Controls.Add(layerBox);levelField.Controls.Add(levelCaption);
            fields.Controls.Add(mapField,0,0);fields.Controls.Add(levelField,1,0);selectors.Controls.Add(fields);
            selectors.Resize+=delegate {fields.Width=Math.Max(1,selectors.ClientSize.Width-selectors.Padding.Horizontal);fields.Height=selectors.ClientSize.Height-selectors.Padding.Vertical;};
            foreach(MapDefinition m in maps) mapBox.Items.Add(m);
            mapBox.SelectedIndex=firstMap;
            toolbar=new FlowLayoutPanel { Dock=DockStyle.Top, Height=46, Padding=new Padding(16,6,0,6), BackColor=UiTheme.Background, WrapContents=false };
            Button fit=MakeButton("Cała mapa",0,0,96), center=MakeButton("Do znacznika",0,0,110), plus=MakeButton("+",0,0,34), minus=MakeButton("−",0,0,34);
            fit.Click+=delegate { canvas.FitMap(); };
            center.Click+=delegate { canvas.CenterPosition(); };
            plus.Click+=delegate { canvas.SmoothZoom(1.25f); };
            minus.Click+=delegate { canvas.SmoothZoom(.8f); };
            follow=new UiToggle { Text="Śledź pozycję",AccessibleName="Śledź pozycję",Width=126,Checked=false };
            follow.CheckedChanged+=delegate { canvas.Follow=follow.Checked; if(follow.Checked) canvas.CenterPosition(); };
            autoNavigation=new UiToggle { Text="Auto wnętrza",AccessibleName="Automatyczne wnętrza",Width=122,Checked=true };
            autoNavigation.CheckedChanged+=delegate { if(navigator!=null) navigator.Reset(); visualFocus=null; pendingFocus=null; pendingFit=false; if(autoNavigation.Checked && displayed!=null) ApplyNavigation(displayed.Position);else if(!autoNavigation.Checked) navigationLabel.Text="Sterowanie ręczne\nWybierz poziom z menu."; };
            toolbar.Controls.AddRange(new Control[]{fit,center,plus,minus,follow,autoNavigation});
            canvas=new MapCanvas() { Dock=DockStyle.Fill };
            markerPanel=new MarkerPanel(assetsDirectory,cacheDirectory) {Dock=DockStyle.Fill,Visible=false};
            sidebar.Controls.Add(markerPanel);
            markerPanel.Changed+=delegate{canvas.SetMarkers(markerPanel.MapId,markerPanel.VisibleMarkers);};
            markerPanel.BackRequested+=delegate{ShowMarkerFilters(false);};
            markerButton=(UiButton)MakeButton("Filtry",0,0,66);markerButton.AccessibleName="Filtry mapy i questy";
            markerButton.Click+=delegate{ShowMarkerFilters(!markerPanel.Visible);};toolbar.Controls.Add(markerButton);
            detailsTip.SetToolTip(markerButton,"Ekstrakty, wyszukiwanie i wybór questów tej mapy (Ctrl+F).");
            detailsTip.SetToolTip(autoNavigation,"Dopasuj piętro i obszar wnętrza po nowym zrzucie. Mapę wybierasz ręcznie.");
            detailsTip.SetToolTip(follow,"Centruj znacznik po każdym nowym zrzucie.");
            detailsTip.SetToolTip(plus,"Płynnie przybliż mapę");detailsTip.SetToolTip(minus,"Płynnie oddal mapę");
            mapBox.SelectedIndexChanged+=delegate { PopulateLayers(); };
            layerBox.SelectedIndexChanged+=delegate { RequestMap(); };
            layerBox.SelectionChangeCommitted+=delegate { autoNavigation.Checked=false; };
            mapWorker.DoWork+=LoadMapInBackground;
            mapWorker.RunWorkerCompleted+=MapLoaded;
            mapHint = new Label { Text="Rolka: zoom     ·     Przeciągnij: przesuń mapę     ·     F11: pełny ekran", Dock=DockStyle.Bottom, Height=24, Padding=new Padding(14,4,0,0), ForeColor=muted, Font=UiTheme.Small,AutoEllipsis=true };
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
            mapMenu.Items.Add("Questy i ekstrakty (Ctrl+F)",null,delegate {if(fullScreen)SetFullScreen(false);ShowMarkerFilters(true);});
            canvas.ContextMenuStrip=mapMenu;
            main.Controls.Add(canvas); main.Controls.Add(mapHint); main.Controls.Add(toolbar); main.Controls.Add(selectors);
            Controls.Add(main); Controls.Add(sidebar); Controls.Add(header);
            timer.Interval=100;
            timer.Tick+=Tick;
            timer.Start();
            Shown+=delegate { PopulateLayers(); if(Directory.Exists(folderBox.Text)) StartWatching();else if(!String.IsNullOrWhiteSpace(folderBox.Text))statusLabel.Text="Folder niedostępny. Wybierz go ponownie."; canvas.FitMap(); if(startFullScreen) SetFullScreen(true); };
            FormClosed+=delegate { timer.Stop(); SavePreferences();markerPanel.Save();mapWorker.CancelAsync(); if(cleaner!=null)cleaner.Dispose();if(monitor!=null) monitor.Dispose(); if(telemetry!=null) telemetry.Dispose(); ownProcess.Dispose(); };
        }
        void ShowMarkerFilters(bool show)
        {
            positionStack.Visible=!show;markerPanel.Visible=show;markerButton.Primary=show;markerButton.Invalidate();
            if(show){markerPanel.BringToFront();markerPanel.FocusSearch();}
        }
        sealed class MapJob
        { public MapDefinition Map; public MapLayer Layer; public int Generation; public string Path; }
        void PopulateLayers()
        {
            layerBox.Items.Clear();
            MapDefinition m=mapBox.SelectedItem as MapDefinition;
            if(m==null) return;
            markerPanel.SetMap(m.Id);
            if(navigatorMapId!=m.Id)
            {
                navigator=new AutoNavigator(m,Path.Combine(assetsDirectory,"maps.upstream.json"));
                navigatorMapId=m.Id; pendingFocus=null; pendingFit=false; visualFocus=null;
            }
            canvas.SetMarkerLevels(navigator);
            foreach(MapLayer l in m.Layers) layerBox.Items.Add(l);
            layerBox.SelectedIndex=0;
        }
        void ApplyNavigation(Position p)
        {
            if(!autoNavigation.Checked || navigator==null || p==null) return;
            MapLayer selected=layerBox.SelectedItem as MapLayer;
            if(selected==null) return;
            NavigationDecision decision=navigator.Update(p,selected.Id);
            navigationLabel.Text=decision.ContextName+"\n"+(decision.SourceLevelName??"Poziom bazowy");
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
                { visualFocus=building; canvas.FocusArea(building); navigationLabel.Text="Rozpoznany budynek\nWidok dopasowany do wnętrza"; }
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
            if(keyData==(Keys.Control|Keys.F)) {if(fullScreen)SetFullScreen(false);ShowMarkerFilters(true);return true;}
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
        { return new Label { Text=text,Location=new Point(x,y),Size=new Size(w,h),ForeColor=color,Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right }; }
        Button MakeButton(string text,int x,int y,int width)
        {
            return new UiButton { Text=text,Location=new Point(x,y),Size=new Size(width,32) };
        }
        void StartWatching()
        {
            try
            {
                string path=folderBox.Text.Trim().Trim('"');
                if (String.IsNullOrWhiteSpace(path)) throw new IOException("Wybierz folder zrzutów.");
                FolderMonitor next=new FolderMonitor(path);
                if(cleaner!=null){cleaner.Dispose();cleaner=null;}
                if(monitor!=null) monitor.Dispose();
                monitor=next;
                if(deleteScreenshots.Checked && !isDemo)cleaner=new ScreenshotCleaner(next.Folder);
                cleanupQueued=null;
                if(navigator!=null) navigator.Reset(); visualFocus=null; pendingFocus=null; pendingFit=false;
                folderBox.Text=next.Folder;
                SavePreferences();
                detailsTip.SetToolTip(folderBox,next.Folder);
                displayed=null; canvas.SetPosition(null); canvas.FitMap();
                timeLabel.Text="— : — : —"; coordsLabel.Text="X  —       Z  —\nY  —";
                headingLabel.Text="Kierunek · —"; fileLabel.Text="Oczekiwanie na poprawną nazwę pliku…";
                navigationLabel.Text=autoNavigation.Checked?"Oczekiwanie na pozycję\nMapę wybierasz ręcznie.":"Sterowanie ręczne\nWybierz poziom z menu.";
                UpdateWatchButtons();
            }
            catch(Exception ex)
            {
                if(!(ex is IOException) && !(ex is UnauthorizedAccessException) && !(ex is ArgumentException)) throw;
                MessageBox.Show(this,ex.Message,"Nie mogę obserwować folderu",MessageBoxButtons.OK,MessageBoxIcon.Information);
            }
        }
        void StopWatching()
        {
            if(cleaner!=null){cleaner.Dispose();cleaner=null;}
            if(monitor!=null) { monitor.Dispose(); monitor=null; }
            statusLabel.Text="Obserwacja wstrzymana.\nOstatnia pozycja zachowana.";statusLabel.ForeColor=muted;
            UpdateWatchButtons();
        }
        void SavePreferences()
        {
            if(isDemo)return;
            preferences.ScreenshotFolder=folderBox.Text.Trim().Trim('"');
            if(!preferences.Save(preferencesFile))detailsTip.SetToolTip(folderBox,"Nie udało się zapisać ustawień. Sprawdź dostęp do folderu programu.");
        }
        void UpdateWatchButtons()
        {
            string path=folderBox.Text.Trim().Trim('"');
            bool empty=String.IsNullOrWhiteSpace(path);
            startButton.Enabled=!empty && (monitor==null || !String.Equals(path,monitor.Folder,StringComparison.OrdinalIgnoreCase));
            stopButton.Enabled=monitor!=null;
            browseButton.Primary=empty;((UiButton)startButton).Primary=!empty;
            browseButton.Invalidate();startButton.Invalidate();
        }
        void Tick(object sender,EventArgs e)
        {
            if(monitor!=null)
            {
                statusLabel.Text=monitor.Status;
                statusLabel.ForeColor=monitor.Status.StartsWith("Obserwuję",StringComparison.Ordinal)?accent:Color.FromArgb(244,183,83);
                countsLabel.Text="Aktualizacje: "+monitor.Accepted+"  •  Pominięte nazwy: "+monitor.Ignored;
                Snapshot s=monitor.Latest;
                if(s!=null && s!=displayed)
                {
                    displayed=s;
                    canvas.SetPosition(s.Position);
                    ApplyNavigation(s.Position);
                    timeLabel.Text=s.FileTimeUtc.ToLocalTime().ToString("HH:mm:ss");
                    coordsLabel.Text=String.Format(CultureInfo.InvariantCulture,"X {0,7:F2}  Z {1,7:F2}\nY {2,7:F2}",s.Position.X,s.Position.Z,s.Position.Y);
                    headingLabel.Text=s.Position.HasHeading ? String.Format(CultureInfo.InvariantCulture,"Kierunek · {0:F1}°",s.Position.Yaw) : "Pionowo · brak kierunku";
                    fileLabel.Text=s.Position.FileName;
                    detailsTip.SetToolTip(timeLabel,s.Position.FileName);
                }
            }
            bool stale=true;
            if(displayed!=null)
            {
                double age=Math.Max(0,(DateTime.UtcNow-displayed.FileTimeUtc).TotalSeconds);
                stale=age>=30;
                string ageText=age<60 ? ((int)age)+" s temu" : age<3600 ? ((int)(age/60))+" min temu" : ((int)(age/3600))+" h temu";
                bool onMap=canvas.Layer!=null && canvas.Layer.Contains(displayed.Position);
                ageLabel.Text=canvas.Layer==null ? "Wczytywanie mapy…\nPozycja z "+displayed.FileTimeUtc.ToLocalTime().ToString("HH:mm:ss") : onMap ? (stale?"Starszy zrzut":"Świeży zrzut")+" · "+ageText+"\n"+displayed.FileTimeUtc.ToLocalTime().ToString("dd.MM.yyyy") : "Poza obszarem mapy\nSprawdź wybraną lokację.";
                ageLabel.ForeColor=stale || !onMap ? Color.FromArgb(244,183,83) : accent;
            }
            else if(monitor!=null && monitor.Ignored>0)
            {ageLabel.Text="Nie rozpoznano pozycji\nPominięte PNG: "+monitor.Ignored;ageLabel.ForeColor=Color.FromArgb(244,183,83);}
            else {ageLabel.Text="Brak pozycji\nWykonaj zrzut w grze.";ageLabel.ForeColor=muted;}
            if(stale!=oldStale) { canvas.Stale=stale; canvas.Invalidate(); oldStale=stale; }
            canvas.RefreshTiles();
            if(cleaner!=null && displayed!=null && displayed!=cleanupQueued && !mapWorker.IsBusy && canvas.Definition!=null &&
                requestedMap!=null && canvas.Definition.Id==requestedMap.Id && canvas.Layer.Contains(displayed.Position))
            {
                canvas.Update(); // Complete the position repaint before acknowledging this file.
                cleaner.ConfirmDisplayed(displayed);cleanupQueued=displayed;
            }
            if(cleaner!=null && cleaner.Failed>0)detailsTip.SetToolTip(deleteScreenshots,"Nie udało się usunąć części zrzutów. Pozostały w folderze. Usunięto: "+cleaner.Deleted);
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
            if(disposing) { timer.Dispose(); detailsTip.Dispose(); titleFont.Dispose(); valueFont.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
