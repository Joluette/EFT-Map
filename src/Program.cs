// SPDX-License-Identifier: GPL-3.0-only
using System;
using System.Windows.Forms;

namespace EftPosition
{
    static class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try
            {
                string folder=null, telemetry=null, preview=null, cache=null, map=null;
                int seconds=0;
                bool exercise=false, demo=false,cycle=false,fullscreen=false;
                for(int i=0;i<args.Length;i++)
                {
                    if(args[i]=="--folder" && i+1<args.Length) folder=args[++i];
                    else if(args[i]=="--map" && i+1<args.Length) map=args[++i];
                    else if(args[i]=="--telemetry" && i+1<args.Length) telemetry=args[++i];
                    else if(args[i]=="--exit-after" && i+1<args.Length) seconds=Int32.Parse(args[++i]);
                    else if(args[i]=="--exercise-view") exercise=true;
                    else if(args[i]=="--cycle-maps") cycle=true;
                    else if(args[i]=="--fullscreen") fullscreen=true;
                    else if(args[i]=="--cache-dir" && i+1<args.Length) cache=args[++i];
                    else if(args[i]=="--preview" && i+1<args.Length) preview=args[++i];
                    else if(args[i]=="--demo") demo=true;
                    else throw new ArgumentException(UiText.T("Nieznany lub niepełny parametr: ")+args[i]);
                }
                using(MainForm form=new MainForm(folder,telemetry,seconds,exercise,demo,preview,cache,cycle,fullscreen,map)) Application.Run(form);
            }
            catch(Exception ex)
            {
                MessageBox.Show(UiText.T("Nie udało się uruchomić aplikacji.\n")+UiText.Error(ex.Message),"EFT Position",MessageBoxButtons.OK,MessageBoxIcon.Error);
                Environment.ExitCode=1;
            }
        }
    }
}
