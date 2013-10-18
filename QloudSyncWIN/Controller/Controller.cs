using System;
using System.Diagnostics;
using System.IO;
using System.Threading;

using MonoMac.Foundation;
using MonoMac.AppKit;
using GreenQloud.Synchrony;
using System.Collections.Generic;
using System.Net.NetworkInformation;
using GreenQloud.Model;
using GreenQloud.UI;
using System.Windows.Forms;

 

namespace GreenQloud {
    public class Controller : AbstractApplicationController
    {
        public override void Initialize()
        {
            OnIdle += delegate()
            {
                UIManager.GetInstance().OnIdle();
            };
            OnSyncing += delegate()
            {
                UIManager.GetInstance().OnSyncing();
            };
            OnError += delegate()
            {
                UIManager.GetInstance().OnError();
            };
            OnPaused += delegate()
            {
                UIManager.GetInstance().OnPaused();
            };
        }

        public override void CreateStartupItem() {
            UIManager.GetInstance().BuildMenu();
        }
        public override void Quit()
        {
            Program.Exit();
        }
        public override void OpenFolder(string path)
		{
            Process.Start("explorer.exe", path); 
		}
        public override void OpenWebsite(string url)
        {
            Process.Start(url);
        }

        public override void CheckForUpdates()
        {
            new Thread(delegate() {
            Process p = Process.Start(RuntimeSettings.AutoUpdaterPath, "--mode unattended");
            p.WaitForExit();

            if (p.ExitCode == 0)
            {
                MessageBox.Show("New version available, or is it?.");
            } else {
                MessageBox.Show("QloudSync is up to date!");
            }}).Start();
        }
    }
}
