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
    public sealed class MainForm : ThemedForm
    {
        Color muted {get{return UiTheme.Muted;}}
        Color accent {get{return UiTheme.Accent;}}
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
        readonly Panel capturePanel,statusStrip,fullscreenBar;
        readonly UiIconButton settingsButton;
        SettingsForm settingsWindow;
        bool questOpen;
        public Form SettingsWindow {get{return settingsWindow;}}
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
        readonly Font titleFont = new Font("Segoe UI Semibold",12.5f);
        readonly Font valueFont = new Font("Segoe UI Semibold",24);
        readonly ToolTip detailsTip=new ToolTip { AutoPopDelay=12000,InitialDelay=450,ReshowDelay=150 };
        FolderMonitor monitor;
        Snapshot displayed;
        DateTime nextTelemetry = DateTime.MinValue;
        int viewStep;
        bool oldStale;
        MiniMapOverlay miniMap;
        OverlayHotkeys overlayHotkeys;
        GameMapMonitor gameMapMonitor;
        long gameMapRevision;
        DateTime positionNotBefore=DateTime.MinValue;
        public MiniMapOverlay MiniMap {get{return miniMap;}}

        public MainForm(string folder, string telemetryPath, int duration, bool exerciseView, bool demo, string preview, string cache=null, bool cycle=false, bool startFullScreen=false, string initialMap=null)
        {
            Text = UiText.T("EFT Position · 13 map") + (demo ? UiText.T(" · DANE PRZYKŁADOWE") : "");
            MinimumSize = new Size(950,620);
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
            UiText.Language=preferences.Language;
            UiTheme.Change(preferences.Theme,preferences.AccentArgb);
            UiTheme.CornerRadius=preferences.RoundedCorners?8:0;
            BackColor=UiTheme.Background;ForeColor=UiTheme.Text;
            string initialFolder=folder??(demo?"":preferences.ScreenshotFolder);
            string iconPath=Path.Combine(assetsDirectory,"app.ico");
            if(File.Exists(iconPath)) using(Icon appIcon=new Icon(iconPath)) Icon=(Icon)appIcon.Clone();
            maps=MapCatalog.Load(assetsDirectory);
            string firstMapId=initialMap??"customs";
            int firstMap=Array.FindIndex(maps,delegate(MapDefinition m) {return String.Equals(m.Id,firstMapId,StringComparison.OrdinalIgnoreCase);});
            if(firstMap<0) throw new ArgumentException(UiText.T("Nieznana mapa: ")+firstMapId);
            previewPath = preview;
            if (!String.IsNullOrEmpty(telemetryPath))
            {
                telemetry = new StreamWriter(telemetryPath,false,new UTF8Encoding(false));
                telemetry.AutoFlush = true;
                telemetry.WriteLine("elapsedSeconds,workingSetBytes,privateBytes,peakWorkingSetBytes,handles,accepted,ignored,cacheEntries,latestX,latestZ,latestName,mapId,layerId,mapLoads,fullscreen,vector");
            }

            header=new Panel {Dock=DockStyle.Top,Height=68,BackColor=UiTheme.Surface};
            var brandPanel=new Panel {Dock=DockStyle.Left,Width=166};
            brandPanel.Controls.Add(new Label {Text="EFT / POSITION",Font=titleFont,Location=new Point(18,13),AutoSize=true});
            brandPanel.Controls.Add(new Label {Text=demo?UiText.T("DANE PRZYKŁADOWE"):"MAP COMPANION",Font=UiTheme.Caption,ForeColor=muted,Location=new Point(20,40),AutoSize=true});
            var headerActions=new FlowLayoutPanel {Dock=DockStyle.Right,Width=206,Padding=new Padding(0,18,12,0),WrapContents=false};
            markerButton=new UiButton {Text=UiText.T("Questy i filtry"),Size=new Size(106,32),AccessibleName=UiText.T("Filtry mapy i questy"),Margin=new Padding(0,0,6,0)};
            settingsButton=new UiIconButton {Glyph="gear",AccessibleName=UiText.T("Ustawienia"),Margin=new Padding(0,0,6,0)};
            fullScreenButton=new UiIconButton {Glyph="fullscreen",AccessibleName=UiText.T("Pełny ekran"),Margin=Padding.Empty};
            headerActions.Controls.AddRange(new Control[]{markerButton,settingsButton,fullScreenButton});
            selectors=new FlowLayoutPanel {Dock=DockStyle.Fill,Padding=new Padding(4,6,16,4),WrapContents=false,BackColor=UiTheme.Surface};
            var fields=new TableLayoutPanel {ColumnCount=2,RowCount=1,Height=55,Margin=Padding.Empty,Padding=Padding.Empty};
            fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,48));fields.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,52));fields.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            var mapField=new Panel {Dock=DockStyle.Fill,Margin=new Padding(0,0,10,0)};
            var levelField=new Panel {Dock=DockStyle.Fill,Margin=Padding.Empty};
            mapBox=new UiComboBox {Dock=DockStyle.Bottom,AccessibleName=UiText.T("Wybór mapy")};layerBox=new UiComboBox {Dock=DockStyle.Bottom,AccessibleName=UiText.T("Wybór piętra")};
            mapField.Controls.Add(mapBox);mapField.Controls.Add(new Label {Text=UiText.T("MAPA"),Dock=DockStyle.Top,Height=17,Font=UiTheme.Caption,ForeColor=muted});
            levelField.Controls.Add(layerBox);levelField.Controls.Add(new Label {Text=UiText.T("POZIOM"),Dock=DockStyle.Top,Height=17,Font=UiTheme.Caption,ForeColor=muted});
            fields.Controls.Add(mapField,0,0);fields.Controls.Add(levelField,1,0);selectors.Controls.Add(fields);
            selectors.Resize+=delegate {fields.Width=Math.Max(1,selectors.ClientSize.Width-selectors.Padding.Horizontal);};
            header.Controls.Add(selectors);header.Controls.Add(headerActions);header.Controls.Add(brandPanel);
            mapSubtitle=new Label {Visible=false};foreach(MapDefinition m in maps)mapBox.Items.Add(m);mapBox.SelectedIndex=firstMap;

            var main=new Panel {Dock=DockStyle.Fill,BackColor=UiTheme.Background};
            toolbar=new FlowLayoutPanel {Dock=DockStyle.Top,Height=42,Padding=new Padding(16,5,12,5),BackColor=UiTheme.Background,WrapContents=false};
            Button fit=MakeButton(UiText.T("Cała mapa"),0,0,88),center=MakeButton(UiText.T("Do znacznika"),0,0,108),plus=MakeButton("+",0,0,32),minus=MakeButton("−",0,0,32);
            fit.Click+=delegate{canvas.FitMap();};center.Click+=delegate{canvas.CenterPosition();};plus.Click+=delegate{canvas.SmoothZoom(1.25f);};minus.Click+=delegate{canvas.SmoothZoom(.8f);};
            follow=new UiToggle {Text=UiText.T("Śledź pozycję"),AccessibleName=UiText.T("Śledź pozycję"),Width=126};
            follow.CheckedChanged+=delegate{canvas.Follow=follow.Checked;if(follow.Checked)canvas.CenterPosition();};
            autoNavigation=new UiToggle {Text=UiText.T("Auto wnętrza"),AccessibleName=UiText.T("Automatyczne wnętrza"),Width=122,Checked=true};
            autoNavigation.CheckedChanged+=delegate{if(navigator!=null)navigator.Reset();visualFocus=null;pendingFocus=null;pendingFit=false;if(autoNavigation.Checked && displayed!=null)ApplyNavigation(displayed.Position);else if(!autoNavigation.Checked)navigationLabel.Text=UiText.T("Sterowanie ręczne");};
            toolbar.Controls.AddRange(new Control[]{fit,center,plus,minus,follow,autoNavigation});
            var workspace=new Panel {Dock=DockStyle.Fill};canvas=new MapCanvas {Dock=DockStyle.Fill};
            sidebar=new Panel {Dock=DockStyle.Left,Width=292,Padding=new Padding(14,14,12,12),BackColor=UiTheme.Surface,Visible=false};
            markerPanel=new MarkerPanel(assetsDirectory,cacheDirectory) {Dock=DockStyle.Fill};sidebar.Controls.Add(markerPanel);
            markerPanel.Changed+=delegate{canvas.SetMarkers(markerPanel.MapId,markerPanel.VisibleMarkers);};markerPanel.BackRequested+=delegate{ShowMarkerFilters(false);};
            markerButton.Click+=delegate{ShowMarkerFilters(!sidebar.Visible);};settingsButton.Click+=delegate{ShowSettings();};fullScreenButton.Click+=delegate{SetFullScreen(!fullScreen);};
            workspace.Controls.Add(canvas);workspace.Controls.Add(sidebar);
            fullscreenBar=new Panel {Size=new Size(128,42),BackColor=UiTheme.Surface,Visible=false};
            var floatingQuest=new UiIconButton {Glyph="quests",Location=new Point(5,5),AccessibleName=UiText.T("Questy na pełnym ekranie")};
            var floatingSettings=new UiIconButton {Glyph="gear",Location=new Point(45,5),AccessibleName=UiText.T("Ustawienia na pełnym ekranie")};
            var floatingExit=new UiIconButton {Glyph="fullscreen",Location=new Point(85,5),AccessibleName=UiText.T("Wyjdź z pełnego ekranu")};
            floatingQuest.Click+=delegate{ShowMarkerFilters(!sidebar.Visible);};floatingSettings.Click+=delegate{ShowSettings();};floatingExit.Click+=delegate{SetFullScreen(false);};
            fullscreenBar.Controls.AddRange(new Control[]{floatingQuest,floatingSettings,floatingExit});canvas.Controls.Add(fullscreenBar);
            canvas.Resize+=delegate{fullscreenBar.Location=new Point(Math.Max(10,canvas.Width-fullscreenBar.Width-16),14);};

            statusStrip=new Panel {Dock=DockStyle.Bottom,Height=42,BackColor=UiTheme.Surface,Padding=new Padding(14,2,14,2)};
            var metrics=new TableLayoutPanel {Dock=DockStyle.Fill,ColumnCount=6,RowCount=1,Margin=Padding.Empty};
            foreach(int width in new[]{164,88,135,208,102})metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,width));metrics.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));metrics.RowStyles.Add(new RowStyle(SizeType.Percent,100));
            statusLabel=MakeLabel(UiText.T("Wybierz folder w ustawieniach"),0,0,160,36,muted);statusLabel.Font=UiTheme.Small;
            timeLabel=MakeLabel("—:—:—",0,0,72,36,ForeColor);timeLabel.Font=UiTheme.Numbers;
            ageLabel=MakeLabel(UiText.T("Brak pozycji\nWykonaj zrzut w grze."),0,0,115,36,muted);ageLabel.Font=UiTheme.Small;
            coordsLabel=MakeLabel("X —   Z —   Y —",0,0,208,36,ForeColor);coordsLabel.Font=UiTheme.Small;
            headingLabel=MakeLabel(UiText.T("Kierunek · —"),0,0,102,36,accent);headingLabel.Font=UiTheme.Small;
            navigationLabel=MakeLabel(UiText.T("Wybierz aktualną mapę"),0,0,180,36,muted);navigationLabel.Font=UiTheme.Small;
            Label[] metricLabels={statusLabel,timeLabel,ageLabel,coordsLabel,headingLabel,navigationLabel};
            for(int i=0;i<metricLabels.Length;i++){metricLabels[i].Dock=DockStyle.Fill;metricLabels[i].TextAlign=ContentAlignment.MiddleLeft;metricLabels[i].AutoEllipsis=true;metricLabels[i].Margin=new Padding(0,0,8,0);metrics.Controls.Add(metricLabels[i],i,0);}statusStrip.Controls.Add(metrics);
            fileLabel=new Label {Visible=false};countsLabel=new Label {Visible=false};mapHint=new Label {Visible=false};

            capturePanel=new Panel {Size=new Size(410,295),BackColor=UiTheme.Background};
            capturePanel.Controls.Add(new Label {Text=UiText.T("Folder zrzutów gry"),Location=new Point(0,8),AutoSize=true,Font=UiTheme.Section});
            var folderFrame=new Panel {Location=new Point(0,43),Size=new Size(406,34),Padding=new Padding(8),BackColor=UiTheme.Field,Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right};
            folderBox=new TextBox {Dock=DockStyle.Fill,BackColor=UiTheme.Field,ForeColor=UiTheme.Text,BorderStyle=BorderStyle.None,Text=initialFolder??"",Font=UiTheme.Small,AccessibleName=UiText.T("Ścieżka folderu zrzutów")};folderFrame.Controls.Add(folderBox);capturePanel.Controls.Add(folderFrame);
            browseButton=(UiButton)MakeButton(UiText.T("Wybierz folder…"),0,90,182);browseButton.AccessibleName=UiText.T("Wybierz folder zrzutów");
            browseButton.Click+=delegate{using(var dialog=new FolderBrowserDialog {Description=UiText.T("Folder zrzutów zapisanych przez EFT"),ShowNewFolderButton=false}){if(Directory.Exists(folderBox.Text))dialog.SelectedPath=folderBox.Text;if(dialog.ShowDialog(settingsWindow??(IWin32Window)this)==DialogResult.OK){folderBox.Text=dialog.SelectedPath;StartWatching();}}};
            startButton=MakeButton(UiText.T("Obserwuj"),0,135,124);stopButton=MakeButton(UiText.T("Zatrzymaj"),134,135,124);
            startButton.Click+=delegate{StartWatching();};stopButton.Click+=delegate{StopWatching();};folderBox.TextChanged+=delegate{UpdateWatchButtons();};
            deleteScreenshots=new CheckBox {Text=UiText.T("Usuwaj zrzut po odczycie"),AccessibleName=UiText.T("Usuwaj zrzut po odczycie"),Location=new Point(0,184),Size=new Size(400,26),Font=UiTheme.Body,Checked=preferences.DeleteScreenshots,Enabled=!demo};
            deleteScreenshots.CheckedChanged+=delegate{preferences.DeleteScreenshots=deleteScreenshots.Checked;SavePreferences();if(!deleteScreenshots.Checked && cleaner!=null){cleaner.Dispose();cleaner=null;}else if(monitor!=null && !isDemo)cleaner=new ScreenshotCleaner(monitor.Folder);};
            capturePanel.Controls.AddRange(new Control[]{browseButton,startButton,stopButton,deleteScreenshots,new Label {Text=UiText.T("Usuwa rozpoznany PNG po pokazaniu pozycji na mapie.\nPozostałe pliki zostają w folderze. Folder i wybór są\nzapamiętywane na następne uruchomienie."),Location=new Point(0,222),Size=new Size(406,60),Font=UiTheme.Small,ForeColor=UiTheme.Muted}});UpdateWatchButtons();

            detailsTip.SetToolTip(settingsButton,UiText.T("Wygląd, przezroczystość i folder zrzutów (Ctrl+,)"));detailsTip.SetToolTip(fullScreenButton,UiText.T("Pełny ekran / powrót · F11"));
            detailsTip.SetToolTip(markerButton,UiText.T("Wysuń questy i ekstrakty tej mapy · Ctrl+F"));detailsTip.SetToolTip(floatingSettings,UiText.T("Ustawienia · Ctrl+,"));detailsTip.SetToolTip(floatingQuest,UiText.T("Questy i ekstrakty · Ctrl+F"));detailsTip.SetToolTip(floatingExit,UiText.T("Powrót do okna · F11 / Esc"));
            detailsTip.SetToolTip(autoNavigation,UiText.T("Dopasuj piętro i widok po zrzucie. Automatyczną mapę włączysz w ustawieniach."));detailsTip.SetToolTip(follow,UiText.T("Centruj pozycję po nowym zrzucie."));detailsTip.SetToolTip(timeLabel,UiText.T("Czas ostatniego zrzutu; najedź po odczycie, aby zobaczyć nazwę pliku."));
            detailsTip.SetToolTip(folderBox,UiText.T("Folder screenshotów zapisanych przez EFT. Możesz wkleić ścieżkę."));
            var mapMenu=new ContextMenuStrip();var lastUpdate=new ToolStripMenuItem(UiText.T("Brak zrzutu")) {Enabled=false};var mapCredit=new ToolStripMenuItem(UiText.T("Mapa")) {Enabled=false};
            mapMenu.Items.Add(lastUpdate);mapMenu.Items.Add(mapCredit);mapMenu.Items.Add(new ToolStripSeparator());
            mapMenu.Opening+=delegate{lastUpdate.Text=displayed==null?UiText.T("Brak zrzutu"):UiText.T("Ostatni zrzut: ")+displayed.FileTimeUtc.ToLocalTime().ToString("HH:mm:ss");mapCredit.Text=canvas.Definition==null?UiText.T("Wczytywanie mapy…"):UiText.T("Mapa: ")+canvas.Definition.Author;};
            mapMenu.Items.Add(UiText.T("Ustawienia (Ctrl+,)"),null,delegate{ShowSettings();});mapMenu.Items.Add(UiText.T("Questy i ekstrakty (Ctrl+F)"),null,delegate{ShowMarkerFilters(!sidebar.Visible);});mapMenu.Items.Add(UiText.T("Pełny ekran / powrót (F11)"),null,delegate{SetFullScreen(!fullScreen);});
            mapMenu.Items.Add(UiText.T("Cała mapa"),null,delegate{canvas.FitMap();});mapMenu.Items.Add(UiText.T("Do znacznika"),null,delegate{canvas.CenterPosition();});mapMenu.Items.Add(UiText.T("Pełna widoczność (F10)"),null,delegate{RestoreVisibility();});canvas.ContextMenuStrip=mapMenu;
            mapBox.SelectedIndexChanged+=delegate{PopulateLayers();};layerBox.SelectedIndexChanged+=delegate{RequestMap();};layerBox.SelectionChangeCommitted+=delegate{autoNavigation.Checked=false;};
            mapWorker.DoWork+=LoadMapInBackground;mapWorker.RunWorkerCompleted+=MapLoaded;
            main.Controls.Add(workspace);main.Controls.Add(statusStrip);main.Controls.Add(toolbar);Controls.Add(main);Controls.Add(header);
            miniMap=new MiniMapOverlay(canvas,preferences);
            miniMap.ConfigurationChanged+=delegate{UpdateOverlaySettings();SavePreferences();if(settingsWindow!=null)settingsWindow.RefreshValues();};
            miniMap.EditingChanged+=delegate{RefreshOverlayStatus();};
            if(!isDemo)overlayHotkeys=new OverlayHotkeys(ToggleOverlay,ToggleOverlayEditing);
            ApplyAppearance(false);
            RefreshLanguage();

            timer.Interval=100;
            timer.Tick+=Tick;
            timer.Start();
            Shown+=delegate { PopulateLayers();ConfigureMapDetection(); if(Directory.Exists(folderBox.Text)) StartWatching();else if(!String.IsNullOrWhiteSpace(folderBox.Text))statusLabel.Text=UiText.T("Folder niedostępny. Wybierz go ponownie."); canvas.FitMap(); if(startFullScreen) SetFullScreen(true); UpdateOverlaySettings(); };
            FormClosed+=delegate { timer.Stop();if(overlayHotkeys!=null)overlayHotkeys.Dispose();if(miniMap!=null && !miniMap.IsDisposed)miniMap.Close(); SavePreferences();markerPanel.Save();mapWorker.CancelAsync(); if(cleaner!=null)cleaner.Dispose();if(monitor!=null) monitor.Dispose(); if(telemetry!=null) telemetry.Dispose(); ownProcess.Dispose(); };
        }
        void ShowMarkerFilters(bool show)
        {
            questOpen=show;sidebar.Visible=show;markerButton.Primary=show;markerButton.Invalidate();
            if(show){markerPanel.BringToFront();markerPanel.FocusSearch();}
        }
        public void ShowSettings()
        {
            if(settingsWindow!=null && !settingsWindow.IsDisposed){settingsWindow.Activate();return;}
            var dialog=new SettingsForm(preferences,cacheDirectory,capturePanel);settingsWindow=dialog;
            if(Icon!=null)dialog.Icon=(Icon)Icon.Clone();settingsButton.Primary=true;settingsButton.Invalidate();
            dialog.AppearanceChanged+=delegate{ApplyAppearance(true);};
            dialog.LanguageChanged+=delegate{RefreshLanguage();SavePreferences();};
            dialog.OverlayChanged+=delegate{UpdateOverlaySettings();SavePreferences();dialog.RefreshValues();};
            dialog.EditOverlayRequested+=delegate{ToggleOverlayEditing();};
            dialog.MapDetectionChanged+=delegate{ConfigureMapDetection();SavePreferences();dialog.RefreshValues();};
            dialog.RestoreVisibility+=delegate{RestoreVisibility();};
            dialog.FormClosed+=delegate{if(settingsWindow==dialog)settingsWindow=null;settingsButton.Primary=false;settingsButton.Invalidate();if(!IsDisposed && !Disposing)SavePreferences();};
            Rectangle area=Screen.FromControl(this).WorkingArea;
            dialog.StartPosition=FormStartPosition.Manual;dialog.Location=new Point(Math.Max(area.Left,Math.Min(area.Right-dialog.Width,Bounds.Right-dialog.Width-18)),Math.Max(area.Top,Math.Min(area.Bottom-dialog.Height,Bounds.Top+70)));
            dialog.Show(this);
            RefreshOverlayStatus();
        }
        void ApplyAppearance(bool save)
        {
            preferences.Normalize();UiTheme.CornerRadius=preferences.RoundedCorners?8:0;var palette=UiTheme.Change(preferences.Theme,preferences.AccentArgb);UiTheme.Recolor(this,palette);UiTheme.Recolor(capturePanel,palette);
            ApplyWindowTheme();
            if(settingsWindow!=null){UiTheme.Recolor(settingsWindow,palette);((ThemedForm)settingsWindow).ApplyWindowTheme();}
            canvas.ApplyAppearance(preferences,assetsDirectory);statusStrip.Visible=!fullScreen && preferences.ShowStatusBar;
            ApplyWindowOpacity();if(save)SavePreferences();
            UpdateOverlaySettings();
        }
        void UpdateOverlaySettings()
        {if(miniMap==null || miniMap.IsDisposed)return;miniMap.ApplySettings(Screen.FromControl(this));miniMap.Pulse(displayed==null?(DateTime?)null:displayed.FileTimeUtc);RefreshOverlayStatus();}
        void RefreshOverlayStatus()
        {if(settingsWindow!=null && !settingsWindow.IsDisposed)settingsWindow.SetOverlayStatus(miniMap!=null && miniMap.IsEditing,overlayHotkeys==null?UiText.T("Skróty globalne są wyłączone w trybie demonstracyjnym."):overlayHotkeys.Status);}
        public void ToggleOverlay()
        {preferences.OverlayEnabled=!preferences.OverlayEnabled;UpdateOverlaySettings();SavePreferences();if(settingsWindow!=null)settingsWindow.RefreshValues();}
        public void ToggleOverlayEditing()
        {
            if(!preferences.OverlayEnabled){preferences.OverlayEnabled=true;UpdateOverlaySettings();}
            miniMap.SetEditing(!miniMap.IsEditing);SavePreferences();if(settingsWindow!=null)settingsWindow.RefreshValues();RefreshOverlayStatus();
        }
        void ConfigureMapDetection()
        {
            if(gameMapMonitor!=null){gameMapMonitor.Dispose();gameMapMonitor=null;}gameMapRevision=0;
            if(preferences.AutoMapEnabled && !isDemo)
            {
                string folder=String.IsNullOrWhiteSpace(preferences.GameLogsFolder)?GameMapMonitor.FindDefaultFolder():GameMapMonitor.ResolveFolder(preferences.GameLogsFolder);
                if(folder!=null){preferences.GameLogsFolder=folder;gameMapMonitor=new GameMapMonitor(folder);}
            }
            RefreshMapDetectionStatus();
        }
        void RefreshMapDetectionStatus()
        {
            if(settingsWindow==null || settingsWindow.IsDisposed)return;
            string status=!preferences.AutoMapEnabled?UiText.T("Automatyczne przełączanie mapy jest wyłączone."):
                isDemo?UiText.T("Logi gry są wyłączone w trybie demonstracyjnym."):
                gameMapMonitor==null?UiText.T("Nie znaleziono logów. Wybierz folder Logs w katalogu gry."):
                gameMapMonitor.Status=="unavailable"?UiText.T("Logi chwilowo niedostępne. Ponawiam odczyt."):
                gameMapMonitor.Latest==null?UiText.T("Obserwuję logi. Oczekiwanie na załadowanie mapy."):
                UiText.T("Mapa wykryta z logów: ")+gameMapMonitor.Latest.MapId;
            settingsWindow.SetMapDetectionStatus(status);
        }
        void UpdateDetectedMap()
        {
            if(gameMapMonitor==null)return;
            DetectedGameMap detected=gameMapMonitor.Latest;
            if(detected==null || detected.Revision==gameMapRevision)return;
            gameMapRevision=detected.Revision;
            int index=Array.FindIndex(maps,delegate(MapDefinition m){return m.Id==detected.MapId;});
            if(index<0)return;
            positionNotBefore=detected.TimeUtc;
            if(displayed!=null && displayed.FileTimeUtc<positionNotBefore)
            {
                displayed=null;cleanupQueued=null;canvas.SetPosition(null);
                timeLabel.Text="—:—:—";coordsLabel.Text="X —   Z —   Y —";headingLabel.Text=UiText.T("Kierunek · —");
                if(navigator!=null)navigator.Reset();visualFocus=null;pendingFocus=null;pendingFit=false;
            }
            if(mapBox.SelectedIndex!=index)mapBox.SelectedIndex=index;
            navigationLabel.Text=UiText.T("Mapa wykryta z logów: ")+maps[index].Name;
        }
        void ApplyWindowOpacity()
        {Opacity=preferences.FullscreenOpacityOnly && !fullScreen?1:preferences.WindowOpacity/100.0;}
        void RefreshLanguage()
        {
            markerPanel.RefreshLanguage();
            UiText.Apply(this,detailsTip);UiText.Apply(capturePanel,detailsTip);
            MapDefinition selected=mapBox.SelectedItem as MapDefinition;
            Text=selected==null?UiText.T("EFT Position · 13 map"):"EFT Position · "+selected.Name;
            if(isDemo)Text+=UiText.T(" · DANE PRZYKŁADOWE");
            if(displayed!=null)headingLabel.Text=displayed.Position.HasHeading?
                UiText.Format("Kierunek · {0:F1}°",displayed.Position.Yaw):UiText.T("Pionowo · brak kierunku");
            mapProgress=UiText.T(mapProgress);canvas.LoadingText=UiText.T(canvas.LoadingText);
            ApplyWindowTheme();canvas.Invalidate();
            if(miniMap!=null){miniMap.RefreshLanguage();miniMap.Pulse(displayed==null?(DateTime?)null:displayed.FileTimeUtc);}RefreshOverlayStatus();
        }
        void RestoreVisibility()
        {preferences.WindowOpacity=100;ApplyWindowOpacity();if(settingsWindow!=null)settingsWindow.RefreshValues();SavePreferences();}
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
            navigationLabel.Text=UiText.T(decision.ContextName)+"\n"+UiText.T(decision.SourceLevelName??"Poziom bazowy");
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
                { visualFocus=building; canvas.FocusArea(building); navigationLabel.Text=UiText.T("Rozpoznany budynek\nWidok dopasowany do wnętrza"); }
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
                    header.Visible=false;sidebar.Visible=false;toolbar.Visible=false;statusStrip.Visible=false;fullscreenBar.Visible=true;
                    canvas.MapOnly=true; Bounds=screen;
                }
                else
                {
                    fullScreen=false; WindowState=FormWindowState.Normal; FormBorderStyle=FormBorderStyle.Sizable;
                    header.Visible=true;sidebar.Visible=questOpen;toolbar.Visible=true;statusStrip.Visible=preferences.ShowStatusBar;fullscreenBar.Visible=false;
                    canvas.MapOnly=false; Bounds=savedBounds; MinimumSize=savedMinimum; WindowState=savedWindowState;
                }
            }
            finally { ResumeLayout(true); }
            ApplyWindowOpacity();fullscreenBar.BringToFront();
            canvas.Focus(); canvas.Invalidate();
        }
        protected override bool ProcessCmdKey(ref Message msg,Keys keyData)
        {
            if(keyData==(Keys.Control|Keys.F)){ShowMarkerFilters(!sidebar.Visible);return true;}
            if(keyData==(Keys.Control|Keys.Oemcomma)){ShowSettings();return true;}
            if(keyData==Keys.F10){RestoreVisibility();return true;}
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
            mapProgress=level.Remote?UiText.T("Wczytywanie mapy; pierwszy raz wymaga internetu…"):UiText.T("Wczytywanie mapy…");
            canvas.LoadingText=mapProgress;
            mapSubtitle.Text=selected.Name.ToUpperInvariant()+" · "+UiText.T(level.Name);
            Text="EFT Position · "+selected.Name+(isDemo?UiText.T(" · DANE PRZYKŁADOWE"):"");
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
                canvas.LoadingText=UiText.T("Nie udało się wczytać mapy.\nSprawdź internet lub pliki programu i wybierz mapę ponownie.\n")+UiText.Error(e.Error.GetBaseException().Message);
                canvas.Invalidate(); return;
            }
            try
            {
                canvas.LoadMap(job.Map,job.Layer,job.Path,cacheDirectory); completedMapLoads++;
                if(autoNavigation.Checked && displayed!=null) ApplyNavigation(displayed.Position);
                ApplyPendingView();
            }
            catch(Exception ex) { canvas.LoadingText=UiText.T("Nie mogę otworzyć mapy: ")+UiText.Error(ex.Message); canvas.Invalidate(); }
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
                if (String.IsNullOrWhiteSpace(path)) throw new IOException(UiText.T("Wybierz folder zrzutów."));
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
                timeLabel.Text="—:—:—"; coordsLabel.Text="X —   Z —   Y —";
                headingLabel.Text=UiText.T("Kierunek · —"); fileLabel.Text=UiText.T("Oczekiwanie na poprawną nazwę pliku…");
                navigationLabel.Text=autoNavigation.Checked?(preferences.AutoMapEnabled?UiText.T("Oczekiwanie na mapę i pozycję"):UiText.T("Oczekiwanie na pozycję\nMapę wybierasz ręcznie.")):UiText.T("Sterowanie ręczne\nWybierz poziom z menu.");
                UpdateWatchButtons();
            }
            catch(Exception ex)
            {
                if(!(ex is IOException) && !(ex is UnauthorizedAccessException) && !(ex is ArgumentException)) throw;
                MessageBox.Show(this,UiText.Error(ex.Message),UiText.T("Nie mogę obserwować folderu"),MessageBoxButtons.OK,MessageBoxIcon.Information);
            }
        }
        void StopWatching()
        {
            if(cleaner!=null){cleaner.Dispose();cleaner=null;}
            if(monitor!=null) { monitor.Dispose(); monitor=null; }
            statusLabel.Text=UiText.T("Obserwacja wstrzymana.\nOstatnia pozycja zachowana.");statusLabel.ForeColor=muted;
            UpdateWatchButtons();
        }
        void SavePreferences()
        {
            if(isDemo)return;
            preferences.ScreenshotFolder=folderBox.Text.Trim().Trim('"');
            if(!preferences.Save(preferencesFile))detailsTip.SetToolTip(folderBox,UiText.T("Nie udało się zapisać ustawień. Sprawdź dostęp do folderu programu."));
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
            UpdateDetectedMap();RefreshMapDetectionStatus();RefreshOverlayStatus();
            if(monitor!=null)
            {
                statusLabel.Text=monitor.Status.StartsWith("Obserwuję",StringComparison.Ordinal)?UiText.T("● Obserwacja aktywna"):UiText.T(monitor.Status);
                statusLabel.ForeColor=monitor.Status.StartsWith("Obserwuję",StringComparison.Ordinal)?accent:Color.FromArgb(244,183,83);
                countsLabel.Text=UiText.T("Aktualizacje: ")+monitor.Accepted+UiText.T("  •  Pominięte nazwy: ")+monitor.Ignored;
                Snapshot s=monitor.Latest;
                if(s!=null && s!=displayed && s.FileTimeUtc>=positionNotBefore)
                {
                    displayed=s;
                    canvas.SetPosition(s.Position);
                    ApplyNavigation(s.Position);
                    timeLabel.Text=s.FileTimeUtc.ToLocalTime().ToString("HH:mm:ss");
                    coordsLabel.Text=String.Format(CultureInfo.InvariantCulture,"X {0:F1}   Z {1:F1}   Y {2:F1}",s.Position.X,s.Position.Z,s.Position.Y);
                    headingLabel.Text=s.Position.HasHeading ? String.Format(CultureInfo.InvariantCulture,UiText.T("Kierunek · {0:F1}°"),s.Position.Yaw) : UiText.T("Pionowo · brak kierunku");
                    fileLabel.Text=s.Position.FileName;
                    detailsTip.SetToolTip(timeLabel,s.Position.FileName);
                }
            }
            bool stale=true;
            if(displayed!=null)
            {
                double age=Math.Max(0,(DateTime.UtcNow-displayed.FileTimeUtc).TotalSeconds);
                stale=age>=30;
                string ageText=age<60 ? ((int)age)+UiText.T(" s temu") : age<3600 ? ((int)(age/60))+UiText.T(" min temu") : ((int)(age/3600))+UiText.T(" h temu");
                bool onMap=canvas.Layer!=null && canvas.Layer.Contains(displayed.Position);
                ageLabel.Text=canvas.Layer==null ? UiText.T("Wczytywanie mapy…\nPozycja z ")+displayed.FileTimeUtc.ToLocalTime().ToString("HH:mm:ss") : onMap ? (stale?UiText.T("Starszy"):UiText.T("Zrzut"))+" · "+ageText+"\n"+displayed.FileTimeUtc.ToLocalTime().ToString("dd.MM.yyyy") : UiText.T("Poza obszarem mapy\nSprawdź wybraną lokację.");
                ageLabel.ForeColor=stale || !onMap ? Color.FromArgb(244,183,83) : accent;
            }
            else if(monitor!=null && monitor.Ignored>0)
            {ageLabel.Text=UiText.T("Nie rozpoznano pozycji\nPominięte PNG: ")+monitor.Ignored;ageLabel.ForeColor=Color.FromArgb(244,183,83);}
            else {ageLabel.Text=UiText.T("Brak pozycji\nWykonaj zrzut w grze.");ageLabel.ForeColor=muted;}
            if(stale!=oldStale) { canvas.Stale=stale; canvas.Invalidate(); oldStale=stale; }
            canvas.RefreshTiles();
            if(miniMap!=null)miniMap.Pulse(displayed==null?(DateTime?)null:displayed.FileTimeUtc);
            bool mainMapVisible=Visible && WindowState!=FormWindowState.Minimized && canvas.Visible;
            bool miniMapVisible=miniMap!=null && miniMap.Visible && canvas.MiniMapHasPosition;
            if(cleaner!=null && displayed!=null && displayed!=cleanupQueued && !mapWorker.IsBusy && canvas.Definition!=null &&
                requestedMap!=null && canvas.Definition.Id==requestedMap.Id && canvas.Layer.Contains(displayed.Position) && (mainMapVisible || miniMapVisible))
            {
                if(mainMapVisible)canvas.Update();
                if(miniMapVisible)miniMap.Update(); // Also acknowledge a completed minimap repaint when the main window is minimized.
                cleaner.ConfirmDisplayed(displayed);cleanupQueued=displayed;
            }
            if(cleaner!=null && cleaner.Failed>0)detailsTip.SetToolTip(deleteScreenshots,UiText.T("Nie udało się usunąć części zrzutów. Pozostały w folderze. Usunięto: ")+cleaner.Deleted);
            if(mapWorker.IsBusy)
            {
                string progress=mapProgress;
                const string prefix="Pobieranie mapy: ";
                canvas.LoadingText=progress!=null && progress.StartsWith(prefix,StringComparison.Ordinal)?UiText.T(prefix)+progress.Substring(prefix.Length):UiText.T(progress);
                canvas.Invalidate();
            }
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
            if(disposing && gameMapMonitor!=null){gameMapMonitor.Dispose();gameMapMonitor=null;}
            if(disposing) {if(overlayHotkeys!=null)overlayHotkeys.Dispose();if(miniMap!=null)miniMap.Dispose();if(settingsWindow!=null && !settingsWindow.IsDisposed)settingsWindow.Close();capturePanel.Dispose();timer.Dispose(); detailsTip.Dispose(); titleFont.Dispose(); valueFont.Dispose(); }
            base.Dispose(disposing);
        }
    }
}


