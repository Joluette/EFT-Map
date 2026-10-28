// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace EftPosition
{
    public sealed class SettingsForm : ThemedForm
    {
        readonly AppPreferences preferences;
        readonly string cache;
        readonly Panel capturePanel;
        readonly FlowLayoutPanel appearance,windowPage,overlayPage;
        readonly Panel capturePage;
        readonly ComboBox language,theme,background;
        readonly Label imageName;
        readonly TrackBar dim,mapOpacity,transparency,markerSize;
        readonly CheckBox fullscreenOnly,statusBar,rounded;
        readonly UiButton appearanceTab,windowTab,captureTab,overlayTab;
        readonly CheckBox overlayEnabled;
        readonly ComboBox overlayMonitor,overlayCorner;
        readonly TrackBar overlaySize,overlayZoom,overlayTransparency;
        readonly UiButton overlayEdit;
        readonly Label overlayStatus;
        readonly CheckBox autoMap;
        readonly TextBox logsFolder;
        readonly Label mapDetectionStatus;
        readonly string[] monitorIds;
        bool rebuilding;
        public event EventHandler AppearanceChanged;
        public event EventHandler RestoreVisibility;
        public event EventHandler LanguageChanged;
        public event EventHandler OverlayChanged,EditOverlayRequested;
        public event EventHandler MapDetectionChanged;
        public SettingsForm(AppPreferences current,string cacheDirectory,Panel capture)
        {
            preferences=current;cache=cacheDirectory;capturePanel=capture;
            Text=UiText.T("Ustawienia · EFT Position");ClientSize=new Size(470,630);MinimumSize=new Size(486,570);
            StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.SizableToolWindow;
            BackColor=UiTheme.Background;ForeColor=UiTheme.Text;Font=UiTheme.Body;ShowInTaskbar=false;Opacity=1;KeyPreview=true;
            AutoScaleMode=AutoScaleMode.Dpi;
            var header=new Panel {Dock=DockStyle.Top,Height=82,Padding=new Padding(22,16,22,0),BackColor=UiTheme.Surface};
            header.Controls.Add(new Label {Text=UiText.T("Dopasuj widok do siebie"),Font=UiTheme.Section,AutoSize=true,Location=new Point(22,17)});
            header.Controls.Add(new Label {Text=UiText.T("Zmiany widać od razu i zapisują się automatycznie."),Font=UiTheme.Small,ForeColor=UiTheme.Muted,AutoSize=true,Location=new Point(22,43)});
            var tabs=new FlowLayoutPanel {Dock=DockStyle.Top,Height=54,Padding=new Padding(20,11,0,0),WrapContents=false};
            appearanceTab=new UiButton {Text=UiText.T("Wygląd"),Width=100,Height=32,AccessibleName=UiText.T("Ustawienia wyglądu")};
            windowTab=new UiButton {Text=UiText.T("Okno i mapa"),Width=118,Height=32,AccessibleName=UiText.T("Ustawienia okna i mapy")};
            captureTab=new UiButton {Text=UiText.T("Zrzuty"),Width=96,Height=32,AccessibleName=UiText.T("Ustawienia zrzutów")};
            overlayTab=new UiButton {Text=UiText.T("Minimapa"),Width=102,Height=32,AccessibleName=UiText.T("Ustawienia minimapy")};
            tabs.Controls.AddRange(new Control[]{appearanceTab,windowTab,captureTab,overlayTab});
            var body=new Panel {Dock=DockStyle.Fill,Padding=new Padding(20,6,20,8)};
            appearance=Page();windowPage=Page();overlayPage=Page();capturePage=new Panel {Dock=DockStyle.Fill,AutoScroll=true,Visible=false};
            body.Controls.AddRange(new Control[]{appearance,windowPage,capturePage,overlayPage});
            language=Combo("Język / Language",new[]{"Polski (PL)","English (EN)"},appearance);
            theme=Combo(UiText.T("Motyw kolorystyczny"),new[]{UiText.T("Tarkov · grafit i piasek"),UiText.T("Noc · chłodny turkus"),UiText.T("Las · oliwka")},appearance);
            var accentRow=new Panel {Width=410,Height=48,Margin=Padding.Empty};
            var accentLabel=new Label {Text=UiText.T("Kolor akcentu"),AutoSize=true,Location=new Point(0,8),ForeColor=UiTheme.Muted};
            var accent=new UiButton {Text=UiText.T("Wybierz kolor…"),Location=new Point(222,0),Size=new Size(174,32),AccessibleName=UiText.T("Wybierz kolor akcentu")};
            accentRow.Controls.AddRange(new Control[]{accentLabel,accent});appearance.Controls.Add(accentRow);
            rounded=Check(UiText.T("Zaokrąglone krawędzie kontrolek i list"),appearance,UiText.T("Zaokrąglone krawędzie"));
            background=Combo(UiText.T("Tło aplikacji"),new[]{UiText.T("Grafika Tarkova"),UiText.T("Własny obraz"),UiText.T("Jednolity kolor"),"Tarkov × Cyberpunk (HD)"},appearance);
            var imageRow=new Panel {Width=410,Height=48,Margin=Padding.Empty};
            var choose=new UiButton {Text=UiText.T("Wybierz obraz…"),Location=Point.Empty,Size=new Size(144,32),AccessibleName=UiText.T("Wybierz własne tło")};
            imageName=new Label {Location=new Point(154,7),Size=new Size(244,22),Font=UiTheme.Small,ForeColor=UiTheme.Muted,AutoEllipsis=true};
            imageRow.Controls.AddRange(new Control[]{choose,imageName});appearance.Controls.Add(imageRow);
            dim=Slider(UiText.T("Przyciemnienie tła"),0,90,appearance,UiText.T("Przyciemnienie tła"));
            mapOpacity=Slider(UiText.T("Krycie mapy"),40,100,appearance,UiText.T("Krycie mapy"));
            var restore=new UiButton {Text=UiText.T("Przywróć wygląd domyślny"),Width=252,Height=32,Margin=new Padding(0,6,0,12),AccessibleName=UiText.T("Przywróć wygląd domyślny")};appearance.Controls.Add(restore);
            transparency=Slider(UiText.T("Przezroczystość całego okna"),0,70,windowPage,UiText.T("Przezroczystość okna"));
            fullscreenOnly=Check(UiText.T("Stosuj tylko na pełnym ekranie"),windowPage,UiText.T("Przezroczystość tylko na pełnym ekranie"));
            windowPage.Controls.Add(new Label {Text=UiText.T("Odsłania pulpit lub aplikację pod tym oknem.\nMapa i znaczniki również stają się przezroczyste."),Width=404,Height=48,Font=UiTheme.Small,ForeColor=UiTheme.Muted,Margin=new Padding(0,3,0,10)});
            var readable=new UiButton {Text=UiText.T("Pełna widoczność · F10"),Width=252,Height=32,Margin=new Padding(0,0,0,18),AccessibleName=UiText.T("Przywróć pełną widoczność")};windowPage.Controls.Add(readable);
            markerSize=Slider(UiText.T("Rozmiar znaczników"),80,150,windowPage,UiText.T("Rozmiar znaczników"));
            statusBar=Check(UiText.T("Pokaż dolny pasek pozycji"),windowPage,UiText.T("Pokaż pasek pozycji"));
            windowPage.Controls.Add(new Label {Text=UiText.T("Krycie mapy zmienia tylko rysunek mapy.\nZnaczniki pozostają czytelne na własnym tle."),Width=404,Height=44,Font=UiTheme.Small,ForeColor=UiTheme.Muted,Margin=new Padding(0,10,0,0)});
            autoMap=Check(UiText.T("Automatycznie przełączaj mapę z logów gry"),windowPage,UiText.T("Automatyczna mapa"));
            windowPage.Controls.Add(new Label {Text=UiText.T("Nazwa mapy pochodzi z lokalnych logów EFT.\nPozycję nadal aktualizuje nowy zrzut ekranu."),Width=404,Height=44,Font=UiTheme.Small,ForeColor=UiTheme.Muted,Margin=new Padding(0,5,0,5)});
            logsFolder=new TextBox {Width=396,ReadOnly=true,BackColor=UiTheme.Field,ForeColor=UiTheme.Text,BorderStyle=BorderStyle.FixedSingle,Font=UiTheme.Small,AccessibleName=UiText.T("Folder logów gry")};windowPage.Controls.Add(logsFolder);
            var logButtons=new FlowLayoutPanel {Width=410,Height=42,Margin=new Padding(0,7,0,0)};
            var findLogs=new UiButton {Text=UiText.T("Wykryj folder"),Width=166,Height=32,AccessibleName=UiText.T("Wykryj folder logów")};
            var chooseLogs=new UiButton {Text=UiText.T("Wybierz folder…"),Width=166,Height=32,AccessibleName=UiText.T("Wybierz folder logów")};
            logButtons.Controls.AddRange(new Control[]{findLogs,chooseLogs});windowPage.Controls.Add(logButtons);
            mapDetectionStatus=new Label {Width=404,Height=66,Font=UiTheme.Small,ForeColor=UiTheme.Muted};windowPage.Controls.Add(mapDetectionStatus);
            autoMap.CheckedChanged+=delegate{if(rebuilding)return;preferences.AutoMapEnabled=autoMap.Checked;RaiseMapDetectionChanged();};
            findLogs.Click+=delegate{string found=GameMapMonitor.FindDefaultFolder();if(found!=null){preferences.GameLogsFolder=found;RefreshValues();RaiseMapDetectionChanged();}else SetMapDetectionStatus(UiText.T("Nie znaleziono logów. Wybierz folder Logs w katalogu gry."));};
            chooseLogs.Click+=delegate{using(var dialog=new FolderBrowserDialog {Description=UiText.T("Wybierz folder Logs lub katalog instalacji EFT"),ShowNewFolderButton=false})if(dialog.ShowDialog(this)==DialogResult.OK){string found=GameMapMonitor.ResolveFolder(dialog.SelectedPath);if(found!=null){preferences.GameLogsFolder=found;RefreshValues();RaiseMapDetectionChanged();}else SetMapDetectionStatus(UiText.T("Nie znaleziono logów. Wybierz folder Logs w katalogu gry."));}};
            overlayEnabled=Check(UiText.T("Pokaż minimapę nad grą"),overlayPage,UiText.T("Włącz minimapę"));
            overlayPage.Controls.Add(new Label {Text=UiText.T("Do gry w oknie bez ramek. Kliknięcia przechodzą do gry.\nPozycja zmienia się po nowym zrzucie.\nMinimapa działa też po zminimalizowaniu aplikacji."),Width=404,Height=64,Font=UiTheme.Small,ForeColor=UiTheme.Muted,Margin=new Padding(0,7,0,8)});
            Screen[] screens=Screen.AllScreens;monitorIds=new string[screens.Length+1];monitorIds[0]="";
            string[] monitorLabels=new string[screens.Length+1];monitorLabels[0]=UiText.T("Monitor głównego okna");
            for(int i=0;i<screens.Length;i++){monitorIds[i+1]=screens[i].DeviceName;monitorLabels[i+1]=(i+1)+" · "+screens[i].Bounds.Width+" × "+screens[i].Bounds.Height+(screens[i].Primary?" ★":"");}
            overlayMonitor=Combo(UiText.T("Monitor minimapy"),monitorLabels,overlayPage);
            overlayCorner=Combo(UiText.T("Położenie minimapy"),new[]{UiText.T("Lewy górny"),UiText.T("Prawy górny"),UiText.T("Lewy dolny"),UiText.T("Prawy dolny"),UiText.T("Własne położenie")},overlayPage);
            overlaySize=Slider(UiText.T("Rozmiar minimapy"),240,520,overlayPage,UiText.T("Rozmiar minimapy")," px");
            overlayZoom=Slider(UiText.T("Przybliżenie minimapy"),100,400,overlayPage,UiText.T("Przybliżenie minimapy"));
            overlayTransparency=Slider(UiText.T("Przezroczystość minimapy"),0,70,overlayPage,UiText.T("Przezroczystość minimapy"));
            overlayEdit=new UiButton {Text=UiText.T("Ustaw minimapę"),Width=252,Height=32,AccessibleName=UiText.T("Ustaw położenie minimapy"),Margin=new Padding(0,4,0,8)};overlayPage.Controls.Add(overlayEdit);
            overlayPage.Controls.Add(new Label {Text=UiText.T("Ctrl+Alt+M lub F8 — pokaż / ukryj\nCtrl+Alt+Shift+M — ustawianie\nW trybie ustawiania przeciągnij okno lub jego prawy dolny róg."),Width=404,Height=70,Font=UiTheme.Small,ForeColor=UiTheme.Muted,Margin=new Padding(0,2,0,8)});
            overlayStatus=new Label {Width=404,Height=48,Font=UiTheme.Small,ForeColor=UiTheme.Muted,Margin=Padding.Empty};overlayPage.Controls.Add(overlayStatus);
            capturePanel.Dock=DockStyle.Top;capturePage.Controls.Add(capturePanel);
            var footer=new Panel {Dock=DockStyle.Bottom,Height=52,Padding=new Padding(20,9,20,10),BackColor=UiTheme.Surface};
            var close=new UiButton {Text=UiText.T("Gotowe"),Dock=DockStyle.Right,Width=110,AccessibleName=UiText.T("Zamknij ustawienia"),Primary=true};close.Click+=delegate{Close();};footer.Controls.Add(close);
            footer.Controls.Add(new Label {Text="v"+Application.ProductVersion+"  ·  Ctrl+,",Dock=DockStyle.Left,Width=225,Font=UiTheme.Small,ForeColor=UiTheme.Muted,TextAlign=ContentAlignment.MiddleLeft});
            Controls.Add(body);Controls.Add(footer);Controls.Add(tabs);Controls.Add(header);
            appearanceTab.Click+=delegate{SelectPage(0);};windowTab.Click+=delegate{SelectPage(1);};captureTab.Click+=delegate{SelectPage(2);};
            overlayTab.Click+=delegate{SelectPage(3);};
            overlayEnabled.CheckedChanged+=delegate{if(rebuilding)return;preferences.OverlayEnabled=overlayEnabled.Checked;RaiseOverlayChanged();};
            overlayMonitor.SelectedIndexChanged+=delegate{if(rebuilding)return;if(overlayMonitor.SelectedIndex>=0)preferences.OverlayMonitor=monitorIds[overlayMonitor.SelectedIndex];preferences.OverlayCustomPosition=false;RaiseOverlayChanged();};
            overlayCorner.SelectedIndexChanged+=delegate{if(rebuilding)return;int index=overlayCorner.SelectedIndex;preferences.OverlayCustomPosition=index==4;if(index>=0 && index<4)preferences.OverlayCorner=new[]{"top-left","top-right","bottom-left","bottom-right"}[index];RaiseOverlayChanged();};
            overlaySize.ValueChanged+=delegate{if(rebuilding)return;preferences.OverlaySize=overlaySize.Value;RaiseOverlayChanged();};
            overlayZoom.ValueChanged+=delegate{if(rebuilding)return;preferences.OverlayZoom=overlayZoom.Value;RaiseOverlayChanged();};
            overlayTransparency.ValueChanged+=delegate{if(rebuilding)return;preferences.OverlayOpacity=100-overlayTransparency.Value;RaiseOverlayChanged();};
            overlayEdit.Click+=delegate{if(EditOverlayRequested!=null)EditOverlayRequested(this,EventArgs.Empty);};
            language.SelectedIndexChanged+=delegate
            {
                if(rebuilding)return;
                preferences.Language=language.SelectedIndex==1?"en":"pl";UiText.Language=preferences.Language;
                RefreshLanguage();if(LanguageChanged!=null)LanguageChanged(this,EventArgs.Empty);
            };
            theme.SelectedIndexChanged+=delegate {if(rebuilding)return;preferences.Theme=theme.SelectedIndex==1?"night":theme.SelectedIndex==2?"forest":"tarkov";preferences.AccentArgb=0;RaiseChanged();};
            background.SelectedIndexChanged+=delegate {if(rebuilding)return;preferences.BackgroundMode=background.SelectedIndex==1?"custom":background.SelectedIndex==2?"plain":background.SelectedIndex==3?"cyberpunk":"tarkov";RaiseChanged();};
            dim.ValueChanged+=delegate {if(rebuilding)return;preferences.BackgroundDim=dim.Value;RaiseChanged();};
            mapOpacity.ValueChanged+=delegate {if(rebuilding)return;preferences.MapOpacity=mapOpacity.Value;RaiseChanged();};
            transparency.ValueChanged+=delegate {if(rebuilding)return;preferences.WindowOpacity=100-transparency.Value;RaiseChanged();};
            fullscreenOnly.CheckedChanged+=delegate {if(rebuilding)return;preferences.FullscreenOpacityOnly=fullscreenOnly.Checked;RaiseChanged();};
            markerSize.ValueChanged+=delegate {if(rebuilding)return;preferences.MarkerSize=markerSize.Value;RaiseChanged();};
            statusBar.CheckedChanged+=delegate {if(rebuilding)return;preferences.ShowStatusBar=statusBar.Checked;RaiseChanged();};
            rounded.CheckedChanged+=delegate {if(rebuilding)return;preferences.RoundedCorners=rounded.Checked;RaiseChanged();};
            accent.Click+=delegate {using(var dialog=new ColorDialog {Color=UiTheme.Accent,FullOpen=true})if(dialog.ShowDialog(this)==DialogResult.OK){preferences.AccentArgb=dialog.Color.ToArgb();RaiseChanged();}};
            choose.Click+=delegate {ChooseBackground();};restore.Click+=delegate{preferences.ResetAppearance();RefreshValues();RaiseChanged();};
            readable.Click+=delegate{if(RestoreVisibility!=null)RestoreVisibility(this,EventArgs.Empty);RefreshValues();};
            FormClosing+=delegate {capturePage.Controls.Remove(capturePanel);};
            KeyDown+=delegate(object sender,KeyEventArgs e){if(e.KeyCode==Keys.Escape){e.Handled=true;Close();}else if(e.KeyCode==Keys.F10){if(RestoreVisibility!=null)RestoreVisibility(this,EventArgs.Empty);RefreshValues();e.Handled=true;}};
            RefreshValues();RefreshLanguage();SelectPage(0);
            Shown+=delegate{Rectangle area=Screen.FromControl(this).WorkingArea;MinimumSize=new Size(Math.Min(MinimumSize.Width,area.Width),Math.Min(MinimumSize.Height,area.Height-12));Size=new Size(Math.Min(Width,area.Width),Math.Min(Height,area.Height-12));Location=new Point(Math.Max(area.Left,Math.Min(Left,area.Right-Width)),Math.Max(area.Top,Math.Min(Top,area.Bottom-Height)));};
        }
        FlowLayoutPanel Page(){return new FlowLayoutPanel {Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Visible=false,Margin=Padding.Empty};}
        ComboBox Combo(string caption,string[] items,FlowLayoutPanel page)
        {
            var row=new Panel {Width=410,Height=62,Margin=Padding.Empty};
            row.Controls.Add(new Label {Text=caption,ForeColor=UiTheme.Muted,Location=Point.Empty,AutoSize=true,Font=UiTheme.Small});
            var box=new UiComboBox {Location=new Point(0,22),Width=396,AccessibleName=caption};box.Items.AddRange(items);row.Controls.Add(box);page.Controls.Add(row);return box;
        }
        CheckBox Check(string text,FlowLayoutPanel page,string name)
        {var box=new CheckBox {Text=text,Width=402,Height=28,Margin=Padding.Empty,ForeColor=UiTheme.Text,AccessibleName=name};page.Controls.Add(box);return box;}
        TrackBar Slider(string caption,int low,int high,FlowLayoutPanel page,string name,string suffix="%")
        {
            var row=new Panel {Width=410,Height=64,Margin=Padding.Empty};
            var label=new Label {Text=caption,AutoSize=true,Font=UiTheme.Small,ForeColor=UiTheme.Muted};
            var value=new Label {Location=new Point(343,0),Size=new Size(55,22),TextAlign=ContentAlignment.TopRight,Font=UiTheme.Small,ForeColor=UiTheme.Text};
            var bar=new TrackBar {Location=new Point(-8,21),Size=new Size(410,39),AutoSize=false,Minimum=low,Maximum=high,TickStyle=TickStyle.None,SmallChange=1,LargeChange=10,AccessibleName=name,BackColor=UiTheme.Background};
            bar.ValueChanged+=delegate{value.Text=bar.Value+suffix;};value.Text=bar.Value+suffix;row.Controls.AddRange(new Control[]{label,value,bar});page.Controls.Add(row);return bar;
        }
        void RaiseChanged(){if(AppearanceChanged!=null)AppearanceChanged(this,EventArgs.Empty);}
        void RaiseOverlayChanged(){if(OverlayChanged!=null)OverlayChanged(this,EventArgs.Empty);}
        void RaiseMapDetectionChanged(){if(MapDetectionChanged!=null)MapDetectionChanged(this,EventArgs.Empty);}
        void SelectPage(int page)
        {appearance.Visible=page==0;windowPage.Visible=page==1;capturePage.Visible=page==2;overlayPage.Visible=page==3;appearanceTab.Primary=page==0;windowTab.Primary=page==1;captureTab.Primary=page==2;overlayTab.Primary=page==3;appearanceTab.Invalidate();windowTab.Invalidate();captureTab.Invalidate();overlayTab.Invalidate();}
        public void ShowCapturePage(){SelectPage(2);}
        public void ShowOverlayPage(){SelectPage(3);}
        public void ShowWindowPage(){SelectPage(1);}
        public void SetMapDetectionStatus(string status){mapDetectionStatus.Text=status;}
        public void SetOverlayStatus(bool editing,string status)
        {overlayEdit.Text=editing?UiText.T("Zablokuj minimapę"):UiText.T("Ustaw minimapę");overlayStatus.Text=status;}
        public void RefreshLanguage()
        {rebuilding=true;try{UiText.Apply(this);}finally{rebuilding=false;}RefreshValues();}
        public void RefreshValues()
        {
            rebuilding=true;
            language.SelectedIndex=preferences.Language=="en"?1:0;
            autoMap.Checked=preferences.AutoMapEnabled;logsFolder.Text=preferences.GameLogsFolder??"";
            overlayEnabled.Checked=preferences.OverlayEnabled;overlaySize.Value=preferences.OverlaySize;overlayZoom.Value=preferences.OverlayZoom;overlayTransparency.Value=100-preferences.OverlayOpacity;
            int monitorIndex=Array.IndexOf(monitorIds,preferences.OverlayMonitor??"");overlayMonitor.SelectedIndex=Math.Max(0,monitorIndex);
            overlayCorner.SelectedIndex=preferences.OverlayCustomPosition?4:preferences.OverlayCorner=="top-left"?0:preferences.OverlayCorner=="bottom-left"?2:preferences.OverlayCorner=="bottom-right"?3:1;
            try{theme.SelectedIndex=preferences.Theme=="night"?1:preferences.Theme=="forest"?2:0;background.SelectedIndex=preferences.BackgroundMode=="custom"?1:preferences.BackgroundMode=="plain"?2:preferences.BackgroundMode=="cyberpunk"?3:0;dim.Value=preferences.BackgroundDim;mapOpacity.Value=preferences.MapOpacity;transparency.Value=100-preferences.WindowOpacity;markerSize.Value=preferences.MarkerSize;fullscreenOnly.Checked=preferences.FullscreenOpacityOnly;statusBar.Checked=preferences.ShowStatusBar;rounded.Checked=preferences.RoundedCorners;imageName.Text=String.IsNullOrEmpty(preferences.BackgroundFile)?UiText.T("JPG / PNG · do 12 MB"):UiText.T("Własny obraz zapisany");}
            finally{rebuilding=false;}
        }
        void ChooseBackground()
        {
            using(var dialog=new OpenFileDialog {Title=UiText.T("Wybierz tło aplikacji"),Filter=UiText.T("Obrazy JPG i PNG|*.jpg;*.jpeg;*.png|Obrazy BMP|*.bmp"),CheckFileExists=true})
            {
                if(dialog.ShowDialog(this)!=DialogResult.OK)return;
                try
                {
                    ImportBackground(dialog.FileName);
                }
                catch(Exception ex){if(!(ex is IOException) && !(ex is UnauthorizedAccessException) && !(ex is ArgumentException) && !(ex is OutOfMemoryException))throw;MessageBox.Show(this,UiText.Error(ex.Message),UiText.T("Nie można ustawić tła"),MessageBoxButtons.OK,MessageBoxIcon.Information);}
            }
        }
        public void ImportBackground(string source)
        {
            using(Bitmap test=MapBackdrop.ReadImage(source)){}
            string folder=Path.Combine(cache,"appearance");Directory.CreateDirectory(folder);string file=Path.Combine(folder,"background-image");
            File.Copy(source,file+".part",true);if(File.Exists(file))File.Replace(file+".part",file,null);else File.Move(file+".part",file);
            preferences.BackgroundFile=file;preferences.BackgroundMode="custom";preferences.BackgroundRevision=preferences.BackgroundRevision==Int64.MaxValue?0:preferences.BackgroundRevision+1;RefreshValues();RaiseChanged();
        }
    }
}
