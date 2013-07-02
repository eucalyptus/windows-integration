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
using System.Collections;
using System.Collections.Generic;
using System.Configuration.Install;
using System.ComponentModel;
using System.ServiceProcess;
using System.Text;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Management;
using System.IO;
using Microsoft.Win32;
using ICSharpCode.SharpZipLib.Zip;
using ICSharpCode.SharpZipLib.Zip.Compression.Streams;

namespace Com.Eucalyptus.Windows.EucaWindowsService
{
    [RunInstaller(true)]
    public class EucaWindowsServiceInstaller : Installer
    { 
        private ServiceInstaller eucaSvcInstaller;
        private ServiceProcessInstaller eucaProcInstaller;
        private const string ServiceUserName = "EucaService";

        public EucaWindowsServiceInstaller()
        {
            eucaProcInstaller = new ServiceProcessInstaller();
            eucaSvcInstaller = new ServiceInstaller();

            eucaSvcInstaller.StartType = ServiceStartMode.Automatic;
            eucaSvcInstaller.ServiceName = "Eucalyptus Windows Service";
            eucaSvcInstaller.DisplayName = "Eucalyptus Windows Service";
            eucaSvcInstaller.Description = "Eucalyptus Windows Service";
            
            Installers.Add(eucaSvcInstaller);
            Installers.Add(eucaProcInstaller);        
        }

        /// <summary>
        /// two privileges are required to run the service: change admin's password and reboot
        /// by default, the local system can set the admin's password. It also has the privilege to reboot,
        /// but it's not enabled by default. So here the code includes the win32-way to "enable" that privilege.
        /// </summary>
        /// <param name="savedState"></param>
        protected override void OnBeforeInstall(System.Collections.IDictionary savedState)
        {
            eucaProcInstaller.Account = ServiceAccount.LocalSystem;
   
           // eucaProcInstaller.Account = ServiceAccount.LocalSystem;
            base.OnBeforeInstall(savedState);
        }

        private string _installLocation = null;
        private void Log(string msg)
        {
            string path = (_installLocation != null) ? _installLocation+"\\eucalog_install.txt" : "C:\\eucalog_install.txt";

            using (System.IO.StreamWriter sw = new System.IO.StreamWriter(path, true))
            {
                sw.WriteLine(msg);
            }
        }

