using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Management;
using System.Threading;

namespace LagSwitch
{
    class PickProcess
    {
        public static string ProcessExecutablePath(Process process)
        {
            try
            {
                return process.MainModule.FileName; //<< 64 bit
            }
            catch // 32 bit \/
            {
                string query = "SELECT ExecutablePath, ProcessID FROM Win32_Process WHERE ProcessId = " + process.Id.ToString();
                ManagementObjectSearcher searcher = new ManagementObjectSearcher(query);

                foreach (ManagementObject item in searcher.Get())
                {
                    object id = item["ProcessID"];
                    object path = item["ExecutablePath"];

                    if (path != null && id.ToString() == process.Id.ToString())
                    {
                        return path.ToString();
                    }
                }
            }

            return "";
        }

        private static void CreatePlaceholder(List<SelectableMenu.Option> Options, string text)
        {
            Options.Add(new SelectableMenu.Option
            {
                OptionName = text,
                proc = null,
                Selected = false,
                Action = null
            });
            if (SelectableMenu.Placeholders == null)
            {
                SelectableMenu.Placeholders = new List<string>();
            }
            SelectableMenu.Placeholders.Add(text);
        }

        public static string[] SystemProcesses = { "explorer", "ntoskrnl", "WerFault", "backgroundTaskHost", "backgroundTransferHost", "winlogon", "wininit", "csrss", "lsass", "smss", "services", "taskeng", "taskhost", "dwm", "conhost", "svchost", "sihost" };

        private static bool DisplayedOption(List<SelectableMenu.Option> Options, string ProcessName)
        {
            bool ret = false;
            foreach (var o in Options)
            {
                if (o.OptionName == ProcessName)
                {
                    ret = true;
                }
            }
            return ret;
        }

        public static List<SelectableMenu.Option> SetOptions(List<SelectableMenu.Option> Options)
        {
            SelectableMenu.Placeholders = null;
            CreatePlaceholder(Options, "--- Apps ---");

            Process[] ProcessesWithWindows = Process.GetProcesses().Where(p => (long)p.MainWindowHandle != 0).ToArray();

            for (int i = 0; i < ProcessesWithWindows.Length; i++)
            {
                Options.Add(new SelectableMenu.Option
                {
                    OptionName = ProcessesWithWindows[i].ProcessName,
                    Selected = i == 0,
                    Action = Process_Selected_Action,
                    proc = ProcessesWithWindows[i]
                });
            }

            Console.Title = "Lag Switch - Pick a process (Up/Down arrow keys)";


            // \/\/ Add Placeholder \/\/
            CreatePlaceholder(Options, "--- Background Processes ---");

            foreach (Process p in Process.GetProcesses())
            {
                if (DisplayedOption(Options, p.ProcessName) == false && ProcessExecutablePath(p) != string.Empty)
                {
                    if (SystemProcesses.Contains(p.ProcessName) == false)
                    {
                        Options.Add(new SelectableMenu.Option
                        {
                            OptionName = p.ProcessName,
                            Selected = false,
                            Action = Process_Selected_Action,
                            proc = p
                        });
                    }
                }
                //string t = ProcessExecutablePath(p);
                //if (t == string.Empty)
                //{
                //    Console.WriteLine(p.ProcessName + " _ " + t);
                //}
            }
            return Options;
        }

        public static void DisplayProcessList()
        {
            List<SelectableMenu.Option> Options = new List<SelectableMenu.Option>();

            Console.Title = "Lag Switch - Generating process list";

            Options = SetOptions(Options);

            SelectableMenu.Options = Options;
            SelectableMenu.DisplayMenuPrompt();
        }

        public static Process TARGETPROCESS = null;

