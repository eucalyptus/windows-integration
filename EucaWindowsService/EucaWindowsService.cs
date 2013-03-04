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
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.ServiceProcess;
using System.Text;
using System.IO;
using System.Reflection;
using Microsoft.Win32;

namespace Com.Eucalyptus.Windows.EucaWindowsService
{
    /// <summary>
    /// This is a "shim" service for Eucalyptus windows service.
    /// Because this service couldn't be updated once win. instances are deployed,
    /// we make sure this is bug-free.
    /// </summary>
    public partial class EucaWindowsService : ServiceBase
    {
        public EucaWindowsService()
        {
            InitializeComponent();
            base.ServiceName = "Eucalyptus Windows Service";
        }

        private const string EucaDrive = @"A:\";
        private const string EucaConfigFile = EucaDrive + "configuration.xml";
        private const string EucaLegacyBatFile = EucaDrive + "EUCA.BAT";
        private const string EucaLegacyBatFileSmallCapital = EucaDrive + "euca.bat";
        private const string EucaDllImportLocation = EucaDrive + "EucaService.dll";

        private static string EucaProgramRoot
        {
            get
            {// find the default installation location from registry
               string programRoot = null;
                try
                {
                    RegistryKey regKey = 
                        Registry.LocalMachine.OpenSubKey("SOFTWARE").OpenSubKey("Eucalyptus Systems")
                        .OpenSubKey("Eucalyptus");
                    programRoot = (string)regKey.GetValue("InstallLocation");
                    regKey.Close();               
                    return programRoot;
                }
                catch (Exception e)
                {
                    return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles) + "\\Eucalyptus";
                }
            }
        }
        private static string LogFile = EucaProgramRoot + "\\eucalog_service.txt";
        private static string EucaDllLocation = EucaProgramRoot+"\\EucaService.dll";

        protected override void OnStart(string[] args)
        {
            /// make sure EucaProgramRoot exists, if not create it
            /// 
            if (!Directory.Exists(EucaProgramRoot))
                Directory.CreateDirectory(EucaProgramRoot);

            Log("Starting Euca Windows Service");
            /// find configuration.xml under A:\\
            /// 
      
            const int CONFIG_LOOKUP_TIMEOUT_SEC = 90; // it takes very long for the floppy drive discoverable..so don't change this! 
            base.RequestAdditionalTime(CONFIG_LOOKUP_TIMEOUT_SEC * 1000);
            DateTime configLookupStartTime = DateTime.Now;
          
        LB_CONFIG:    
            bool configFileFound = false;
            string batFileLoc = null;
            if (File.Exists(EucaConfigFile))
                configFileFound = true;
            else if (File.Exists(EucaLegacyBatFile) || File.Exists(EucaLegacyBatFileSmallCapital))
                ;
            else
            {
                if ((new TimeSpan(DateTime.Now.Ticks - configLookupStartTime.Ticks)).TotalSeconds < CONFIG_LOOKUP_TIMEOUT_SEC)
                {
                    System.Threading.Thread.Sleep(1000);
                    goto LB_CONFIG;
                }
            }

            if (File.Exists(EucaLegacyBatFile))
                batFileLoc = EucaLegacyBatFile;
            else if (File.Exists(EucaLegacyBatFileSmallCapital))
                batFileLoc = EucaLegacyBatFileSmallCapital;

            if (configFileFound && batFileLoc!=null)
            {
                try
                {
                    File.Delete(batFileLoc);
                    batFileLoc = null;
                    Log("euca.bat deleted");
                }
                catch (Exception) {
                    Log("Couldn't delete euca.bat");
                }
            }

            if (!configFileFound && batFileLoc == null)
            {
                Log("CRITICAL: Cannot find A:configuration.xml files, nothing to do! ");
                return;
            }

            if (!configFileFound && batFileLoc != null)
            {
                using (StreamReader sr = new StreamReader(File.OpenRead(batFileLoc)))
                {
                    string s = sr.ReadToEnd();
                    if (s.Contains("MAGICEUCALYPTUSPASSWORDPLACEHOLDER")) // this means the EUCA.BAT file was not generated by NC
                    {
                        Log("The instance is not created by Eucalyptus-NC; Service can't proceed");
                        return;
                    }
                    sr.Close();
                }  
                
                try
                {
                    CreateConfigFromBatFile(batFileLoc, EucaConfigFile);
                    configFileFound = true;
                }
                catch (Exception e)
                {
                    Log(string.Format("CRITICAL: Could not create config file from EUCA.BAT ({0})", e.Message));
                    return;
                }            
            }

            try
            {
                using (StreamReader sr = new StreamReader(File.OpenRead(EucaConfigFile)))
                {
                    string s = sr.ReadToEnd();
                    if (s.Contains("MAGICEUCALYPTUSPASSWORDPLACEHOLDER")) // this means the EUCA.BAT file was not generated by NC
                    {
                        Log("The instance is not created by Eucalyptus-NC; Service can't proceed");
                        return;
                    }
                    sr.Close();
                }
            }
            catch (Exception)
            {
                Log(string.Format("CRITICAL: Could not open config file ({0})", EucaConfigFile));
                return;
            }

            if (!configFileFound) {
                Log("CRITICAL: configuration file is not found, nothing to do.");
                return;
            }
            
            /// if EucaService.dll is found under A:\\, check its version and if newer, copy it to EucaProgramRoot
            /// 
            if (!File.Exists(EucaDllLocation))
            {
                if (!File.Exists(EucaDllImportLocation))
                    Log("CRITICAL - no Euca service DLL is found");
                else
                    ImportEucaDll(); // if no DLL is found in program root, copy it always
            }else if(File.Exists(EucaDllImportLocation))
            {
                try
                {
                    AssemblyName newDLL = AssemblyName.GetAssemblyName(EucaDllImportLocation);
                    AssemblyName oldDLL = AssemblyName.GetAssemblyName(EucaDllLocation);

                    if (newDLL.Version > oldDLL.Version)
                        ImportEucaDll();
                    else
                        Log("WARNING: DLL is found on import path, but it's version is not newer than the current version");
                }
                catch (Exception e)
                {
                    Log("Cannot import the DLL file to a program directory");
                    Log(e.Message);
                    Log(e.StackTrace);
                }
            }
            
            /// invoke Bootstrap method of ROOT\EucaService.dll
            /// 
            try
            {
                Assembly eucaDll = Assembly.LoadFrom(EucaDllLocation);
                Type tEucaBoot = eucaDll.GetType("Com.Eucalyptus.Windows.EucaServiceLibrary.Bootstrapper");
                object bootObj = Activator.CreateInstance(tEucaBoot);
                MethodInfo mEuca = tEucaBoot.GetMethod("DoBootstrap");
                Log("Calling DoBootstrap method");
                if (configFileFound)
                    mEuca.Invoke(bootObj, new object[] { EucaConfigFile });
                else
                    mEuca.Invoke(bootObj, new object[] { null });
            }
            catch (Exception e)
            {
                string errorMsg = string.Format("CRITICAL: Error from the bootstrapping process \n -- {0}\n   --{1}", e.Message, e.StackTrace);
                Log(errorMsg);
            }
        }

