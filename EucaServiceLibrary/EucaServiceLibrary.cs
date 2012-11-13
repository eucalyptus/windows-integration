/*************************************************************************
 * Copyright 2010-2012 Eucalyptus Systems, Inc.
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

//#define EUCA_DEBUG
using System;
using System.Collections.Generic;
using System.Net;
using System.IO;
using System.Text;
using System.Threading;
using System.Management;
using Microsoft.Win32;
using System.Xml;
using System.Xml.Serialization;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.ServiceProcess;
using NetFwTypeLib;

namespace Com.Eucalyptus.Windows.EucaServiceLibrary
{
    public class Bootstrapper
    {
        /*
         * Sequences for bootstrapping
         * 
         * 1. Prepares the environment (ACPI, remote desktop allowance)
         * 2. Change admin's password
         * 3. Change hostname by ns reverse lookups
         * 4. join AD
         */
      
        private static string _configLocation = null;
        internal static string ConfigLocation
        {
            get{ return _configLocation; }           
        }

        private static Configuration _eucaConfig = null;
        internal static Configuration Configuration
        {
            get { return _eucaConfig; }
            set { _eucaConfig = value; }
        }

        public static string[] EucaRegistryPath = {
                                        "SOFTWARE","Eucalyptus Systems","Eucalyptus"
                                                        };

        /// <summary>
        /// Steps to configure the windows instances
        /// 1. Wait for the network connectivity become active
        /// 2. Setup ACPI/remote desktop allowance
        /// 3. Format uninitialize drives
        /// 4. Change admin's password
        /// 5. Change hostname (and reboot)
        /// 6. Join AD
        /// </summary>
        /// <param name="configFileLocation"></param>
        public virtual void DoBootstrap(string configFileLocation)
        {           
            try
            {
                string installLocation = (string)EucaUtil.GetRegistryValue(Registry.LocalMachine, EucaRegistryPath, "InstallLocation");
                EucaLogger.LogLocation = string.Format("{0}\\eucalog.txt", installLocation);

                _configLocation = configFileLocation;
                EucaLogger.Info(string.Format("Eucalyptus Systems Inc. Windows Service ver 1.02, 11/13/2012"));
                EucaLogger.Info(string.Format("EucaServiceLibrary with config= {0}!", configFileLocation));
                EucaLogger.Info(string.Format("OS: {0},  SP: {1}, 64bit?:{2}", OSEnvironment.OS_Name, OSEnvironment.OS_ServicePack, OSEnvironment.Is64bit));
                try
                {
                    _eucaConfig = ConfigurationParser.Parse(configFileLocation);
#if EUCA_DEBUG
                    EucaLogger.Debug(_eucaConfig.ToString());
#endif
                }
                catch (Exception e)
                {   
                    EucaLogger.Exception("Failed to parse configuration; admin's password and AD setting will not be changed", e);
                }

                try
                {
                    RecordInstanceLaunch(_eucaConfig);
                }
                catch (Exception e) 
                {
                    EucaLogger.Exception("Could not record and check the instance's launch status", e);
                }

                DoBootstrapThreaded(); // will return immediately
                EucaLogger.Info("Windows service started");
                /// service will return now         
            }catch (Exception e)
            {
                EucaLogger.Exception("Bootstrapping failed", e);
            }
        }

        /// <summary/>
        /// <param name="eucaConfig"></param>
        /// <exception cref="EucaException"/>
        private static void RecordInstanceLaunch(Configuration eucaConfig)
        {
            /// location => C:\\program files\\
            string installLocation = (string)EucaUtil.GetRegistryValue(Registry.LocalMachine, EucaRegistryPath, "InstallLocation");
            string instanceHistoryFile = string.Format("{0}\\{1}", installLocation, "instances");

            /// check if the hostname in the eucaConfig is in the instance history
            /// 
            bool instanceFound = false;
            if (File.Exists(instanceHistoryFile))
            {
                using (StreamReader sr = new StreamReader(new FileStream(
                    instanceHistoryFile, FileMode.Open, FileAccess.Read)))
                {
                    string line = null;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line.ToLower().Trim() == eucaConfig.Hostname.ToLower().Trim())
                        {
                            instanceFound = true;
                            break;
                        }
                    }
                }
            }

            if (instanceFound)
                EucaConstant.JustLaunched = false;
            else
            {
                EucaConstant.JustLaunched = true;
                using(StreamWriter sw = new StreamWriter(
                    new FileStream(instanceHistoryFile, FileMode.Append, FileAccess.Write)))
                {
                    sw.WriteLine(eucaConfig.Hostname);
                }
                EucaLogger.Debug(string.Format("Instance {0} just launched", eucaConfig.Hostname));
            }
        }

        private static void DoBootstrapThreaded()
        {
            ThreadStart ts = new ThreadStart(DoBootstrapThreadedRun);
            Thread t = new Thread(ts);
            t.Start();
        }

        private static void DoBootstrapThreadedRun()
        {
            try
            {
                EnvironmentManager.Instance.UpdateEnvironment();
            }
            catch (Exception e)
            {
                EucaLogger.Exception(string.Format("Failed to setup ACPI/Remote desktop allowance ({0})", e.Message), e);
            }

            string uname = null, passwd = null;
            try
            {
                uname = Configuration.LocalAccount.Username.Trim();
                passwd = Configuration.LocalAccount.Password.Trim();
                AccountManager.Instance.UpdateAccount(uname, passwd);
            }
            catch (Exception e)
            {
                EucaLogger.Exception(string.Format("Failed to change {0}'s password to {1}", uname, passwd), e);
            }

            //// create a partiton and format ephemeral storage
            // 1. On initial boot, or restart, the ES will appear as an
            //    un-initialized disk with no partitions.
            // 2. On reboot, the ES setting are non-volitile, thus needs no formatting.
            // 3. Note: This step is not reached if the service is not running 
            //    on a ec2 image since the instance_id can not detected.

            //  euca 3011 .. this is sequence all the time. 
            //
            // if (EucaConstant.JustLaunched)
            // {
                try
                {
                    int format = (int)EucaUtil.GetRegistryValue(
                        Registry.LocalMachine, Bootstrapper.EucaRegistryPath, "FormatDrives");
                    if (format == 1)
                    {
                        EucaLogger.Debug("Attempting to initialize attached disks");
                        DiskManager.Instance.PrepareEphemeral();
                    }
                }
                catch (Exception e)
                {
                    EucaLogger.Debug("No Ephemeral attached disks were formatted");
                   // EucaLogger.Exception(string.Format("Failed to initialize attached disks ({0})", e.Message), e);
                }
            //}

            // if the instance was just launched, and already a member of AD, then detach it from AD
            try
            {
                if (EucaConstant.JustLaunched)
                    ActiveDirectoryManager.CheckAndDetach();               
            }
            catch (Exception e)
            {
                EucaLogger.Exception("Couldn't check and detach instance from the domain", e);
            }

            try
            {
                HostnameManager.Instance.UpdateHostname(Configuration);
                if (_rebootRequired)
                {
                    EucaLogger.Info("The system is now rebooting");
                    RebootMachine();
                }
            }
            catch (Exception e)
            {
                EucaLogger.Exception(string.Format("Failed to change hostname ({0})", e.Message), e);
            }

            try
            {
                if (!_rebootRequired)
                {
                    ActiveDirectoryManager adman = new ActiveDirectoryManager(Configuration);
                    adman.JoinActiveDirectory();
                    adman.SetRDPermission();
                    if (_rebootRequired) /// comment
                    {
                        EucaLogger.Info("The system is now rebooting");
                        RebootMachine();
                    }
                }
               
            }catch(Exception e)
            {
                EucaLogger.Exception("Active directory join failed", e);
            }

            EucaLogger.Info("Eucalyptus instance bootstrapping succeedeed");
        }

        private static bool _rebootRequired = false;
        internal static void SetReboot() {
            _rebootRequired = true;
        }
        
        // beginnning of win32 interop to adjust local system's privilege
        // Structures needed for the API calls 
        private struct LUID
        {
            public int LowPart;
            public int HighPart;
        }
        private struct LUID_AND_ATTRIBUTES
        {
            public LUID pLuid;
            public int Attributes;
        }
        private struct TOKEN_PRIVILEGES
        {
            public int PrivilegeCount;
            public LUID_AND_ATTRIBUTES Privileges;
        }

        [DllImport("advapi32.dll")]
        static extern int OpenProcessToken(IntPtr ProcessHandle,
                             int DesiredAccess, out IntPtr TokenHandle);

        [DllImport("advapi32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AdjustTokenPrivileges(IntPtr TokenHandle,
            [MarshalAs(UnmanagedType.Bool)]bool DisableAllPrivileges,
            ref TOKEN_PRIVILEGES NewState,
            UInt32 BufferLength,
            IntPtr PreviousState,
            IntPtr ReturnLength);

        [DllImport("advapi32.dll")]
        static extern int LookupPrivilegeValue(string lpSystemName,
                               string lpName, out LUID lpLuid);
        [DllImport("user32.dll", SetLastError = true)]
        static extern bool ExitWindowsEx(uint uFlags, uint dwReason);

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern uint InitiateShutdown(string lpMachineName, string lpMessage,
            UInt32 dwGracePeriod, UInt32 dwShutdownFlags, UInt32 dwReason);

        [DllImport("advapi32.dll", SetLastError = true)]
        static extern bool InitiateSystemShutdown(string lpMachineName, string lpMessage,
            UInt32 dwTimeout, bool bForceAppsClosed, bool bRebootAfterShutdown);
        // end of win32 interop
        
        private const int REBOOT_GRACE_SEC = 5;
        static private void RebootMachine()
        {
            DoRebootMachine();
        }
      
        private static void DoRebootMachine()
        {
            try // trying to enable SE_SHUTDOWN privilege
            {
                const string SE_SHUTDOWN_NAME = "SeShutdownPrivilege";
                const short SE_PRIVILEGE_ENABLED = 2;
                const uint EWX_REBOOT = 2;
                const uint EWX_FORCE = 0x00000004;
                const uint EWX_FORCEIFHUNG = 0x00000010;

                const uint SHUTDOWN_FORCE_OTHERS = 0x00000001;
                const uint SHUTDOWN_FORCE_SELF = 0x00000002;
                const uint SHUTDOWN_RESTART = 0x00000004;

                const short TOKEN_ADJUST_PRIVILEGES = 32;
                const short TOKEN_QUERY = 8;
                IntPtr hToken;
                TOKEN_PRIVILEGES tkp;
                OpenProcessToken(System.Diagnostics.Process.GetCurrentProcess().Handle, 
                    TOKEN_ADJUST_PRIVILEGES | TOKEN_QUERY, out hToken);
                tkp.PrivilegeCount = 1;
                tkp.Privileges.Attributes = SE_PRIVILEGE_ENABLED;
                LookupPrivilegeValue("", SE_SHUTDOWN_NAME, out tkp. Privileges.pLuid);
                bool ok = AdjustTokenPrivileges(hToken, false, ref tkp, 0U, IntPtr.Zero,
                      IntPtr.Zero);
                if (!ok)
                    throw new Exception("AdjustTokenPrivileges returned error");
#if EUCA_DEBUG
                EucaLogger.Debug("Enabled SE_SHUTDOWN_NAME privilege");
#endif              
                uint dwReason = 0x00040000 | 0x00000001 | 0x80000000; // major application, minor maintenance, planned
                
                
                try
                {   // this is to make sure system is fully booted and is in stable state
                    // also we don't want to wait too long before reboot, because it opens RDP.
                    Bootstrapper.PollNetworkConnection(true);
                }
                catch (Exception e)
                {
                    EucaLogger.Exception(string.Format("DoReboot:cannot wait for the network connection ({0})", e.Message), e);
                }

                if (OSEnvironment.OS_Name == OSEnvironment.Enum_OsName.XP ||
                    OSEnvironment.OS_Name == OSEnvironment.Enum_OsName.S2003 ||
                    OSEnvironment.OS_Name == OSEnvironment.Enum_OsName.S2003R2)
                {                 

                    ok = ExitWindowsEx(EWX_REBOOT | EWX_FORCE | EWX_FORCEIFHUNG, dwReason);
                    if (!ok)
                        throw new Exception("Reboot failed");
                }
                else
                {
                    // reboot now!
                    uint ret = InitiateShutdown(null, null, 
                        REBOOT_GRACE_SEC, SHUTDOWN_FORCE_OTHERS | SHUTDOWN_FORCE_SELF | SHUTDOWN_RESTART, dwReason);
                    //ExitWindowsEx(EWX_REBOOT | EWX_FORCE | EWX_FORCEIFHUNG, 0);
                    if (ret != 0)
                        throw new Exception("InitiateShutdown failed");
                }
            }
            catch (Exception e)
            {
                EucaLogger.Exception("Couldn't reboot automatically", e);
            }
        }

        
        
        internal static void PollNetworkConnection(bool writeIPToFloppy)
        {
            PollNetworkConnectionDns(writeIPToFloppy);
        }
        
        /// <summary>
        /// in older windows (pre xp sp3), classes under networkinformation is not supported.
        /// instead Dns classes are used here
        /// </summary>
        private static void PollNetworkConnectionDns(bool writeIPToFloppy)
        {
            string hostname = System.Net.Dns.GetHostName();
       
            int warningAfterSec = 30;
            DateTime lastWarning = DateTime.Now;       
            bool activeNetFound=false;
            byte[] bIPAddr = null;
            do
            {
                foreach (System.Net.IPAddress addr in Dns.GetHostEntry(hostname).AddressList)
                {
                    if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        bIPAddr = addr.GetAddressBytes();
                        if (IsValidIP(bIPAddr))
                        {
                            activeNetFound = true;
                            EucaLogger.Debug(string.Format("IP address {0} found", addr.ToString()));
                            break;
                        }
                    }
                }
                if (activeNetFound)
                    break;
                
                System.Threading.Thread.Sleep(1000);
                if (new TimeSpan(DateTime.Now.Ticks - lastWarning.Ticks).Seconds > warningAfterSec)
                {
                    EucaLogger.Warning("No active network connection");
                    lastWarning = DateTime.Now;
                }
            } while (true);

            if (activeNetFound && writeIPToFloppy)
            {
                string ip = ToIPString(bIPAddr);
                string ipLine = string.Format("<IPADDRESS>{0}</IPADDRESS>", ip);
                AppendInstanceInfo(ipLine);
            }
        }
        private const string InfoFileName = "instinfo.xml";
        private static void AppendInstanceInfo(string info)
        {
            try
            {
                FileInfo fi = new FileInfo(_configLocation);
                string dir = null;
                if (fi == null)
                    dir = "A:\\";
                else
                    dir = fi.DirectoryName;

                string infoFile = string.Format("{0}\\{1}", dir, InfoFileName);

                using (StreamWriter sw = new StreamWriter(
                    new FileStream(infoFile, FileMode.Append, FileAccess.Write)))
                {
                    sw.WriteLine(info);
                    sw.Flush();
                    sw.Close();
                }
            }
            catch (Exception e)
            {
                EucaLogger.Exception("Could not write instance info to the floppy", e);
            }
        }

        private static string ToIPString(byte[] ip)
        {
            if (ip == null || ip.Length != 4)
                return null;

            return string.Format("{0}.{1}.{2}.{3}",
                Convert.ToInt16(ip[0]), Convert.ToInt16(ip[1]), Convert.ToInt16(ip[2]), Convert.ToInt16(ip[3]));
        }

        private static bool IsValidIP(byte[] bIPAddr)
        {
            if (bIPAddr[0] == 0 && bIPAddr[1] == 0 && bIPAddr[2] == 0 && bIPAddr[3]==0)
                return false;

            if (bIPAddr[0] == 127 && bIPAddr[1] == 0 && bIPAddr[2] == 0 && bIPAddr[3] == 1)
                return false;

            if (bIPAddr[0] == 169 && bIPAddr[1] == 254) // automatic private IP address
                return false;

            return true;
        }

        /// <summary>
        /// poll network connection via netinfo classes (XP not supported)
        /// </summary>
        private static void PollNetworkConnectionNetinfo()
        {
            System.Net.NetworkInformation.NetworkInterface[] netAdapters =
                System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces();
            if (netAdapters == null || netAdapters.Length < 1)
                throw new EucaException("No network adapter is found");

            int warningAfterSec = 30;
            DateTime lastWarning = DateTime.Now;
            bool activeIfFound = false;
            do
            {
                foreach(System.Net.NetworkInformation.NetworkInterface iface in netAdapters)
                {
                    if (!(iface.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Ethernet
                        || iface.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.Ethernet3Megabit
                        || iface.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.FastEthernetFx
                        || iface.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.FastEthernetT
                        || iface.NetworkInterfaceType == System.Net.NetworkInformation.NetworkInterfaceType.GigabitEthernet))
                        continue;

                    if (iface.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up)
                    {
                        activeIfFound = true;
                        break;
                    }
                }
                if (activeIfFound)
                    break;

                System.Threading.Thread.Sleep(1000);
                if (new TimeSpan(DateTime.Now.Ticks - lastWarning.Ticks).Seconds > warningAfterSec)
                {
                    EucaLogger.Warning("No active network connection");
                    lastWarning = DateTime.Now;
                }
            } while (true);
        }
    }

    internal class ConfigurationParser
    {
        internal static Configuration Parse(string configFileLocation)
        {
            if (configFileLocation == null || configFileLocation == "" || !System.IO.File.Exists(configFileLocation))
                throw new System.IO.IOException(string.Format("File {0} not found", configFileLocation));

            StringBuilder sbTmp = new StringBuilder();
            // make sure password is correctly formatted
            using (StreamReader sr = new StreamReader(configFileLocation))
            {
                string strFile = sr.ReadToEnd();
                int idxHostnameStart = strFile.IndexOf("<Hostname>") + "<Hostname>".Length;
                int idxHostnameEnd = strFile.IndexOf("</Hostname>");
                if (idxHostnameStart > 0 && idxHostnameEnd > idxHostnameStart)
                {
                    string strHostname = strFile.Substring(idxHostnameStart, 10); // only the first 10 characters make up EC2 instance name
                    sbTmp.Append(strFile.Substring(0, idxHostnameStart)); // before hostname
                    sbTmp.Append(strHostname);
                    sbTmp.Append(strFile.Substring(idxHostnameEnd));
                    strFile = sbTmp.ToString();
                    sbTmp.Remove(0, sbTmp.Length);
                }

                int idxPwdStart = strFile.IndexOf("<Password>") + "<Password>".Length;
                int idxPwdEnd = strFile.IndexOf("</Password>");
                if (idxPwdStart > 0 && idxPwdEnd > idxPwdStart)
                {
                    string strPasswd = strFile.Substring(idxPwdStart, idxPwdEnd - idxPwdStart);
                    char[] passwdChars = strPasswd.ToCharArray();

                    int i = 0;
                    const string symbols = "{}[]!@#$%^&*()_+-=`~?.,/'|<>";
                    for (i = 0; i < passwdChars.Length; i++)
                    {
                        char c = passwdChars[i];
                        if (!(char.IsLetterOrDigit(c) || char.IsSymbol(c) || char.IsNumber(c) || symbols.Contains(new string(c,1)) ))
                        {
                            break;
                        }
                    }
                    strPasswd = new string(passwdChars, 0, i);
                    sbTmp.Append(strFile.Substring(0, idxPwdStart));
                    sbTmp.Append(strPasswd);
                    sbTmp.Append(strFile.Substring(idxPwdEnd));
                }               
            }

            try
            {
                XmlSerializer xs = new XmlSerializer(typeof(Configuration));
                Configuration obj = null;
                if (sbTmp.Length > 0)
                {
                    using (StringReader sr = new StringReader(sbTmp.ToString()))
                    {
                        obj = (Configuration)xs.Deserialize(sr);
                    }
                }
                else
                {
                    using (Stream s = File.OpenRead(configFileLocation))
                    {
                        obj = (Configuration)xs.Deserialize(s);
                    }
                }

                return obj;
            }
            catch (Exception e)
            {
                throw new EucaException("XML contents is not properly formatted", e);
            }
        }

        internal static void Write(Configuration configObj, string filename, bool overwrite)
        {
            XmlSerializer xs = new XmlSerializer(typeof(Configuration));
            if (File.Exists(filename))
            {
                if (!overwrite)
                    throw new IOException("File exists");
                else
                    File.Delete(filename);
            }

            using (Stream s = File.Create(filename))
            {
                xs.Serialize(s, configObj);
                s.Flush();
                s.Close();
            }
        }
    }
    internal class DiskManager
    {
        private DiskManager() { }
        static private DiskManager _instance = new DiskManager();
        public static DiskManager Instance {
            get { return _instance; }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <exception>EucaException</exception>
        public void PrepareEphemeral()
        {
            string[] partitionedLetters = null;
            try
            {
                partitionedLetters = this.CreatePartition();
            }
            catch (Exception e)
            {
                throw new EucaException("Exception thrown while creating ephemeral partitions", e);
            }
            if (partitionedLetters == null || partitionedLetters.Length <= 0)
            {
                EucaLogger.Debug("No new partition is created");
                return;
            }
            else
                EucaLogger.Debug(string.Format("{0} partitions created on uninitialized disks", partitionedLetters.Length));
            
            try
            {
                this.Format(partitionedLetters);
            }
            catch (Exception e)
            {
                throw new EucaException("Exception thrown while formatting drives", e);
            }
        }

        private const int NumPartitionTrials = 3;
        private string[] CreatePartition()
        {
            DateTime startTime = DateTime.Now;
            int[] uninitDisks = null;            
            int i=0;
            List<string> partitionedDriveLetters = new List<string>();
            while(i<NumPartitionTrials)
            {
                try
                {
                    uninitDisks = this.GetUninitializedDisks();
                    if (uninitDisks != null && uninitDisks.Length > 0)
                        break;
                    i++;
                    Thread.Sleep(1000);
                    EucaLogger.Warning(string.Format("Diskpart can't find any uninitialized disks in {0}'th attempt", i));
                }
                catch (Exception e)
                {
                    i++;
                    EucaLogger.Exception(EucaLogger.LOG_WARNING, e);
                }
            }
            
            if (uninitDisks == null || uninitDisks.Length <= 0)
            {
                EucaLogger.Debug("No uninitialized disks are found");
                return null;
            }

            EucaLogger.Debug("Number of uninitialized disks: "+uninitDisks.Length);
            List<string> preExistingLetters = new List<string>();
            try
            {
                string[] tmp = this.GetAssignedLetters();
                if (tmp != null)
                {
                    foreach (string s in tmp)
                    {
                        if (s != null)
                        {
                            preExistingLetters.Add(s.ToUpper());
                        }
                    }
                }
            }
            catch (Exception e) {
                throw new EucaException("Can't get the list of pre-assigned drive letters", e);
            }
            string[] letters = new string[]{
                "D","E","F","G","H","I","J","K","L","M","N","O","P","Q","R","S","T","U","V","W","X","Y","Z"
            };
            List<string> driveLetters = new List<string>();
            foreach (string s in letters)
                driveLetters.Add(s);

            foreach (int diskNum in uninitDisks)
            {
                string letter = null;
                foreach (string s in driveLetters)
                {
                    if (!preExistingLetters.Contains(s))
                    {
                        letter = s;
                        break;
                    }
                }
                if (letter == null)
                    throw new EucaException("Can't find unassigned drive letter");
                driveLetters.Remove(letter);
                EucaLogger.Debug("Driver letter chosen for new partition: " + letter);
                StringBuilder sbDiskPart = new StringBuilder();
                if (OSEnvironment.OS_Name == OSEnvironment.Enum_OsName.S2008 ||
                    OSEnvironment.OS_Name == OSEnvironment.Enum_OsName.S2008R2 ||
                    OSEnvironment.OS_Name == OSEnvironment.Enum_OsName.Win7 ||
                    OSEnvironment.OS_Name == OSEnvironment.Enum_OsName.Vista)
                {
                    ///"select disk 1", "attribute disk clear readonly", "online disk noerr", "clean", "create partition primary", "assign letter=G", "exit"
                    sbDiskPart.AppendLine(string.Format("select disk {0}", diskNum));
                    sbDiskPart.AppendLine("attribute disk clear readonly");
                    sbDiskPart.AppendLine("online disk noerr");
                    sbDiskPart.AppendLine("clean");
                    sbDiskPart.AppendLine("create partition primary");
                    sbDiskPart.AppendLine(string.Format("assign letter={0}", letter));
                    sbDiskPart.AppendLine("exit");
                }
                else
                {
                    //"select disk 1", "online noerr", "clean", "create partition primary", "assign letter=G", "exit"
                    sbDiskPart.AppendLine(string.Format("select disk {0}", diskNum));
                    sbDiskPart.AppendLine("online noerr");
                    sbDiskPart.AppendLine("clean");
                    sbDiskPart.AppendLine("create partition primary");
                    sbDiskPart.AppendLine(string.Format("assign letter={0}", letter));
                    sbDiskPart.AppendLine("exit");
                }

                try
                {
                    using (StreamWriter sw = new StreamWriter(
                        new FileStream("C:\\diskpart.txt", FileMode.Create, FileAccess.Write)))
                    {
                        sw.Write(sbDiskPart.ToString());
                        sw.Flush();
                    }
                    Win32_CommandResult result = EucaUtil.SpawnProcessAndWait("diskpart.exe", "/s C:\\diskpart.txt");
                    if (result.Stdout == null || !result.Stdout.Contains("successfully assigned the drive letter"))
                    {
                        StringBuilder sbLog = new StringBuilder();
                        sbLog.AppendLine("Diskpart failed --");
                        if (result.Stdout != null)
                            sbLog.AppendLine(string.Format("stdout: {0}", result.Stdout));
                        if (result.Stderr != null)
                            sbLog.AppendLine(string.Format("stderr: {0}", result.Stderr));
                        EucaLogger.Warning(sbLog.ToString());
                    }
                    else
                    {
                        partitionedDriveLetters.Add(letter);
                        EucaLogger.Debug(string.Format("New partition created and assigned {0}", letter));
                        Thread.Sleep(500);
                    }
                }
                finally
                {
                    if (File.Exists("C:\\diskpart.txt"))
                        File.Delete("C:\\diskpart.txt");
                }
            }
            return partitionedDriveLetters.ToArray();
        }

        private string[] GetAssignedLetters()
        {
            using (StreamWriter sw = new StreamWriter(
                    new FileStream("C:\\diskpart.txt", FileMode.Create, FileAccess.Write)))
            {
                sw.WriteLine("list volume");
                sw.WriteLine("exit");
                sw.Flush();
            }
            List<string> possibleLetters = new List<string>();
            string[] pletters = new string[]{"C","D","E","F","G","H","I","J","K","L","M","N","O","P","Q","R","S","T","U","V","W","X","Y","Z"
            };
            foreach(string s in pletters)
                possibleLetters.Add(s);

            List<string> assigned = new List<string>(10);
            try
            {
                Win32_CommandResult result = EucaUtil.SpawnProcessAndWait("diskpart.exe", "/s C:\\diskpart.txt");
                if (result.ExitCode != 0)
                    throw new EucaException("diskpart list volume returned exit code " + result.ExitCode);
                StringReader sr = new StringReader(result.Stdout);                
                string line = null;
                string[] parts = null;
                while ((line = sr.ReadLine()) != null)
                {
                    line = line.Trim();
                    if (line.Length <= 0)
                        continue;
                    if (line.StartsWith("Volume") && !line.Contains("#"))
                    {
                        parts = line.Split(' ');
                        foreach (string s in parts)
                        {
                            if (s.Length == 1 && possibleLetters.Contains(s.ToUpper()))
                                assigned.Add(s);                                                           
                        }
                    }
                }
                return assigned.ToArray();
            }
            catch (Exception e)
            {
                throw new EucaException("Exception thrown while getting the list of assigned drive letters", e);
            }
        }
        
        private int[] GetUninitializedDisks()
        {
            try
            {
                /// get a list of all disks
                using (StreamWriter sw = new StreamWriter(
                    new FileStream("C:\\diskpart.txt", FileMode.Create, FileAccess.Write)))
                {
                    sw.WriteLine("list disk");
                    sw.WriteLine("exit");
                    sw.Flush();
                }
                List<int> disks = new List<int>(10);
                Win32_CommandResult result = EucaUtil.SpawnProcessAndWait("diskpart.exe", "/s C:\\diskpart.txt");
                if (result.ExitCode != 0)
                    throw new EucaException("diskpart list returned exit code "+result.ExitCode);                
                StringReader sr = new StringReader(result.Stdout);
                string line = null;
                string[] parts = null;
                while((line = sr.ReadLine())!=null)
                {
                    line = line.Trim();
                    if(line.Length ==0)
                        continue;

                    if (line.StartsWith("Disk") && !line.Contains("#"))
                    {
                        parts = line.Split(' ');
                        disks.Add(int.Parse(parts[1]));
                    }
                }

                List<int> uninitDisks = new List<int>(disks.Count);
                /// check which disks has no partition in it
                /// 
                foreach(int disk in disks)
                {
                    using (StreamWriter sw = new StreamWriter(
                        new FileStream("C:\\diskpart.txt", FileMode.Create, FileAccess.Write)))
                    {
                        sw.WriteLine(string.Format("select disk {0}", disk));
                        sw.WriteLine("list partition");
                        sw.WriteLine("exit");
                        sw.Flush();
                    }
                    result = EucaUtil.SpawnProcessAndWait("diskpart.exe", "/s C:\\diskpart.txt");
                    if (result.ExitCode != 0)
                        throw new EucaException("diskpart list partition returned exit code " + result.ExitCode);
                    
                    if (result.Stdout.ToLower().Contains("no partitions on this disk"))
                        uninitDisks.Add(disk);                    
                 }
                return uninitDisks.ToArray();
            }
            catch (Exception e)
            {
                throw new EucaException("Exception thrown while checking uninitialized disks", e);
            }
            finally
            {
                if (File.Exists("C:\\diskpart.txt"))
                    File.Delete("C:\\diskpart.txt");
            }
        }

        private void Format(string[] driveLetters)
        {
            int i = 0;
            foreach (string letter in driveLetters)
            {
                try
                {
                    using (StreamWriter sw = new StreamWriter(
                        new FileStream("C:\\dformat.bat", FileMode.Create, FileAccess.Write)))
                    {
                        //sw.WriteLine(string.Format("echo Y echo Y | format {0}: /q /v:Ephemeral{1} /fs:ntfs", letter, i++));
                        sw.WriteLine(string.Format("echo Y echo Y | format {0}: /q /fs:ntfs", letter, i++));
                        sw.Flush();
                    }
                    EucaLogger.Debug("Formatting Drive:" + letter);
                    Win32_CommandResult result = EucaUtil.SpawnProcessAndWait("C:\\dformat.bat", null);
                    if (result.Stdout == null || !result.Stdout.Contains("Format complete"))
                    {
                        StringBuilder sbLog = new StringBuilder();
                        sbLog.AppendLine(string.Format("format {0} failed --", letter));
                        if (result.Stdout != null)
                            sbLog.AppendLine(string.Format("stdout: {0}", result.Stdout));
                        if (result.Stderr != null)
                            sbLog.AppendLine(string.Format("stderr: {0}", result.Stderr));
                        EucaLogger.Warning(sbLog.ToString());
                    }
                    else
                        EucaLogger.Debug(string.Format("Drive {0} formatted successfully", letter));
                }
                finally
                {
                    if (File.Exists("C:\\dformat.bat"))
                        File.Delete("C:\\dformat.bat");
                }
            }
        }
    }
    internal class EnvironmentManager
    {
        private EnvironmentManager() { }
        static private EnvironmentManager _instance = new EnvironmentManager();
        public static EnvironmentManager Instance
        {
            get
            {
                return _instance;
            }
        }
        private const int ACPI_SETTING_TIMEOUT_SEC = 10;
        private const int FIREWALL_CHECK_TIMEOUT_SEC = 600;
        private const int REMOTE_DESKTOP_ALLOWANCE_TIMEOUT_SEC = 10;
        internal virtual void UpdateEnvironment()
        {
            // allow ACPI setting
            DateTime acpiSettingCheckStartTime = DateTime.Now;
        LB_SHUTDOWN_WITHOUT_LOGON:
            try
            {
                AllowShutdownWithoutLogon();
            }
            catch (Exception e)
            {
                if ((new TimeSpan(DateTime.Now.Ticks - acpiSettingCheckStartTime.Ticks)).TotalSeconds < ACPI_SETTING_TIMEOUT_SEC)
                {
                    EucaLogger.Warning("Changing ACPI setting has failed; will retry");
                    Thread.Sleep(1000);
                    goto LB_SHUTDOWN_WITHOUT_LOGON;
                }
                EucaLogger.Exception("Could not allow shutdown without logon", e);
            }

        /*if (!(OSEnvironment.OS_Name == OSEnvironment.Enum_OsName.S2008 ||
                    OSEnvironment.OS_Name == OSEnvironment.Enum_OsName.S2008R2 ||
                    OSEnvironment.OS_Name == OSEnvironment.Enum_OsName.Win7))
            {*/ // commented out this condition at 08/05/11 
            try
            {
                CheckFirewallThreaded();
            }
            catch (Exception e)
            {
                EucaLogger.Exception("Could not start thread for checking firewall", e);
            }
            //}

            DateTime rdpAllowCheckStartTime = DateTime.Now;
        LB_ALLOW_REMOTE_DESKTOP:
            try
            {
                AllowRemoteDesktop();
            }
            catch (Exception e)
            {
                if ((new TimeSpan(DateTime.Now.Ticks - rdpAllowCheckStartTime.Ticks).TotalSeconds < REMOTE_DESKTOP_ALLOWANCE_TIMEOUT_SEC))
                {
                    EucaLogger.Warning("{0}th attempt to allow remote desktop setting has failed; will retry");
                    Thread.Sleep(1000);
                    goto LB_ALLOW_REMOTE_DESKTOP;
                }
                EucaLogger.Exception("Can't allow remote desktop", e);
            }

            try
            {
                DisableShutdownReasonUI();
            }
            catch (Exception e)
            {
                EucaLogger.Exception("Could not disable shutdown reason UI", e);
            }
        }

        protected virtual void DisableShutdownReasonUI()
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
                        regKey=Registry.LocalMachine.OpenSubKey("SOFTWARE").OpenSubKey("Policies").OpenSubKey("Microsoft", true).CreateSubKey("Windows NT");
                        if(regKey==null)
                            throw new Exception("Couldn't create/open the registry key to change (HKLM.SOFTWARE.Policies.Microsoft.Windows NT");
                        regKey.Close();
                    }
                    regKey = Registry.LocalMachine.OpenSubKey("SOFTWARE").OpenSubKey("Policies").OpenSubKey("Microsoft").OpenSubKey("Windows NT", true)
                        .CreateSubKey("Reliability");
                    if (regKey == null)
                        throw new Exception("Couldn't create/open the registry key to change(HKLM.SOFTWARE.Policies.Microsoft.Windows NT.Reliability)");
                    regKey.Close();
                    regKey = Registry.LocalMachine.OpenSubKey("SOFTWARE").OpenSubKey("Policies").OpenSubKey("Microsoft").OpenSubKey("Windows NT").OpenSubKey("Reliability", true);
                    if (regKey == null)
                        throw new Exception("Couldn't create/open the registry key to change(HKLM.SOFTWARE.Policies.Microsoft.Windows NT.Reliability)");                  
                }       
                regKey.SetValue("ShutdownReasonUI", 0);
                regKey.SetValue("ShutdownReasonOn", 0);
                regKey.Close();
                EucaLogger.Info("ShutdownReasonUI turned off");
            }
            catch (Exception e)
            {
                throw e;
               // EucaLogger.Warning("Could not disable shutdown reason UI");
                //EucaLogger.Exception(e);
            }
        }

        /// <summary>
        /// manually changes the registry setting to enable shutdownwithoutlogon 
        /// should work for the these windows versions: Win 7, vista, xp, 2003(STD,SP1), 2008(ENT), 2008 R2
        /// </summary>
        /// <exception cref="Com.Eucalyptus.Windows.EucaServiceLibrary.EucaException"/>
        protected virtual void AllowShutdownWithoutLogon()
        {
            const string hklm = "HKEY_LOCAL_MACHINE";
            const string keyName = hklm + "\\SOFTWARE\\Microsoft\\Windows\\CurrentVersion\\Policies\\System";
            const string valueName = "shutdownwithoutlogon";
            try
            {
                int curSetting = (int)Registry.GetValue(keyName, valueName, null);
                if (curSetting != 1)
                {
                    Registry.SetValue(keyName, valueName, 1);
                    EucaLogger.Info("Registry setting for 'shutdownwithoutlogon' successfully updated");
                }
                else
                    EucaLogger.Info("No change to registry setting for 'shutdownwithoutlogon'; it's already set");
            }
            catch (System.Security.SecurityException e)
            {
                throw new EucaException("Can't update registry setting", e);
            }
            catch (System.IO.IOException e)
            {
                throw new EucaException("Can't update registry setting", e);
            }
            catch (ArgumentException e)
            {
                throw new EucaException("Can't update registry setting", e);
            }
        }

        private void CheckFirewallThreaded()
        {
            System.Threading.ThreadStart ts =
                new ThreadStart(CheckFirewallThreadedRun);
            System.Threading.Thread t = new Thread(ts);
            t.Start();
        }

        private void CheckFirewallThreadedRun()
        {
            DateTime firewallCheckStartTime = DateTime.Now;
        LB_FIREWALL_CHECK:
            try
            {
                CheckFirewall();
            }
            catch (Exception e)
            {
                if ((new TimeSpan(DateTime.Now.Ticks - firewallCheckStartTime.Ticks)).TotalSeconds < FIREWALL_CHECK_TIMEOUT_SEC)
                {
                    EucaLogger.Warning("Changing firewall setting has failed; will retry");
                    Thread.Sleep(3000);
                    goto LB_FIREWALL_CHECK;
                }
                EucaLogger.Exception("Can't create RDP exception rule in firewall setting", e);
            }
        }

        /// <summary>
        /// check if the firewall doesn't block RDP port (tcp 3389)        /// 
        /// called when serivce runs under {XP, S2003, S2003R2, Vista(non sp1))
        /// </summary>
        protected virtual void CheckFirewall()
        {
            // win 2003, 2003r2 doesn't run firewall service by default. if that's the case, we shouldn't try 
            // enabling rdp exception
            try
            {
                RegistryKey regKey = null;
                if (OSEnvironment.OS_Name == OSEnvironment.Enum_OsName.Vista ||
                    OSEnvironment.OS_Name == OSEnvironment.Enum_OsName.S2008 || 
                    OSEnvironment.OS_Name == OSEnvironment.Enum_OsName.S2008R2 ||
                    OSEnvironment.OS_Name == OSEnvironment.Enum_OsName.Win7)
                    regKey = Registry.LocalMachine.OpenSubKey("SYSTEM").OpenSubKey("CurrentControlSet")
                    .OpenSubKey("Services").OpenSubKey("MpsSvc");
                else
                    regKey = Registry.LocalMachine.OpenSubKey("SYSTEM").OpenSubKey("CurrentControlSet")
                    .OpenSubKey("Services").OpenSubKey("SharedAccess");

                object val = regKey.GetValue("Start");
                regKey.Close();

                if ((int)val != 2) /// firewall not automatic
                {
                    EucaLogger.Info("Firewall service is not auto-start");
                    return;
                }
            }
            catch (Exception e)
            {
                EucaLogger.Warning(string.Format("Could not open registry key for firewall service ({0})", e.Message));
            }

            // let's wait until firewall service starts up
            try
            {
                bool svcFound = false;
                const int timeoutSec = 30;
                ServiceController[] svcs = ServiceController.GetServices();
                foreach (ServiceController svc in svcs)
                {
                    if (svc.DisplayName.Contains("Windows Firewall")) // I believe this covers all cases
                    {
                        svcFound = true;
                        svc.WaitForStatus(ServiceControllerStatus.Running,
                            new TimeSpan(DateTime.Now.AddSeconds(timeoutSec).Ticks - DateTime.Now.Ticks));
#if EUCA_DEBUG
                        EucaLogger.Debug("Windows Firewall Service is now running");
#endif
                    }
                }
                if (!svcFound)
                    EucaLogger.Warning("Firewall service is not found in the system");
            }
            catch (System.ServiceProcess.TimeoutException)
            {
                EucaLogger.Warning("Firewall service is not running (timed out)");
            }
            catch (Exception e)
            {
                EucaLogger.Exception("Firewall service is not running", e);
            }

            // should check firewall setting everytime service starts 
            /*try
            {
                // if we did unblock the firewall for remote desktop, do nothing
                object obj = EucaConstant.GetRegistryValue("FirewallCheck");
                if (obj != null && (int)obj==1)
                {
                    EucaLogger.Info("Firewall already unblocked");
                    return;
                }
            }
            catch (Exception) {
                EucaLogger.Warning("Could not retrieve registry value for firewall check");
            }*/

            Type FwMgrType = null;
            INetFwMgr mgr = null;
            try
            {
                FwMgrType = Type.GetTypeFromProgID("HNetCfg.FwMgr", true);
                mgr = (INetFwMgr)Activator.CreateInstance(FwMgrType);
            }
            catch (Exception e)
            {
                throw new EucaException("Can't get the COM object for firewall change", e);
            }
            
            foreach ( NET_FW_PROFILE_TYPE_ profile in Enum.GetValues(typeof(NET_FW_PROFILE_TYPE_)))
            {
                if (profile == NET_FW_PROFILE_TYPE_.NET_FW_PROFILE_TYPE_MAX)
                    continue;
                try
                {
                    INetFwServices svcs =  mgr.LocalPolicy.GetProfileByType(profile).Services;
                    bool svcFound = false;
                    foreach (INetFwService svc in svcs)
                    {
                        if (svc.Name == "Remote Desktop")
                        {
                            svcFound = true;
                            if (!svc.Enabled) {
                                svc.Enabled = true;
                                EucaLogger.Info(string.Format("Remote desktop service is unblocked in firewall setting in {0}", profile));
                            }
                            else
                                EucaLogger.Info(string.Format("Remote desktop service was already unblocked in firewall setting in {0}", profile));
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
                    EucaLogger.Exception(string.Format("Can't unblock RDP port (tcp-3389) in {0}", profile), e);
                }
            }

            EucaServiceLibraryUtil.SetSvcRegistryValue("FirewallCheck", 1);
        }

        /// <summary>
        /// Allow remote desktop connection to this system. It creates exception rule to firewall setting
        /// </summary>
        protected virtual void AllowRemoteDesktop()
        {
            try
            {
                string connString = null;
                if (OSEnvironment.OS_Name == OSEnvironment.Enum_OsName.XP ||
                    OSEnvironment.OS_Name == OSEnvironment.Enum_OsName.S2003 ||
                    OSEnvironment.OS_Name == OSEnvironment.Enum_OsName.S2003R2)
                    connString = @"\\.\root\CIMV2";
                else
                    connString = @"\\.\root\CIMV2\TerminalServices";

                using (ManagementObject tsObj = 
                    WMIUtil.QueryLocalWMI(connString, "Select * from Win32_TerminalServiceSetting"))
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
                        
                        inParams.Dispose();
                        outParams.Dispose();

                        if (ret > 1)    // 0=Success, 1=Informational, 2=Warning, 3=Error
                            throw new EucaException(string.Format("SetAllowTsConnects failed with error code {0}", ret));
                        else
                            EucaLogger.Info("Remote desktop allowance succeeded.");
                    }
                    else
                        EucaLogger.Info("No change to remote desktop allowance;already set");
                }
            }
            catch (EucaException e)
            {
                throw e;
            }
            catch (ManagementException e)
            {
                throw new EucaException("WMI provider generated an exception", e);
            }
            catch (Exception e)
            {
                throw new EucaException("Unknown exception generated", e);
            }
        }
    }

    internal class HostnameManager
    {
        private HostnameManager() { }
        private static HostnameManager _instance = new HostnameManager();
        public static HostnameManager Instance { get { return _instance; } }

        internal virtual void UpdateHostname(Configuration config)
        {
            /// if the host is already a member of active directory, the hostname shouldn't be changed.            /// 
            try
            {
                using (ManagementObject comObj = WMIUtil.QueryLocalWMI("Select * from win32_computersystem"))
                {
                    bool inDomain = (bool)comObj["PartOfDomain"];
                    if (inDomain)
                    {
                        EucaLogger.Info("No hostname change because the host is already a part of domain");
                        return;
                    }
                }
            }
            catch (Exception e)
            {
                EucaLogger.Exception("Can't determine if the host is a part of domain", e);
            }           

            string newHostname = null;
            if (config != null && config.Hostname != null && config.Hostname.Length>0)
            {
                EucaLogger.Info(string.Format("The configuration file has fixed hostname ({0})", config.Hostname.Trim()));
                newHostname = config.Hostname.Trim();
            }
            else
            {
                string hostnameFile = EucaConstant.ProgramRoot + "\\hostname";
                if (File.Exists(hostnameFile))
                {
                    using (StreamReader sr = new StreamReader(hostnameFile))
                    {
                        newHostname = sr.ReadLine();
                    }
                }
                else
                {
                    const string EUCA_HOSTNAME_PREFIX = "euca-";
                    string strGuid = Guid.NewGuid().ToString();
                    strGuid = strGuid.Substring(0, 1) + strGuid.Substring(strGuid.LastIndexOf("-") + 1);
                    if (strGuid.Length > 6)
                        strGuid = strGuid.Substring(0, 6);
                    newHostname = string.Format("{0}{1}", EUCA_HOSTNAME_PREFIX, strGuid);
                    EucaLogger.Info(string.Format("Random host name generated: {0}", newHostname));
                    using (StreamWriter sw = new StreamWriter(hostnameFile))
                    {
                        sw.WriteLine(newHostname);
                        sw.Flush();
                    }
                }
            }
            /// what if the resolved hostname is changed while instance has been running?
            /// we change the local hostname as soon as we find it...but is it always right?           
            try
            {
                using (ManagementObject compObj = WMIUtil.QueryLocalWMI(@"\\.\root\CIMV2", "Select * from win32_computersystem"))
                {
                    String curName = (string) compObj["Name"];
                    if (newHostname.ToLower() != curName.ToLower())
                    {
                        EucaLogger.Debug(string.Format("Old hostname: {0}, new hostname: {1}", curName, newHostname));
                        ManagementBaseObject inParams = compObj.GetMethodParameters("Rename");
                        inParams["Name"] = newHostname;
                        ManagementBaseObject retVal = compObj.InvokeMethod("Rename", inParams, null);
                        if ((UInt32)retVal["ReturnValue"] == 0)
                        {
                            EucaLogger.Info(string.Format("The host name is changed to {0}; system requires reboot", newHostname));
                            Bootstrapper.SetReboot();
                        }
                        inParams.Dispose();
                        retVal.Dispose();
                    }
                    else
                        EucaLogger.Info("No hostname change");
                        //throw new EucaException(EucaException.ExceptionLevel.critical, string.Format("The WMI failed to change host name; return code={0}", retVal["ReturnValue"]));
                }
            }
            catch (EucaException)
            {
                throw;
            }
            catch (Exception e)
            {
                throw new EucaException(string.Format("Couldn't complete WMI call to change hostname: {0}", e.Message));
            }
        }

        static private string DoReverseNSLookup(string ipAddr)
        {
            string output = "";

            Win32_CommandResult result= EucaUtil.SpawnProcessAndWait(Environment.GetFolderPath(Environment.SpecialFolder.System)+"\\nslookup.exe", ipAddr);
            output = result.Stdout;

#if EUCA_DEBUG
            EucaLogger.Debug(string.Format("nslookup output --- {0}{1}", Environment.NewLine, output));
#endif

            /***
            * Server: [dns server]
            * Address: [dns server address]
            * 
            * Name: host name
            * Address: host address 
            * 
            ***/
            int idxHostName = output.IndexOf("Name: ", 0);
            if (idxHostName < 0)
                return null;
            else
            {
                int idxHostAddr = output.IndexOf("Address: ", idxHostName, output.Length - idxHostName);
                if (idxHostAddr < 0)
                    return null;
                string hostName = output.Substring(idxHostName + 5, idxHostAddr - (idxHostName + 5) - 1);

                // extract the prefix (xxx.yyy => xxx)
                hostName = hostName.Trim();
                if (hostName.IndexOf(".") > 0)
                    hostName = hostName.Remove(hostName.IndexOf("."));

                return hostName;
            }
        }
    }

    internal class AccountManager
    {
        private AccountManager() { }
        private static AccountManager _instance = new AccountManager();
        public static AccountManager Instance { get { return _instance; } }

        private const int UPDATE_ACCOUNT_RETRY = 0;
        /// is it ok to change username/password everytime system boots?
        /// assuming the answer is yes.
        internal virtual void UpdateAccount(string username, string password)
        {
            /// check registry if the password is already changed
            ///
            if (username.ToLower() == "administrator" && (
                OSEnvironment.OS_Name == OSEnvironment.Enum_OsName.Vista ||
                OSEnvironment.OS_Name == OSEnvironment.Enum_OsName.Win7
                )
            )
            {
                /*object obj = EucaServiceLibraryUtil.GetSvcRegistryValue("AdminActivated");
                if (obj != null && (int)obj == 1)
                { EucaLogger.Info("Administrator account has been activated already"); }
                else
                {*/
                try // attempt to activate Admin account every reboot, assuming that it won't do no harm
                {
                    ActivateAdminUsingNet();
                    EucaLogger.Info("Activated administrator account");
                    EucaServiceLibraryUtil.SetSvcRegistryValue("AdminActivated", 1);
                }
                catch (Exception e)
                {
                    EucaServiceLibraryUtil.SetSvcRegistryValue("AdminActivated", 0);
                    EucaLogger.Exception("Could not activate Administrator account", e);
                }
                //}
            }

            bool passwdChanged = false;
           /* try  // this has an issue with euca-bundle-instance
            {
                object objVal = EucaConstant.GetRegistryValue("PasswordSet");
                if (objVal != null && (int)objVal == 1)
                {
                    EucaLogger.Info("Password has been changed already");
                    passwdChanged = true;
                }
            }
            catch (Exception e)
            {
                EucaLogger.Warning("Can't check registry setting for password set");
                EucaLogger.Exception(e);
            }*/

            if (!passwdChanged)
            {
                CheckPasswordPolicy(username, password);

                int _numTrial = 1;
            LB_UPDATE_ACCOUNT:
                try
                {
                    ChangePasswdUsingNet(username, password);
                    EucaLogger.Info(string.Format("{0}'s password has changed", username));
                    EucaServiceLibraryUtil.SetSvcRegistryValue("PasswordSet", 1);
                    ForgetPassword();
                }
                catch (Exception e)
                {
                    if (_numTrial++ <= UPDATE_ACCOUNT_RETRY)
                    {
                        EucaLogger.Warning(string.Format("{0}'th attempt to change password failed; will retry",
                            (_numTrial - 1)));
                        goto LB_UPDATE_ACCOUNT;
                    }
                    EucaServiceLibraryUtil.SetSvcRegistryValue("PasswordSet", 0);
                    throw new EucaException("Could not change account password using 'net'", e);
                }
                finally
                {
                    RevertPasswordPolicy();
                }
            }
        }

        // the O/S might have a password policy (complexity, length) set. If the new passwd can't meet 
        // the requirements, change the password policy before attemping to change passwd.
        private bool _passwdPolicyChanged = false;
        private string _oldConfigFile = null;
        private void CheckPasswordPolicy(string username, string passwd)
        {
            string secedit = Environment.GetFolderPath(Environment.SpecialFolder.System) + "\\secedit.exe";
            string cfgFile = string.Format("{0}{1}.cfg", EucaConstant.ProgramRoot, Guid.NewGuid().ToString());
            try
            {
                Win32_CommandResult result = EucaUtil.SpawnProcessAndWait(secedit, string.Format("/export /cfg \"{0}\"", cfgFile));
                if (!File.Exists(cfgFile))
                    throw new Exception(string.Format("Could not find {0}", cfgFile));

                string line = null;
                StringBuilder sbNewPolicy = new StringBuilder(20000);
                bool policyChanged = false;
                using (StreamReader sr = new StreamReader(File.OpenRead(cfgFile)))
                {
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line.Trim().StartsWith("PasswordComplexity") && line.IndexOf("=") > 0)
                        {
                            int passComplexity = int.Parse(line.Substring(line.IndexOf("=") + 1).Trim());
                            if (passComplexity != 1)
                            {
                                sbNewPolicy.AppendLine(line);
                                continue;
                            }
                            //1 shouldn't contain username in the password
                            if (passwd.Contains(username))
                            {
                                sbNewPolicy.AppendLine("PasswordComplexity = 0");
                                policyChanged = true;
                                continue;
                            }

                            // 2 Contain characters from three of the following five categories: 
                            //  English uppercase characters (A through Z)
                            //  English lowercase characters (a through z)
                            //   Base 10 digits (0 through 9)
                            //   Non-alphabetic characters (for example, !, $, #, %)
                            //  A catch-all category of any Unicode character that does not fall under the previous four categories. This fifth category can be regionally specific.

                            int upperFound = 0, lowerFound = 0, decimalFound = 0, nonalphaFound = 0;
                            foreach (char c in passwd.ToCharArray())
                            {
                                if (char.IsUpper(c))
                                    upperFound = 1;
                                else if (char.IsLower(c))
                                    lowerFound = 1;
                                else if (char.IsNumber(c))
                                    decimalFound = 1;
                                else if (char.IsSymbol(c))
                                    nonalphaFound = 1;
                            }
                            int numVariation = upperFound + lowerFound + decimalFound + nonalphaFound;
                            if (numVariation < 3)
                            {
                                sbNewPolicy.AppendLine("PasswordComplexity = 0");
                                policyChanged = true;
                            }
                            else
                                sbNewPolicy.AppendLine(line);
                            continue;
                        }
                        else if (line.Trim().StartsWith("MinimumPasswordLength") && line.IndexOf("=") > 0)
                        {
                            int minPasswdLength = int.Parse(line.Substring(line.IndexOf("=") + 1).Trim());
                            if (minPasswdLength > passwd.Length)
                            {
                                sbNewPolicy.AppendLine(string.Format("MinimumPasswordLength = {0}", passwd.Length - 1));
                                policyChanged = true;
                            }
                            else
                                sbNewPolicy.AppendLine(line);
                            continue;
                        }
                        else
                            sbNewPolicy.AppendLine(line);
                    }
                    sr.Close();
                }

                if (!policyChanged)
                {
#if EUCA_DEBUG
                    EucaLogger.Debug("Password policy unchanged");
#endif
                    return;
                }

                string newPolicyFile = string.Format("{0}{1}.cfg", EucaConstant.ProgramRoot, Guid.NewGuid().ToString());
                using (StreamWriter sw = new StreamWriter(File.OpenWrite(newPolicyFile)))
                {
                    sw.Write(sbNewPolicy);
                    sw.Flush();
                    sw.Close();
                }

                // secedit /configure /db %windir%\security\new.sdb /cfg C:\new.cfg /areas SECURITYPOLICY
                string args = string.Format("/configure /db {0}\\security\\new.sdb /cfg \"{1}\" /areas SECURITYPOLICY",
                    Environment.GetEnvironmentVariable("SystemRoot"), newPolicyFile);

                result = EucaUtil.SpawnProcessAndWait(secedit, args);

                /// for later reversal
                _passwdPolicyChanged = true;
                _oldConfigFile = cfgFile;
#if EUCA_DEBUG
                EucaLogger.Debug("Password policy changed");
#endif
                if (File.Exists(newPolicyFile))
                    File.Delete(newPolicyFile);
            }
            catch (Exception e)
            {
                EucaLogger.Exception("Could not change password policy requirements", e);
            }
            finally
            {
                if (File.Exists(cfgFile))
                    File.Delete(cfgFile);
            }
        }

        private void RevertPasswordPolicy()
        {
            if (!_passwdPolicyChanged || !File.Exists(_oldConfigFile))
                return;
            try
            {
                string args = string.Format("/configure /db {0}\\security\\new.sdb /cfg \"{1}\" /areas SECURITYPOLICY",
                    Environment.GetEnvironmentVariable("SystemRoot"), _oldConfigFile);
                string secedit = Environment.GetFolderPath(Environment.SpecialFolder.System) + "\\secedit.exe";
                Win32_CommandResult result = EucaUtil.SpawnProcessAndWait(secedit, args);
#if EUCA_DEBUG
                EucaLogger.Debug("Password policy reverted");
#endif
                if (File.Exists(_oldConfigFile))
                    File.Delete(_oldConfigFile);
            }
            catch (Exception e)
            {
                EucaLogger.Exception("Password policy reversal failed", e);
            }
        }

        // if password change was successful, delete the password info from the "floppy"
        private void ForgetPassword()
        {
            try
            {
                Bootstrapper.Configuration.LocalAccount.Username = null;
                Bootstrapper.Configuration.LocalAccount.Password = null;
                ConfigurationParser.Write(Bootstrapper.Configuration, Bootstrapper.ConfigLocation, true);
            }
            catch (Exception e)
            {
                EucaLogger.Exception("Could not forget account information", e);
            }
        }

        private void ActivateAdminUsingNet()
        {
            Win32_CommandResult result =
                EucaUtil.SpawnProcessAndWait(Environment.GetFolderPath(Environment.SpecialFolder.System) + "\\net.exe",
                "user administrator /active:yes");
            if (!(result.ExitCode == 1 || result.ExitCode == 0))
            {
                throw new EucaException(string.Format("net command failed with exit code = {0}, stdout= {1}, stderr={2}", 
                    result.ExitCode, result.Stdout, result.Stderr));
            }         
        }

        private void ChangePasswdUsingNet(string username, string password)
        {
            Win32_CommandResult result =
                EucaUtil.SpawnProcessAndWait(Environment.GetFolderPath(Environment.SpecialFolder.System) + "\\net.exe",
                 string.Format("user {0} {1}", username, password));

            if (!(result.ExitCode == 1 || result.ExitCode == 0))
            {
                throw new EucaException(string.Format("net command failed with exit code = {0}, stdout= {1}, stderr={2}", result.ExitCode, 
                    result.Stdout, result.Stderr));
            }
        }
    }

    /// <summary>
    ///  Proof of concept ad auto join implementation
    /// </summary>
    /// <remarks>
    /// need to be careful about AD token. The user to the instance shouldn't be able to read it because
    /// that's the privilege that AD administrator assigns to the Euca service.
    /// </remarks>
    internal class ActiveDirectoryManager
    {
        private string _adName = null;
        private string _userName = null;
        private string _password = null;
        private string _ou = null;

        internal ActiveDirectoryManager(Configuration config)
        {
            if (EucaServiceLibraryUtil.GetSvcRegistryValue("ADAddress") != null &&
                EucaServiceLibraryUtil.GetSvcRegistryValue("ADUsername") != null &&
                EucaServiceLibraryUtil.GetSvcRegistryValue("ADPassword") != null)
            {
                _adName = (string)EucaServiceLibraryUtil.GetSvcRegistryValue("ADAddress");
                _userName = (string)EucaServiceLibraryUtil.GetSvcRegistryValue("ADUsername");
                _password = (string)EucaServiceLibraryUtil.GetSvcRegistryValue("ADPassword");
                _password = EucaUtil.Decrypt(_password);

                if (EucaServiceLibraryUtil.GetSvcRegistryValue("ADOU") != null)
                    _ou = (string)EucaServiceLibraryUtil.GetSvcRegistryValue("ADOU");
            }

            if (config.ActiveDirectory != null)
            {
                if (config.ActiveDirectory.JoinOnBootSpecified && config.ActiveDirectory.JoinOnBoot)
                {
                    if (_adName == null ||
                        (config.ActiveDirectory.OverwriteSpecified && config.ActiveDirectory.Overwrite))
                    {
                        _adName = config.ActiveDirectory.AD;
                        _userName = config.ActiveDirectory.ADUsername;
                        _password = config.ActiveDirectory.ADPassword;
                        _ou = config.ActiveDirectory.OU;
                    }
                }
                ForgetADPassword();
            }
        }
        
        internal ActiveDirectoryManager(string ADName, string username, string password)
        {
            _adName = ADName;
            _userName = username;
            _password = password;
        }

        internal ActiveDirectoryManager(string ADName, string username, string password, string OU)
            : this(ADName, username, password)
        {
            _ou = OU;
        }

        private void ForgetADPassword()
        {
            try
            {
                Bootstrapper.Configuration.ActiveDirectory.ADUsername = null;
                Bootstrapper.Configuration.ActiveDirectory.ADPassword = null;
                ConfigurationParser.Write(Bootstrapper.Configuration, Bootstrapper.ConfigLocation, true);
            }
            catch (Exception e)
            {
                EucaLogger.Exception("Could not forget about AD account information", e);
            }
        }

        internal static void CheckAndDetach()
        {
            try
            {
                using (ManagementObject comObj = WMIUtil.QueryLocalWMI("Select * from Win32_ComputerSystem"))
                {
                    if ((bool)comObj["PartOfDomain"])
                    {
                        ManagementBaseObject paramIn = comObj.GetMethodParameters("UnjoinDomainOrWorkgroup");
                        paramIn["Password"] = null;
                        paramIn["UserName"] = null;
                        paramIn["FUnjoinOptions"] = (UInt32)0x00; // default; No option

                        ManagementBaseObject paramOut = comObj.InvokeMethod("UnjoinDomainOrWorkgroup", paramIn, null);
                        UInt32 retVal = (UInt32)paramOut["ReturnValue"];
                        if (retVal == 0)
                        {
                            EucaLogger.Info("Instance is checked and detached from the domain");
                            Bootstrapper.SetReboot();
                        }
                        else
                            EucaLogger.Warning("Instance couldn't be detached from the domain!");
                    }
                }
            }
            catch (Exception e)
            {
                throw new EucaException("Couldn't check and detach the instance from domain", e);
            }
        }

        internal virtual void JoinActiveDirectory()
        {
            if (_adName == null || _userName == null || _password == null)
            {
                EucaLogger.Info("Active directory information is not provided");
                return;
            }

            if (!_userName.Contains("\\"))
                _userName = string.Format("{0}\\{1}", _adName, _userName);
         
            try
            {
                Bootstrapper.PollNetworkConnection(false);
            }
            catch (Exception e)
            {
                EucaLogger.Exception(string.Format("Cannot wait for the network connection ({0})", e.Message), e);
            }

            const int NUM_TRIAL = 5;
            int i = 0;
            try
            {
                using (ManagementObject comObj = WMIUtil.QueryLocalWMI("Select * from Win32_ComputerSystem"))
                {
                    if ((bool)comObj["PartOfDomain"])
                    {
                        EucaLogger.Info(string.Format("The instance is a part of domain {0}", comObj["Domain"]));
                        EucaServiceLibraryUtil.DeleteSvcRegistryValue("ADUsername");
                        EucaServiceLibraryUtil.DeleteSvcRegistryValue("ADPassword");

                        return;
                    }

                    ManagementBaseObject paramIn = comObj.GetMethodParameters("JoinDomainOrWorkgroup");
                    paramIn["Name"] = _adName;
                    paramIn["Password"] = _password;
                    paramIn["UserName"] = _userName;

                    if (_ou != null)
                        paramIn["AccountOU"] = _ou;

                    paramIn["FJoinOptions"] = (UInt32)0x01 | 0x02; // join and create a computer account

                RETRY_ENTRANCE:
                    ManagementBaseObject paramOut = comObj.InvokeMethod("JoinDomainOrWorkgroup", paramIn, null);
                    UInt32 retVal = (UInt32)paramOut["ReturnValue"];
                    if (retVal == 0)
                        EucaLogger.Info(string.Format("The computer joined domain {0}", _adName));
                    else
                    {
                        if (++i < NUM_TRIAL)
                        {
                            System.Threading.Thread.Sleep(1000);
                            EucaLogger.Warning(string.Format("{0}'th join trial failed", i));
                            paramOut.Dispose();
                            goto RETRY_ENTRANCE;
                        }
                        throw new Exception(string.Format("JoinDomainOrWorkgroup returned {0}", retVal));
                    }
                    paramIn.Dispose();
                    paramOut.Dispose();
                    Bootstrapper.SetReboot();
                }
            }
            catch (Exception e)
            {
                throw new EucaException(string.Format("Couldn't join the computer to domain {0}", _adName), e);
            }
            finally
            {
                // remove all AD keys from the file system
                EucaServiceLibraryUtil.DeleteSvcRegistryValue("ADUsername");
                EucaServiceLibraryUtil.DeleteSvcRegistryValue("ADPassword");
            }
        }

        internal virtual void SetRDPermission() 
        { 
            /// read username/group from the registry
            /// 
            string[] users = null;
            try
            {
                users=this.GetRDPermissionUsers();
            }
            catch (Exception e)
            {
                EucaLogger.Exception("Could not read remote desktop permission from the registry", e);
                return;
            }

            if (users == null || users.Length == 0) {
                EucaLogger.Warning("There is no user/group who has a permission to remote desktop");
                return;
            }

            foreach (string s in users) 
            {
                /// remove 'localhost' from the string
                /// 
                string username = null;
                if (s.StartsWith("localhost\\"))
                    username = s.Replace("localhost\\", "");
                else
                    username = s;
                try
                {
                /// invoke 'net' command line with the list
           /// net localgroup "Remote Desktop Users" /add "DOMAIN\User"  (or replace DOMAIN\User with LocalUser).
                    Win32_CommandResult result = 
                        EucaUtil.SpawnProcessAndWait(Environment.GetFolderPath(Environment.SpecialFolder.System) + "\\net.exe", 
                        string.Format("localgroup \"Remote Desktop Users\" /add \"{0}\"", username));
                    if (result.ExitCode != 0)
                        throw new Exception(string.Format("net returned exitCode={0}", result.ExitCode));                    
                }
                catch (Exception e)
                {
                    EucaLogger.Warning(string.Format("Could not allow remote desktop permission to '{0}'({1})", username, e.Message));
                }
            }            
        }

        private string[] GetRDPermissionUsers()
        {
            RegistryKey regKey =
                Registry.LocalMachine.OpenSubKey("SOFTWARE").OpenSubKey("Eucalyptus Systems").
                OpenSubKey("Eucalyptus").OpenSubKey("RDP", false);

            string[] usernames = regKey.GetValueNames();
            regKey.Close();

            return usernames;
        }
    }

    /// <summary>
    /// This class is not used
    /// </summary>
    internal class DriverManager
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

        internal virtual void UpdateDrivers()
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
                        EucaLogger.Info(string.Format("Drivers in {0} / {1} are the same", dir, dest));
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
                    EucaLogger.Exception(string.Format("Could not copy driver directory from {0} to {1}", dir, dest),e);
                }
            }

            InstallDrivers(updatedDrivers.ToArray());
        }

        // compare 'inf' file in each directory
        private bool DriverExists(string src, string dest)
        {
            if (!Directory.Exists(dest))
                return false;

            // compare size of inf file
            string[] srcInfs = Directory.GetFiles(src, "*.inf");
            if (srcInfs == null || srcInfs.Length == 0)
                return true;    // if there's no INF in the source, we think driver exists
            string[] destInfs = Directory.GetFiles(dest, "*.inf");
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

        private void InstallDrivers(string[] dirs)
        {
            string infFile = null;
            foreach (string dir in dirs)
            {
                try
                {
#if EUCA_DEBUG
                    EucaLogger.Debug(string.Format("Attemping to install driver {0}", dir));
#endif
                    string[] infs = Directory.GetFiles(dir, "*.inf");
                    if (infs == null || infs.Length == 0)
                    {
                        EucaLogger.Warning(string.Format("Can't find 'inf' file in {0}", dir));
                        continue;
                    }
                    if (infs.Length > 1)
                        EucaLogger.Warning(string.Format("There are more than one 'inf' files in {0}", dir));
                    infFile = infs[0];

                    StringBuilder destFile = new StringBuilder(MAX_PATH);
                    int reqSize = 0;
                    StringBuilder destinationInfFileNameComponent = new StringBuilder();
                    bool copied = SetupCopyOEMInf(infFile, null, OemSourceMediaType.SPOST_PATH,
                        OemCopyStyle.SP_COPY_NOOVERWRITE | OemCopyStyle.SP_COPY_FORCE_IN_USE,
                        destFile, MAX_PATH, ref reqSize, destinationInfFileNameComponent);

                    string msgOut = null;
                    if (copied)
                    {
                        msgOut = "Driver installed";
                        EucaLogger.Info(string.Format("INF file successfully copied from {0} to {1}", infFile, destFile.ToString()));
                    }
                    else
                    {
                        int errCode = GetLastError();
                        if (errCode == ERROR_FILE_EXISTS)
                        {
                            EucaLogger.Info(string.Format("Driver {0} already installed in the system", infFile));
                            msgOut = "Driver detected in the system";
                        }
                        else
                        {
                            EucaLogger.Warning(string.Format("Failed to install driver {0}; error code={1}", infFile, errCode));
                            msgOut = string.Format("Driver was not installed (error={0})", errCode);
                        }
                    }

                    using (StreamWriter sw = new StreamWriter(dir + "\\install_result.txt"))
                        sw.WriteLine(msgOut);
                }
                catch (Exception e)
                {
                    EucaLogger.Exception("Couldn't copy a 'INF' file from Eucalyptus' driver folder", e);
                }
            }
        }
    }
}