        protected override void OnAfterInstall(IDictionary savedState)
        {
            base.OnAfterInstall(savedState);
            try
            {
                RegistryKey regKey =
                    Registry.LocalMachine.OpenSubKey("SOFTWARE").OpenSubKey("Eucalyptus Systems").
                    OpenSubKey("Eucalyptus", true);
                if (regKey == null)
                {
                    regKey = Registry.CurrentUser.OpenSubKey("SOFTWARE").OpenSubKey("Eucalyptus Systems").
                    OpenSubKey("Eucalyptus", true);
                }
                object objLocation = regKey.GetValue("InstallLocation");
                regKey.Close();

                if (objLocation == null)
                {
                    string msg = "Can't retrieve Eucalyptus' location from registry";
                    Log(msg);
                    throw new Exception(msg); /// will halt the installation
                }
                _installLocation = (string)objLocation;
                string exe = _installLocation + "\\" + "PostInstallation.exe";
                
                if (!System.IO.File.Exists(exe))
                {
                    string msg = string.Format("Can't find the 'PostInstallation.exe' in {0}", _installLocation);
                    Log(msg);
                    throw new Exception(msg); // will halt the installation
                }

                string xenPVFile = _installLocation + "\\xenpv.zip";
                string virtioFile = _installLocation + "\\virtio.zip";
                string vmwareFile = _installLocation + "\\vmware";
                string hypervFile = _installLocation + "\\hyperv";

                if (!(File.Exists(xenPVFile) || File.Exists(virtioFile) || File.Exists(vmwareFile) || File.Exists(hypervFile)))
                    throw new InstallException("No hypervisor is chosen!");                            
                int retCode = 0;
                try
                {
                    retCode = LaunchEucaPostInstaller(exe, "--registry");
                    if (retCode != 0)
                        Log(string.Format("[FAILURE]'PostInstaller.exe --registry' returned error code ({0})", retCode));
                }
                catch (Exception e)
                {
                    Log(string.Format("[FAILURE] 'PostInstllation.exe --registry generated exception ({0})", e.Message));
                }

                try
                {
                    retCode = LaunchEucaPostInstaller(exe, "--envconf");
                    if (retCode != 0)
                        Log(string.Format("[FAILURE]'PostInstaller.exe --envconf' returned error code ({0})", retCode));
                }
                catch (Exception e)
                {
                    Log(string.Format("[FAILURE] 'PostInstllation.exe --envconf generated exception ({0})", e.Message));
                }

                try
                {
                    retCode = LaunchEucaPostInstaller(exe, "--norecovery");
                    if (retCode != 0)
                        Log(string.Format("[FAILURE] 'PostInstaller.exe --norecovery' returned error code ({0})", retCode));
                }
                catch (Exception e)
                {
                    Log(string.Format("[FAILURE] 'PostInstllation.exe --norecovery generated exception ({0})", e.Message));
                }

                string xenPVDir = _installLocation + "\\xenpv";                
                try
                {
                     if (File.Exists(xenPVFile))
                    {
                        if (unzip(_installLocation, xenPVFile))
                        {
                            ;
                        }
                        else
                            Log(string.Format("[FAILURE] Couldn't install xenpv; unzip failed for file: {0}", xenPVFile));
                    }
                    else // meaning that xenpv is not checked during the installation
                        ;
                }
                catch (Exception e)
                {
                    Log(e.Message);
                    Log(e.StackTrace);
                    throw e;    /// this will termnate installation abnormally
                }
                finally
                {
                    try
                    {
                        if(File.Exists(xenPVFile))
                            File.Delete(xenPVFile);                        
                    }
                    catch (Exception) { }
                }

                string virtioDir = _installLocation +"\\virtio";
                try
                {
                    if (File.Exists(virtioFile))
                    {
                        if (unzip(_installLocation, virtioFile))
                        {
                            string[] paths = Directory.GetDirectories(virtioDir);
                            if (paths != null && paths.Length > 0)
                            {
                                if (paths.Length > 1)
                                    Log(string.Format("[WARNING] Multiple virtio drivers are found; we're using {0}", paths[0]));
                                retCode = LaunchEucaPostInstaller(exe, string.Format("--virtio \"{0}\"", paths[0]));
                                if (retCode != 0)
                                {
                                    Log(string.Format("[FAILURE] 'PostInstallation.exe --virtio returned error code ({0})", retCode));
                                    throw new Exception("Could not complete KVM VirtIO installation; Please see Eucalyptus documentation");
                                }
                                
                            }
                            else
                                Log(string.Format("[FAILURE] Virtio drivers are not found in {0}", virtioDir));
                        }
                        else
                            Log(string.Format("[FAILURE] Virtio installation failed; could not unzip file: {0}", virtioFile));
                    }
                    else
                        ; // means that virtio option is not checked during the installation
                }
                catch (Exception e)
                {
                    throw e;  /// this will termnate installation abnormally
                }
                finally
                {
                    try
                    {
                        if(File.Exists(virtioFile))
                            File.Delete(virtioFile);
                    }
                    catch (Exception) { }
                }

            }
            catch (Exception e)
            {
                Log("Eucalyptus installation has failed.");
                Log(e.Message);
                Log(e.StackTrace);
                throw e;
            }
        }
        
