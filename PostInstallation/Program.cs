/*************************************************************************
 * Copyright 2010-2013 Eucalyptus Systems, Inc.
 *
 * Redistribution and use of this software in source and binary forms,
 * with or without modification, are permitted provided that the following
 * conditions are met:
 *
 *   Redistributions of source code must retain the above copyright notice,
 *   this list of conditions and the following disclaimer.
 *
 *   Redistributions in binary form must reproduce the above copyright
 *   notice, this list of conditions and the following disclaimer in the
 *   documentation and/or other materials provided with the distribution.
 *
 * THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
 * "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
 * LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
 * A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
 * OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
 * SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
 * LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
 * DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
 * THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
 * (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
 * OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
 ************************************************************************/
using System;
using System.Collections.Generic;
using System.Text;
using NetFwTypeLib;
using Microsoft.Win32;
using System.Management;
using System.ServiceProcess;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Com.Eucalyptus.Windows
{
    internal class WMIUtil
    {
        internal static ManagementObject QueryLocalWMI(string query)
        {
            return QueryLocalWMI(null, query);
        }

        internal static ManagementObject QueryLocalWMI(string scope, string query)
        {
            ManagementObjectCollection col = QueryLocalWMICollection(scope, query);
            ManagementObject retObj = null;
            foreach (ManagementObject obj in col)
                retObj = obj;

            return retObj;
        }

        internal static ManagementObjectCollection QueryLocalWMICollection(string query)
        {
            return QueryLocalWMICollection(null, query);
        }

        private const int RETRY = 10;
        private const int PAUSE_SEC_BETWEEN_RETRY = 1;
        internal static ManagementObjectCollection QueryLocalWMICollection(string scope, string query)
        {
            ManagementScope ms = null;

            int numTrial = 0;
            bool connected = false;
            do
            {
                try
                {
                    if (scope == null)
                        ms = new ManagementScope(@"\\.\root\cimv2", new ConnectionOptions());
                    else
                        ms = new ManagementScope(scope, new ConnectionOptions());

                    ms.Connect();
                    connected = true;
                }
                catch (Exception) // in the booting period, the WMI service may not be ready; 
                {
                    //LogTools.Warning("WMI service is not responding; will retry");
                    System.Threading.Thread.Sleep(PAUSE_SEC_BETWEEN_RETRY * 1000);
                    continue;
                }
            } while (numTrial++ < RETRY);

            if (!connected || ms == null || !ms.IsConnected)
                throw new Exception("WMI not connected");

            ManagementObjectSearcher searcher = new ManagementObjectSearcher(ms, new ObjectQuery(query));
            return searcher.Get();
        }
    }

    
    class Program
    {
        private static void SetRegistryValue(string key, object value)
        {
            if (key == null || value == null)
                return;
            try
            {
                RegistryKey regKey =
                    Registry.LocalMachine.OpenSubKey("SOFTWARE").OpenSubKey("Eucalyptus Systems").OpenSubKey("Eucalyptus", true);
                regKey.SetValue(key, value);
                regKey.Flush();
                regKey.Close();
            }
            catch (Exception)
            {
                Log(string.Format("Coult not set registry value ({0}-{1})", key, value));
            }
        }

        public static object GetRegistryValue(string key)
        {
            if (key == null)
                return null;
            try
            {
                RegistryKey regKey =
                  Registry.LocalMachine.OpenSubKey("SOFTWARE").OpenSubKey("Eucalyptus Systems").OpenSubKey("Eucalyptus");
                object val = regKey.GetValue(key);
                regKey.Close();
                return val;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static string strLogPath = null;
        public static void Log(string msg)
        {
            if (strLogPath == null){
                object obj = GetRegistryValue("InstallLocation");
                strLogPath = (obj != null) ? ((string)obj) + "\\eucalog_install.txt" : "C:\\eucalog_install.txt";
                EucaLogger.LogLocation = strLogPath;
            }
            EucaLogger.Debug(msg);
        }

        static int Main(string[] args)
        {
            if (args.Length <= 0)
            {
                Log("ERROR: No option is given");
                return -1;
            }

            string opt = args[0];
            if (opt == "--registry")
            {
                try
                {
                    InitRegistry();
                    return 0;
                }
                catch (Exception e)
                {
                    Log(string.Format("Could not initialize registry setting: {0}", e.Message));
                    return -1;
                }
            }           
            else if (opt == "--firewall")
            {
                return OpenFirewall();
             }
            else if (opt == "--acpi")
            {
                return EnableACPI();
            }
            else if (opt == "--rdp")
            {
                return EnableRDP();
            }
            else if (opt == "--envconf")
            {
                int retCode = OpenFirewall();
                retCode += EnableACPI();
                retCode += EnableRDP();
                retCode += DisableShutdownReasonUI();
                return retCode;
            }
            else if (opt == "--norecovery")
            {
                try
                {
                    if (OSEnvironment.OS_Name == OSEnvironment.Enum_OsName.S2008 ||
                        OSEnvironment.OS_Name == OSEnvironment.Enum_OsName.S2008R2 ||
                        OSEnvironment.OS_Name == OSEnvironment.Enum_OsName.Vista ||
                        OSEnvironment.OS_Name == OSEnvironment.Enum_OsName.Win7)
                    {
                        TurnOffRecovery();
                        Log("System crash recovery option is disabled");
                        return 0;
                    }
                    else
                        return 0;
                }
                catch (Exception e)
                {
                    Log(string.Format("[FAILURE] Could not turn off recovery option ({0})", e.Message));
                    return -1;
                }
            }
            else if (opt == "--recovery")
            {
                try
                {
                    if (OSEnvironment.OS_Name == OSEnvironment.Enum_OsName.S2008 ||
                        OSEnvironment.OS_Name == OSEnvironment.Enum_OsName.S2008R2 ||
                        OSEnvironment.OS_Name == OSEnvironment.Enum_OsName.Vista ||
                        OSEnvironment.OS_Name == OSEnvironment.Enum_OsName.Win7)
                    {
                        TurnOnRecovery();
                        Log("System crash recovery option is enabled");
                        return 0;
                    }
                    else
                        return 0;
                }
                catch (Exception e)
                {
                    Log(string.Format("[FAILURE] Could not turn on recovery option ({0})", e.Message));
                    return -1;
                }
            }
            else if (opt == "--xenpv")
            {
                /// The support for XENPV drivers is deprecated
                string dir = null;
                if (args.Length >= 2)
                    dir = args[1];
                else
                {
                    Log("[FAILURE] Should specify the path to the xenpv drivers");
                    return -1;
                }
                try
                {
                    InstallXenPVDrivers(dir);
                }
                catch (Exception e)
                {
                    Log(string.Format("[FAILURE] Could not install xenpv drivers({0})", e.Message));
                    return -1;
                }
            }
            else if (opt == "--virtio")
            {
                string dir = null;
                if (args.Length >= 2)
                    dir = args[1];
                else
                {
                    Log("[FAILURE] Should specify the path to the virtio drivers");
                    return -1;
                }

                try
                {
                    InstallVirtIODrivers(dir);
                }
                catch (Exception e)
                {
                    Log(string.Format("[FAILURE] Could not install virtio drivers({0})", e.Message));
                    return -1;
                }
            }
            else if (opt == "--cleanup")
            {
                string dir = null;
                if (args.Length >= 2)
                    dir = args[1];
                else
                {
                    Log("[FAILURE] Should specify the path to the Eucalyptus installation");
                    return -1;
                }
                try
                {
                    CleanupFiles(dir);
                    CleanupRegistry();
                    return 0;
                }
                catch (Exception e)
                {
                    Log(string.Format("Could not cleanup Eucalyptus installation: {0}", e.Message));
                    return -1;
                }
            }

            return 0;
        }

        static private int OpenFirewall()
        {
            const int FIREWALL_CHECK_TIMEOUT_SEC = 10;
            DateTime fwCheckStartTime = DateTime.Now;

        LB_FIREWALL:
            try
            {
                UnblockFirewall();
                Log("RDP port was unblocked from firewall");
                SetRegistryValue("FirewallCheck", 1); // avoid unnecessary check during the instance spin-up
                return 0;
            }
            catch (Exception e)
            {
                if ((new TimeSpan(DateTime.Now.Ticks - fwCheckStartTime.Ticks)).TotalSeconds < FIREWALL_CHECK_TIMEOUT_SEC)
                {
                    System.Threading.Thread.Sleep(1000);
                    goto LB_FIREWALL;
                }
                Log(string.Format("[FAILURE] Firewall unblock failed for RDP port - ok if firewall service is not running ({0})", e.Message));
                return -1;
            }
        }

        static private int EnableACPI()
        {
            try
            {
                AllowShutdownWithoutLogon();
                Log("ACPI setting successfully updated");
                SetRegistryValue("ACPICheck", 1);
                return 0;
            }
            catch (Exception e)
            {
                Log(string.Format("[FAILURE] ACPI setting change failed ({0})", e.Message));
                return -1;
            }
        }

        static private int EnableRDP()
        {
            try
            {
                AllowRemoteDesktop();
                Log("Remote desktop connection is allowed");
                SetRegistryValue("RDPCheck", 1);
                return 0;
            }
            catch (Exception e)
            {
                Log(string.Format("[FAILURE] Remote desktop setting change failed ({0})", e.Message));
                return -1;
            }
        }

        static private void InitRegistry()
        {
            string exe = System.Reflection.Assembly.GetExecutingAssembly().Location;
            string dir = exe.Substring(0, exe.LastIndexOf("\\")+1);

            RegistryKey regKey =
                  Registry.LocalMachine.OpenSubKey("SOFTWARE", true);

            RegistryKey sysKey = regKey.OpenSubKey("Eucalyptus Systems", true);
            if(sysKey==null){
                regKey.CreateSubKey("Eucalyptus Systems", RegistryKeyPermissionCheck.ReadWriteSubTree);
                sysKey = regKey.OpenSubKey("Eucalyptus Systems", true);
            }
            RegistryKey eucaKey = sysKey.OpenSubKey("Eucalyptus", true);
            if (eucaKey == null)
            {
                sysKey.CreateSubKey("Eucalyptus", RegistryKeyPermissionCheck.ReadWriteSubTree);
                eucaKey = sysKey.OpenSubKey("Eucalyptus", true);
            }

            RegistryKey rdpKey = eucaKey.OpenSubKey("RDP", true);
            if (rdpKey == null)
            {
                eucaKey.CreateSubKey("RDP", RegistryKeyPermissionCheck.ReadWriteSubTree);
                rdpKey = eucaKey.OpenSubKey("RDP", true);
            }

            rdpKey.SetValue("localhost\\Administrator",""); // the default user that has RDP permission
            eucaKey.SetValue("InstallLocation", dir);
            eucaKey.SetValue("ACPICheck", 0);
            eucaKey.SetValue("AdminActivated", 0);
            eucaKey.SetValue("FirewallCheck", 0);
            eucaKey.SetValue("PasswordSet", 0);
            eucaKey.SetValue("RDPCheck", 0);
            eucaKey.SetValue("FormatDrives", 1);
            rdpKey.Flush();
            rdpKey.Close();
            eucaKey.Flush();
            eucaKey.Close();
            sysKey.Flush();
            sysKey.Close();
            regKey.Flush();
            regKey.Close();
        }
        static private void CleanupRegistry()
        {
            try
            {
                RegistryKey regKey =
                    Registry.LocalMachine.OpenSubKey("SOFTWARE", true);
                regKey.DeleteSubKeyTree("Eucalyptus Systems");
                regKey.Flush();
                regKey.Close();
                //Log("Eucalyptus registry is cleaned");    /// at this moment, the Logger can't find the Euca install location
            }
            catch (Exception)
            {
                //Log("Could not clean Eucalyptus registry"); 
            }
        }
        static private void TurnOffRecovery()
        {
            using (System.Diagnostics.Process proc = new System.Diagnostics.Process())
            {
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.FileName = Environment.GetFolderPath(Environment.SpecialFolder.System) + "\\bcdedit.exe";
                proc.StartInfo.Arguments = "/set {default} recoveryenabled no";
                proc.Start();
                proc.WaitForExit();

                proc.StartInfo.Arguments = "/set {default} bootstatuspolicy ignoreallfailures";
                proc.Start();
                proc.WaitForExit();
            }
        }

        static private void TurnOnRecovery()
        {
            using (System.Diagnostics.Process proc = new System.Diagnostics.Process())
            {
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.FileName = Environment.GetFolderPath(Environment.SpecialFolder.System) + "\\bcdedit.exe";                
                proc.StartInfo.Arguments = "/set {default} recoveryenabled yes";
                proc.Start();
                proc.WaitForExit();

                proc.StartInfo.Arguments = "/set {default} bootstatuspolicy displayallfailures";
                proc.Start();
                proc.WaitForExit();
            }
        }

        static private void AllowRemoteDesktop()
        {
            string connString = null;
            if (OSEnvironment.OS_Name == OSEnvironment.Enum_OsName.XP ||
                OSEnvironment.OS_Name == OSEnvironment.Enum_OsName.S2003 ||
                OSEnvironment.OS_Name == OSEnvironment.Enum_OsName.S2003R2)
                connString = @"\\.\root\CIMV2";
            else
                connString = @"\\.\root\CIMV2\TerminalServices";

            using (ManagementObject tsObj = WMIUtil.QueryLocalWMI(connString, "Select * from Win32_TerminalServiceSetting"))
            {
                if ((UInt32)tsObj["AllowTSConnections"] != 1)
                {
                    ManagementBaseObject inParams = tsObj.GetMethodParameters("SetAllowTSConnections");
                    inParams["AllowTSConnections"] = 1;
                    try
                    {   // this works for only win7, server 2008, 2008R2
                        inParams["ModifyFirewallException"] = 1;
                    }
                    catch (ManagementException)
                    {
                        ;
                    }
                    ManagementBaseObject outParams = tsObj.InvokeMethod("SetAllowTSConnections", inParams, null);
                    UInt32 ret = (UInt32)outParams["ReturnValue"];

                    if (ret > 1)    // 0=Success, 1=Informational, 2=Warning, 3=Error
                        throw new Exception(string.Format("SetAllowTsConnects failed with error code {0}", ret));                    
                }                
            }            
        } 
        
        /// <summary>
        /// manually changes the registry setting to enable shutdownwithoutlogon 
        /// should work for the these windows versions: Win 7, vista, xp, 2003(STD,SP1), 2008(ENT), 2008 R2
        /// </summary>
        /// <exception cref="Com.Eucalyptus.Windows.EucaServiceLibrary.EucaException"/>
        static private void AllowShutdownWithoutLogon()
        {
            const string hklm = "HKEY_LOCAL_MACHINE";
            const string keyName = hklm + "\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System";
            const string valueName = "shutdownwithoutlogon";
            
            int curSetting = (int)Registry.GetValue(keyName, valueName, null);
            if (curSetting != 1)
                Registry.SetValue(keyName, valueName, 1);
        }

        static private int DisableShutdownReasonUI()
        {
            try
            {
                RegistryKey regKey =
                    Registry.LocalMachine.OpenSubKey("SOFTWARE").OpenSubKey("Policies").OpenSubKey("Microsoft")
                    .OpenSubKey("Windows NT", true).OpenSubKey("Reliability", true);
                if (regKey == null)
                {
                    if (Registry.LocalMachine.OpenSubKey("SOFTWARE").OpenSubKey("Policies").OpenSubKey("Microsoft").OpenSubKey("Windows NT") == null)
                    {
                        regKey = Registry.LocalMachine.OpenSubKey("SOFTWARE").OpenSubKey("Policies").OpenSubKey("Microsoft", true).CreateSubKey("Windows NT");
                        if (regKey == null)
                            throw new Exception("Couldn't create/open the registry key to change (HKLM.SOFTWARE.Policies.Microsoft.Windows NT");
                        regKey.Flush();
                        regKey.Close();
                    }
                    regKey = Registry.LocalMachine.OpenSubKey("SOFTWARE").OpenSubKey("Policies").OpenSubKey("Microsoft").OpenSubKey("Windows NT", true)
                        .CreateSubKey("Reliability");
                    if (regKey == null)
                        throw new Exception("Couldn't create/open the registry key to change(HKLM.SOFTWARE.Policies.Microsoft.Windows NT.Reliability)");
                    regKey.Flush();
                    regKey.Close();
                    regKey = Registry.LocalMachine.OpenSubKey("SOFTWARE").OpenSubKey("Policies").OpenSubKey("Microsoft").OpenSubKey("Windows NT").OpenSubKey("Reliability", true);
                    if (regKey == null)
                        throw new Exception("Couldn't create/open the registry key to change(HKLM.SOFTWARE.Policies.Microsoft.Windows NT.Reliability)");
                }
                regKey.SetValue("ShutdownReasonUI", 0);
                regKey.SetValue("ShutdownReasonOn", 0);
                regKey.Flush();
                regKey.Close();

                Log("ShutdownReasonUI was turned off");
                return 0;
            }
            catch (Exception e)
            {
                Log("Could not disable shutdown reason UI");
                return 1;
            }
        }
        
        static private void UnblockFirewall()
        {
            /// in some win versions (s2003, s2003r2), firewall service is not started by default.
            /// before firewall's rule check, we need to make sure they are started.
            /// 
       
           if (!(OSEnvironment.OS_Name == OSEnvironment.Enum_OsName.S2008 ||
                    OSEnvironment.OS_Name == OSEnvironment.Enum_OsName.S2008R2 ||
                    OSEnvironment.OS_Name == OSEnvironment.Enum_OsName.Win7))
            {
                // check if firewall is auto-starting
                try
                {
                    RegistryKey regKey = null;
                    if(OSEnvironment.OS_Name == OSEnvironment.Enum_OsName.Vista)
                        regKey = Registry.LocalMachine.OpenSubKey("SYSTEM").OpenSubKey("CurrentControlSet")
                        .OpenSubKey("Services").OpenSubKey("MpsSvc");
                    else // xp, s2003, s2003r2
                        regKey = Registry.LocalMachine.OpenSubKey("SYSTEM").OpenSubKey("CurrentControlSet")
                        .OpenSubKey("Services").OpenSubKey("SharedAccess");

                    object val = regKey.GetValue("Start");
                    regKey.Close();

                    if ((int)val != 2) // not automatic
                    {
                        Log("Firewall service is not auto-start");
                        return;
                    }                    
                }
                catch (Exception e)
                {
                    Log(string.Format("Could not open registry key for checking firewall/ics service ({0})", e.Message));
                }

                // make sure firewall service is running
                try
                {
                    bool svcFound = false;
                    const int timeoutSec = 5;
                    ServiceController[] svcs = ServiceController.GetServices();
                    foreach (ServiceController svc in svcs)
                    {
                        if (svc.DisplayName.Contains("Windows Firewall")) // I believe this covers all cases
                        {
                            svcFound = true;
                            svc.WaitForStatus(ServiceControllerStatus.Running,
                                new TimeSpan(DateTime.Now.AddSeconds(timeoutSec).Ticks - DateTime.Now.Ticks));
                        }
                    }
                    if (!svcFound)
                        Log("Firewall service is not found in the system");
                }
                catch (System.ServiceProcess.TimeoutException)
                {
                    Log("Firewall service is not running (timed out)");
                    return;
                }
                catch (Exception e)
                {
                    Log(string.Format("Firewall service is not running ({0})", e.Message));
                    return;
                }
            }

            Type FwMgrType = null;
            INetFwMgr mgr = null;
            try
            {
                FwMgrType = Type.GetTypeFromProgID("HNetCfg.FwMgr", true);
                mgr = (INetFwMgr)Activator.CreateInstance(FwMgrType);
            }
            catch (Exception e)
            {
                throw;
            }

            foreach (NET_FW_PROFILE_TYPE_ profile in Enum.GetValues(typeof(NET_FW_PROFILE_TYPE_)))
            {
                if (profile == NET_FW_PROFILE_TYPE_.NET_FW_PROFILE_TYPE_MAX)
                    continue;

                try
                {
                    INetFwServices svcs = mgr.LocalPolicy.GetProfileByType(profile).Services;
                    bool svcFound = false;
                    foreach (INetFwService svc in svcs)
                    {
                        if (svc.Name == "Remote Desktop")
                        {
                            svcFound = true;
                            if (!svc.Enabled)
                            {
                                svc.Enabled = true;
                                Log(string.Format("Remote desktop service is unblocked in firewall setting in {0}", profile));
                            }
                            else
                                Log(string.Format("Remote desktop service was already unblocked in firewall setting in {0}", profile));
                            break;
                        }
                    }

                    if (!svcFound)
                    {
                        throw new Exception("Can't find a RDP service in the existing firewall setting.");
                    }
                }
                catch (Exception e)
                {
                    Log(string.Format("Can't unblock RDP port (tcp-3389) in {0}", profile));
                    Log(e.Message);
                }
            }

            EucaServiceLibraryUtil.SetSvcRegistryValue("FirewallCheck", 1);
        }

        const string GPLPV_XP_MSI = "gplpv_XP_0.11.0.238.msi";
        const string GPLPV_S2003_32_MSI = "gplpv_2003x32_0.11.0.238.msi";
        const string GPLPV_S2003_64_MSI = "gplpv_2003x64_0.11.0.238.msi";
        const string GPLPV_S2008_32_MSI = "gplpv_Vista2008x32_0.11.0.238.msi";
        const string GPLPV_S2008_64_MSI = "gplpv_Vista2008x64_0.11.0.238.msi";

        static private void InstallXenPVDrivers(string baseDir)
        {
            OSEnvironment.Enum_OsName osName = OSEnvironment.OS_Name;
            bool is64bit = OSEnvironment.Is64bit;

            string msiFile = null;
            if (osName == OSEnvironment.Enum_OsName.XP)
                msiFile = baseDir + "\\" +GPLPV_XP_MSI;
            else if(osName == OSEnvironment.Enum_OsName.S2003 || osName == OSEnvironment.Enum_OsName.S2003R2)
            {
                if(is64bit)
                    msiFile = baseDir +"\\"+ GPLPV_S2003_64_MSI;
                else 
                    msiFile = baseDir +"\\"+GPLPV_S2003_32_MSI;
            }else if(osName == OSEnvironment.Enum_OsName.Vista || osName == OSEnvironment.Enum_OsName.Win7
                || osName== OSEnvironment.Enum_OsName.S2008 || osName == OSEnvironment.Enum_OsName.S2008R2){
                if(is64bit)
                    msiFile = baseDir + "\\" + GPLPV_S2008_64_MSI;
                else 
                    msiFile = baseDir + "\\" + GPLPV_S2008_32_MSI;
                
            }else
                throw new Exception("OS type can't be determined");
            
            if (!File.Exists(msiFile))
                throw new Exception(string.Format("Can't find the file {0}", msiFile));

            using (System.Diagnostics.Process proc = new System.Diagnostics.Process())
            {
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.FileName = Environment.GetFolderPath(Environment.SpecialFolder.System)+"\\msiexec.exe";
                proc.StartInfo.Arguments = string.Format("/i \"{0}\"", msiFile);

                if (!File.Exists(proc.StartInfo.FileName))
                    throw new Exception(string.Format("Can't find the exe file ({0})", proc.StartInfo.FileName));
                proc.Start();
                proc.WaitForExit();

                int exitCode = proc.ExitCode;
                if (!(exitCode == 0 || exitCode == 1602)) // complete or user canceled installation
                    throw new Exception(string.Format("XenPV installer returned exit code={0})", exitCode));                
            }
        }

        // the directory path is based on Virtio driver version 0.1-59 (Apr 2013)
        static private void InstallVirtIODrivers(string baseDir)        
        {
            if (!Directory.Exists(baseDir))
                throw new Exception(string.Format("Can't find the driver directory ({0})", baseDir));

            OSEnvironment.Enum_OsName osName = OSEnvironment.OS_Name;
            bool is64bit = OSEnvironment.Is64bit;

            // resolve OS
            string virtioDir = baseDir;
            if (osName == OSEnvironment.Enum_OsName.XP)
                virtioDir += "\\WXP"; // there's  no AMD64 directory ==> should check against 64bit XP
            else if (osName == OSEnvironment.Enum_OsName.S2003 || osName == OSEnvironment.Enum_OsName.S2003R2)
                virtioDir += "\\WNET";
            else if (osName == OSEnvironment.Enum_OsName.Vista || osName == OSEnvironment.Enum_OsName.S2008)
                virtioDir += "\\WLH";
            else if (osName == OSEnvironment.Enum_OsName.Win7 || osName == OSEnvironment.Enum_OsName.S2008R2)
                virtioDir += "\\WIN7";
            else if (osName == OSEnvironment.Enum_OsName.Win8 || osName == OSEnvironment.Enum_OsName.S2012)
                virtioDir += "\\WIN8";
            else
                throw new Exception("OS type can't be determined");
           
            // resolve 64/32 bit
            if (is64bit)
                virtioDir += "\\AMD64";
            else
                virtioDir += "\\X86";

            if (!Directory.Exists(virtioDir))
                throw new Exception(string.Format("Can't find the VirtIO driver directory({0})", virtioDir));

            // this call may throw an exception if failed
            DriverManager.Instance.InstallDrivers(new string[]{virtioDir});           
        }

        // clean up any files upon uninstallation of Eucalyptus package
        static private void CleanupFiles(string baseDir)
        {
            baseDir = baseDir.Trim(new char[] { '\n', '\"' });
            string xenpvdir = string.Format("{0}\\xenpv", baseDir);
            if (Directory.Exists(xenpvdir))
            {
                try
                {
                    Directory.Delete(xenpvdir, true);
                    Log(string.Format("{0} deleted", xenpvdir));
                }
                catch (Exception e)
                {
                    Log(string.Format("Could not delete {0}({1})", xenpvdir, e.Message));
                }
            }
            else
                Log(string.Format("Can't find {0}", xenpvdir));

            string virtiodir = string.Format("{0}\\virtio", baseDir);
            if (Directory.Exists(virtiodir))
            {
                try
                {
                    Directory.Delete(virtiodir, true);
                    Log(string.Format("{0} deleted", virtiodir));
                }
                catch (Exception e) {
                    Log(string.Format("Could not delete {0}({1})", virtiodir, e.Message));
                }
            }else
                Log(string.Format("Can't find {0}", virtiodir));
        }
    }

    public class DriverManager
    {
        private DriverManager()
        {
        }
        private static DriverManager _instance = new DriverManager();
        public static DriverManager Instance { get { return _instance; } }

        /// <summary>
        /// Driver media type
        /// </summary>
        internal enum OemSourceMediaType
        {
            SPOST_NONE = 0,
            SPOST_PATH = 1,
            SPOST_URL = 2,
            SPOST_MAX = 3
        }

        /// <summary>
        /// Driver file copy style
        /// </summary>
        internal enum OemCopyStyle
        {
            SP_COPY_DELETESOURCE = 0x0000001,   // delete source file on successful copy
            SP_COPY_REPLACEONLY = 0x0000002,   // copy only if target file already present
            SP_COPY_NEWER = 0x0000004,   // copy only if source newer than or same as target
            SP_COPY_NEWER_OR_SAME = SP_COPY_NEWER,
            SP_COPY_NOOVERWRITE = 0x0000008,   // copy only if target doesn't exist
            SP_COPY_NODECOMP = 0x0000010,   // don't decompress source file while copying
            SP_COPY_LANGUAGEAWARE = 0x0000020,   // don't overwrite file of different language
            SP_COPY_SOURCE_ABSOLUTE = 0x0000040,   // SourceFile is a full source path
            SP_COPY_SOURCEPATH_ABSOLUTE = 0x0000080,   // SourcePathRoot is the full path
            SP_COPY_IN_USE_NEEDS_REBOOT = 0x0000100,   // System needs reboot if file in use
            SP_COPY_FORCE_IN_USE = 0x0000200,   // Force target-in-use behavior
            SP_COPY_NOSKIP = 0x0000400,   // Skip is disallowed for this file or section
            SP_FLAG_CABINETCONTINUATION = 0x0000800,   // Used with need media notification
            SP_COPY_FORCE_NOOVERWRITE = 0x0001000,   // like NOOVERWRITE but no callback nofitication
            SP_COPY_FORCE_NEWER = 0x0002000,   // like NEWER but no callback nofitication
            SP_COPY_WARNIFSKIP = 0x0004000,   // system critical file: warn if user tries to skip
            SP_COPY_NOBROWSE = 0x0008000,   // Browsing is disallowed for this file or section
            SP_COPY_NEWER_ONLY = 0x0010000,   // copy only if source file newer than target
            SP_COPY_SOURCE_SIS_MASTER = 0x0020000,   // source is single-instance store master
            SP_COPY_OEMINF_CATALOG_ONLY = 0x0040000,   // (SetupCopyOEMInf only) don't copy INF--just catalog
            SP_COPY_REPLACE_BOOT_FILE = 0x0080000,   // file must be present upon reboot (i.e., it's needed by the loader), this flag implies a reboot
            SP_COPY_NOPRUNE = 0x0100000   // never prune this file
        }

        const int MAX_PATH = 260;

        [DllImport("Setupapi.dll")]
        static extern bool SetupCopyOEMInf(string SourceInfFileName, string OEMSourceMediaLocation,
            OemSourceMediaType OEMSourceMediaType, OemCopyStyle CopyStyle, StringBuilder DestinationInfFileName,
            int DestinationInfFileNameSize, ref int RequiredSize, StringBuilder DestinationInfFileNameComponent);
        [DllImport("kernel32.dll")]
        static extern int GetLastError();

        const int ERROR_FILE_EXISTS = 0x50;

        public virtual void UpdateDrivers()
        {
            string[] sourceDirs = Directory.GetDirectories(EucaConstant.EucaDriverSourceDir);
            List<String> updatedDrivers = new List<string>();

            foreach (string dir in sourceDirs)
            {
                string driver = null;
                string dest = null;
                try
                {
                    driver = dir.Substring(dir.LastIndexOf("\\") + 1);
                    dest = EucaConstant.EucaDriverDestinationDir + "\\" + driver;

                    if (DriverExists(dir, dest))
                    {
#if EUCA_DEBUG
                        LogTools.Info(string.Format("Drivers in {0} / {1} are the same", dir, dest));
#endif
                        continue;
                    }
                    // rename the old driver for backup
                    string destOld = dest + ".old";
                    if (Directory.Exists(destOld))
                        Directory.Delete(destOld);
                    if (Directory.Exists(dest))
                        Directory.Move(dest, destOld);

                    Directory.CreateDirectory(dest);
                    foreach (string file in Directory.GetFiles(dir))
                    {
                        string filename = file.Substring(file.LastIndexOf("\\") + 1);
                        File.Copy(file, string.Format("{0}\\{1}", dest, filename));
                    }
                    updatedDrivers.Add(dest);
                }
                catch (Exception e)
                {
                    Program.Log(string.Format("Could not copy driver directory from {0} to {1}", dir, dest));
                    Program.Log(e.StackTrace);
                }
            }

            InstallDrivers(updatedDrivers.ToArray());
        }

        // compare 'inf' file in each directory
        public bool DriverExists(string src, string dest)
        {
            if (!Directory.Exists(dest))
                return false;

            // compare size of inf file
            string[] srcInfs = Directory.GetFiles(src, "*.INF");
            if (srcInfs == null || srcInfs.Length == 0)
                return true;    // if there's no INF in the source, we think driver exists
            string[] destInfs = Directory.GetFiles(dest, "*.INF");
            if (destInfs == null || destInfs.Length == 0)
                return false;

            string srcInf = srcInfs[0];
            string destInf = destInfs[0];

            //FileAttributes srcAttr = File
            FileInfo srcInfInfo = new FileInfo(srcInf);
            long srcSize = srcInfInfo.Length;
            FileInfo destInfInfo = new FileInfo(destInf);
            long destSize = srcInfInfo.Length;

            if (srcSize != destSize)
                return false;

            MD5 md5 = new MD5CryptoServiceProvider();
            FileStream srcFile = new FileStream(srcInf, FileMode.Open);
            byte[] hashSrc = md5.ComputeHash(srcFile);
            srcFile.Close();

            FileStream destFile = new FileStream(destInf, FileMode.Open);
            byte[] hashDest = md5.ComputeHash(destFile);
            destFile.Close();

            if (hashSrc.Length != hashDest.Length)
                return false;
            for (int i = 0; i < hashSrc.Length; i++)
            {
                if (hashSrc[i] != hashDest[i])
                    return false;
            }

            return true;
        }

        public void InstallDrivers(string[] dirs)
        {
            foreach (string dir in dirs)
            {
                try
                {
#if EUCA_DEBUG
                    LogTools.Debug(string.Format("Attemping to install driver {0}", dir));
#endif
                    string[] infs = Directory.GetFiles(dir, "*.INF");
                    if (infs == null || infs.Length == 0)
                    {
                        Program.Log(string.Format("Can't find 'inf' file in {0}", dir));
                        continue;
                    }
                    foreach (string infFile in infs)
                    {
                        StringBuilder destFile = new StringBuilder(MAX_PATH);
                        int reqSize = 0;
                        StringBuilder destinationInfFileNameComponent = new StringBuilder();
                        bool copied = SetupCopyOEMInf(infFile, null, OemSourceMediaType.SPOST_PATH,
                            OemCopyStyle.SP_COPY_NOOVERWRITE | OemCopyStyle.SP_COPY_FORCE_IN_USE,
                            destFile, MAX_PATH, ref reqSize, destinationInfFileNameComponent);

                        if (copied)
                            Program.Log(string.Format("The driver({0}) succesfully installed", infFile));
                        else
                        {
                            int errCode = GetLastError();
                            if (errCode == ERROR_FILE_EXISTS)
                                Program.Log(string.Format("The driver({0}) already found in the system", infFile));
                            else
                            {
                                throw new Exception(string.Format("SetupCopyOEMInf on {0} returned error code ({1})", infFile, errCode));
                            }
                        }
                    }
                }
                catch (Exception e)
                {
                    Program.Log(e.Message);
                    Program.Log(e.StackTrace);
                    throw e;    // terminate the loop without installing the remaining drivers
                }
            }
        }
    }
}
