using Core.Event;
using Core.Librarys;
using Core.Servicers.Interfaces;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace Core.Servicers.Instances
{
    public class Observer : IObserver
    {
        private IntPtr hook;
        Win32API.WinEventDelegate winEventDelegate;


        public event ObserverEventHandler OnAppActive;


        private string activeProcessName, activeProcessFile = null;
        public Observer()
        {
            winEventDelegate = new Win32API.WinEventDelegate(WinEventProc);
        }
        public void Start()
        {
            hook = Win32API.SetWinEventHook(0x0003, 0x0003, IntPtr.Zero,
            winEventDelegate, 0, 0, 0);
        }
        public delegate void NameChangedCallback(object src, System.Windows.Automation.AutomationPropertyChangedEventArgs e);
        public  void ChromeCallback(object src, System.Windows.Automation.AutomationPropertyChangedEventArgs e)
        {
            System.Windows.Automation.AutomationElement sourceElement = src as System.Windows.Automation.AutomationElement;
            Debug.WriteLine("ChromeCallback!");
            if (sourceElement != null)
            {
                string host = Win32API.Chrome_Link(sourceElement);
                if(host != "")
                {
                    EventInvoke("website://"+host,host, "chrome");
                }

            }
      


        }
        private void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
        {
            string processName = String.Empty, processFileName = String.Empty, processDescription = String.Empty;

            try
            {
                int processID = 0;
                Win32API.GetWindowThreadProcessId(hwnd, out processID);
                Process process = Process.GetProcessById(processID);
                processName = process.ProcessName;

                IntPtr processHandle = IntPtr.Zero;
                processHandle = Win32API.OpenProcess(0x001F0FFF, false, processID);

                if (processName == "Snipaste")
                {

                    int error = Marshal.GetLastWin32Error();

                    Debug.WriteLine("The last Win32 Error was: " + error);

                }
                var buffer = new StringBuilder(4096);
                Win32API.GetModuleFileNameExA(processHandle, IntPtr.Zero, buffer, buffer.Capacity);
                processFileName = buffer.ToString();
                Debug.WriteLine("processFileName:" + processFileName);

                if (processFileName.IndexOf("chrome.exe") != -1)
                {
                    Debug.WriteLine("chrome:" + processFileName);
                    processDescription = Win32API.Chrome_AppName(hwnd, (uint)processID, ChromeCallback);
                    processName = "website://"+ processDescription;
                    processFileName = "chrome";

                }
                else {
                    if (processFileName.IndexOf("ApplicationFrameHost.exe") != -1)
                    {
                        processFileName = Win32API.UWP_AppName(hwnd, (uint)processID);
                        if (processFileName != string.Empty && processFileName.IndexOf("\\") != -1)
                        {
                            processName = processFileName.Split('\\').Last();
                            processName = processName.Replace(".exe", "");
                        }
                    }

                    FileVersionInfo info = FileVersionInfo.GetVersionInfo(processFileName);
                    processDescription = info.FileDescription;
                }
                //  handle uwp app
                


                //Debug.WriteLine("file name: ---------> " + processFileName);
                //Debug.WriteLine("app name: ---------> " + processName);
                //Debug.WriteLine("processDescription: ---------> " + processDescription);
            }
            catch (Exception e)
            {
                Logger.Error(e.Message + " | Process Name:" + processName + " | Process File:" + processFileName + " | Process Description:" + processDescription);
            }

            EventInvoke(processName, processDescription, processFileName);
        }

        private void EventInvoke(string processName, string description, string filename)
        {
            //  防止重复和错误响应
            if (string.IsNullOrEmpty(processName) || string.IsNullOrEmpty(filename) || !(File.Exists(filename)||filename=="chrome"))
            {
                return;
            }
            if (filename == activeProcessFile && processName == activeProcessName)
            {
                return;
            }

            activeProcessName = processName;
            activeProcessFile = filename;

            OnAppActive?.Invoke(processName, description, filename);
        }

        public void Stop()
        {
            Win32API.UnhookWinEvent(hook);
        }
    }
}