        private bool unzip(string baseDir, string filepath)
        {
            string origDir = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(baseDir);
            using (ZipInputStream s = new ZipInputStream(File.OpenRead(filepath)))
            {
                ZipEntry theEntry;
                while ((theEntry = s.GetNextEntry()) != null)
                {
                    //Console.WriteLine(theEntry.Name);			
                    string directoryName = Path.GetDirectoryName(theEntry.Name);
                    string fileName = Path.GetFileName(theEntry.Name);

                    // create directory
                    if (directoryName.Length > 0)
                    {
                        Directory.CreateDirectory(directoryName);
                    }

                    if (fileName != String.Empty)
                    {
                        using (FileStream streamWriter = File.Create(theEntry.Name))
                        {

                            int size = 2048;
                            byte[] data = new byte[2048];
                            while (true)
                            {
                                size = s.Read(data, 0, data.Length);
                                if (size > 0)
                                {
                                    streamWriter.Write(data, 0, size);
                                }
                                else
                                {
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            Directory.SetCurrentDirectory(origDir);
            return true;
        }

        private int LaunchEucaPostInstaller(string exe, string arg)
        {
            using (System.Diagnostics.Process proc = new System.Diagnostics.Process())
            {
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.FileName = exe;
                proc.StartInfo.Arguments = arg;

                proc.Start();
                proc.WaitForExit();
                return proc.ExitCode;
            }
        }

        protected override void OnBeforeUninstall(IDictionary savedState)
        {
            try
            {
                RegistryKey regKey =
                    Registry.LocalMachine.OpenSubKey("SOFTWARE").OpenSubKey("Eucalyptus Systems").
                    OpenSubKey("Eucalyptus", true);
                object objLocation = regKey.GetValue("InstallLocation");
                regKey.Close();

                if (objLocation == null)
                {
                    Log("Can't retrieve Eucalyptus' location from registry");
                    return;
                }
                _installLocation = (string)objLocation;
                string exe = _installLocation + "\\" + "PostInstallation.exe";

                int retCode = LaunchEucaPostInstaller(exe, "--recovery");
                if (retCode != 0)
                    Log(string.Format("[FAILURE] 'PostInstaller.exe --recovery' returned error code ({0})", retCode));       

                retCode = LaunchEucaPostInstaller(exe, string.Format("--cleanup \"{0}\"", _installLocation));
                if (retCode != 0)
                    Log(string.Format("[FAILURE] 'PostInstaller.exe --cleanup' returned error code ({0})", retCode));
      
            }
            catch (Exception e)
            {
                Log(string.Format("[FAILURE] 'PostInstllation.exe --recovery generated exception ({0})", e.Message));
            }

            base.OnBeforeUninstall(savedState);
        }

        protected override void OnBeforeRollback(IDictionary savedState)
        {
            try
            {
                RegistryKey regKey =
                    Registry.LocalMachine.OpenSubKey("SOFTWARE").OpenSubKey("Eucalyptus Systems").
                    OpenSubKey("Eucalyptus", true);
                object objLocation = regKey.GetValue("InstallLocation");
                regKey.Close();

                if (objLocation == null)
                {
                    Log("Can't retrieve Eucalyptus' location from registry");
                    return;
                }
                _installLocation = (string)objLocation;
                string exe = _installLocation + "\\" + "PostInstallation.exe";

                int retCode = LaunchEucaPostInstaller(exe, "--recovery");
                if (retCode != 0)
                    Log(string.Format("[FAILURE] 'PostInstaller.exe --recovery' returned error code ({0})", retCode));
                
                retCode = LaunchEucaPostInstaller(exe, string.Format("--cleanup \"{0}\"", _installLocation));
                if (retCode != 0)
                    Log(string.Format("[FAILURE] 'PostInstaller.exe --cleanup' returned error code ({0})", retCode));

            }
            catch (Exception e)
            {
                Log(string.Format("[FAILURE] 'PostInstllation.exe --recovery generated exception ({0})", e.Message));
            }

            base.OnBeforeRollback(savedState);
        }

        protected override void OnAfterRollback(System.Collections.IDictionary savedState)
        {
            base.OnAfterRollback(savedState);
        }

        protected override void OnAfterUninstall(System.Collections.IDictionary savedState)
        {
            // delete EucaService account
            base.OnAfterUninstall(savedState);
        }

        private void DeleteEucalyptusAccount()
        {
            using (System.Diagnostics.Process proc = new System.Diagnostics.Process())
            {
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.FileName = "C:\\Windows\\System32\\net.exe";
                proc.StartInfo.Arguments = string.Format("user {0} /delete", ServiceUserName);

                proc.Start();
                proc.WaitForExit();

                if (!(proc.ExitCode == 1 || proc.ExitCode == 0))
                {
                    throw new Exception(string.Format("delete Eucalyptus account - net command failed with exit code = {0}, stdout= {1}, stderr={2}",
                        proc.ExitCode, proc.StandardOutput.ReadToEnd(), proc.StandardError.ReadToEnd()));
                }
            }
        }

        private bool ExistAccount()
        {
            try
            {
                ManagementObjectCollection objCol = QueryLocalWMICollection("Select * from Win32_useraccount");
                foreach (ManagementObject obj in objCol)
                {
                    if ((string)obj["Name"] == ServiceUserName)
                    {
                        return true;
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }

            return false;
        }

        private string CreateEucalyptusAccount()
        {
            string guid = Guid.NewGuid().ToString();
            string password = string.Format("{0}{1}$",
                "euca", guid.Substring(guid.LastIndexOf("-") + 1));            
            
            if (ExistAccount()) // we could have changed the password of this account, but that may cause issues if people have used the account
            {
                LogFailure("The Eucalyptus account is found in the system. Delete the account and install the service again");
                return null;
            }

            using (System.Diagnostics.Process proc = new System.Diagnostics.Process())
            {
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.FileName = "C:\\Windows\\System32\\net.exe";

                string option = "/add /comment:\"Account to run Eucalyptus service\" /expires:never /fullname:\"Eucalyptus\" /passwordchg:no /times:all /active:yes";
                proc.StartInfo.Arguments = string.Format("user {0} {1} {2}", ServiceUserName, password, option);
                proc.Start();
                proc.WaitForExit();

                if (!(proc.ExitCode == 1 || proc.ExitCode == 0))
                {
                    throw new Exception(string.Format("adding Eucalyptus account - net command failed with exit code = {0}, stdout= {1}, stderr={2}", 
                        proc.ExitCode, proc.StandardOutput.ReadToEnd(), proc.StandardError.ReadToEnd()));
                }

           //     LogFailure(string.Format("password: {0}", password));

                // set password never expires
                
            }

            return password;
        }


        private void AddEucalyptusToAdministrators()
        {
            using (System.Diagnostics.Process proc = new System.Diagnostics.Process())
            {
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.FileName = "C:\\Windows\\System32\\net.exe";

                proc.StartInfo.Arguments = string.Format("localgroup Administrators {0} /add", ServiceUserName);
                proc.Start();
                proc.WaitForExit();

                if (!(proc.ExitCode == 1 || proc.ExitCode == 0))
                {
                    throw new Exception(string.Format("adding Eucalyptus account - net command failed with exit code = {0}, stdout= {1}, stderr={2}",
                        proc.ExitCode, proc.StandardOutput.ReadToEnd(), proc.StandardError.ReadToEnd()));                    
                }

                try
                {
                    /// it appears this doesn't update correctly.
                    ManagementObjectCollection objCol = QueryLocalWMICollection("Select * from Win32_useraccount");
                    foreach (ManagementObject obj in objCol)
                    {
                        if ((string)obj["Name"] == ServiceUserName)
                        {
                            obj.SetPropertyValue("PasswordExpires", 0);
                        }
                    }
                }
                catch (Exception e)
                {
                    LogFailure(string.Format("Couldn't set PasswordExpires=0 ({0})",e.Message));
                }
            }

           // LogFailure("Eucalyptus user is added to admin group");
        }

        internal static ManagementObjectCollection QueryLocalWMICollection(string query)
        {
            ManagementScope ms = new ManagementScope(@"\\.\root\cimv2", new ConnectionOptions());               
            ms.Connect();

            if (ms == null || !ms.IsConnected)
                throw new Exception("Cannot establish connection to the management provider");

            ManagementObjectSearcher searcher = new ManagementObjectSearcher(ms, new ObjectQuery(query));
            return searcher.Get();
        }

        internal static void LogFailure(string msg)
        {
            try
            {
                using (System.IO.StreamWriter sw = new System.IO.StreamWriter("C:\\euca_install_log.txt", true))
                {
                    sw.WriteLine(msg);                   
                }
            }
            catch (Exception)
            {
                ;
            }
        }
    }

    
    public class Program
    {
        static void Main() 
        {   
                ServiceBase[] servicesToRun
                    = new ServiceBase[] { new EucaWindowsService() };
                ServiceBase.Run(servicesToRun);
        }      
    }
}
