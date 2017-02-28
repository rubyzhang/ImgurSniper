﻿using IWshRuntimeLibrary;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace Cleanup {

    class Program {
        private static string DocPath => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ImgurSniper");

        static void Main(string[] args) {
            Console.Title = "Uninstalling ImgurSniper";

            //Remove Startmenu Shortcut
            try {
                string commonStartMenuPath = Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu);
                string shortcutLocation = Path.Combine(commonStartMenuPath, "ImgurSniper" + ".lnk");
                System.IO.File.Delete(shortcutLocation);

                shortcutLocation = Path.Combine(commonStartMenuPath, "ImgurSniper Settings" + ".lnk");
                System.IO.File.Delete(shortcutLocation);

                Console.WriteLine("Removed Start Menu Shortcut..");
            } catch { }

            //Remove Desktop Shortcut
            try {
                object shDesktop = (object)"Desktop";
                WshShell shell = new WshShell();
                string shortcutAddress = (string)shell.SpecialFolders.Item(ref shDesktop) + @"\Imgur Sniper.lnk";
                System.IO.File.Delete(shortcutAddress);
                shortcutAddress = (string)shell.SpecialFolders.Item(ref shDesktop) + @"\Imgur Sniper Settings.lnk";
                System.IO.File.Delete(shortcutAddress);

                Console.WriteLine("Removed Desktop Shortcut..");
            } catch { }


            try {
                using(RegistryKey baseKey = Registry.ClassesRoot.CreateSubKey(@"*\shell")) {
                    baseKey.DeleteSubKeyTree("ImgurSniperUpload");
                }
            } catch { }


            try {
                using(
                    RegistryKey baseKey =
                        Registry.CurrentUser.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run")) {
                    baseKey.DeleteValue("ImgurSniper");
                }
            } catch { }

            try {
                KillTasks();

                //Remove all files
                bool notRemoved = false;

                foreach(string filesDocuments in Directory.GetFiles(DocPath)) {
                    try {
                        System.IO.File.Delete(filesDocuments);
                    } catch {
                        notRemoved = true;
                    }
                }
                foreach(string dirs in Directory.GetDirectories(DocPath)) {
                    try {
                        Directory.Delete(dirs, true);
                    } catch {
                        notRemoved = true;
                    }
                }

                try {
                    Directory.Delete(DocPath, true);
                } catch { }


                if(notRemoved)
                    Console.WriteLine("Error");
            } catch(Exception ex) {
                Console.WriteLine(ex.Message);
            }
        }

        private static void KillTasks() {
            try {
                List<Process> processes = new List<Process>(Process.GetProcesses().Where(p => p.ProcessName.Contains("ImgurSniper")));
                foreach(Process p in processes) {
                    if(p.Id != Process.GetCurrentProcess().Id)
                        p.Kill();
                }
            } catch { }
        }
    }
}
