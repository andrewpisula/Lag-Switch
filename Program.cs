using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Input;
using System.Threading;
//using WindowsFirewallHelper;
//using WindowsFirewallHelper.FirewallAPIv2;
//using WindowsFirewallHelper.FirewallAPIv2.Rules;
using System.Diagnostics;
using System.Management;
using System.Security.Principal;
using Microsoft.Win32;
using System.Runtime.InteropServices;
using System.IO;

namespace LagSwitch
{

    class Program
    {
        [STAThread]
        static void Main(string[] args)
        {
            if (VerifyAdminPrivileges())
            {
                CleanExit.AddExitCallback();
                PickProcess.DisplayProcessList();
            }
            else
            {
                Console.Title = "Lag Switch - This application requires administrator privileges to run properly.";
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("This application requires administrator privileges to run properly." + Environment.NewLine + Environment.NewLine + "Press any key to attempt to re-launch application with administrator privileges.");
                Retry:
                Console.ReadKey();
                try
                {
                    ProcessStartInfo info = new ProcessStartInfo(PickProcess.ProcessExecutablePath(Process.GetCurrentProcess()));
                    info.UseShellExecute = true;
                    info.Verb = "runas";
                    Process.Start(info);
                }
                catch
                {
                    goto Retry;
                }
                Environment.Exit(0);
            }
            while (true) ;
        }

        class CleanExit
        {
            public static bool KillSwitch = false;

            public static void AddExitCallback()
            {
                handler = new ConsoleEventDelegate(ConsoleEventCallback);
                SetConsoleCtrlHandler(handler, true);
            }

            static bool ConsoleEventCallback(int eventType)
            {
                if (eventType == 2)
                {
                    Console.BackgroundColor = ConsoleColor.Red;
                    Console.WriteLine("Lag switch is OFF");
                    Console.ResetColor();
                    CleanRules.DeleteFirewallRules();
                    KillSwitch = true;
                }
                return false;
            }

            static ConsoleEventDelegate handler;   // Keeps it from getting garbage collected
                                                   // Pinvoke
            private delegate bool ConsoleEventDelegate(int eventType);
            [DllImport("kernel32.dll", SetLastError = true)]
            private static extern bool SetConsoleCtrlHandler(ConsoleEventDelegate callback, bool add);
        }

        static bool VerifyAdminPrivileges()
        {
            bool isElevated;
            using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
            {
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                isElevated = principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            return isElevated;
        }
    }

    public class SwitchFunctions
    {
        //https://github.com/sentoa/arma3-lagswitch
        public static bool isRunning = true;
        public static string location;
        public static string firewhash = "LAG_SWITCH_" + System.Guid.NewGuid().ToString();

        [STAThread]
        private static void KB()
        {
            // Creating a new Process
            Process process = new Process();

            // Stop the process from opening a new window
            process.StartInfo.RedirectStandardOutput = true;
            process.StartInfo.UseShellExecute = false;
            process.StartInfo.CreateNoWindow = true;

            // Setup executable and parameters
            process.StartInfo.FileName = "netsh.exe";

            string Message = "";

            while (isRunning)
            {
                Thread.Sleep(40);

                string LocalMessage = "";

                if ((Keyboard.GetKeyStates(Key.F1) & KeyStates.Down) > 0)
                {
                    process.StartInfo.Arguments = ("advfirewall firewall add rule name =\"" + firewhash + "\" dir=in action=block program=\"" + location + "\" enable=yes");
                    process.StartInfo.Arguments = ("advfirewall firewall add rule name =\"" + firewhash + "\" dir=out action=block program=\"" + location + "\" enable=yes");
                    process.Start();
                    process.Start();
                    Console.BackgroundColor = ConsoleColor.Green;
                    LocalMessage = "Lag switch is ON";
                }

                if ((Keyboard.GetKeyStates(Key.F2) & KeyStates.Down) > 0)
                {
                    process.StartInfo.Arguments = ("advfirewall firewall delete rule name=\"" + firewhash + "\" ");
                    process.Start();
                    Console.BackgroundColor = ConsoleColor.Red;
                    LocalMessage = "Lag switch is OFF";
                }

                if (Message != LocalMessage)
                {
                    Message = LocalMessage;
                    Console.WriteLine(Message);
                }
            }
        }
        internal static void Start()
        {
            Thread TH = new Thread(KB);
            TH.SetApartmentState(ApartmentState.STA);
            TH.Start();
        }
    }
}