        private void CreateConfigFromBatFile(string batFile, string destConfig)
        {
            if (!File.Exists(batFile))
                throw new Exception(string.Format("Could not find BAT file ({0})"));

            string username = null;
            string passwd = null;
            using (StreamReader s = new StreamReader(batFile))
            {
                string cmd = s.ReadToEnd();
                cmd = cmd.Replace("net user", "").Trim();

                string[] tmp = cmd.Split(new char[] { ' ' });

                username = tmp[0];
                passwd = tmp[1];        
                s.Close();
            }
            
            if (username == null || passwd == null)
                throw new Exception("Username and password doesn't exist in BAT file");
            username = username.Trim();
            passwd = passwd.Trim();

            StringBuilder sbXml = new StringBuilder();
            sbXml.AppendLine("<?xml version=\"1.0\" encoding=\"utf-8\" ?>");
            sbXml.AppendLine("<Configuration xmlns=\"http://eucalyptus.com\" version=\"0.1\">");
            sbXml.AppendLine(string.Format("<LocalAccount> <Username>{0}</Username><Password>{1}</Password></LocalAccount>",
                username, passwd));
            sbXml.AppendLine("</Configuration>");

            using (StreamWriter s = new StreamWriter(destConfig))
            {
                s.Write(sbXml.ToString());
                s.Flush();
                s.Close();
            }

            try
            {
                File.Delete(batFile);
            }
            catch (Exception)
            {
                ;
            }
        }

        private void ImportEucaDll()
        {
            try
            {
                if (File.Exists(EucaDllImportLocation))
                    File.Copy(EucaDllImportLocation, EucaDllLocation, true);
                else
                    Log("Source DLL to be imported is not found");
            }
            catch (Exception e)
            {
                Log("Import EucaDLL failed!");
                Log(e.Message);
                Log(e.StackTrace);
            }
        }

        private const int LOG_FILE_SIZE = 0x01 << 20;
        private void Log(string msg)
        {
            try
            {
                FileInfo logFileInfo = new FileInfo(LogFile);
                if (logFileInfo.Length > LOG_FILE_SIZE)
                {
                    string backup = LogFile + ".1";
                    File.Copy(LogFile, backup, true);
                    File.Delete(LogFile);
                }
            }
            catch (Exception)
            {
                ;
            }

            using (StreamWriter sw = new StreamWriter(LogFile, true))
            {
                sw.WriteLine(string.Format("{0} at {1}", msg, DateTime.Now.ToString()));
                sw.Flush();
                sw.Close();
            }
        }

        protected override void OnStop()
        {
            Log("Eucalyptus Windows Service stopped");
        }
    }
}