        public static void Process_Selected_Action()
        {
            Console.Title = "Lag Switch - " + TARGETPROCESS.ProcessName;
            Console.Clear();
            SwitchFunctions.location = ProcessExecutablePath(TARGETPROCESS);
            Console.WriteLine("Application Path : " + SwitchFunctions.location);
            Console.WriteLine("Press 'F1' & 'F2' to activate & deactivate the lag switch.");
            SwitchFunctions.Start();
        }
    }

    class SelectableMenu
    {
        public struct Option
        {
            public string OptionName;
            public bool Selected;
            public Action Action;

            public Process proc;
        }

        public static List<Option> Options;
        public static List<string> Placeholders;// Items to skip over

        public static bool RefreshProcessList = true;

        public static void DisplayMenuPrompt()// Make sure options var is set before calling this func
        {
            new Thread(DetectNewProcesses).Start();
            Console.Clear();
            while (true)
            {
                SelectableMenu.WriteMenu(SelectableMenu.Options);
                Console.SetCursorPosition(1, SelectedIndex);
                if (SelectableMenu.ReadInput())
                {
                    break;
                }
                Console.Clear();
            }
        }

        public static bool ReadInput() // Returns true or false whether or not to break the while loop calling this func
        {
            var key = Console.ReadKey(true);
            if (key.Key == ConsoleKey.UpArrow)// || key.Key == ConsoleKey.LeftArrow)
            {
                Options = Shift(Options, true);
            }
            if (key.Key == ConsoleKey.DownArrow)// || key.Key == ConsoleKey.RightArrow)
            {
                Options = Shift(Options, false);
            }
            if (key.Key == ConsoleKey.Enter)
            {
                foreach (Option op in Options)
                {
                    if (op.Selected)
                    {
                        PickProcess.TARGETPROCESS = op.proc;
                        PickProcess.Process_Selected_Action();
                        RefreshProcessList = false;
                        return true;
                    }
                }
            }
            return false;
        }

        private static int SelectedIndex = 0;

        private static List<Option> Shift(List<Option> options, bool Up)//true=UpArrow; false=DownArrow
        {
            Option[] copy = options.ToArray();
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].Selected)
                {
                    try
                    {
                        int ToAdd = 1;//up-, down+
                        if (Up)
                        {
                            ToAdd = ToAdd * -1;
                        }
                        if (Placeholders.Contains(options[i + ToAdd].OptionName))
                        {
                            ToAdd = ToAdd * 2;
                        }
                        copy[i + ToAdd].Selected = true;
                        copy[i].Selected = false;
                        SelectedIndex = i + ToAdd;
                    }
                    catch
                    {
                        // ignore index is outside of bounds when they use down arrow @ the bottom of the list & vice versa with the up arrow & top of list
                    }
                }
            }
            return copy.ToList();
        }

        private static void DetectNewProcesses()
        {
            List<string> CurrentProcessList = new List<string>();
            foreach (Process p in Process.GetProcesses())
            {
                CurrentProcessList.Add(p.ProcessName);
            }
            while (RefreshProcessList)
            {
                bool Refresh = false;
                foreach (Process p in Process.GetProcesses())
                {
                    if (CurrentProcessList.Contains(p.ProcessName) == false)
                    {
                        Refresh = true;
                    }
                }
                if (Refresh)
                {
                    Options = PickProcess.SetOptions(new List<Option>());
                    WriteMenu(Options);
                    Console.Title = "Refreshed!";
                }
            }
        }

        private static void WriteMenu(List<Option> Options)
        {
            Console.ForegroundColor = ConsoleColor.Blue;
            Console.WriteLine("Pick a processs ");
            Console.WriteLine("Processes with windows: ");
            Console.ResetColor();
            foreach (Option op in Options)
            {
                if (op.Selected)
                {
                    Console.ForegroundColor = ConsoleColor.Blue;
                    Console.BackgroundColor = ConsoleColor.White;
                    Console.WriteLine(">> " + op.OptionName);
                    Console.ResetColor();
                }
                else
                {
                    Console.WriteLine(op.OptionName);
                }
            }
        }
    }
}