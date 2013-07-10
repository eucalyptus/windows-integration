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
using Microsoft.Win32;
using System.Management;
using System.IO;
using System.Security.Cryptography;

namespace Com.Eucalyptus.Windows
{
    /*
    public class OSEnvironment
    {
        public enum Enum_OsName { XP, Vista, Win7, S2003, S2003R2, S2008, S2008R2, NOTYETDETERMINED, UNKNOWN }

        private static Enum_OsName _osName = Enum_OsName.NOTYETDETERMINED;
        public static Enum_OsName OS_Name
        {
            get
            {
                if (_osName == Enum_OsName.NOTYETDETERMINED)
                {
                    try
                    {
                        string osName = "";
                        using (ManagementObject manObj =
                           WMIUtil.QueryLocalWMI(@"\\.\root\CIMV2", "Select * from win32_operatingsystem"))
                        {
                            osName = (string)manObj["Name"];
                        }
                        if (osName.Contains("XP"))
                        {
                            _osName = Enum_OsName.XP;
                        }
                        else if (osName.Contains("2003 R2"))
                        {
                            _osName = Enum_OsName.S2003R2;
                        }
                        else if (osName.Contains("2003"))
                        {
                            _osName = Enum_OsName.S2003;
                        }
                        else if (osName.Contains("Windows 7") | osName.Contains("Windowsr 7"))
                        {
                            _osName = Enum_OsName.Win7;
                        }
                        else if (osName.Contains("2008 R2"))
                        {
                            _osName = Enum_OsName.S2008R2;
                        }
                        else if (osName.Contains("2008"))
                        {
                            _osName = Enum_OsName.S2008;
                        }
                        else if (osName.Contains("Vista"))
                        {
                            _osName = Enum_OsName.Vista;
                        }
                        else
                            _osName = Enum_OsName.UNKNOWN;
                    }
                    catch (Exception e)
                    {
                        _osName = OSEnvironment.Enum_OsName.UNKNOWN;
                        EucaLogger.Exception("Can't figure out the OS name and version", e);
                    }
                }
                return _osName;
            }

        }

        /// <summary>
        ///  the installed service pack
        /// </summary>
        private static string _osServicePack = null;
        private static bool _osServicePackQueried = false;
        public static string OS_ServicePack
        {
            get
            {
                if (!_osServicePackQueried)
                {
                    using (ManagementObject objOs = WMIUtil.QueryLocalWMI(@"\\.\root\CIMV2", "Select * from win32_operatingsystem"))
                    {
                        try
                        {
                            _osServicePack = (string)objOs["CSDVersion"];
                        }
                        catch (Exception)
                        {
                            EucaLogger.Warning("Service pack couldn't be detected");
                            _osServicePack = null;
                        }
                        finally
                        {
                            _osServicePackQueried = true;
                        }
                    }
                }
                return _osServicePack;
            }
        }

        public static bool ServicePackEqualHigherThan(int spNum)
        {
            try
            {
                string strSP = OS_ServicePack;
                int nSP = int.Parse(strSP.Replace("Service Pack", "").Trim());
                return spNum <= nSP;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static bool _bitSizeQueried = false;
        private static bool _is64bit = false;
        public static bool Is64bit
        {
            get
            {
                if (!_bitSizeQueried)
                {
                    try
                    {
                        using (ManagementObject objOs = WMIUtil.QueryLocalWMI(@"\\.\root\CIMV2", "Select * from win32_computersystem"))
                        {
                            string systype = (string)objOs["SystemType"];
                            if (systype.Contains("64"))
                                _is64bit = true;
                            else
                                _is64bit = false;
                        }
                    }
                    catch (Exception)
                    {
                        EucaLogger.Warning("System type couldn't be detected");
                        _is64bit = false;
                    }
                    finally
                    {
                        _bitSizeQueried = true;
                    }
                }
                return _is64bit;
            }
        }
    }
    */
    
    public class EucaServiceLibraryUtil
    {
      /*  public static void PutSleep(int sec)
        {
            DateTime start = DateTime.Now;
            do
            {
                System.Threading.Thread.Sleep(10);
            } while ((new TimeSpan(DateTime.Now.Ticks - start.Ticks)).TotalMilliseconds < sec * 1000);
        }

       public static Win32_CommandResult SpawnProcessAndWait(string exe, string args)
        {
            using (System.Diagnostics.Process proc = new System.Diagnostics.Process())
            {
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.FileName = exe;
                proc.StartInfo.Arguments = args;
                proc.Start();
                //proc.WaitForExit();
                string stdout = proc.StandardOutput.ReadToEnd();
                proc.WaitForExit();

                Win32_CommandResult output = new Win32_CommandResult();
                output.ExitCode = proc.ExitCode;
                output.Stdout = stdout;
                output.Stderr = proc.StandardError.ReadToEnd();

                return output;
            }
        } */

        public static void SetSvcRegistryValue(string key, object value)
        {
            if (key == null || value == null)
                return;
            try
            {
                Com.Eucalyptus.SystemsUtil.SetRegistryValue(Registry.LocalMachine,
                    new string[] { "SOFTWARE", "Eucalyptus Systems", "Eucalyptus" }, key, value, false);
            }
            catch (Exception e)
            {
                EucaLogger.Exception("Could not set registry value", e);
            }
        }

        public static void DeleteSvcRegistryValue(string key)
        {
            if (key == null)
                return;
            try
            {
                Com.Eucalyptus.SystemsUtil.DeleteRegistryValue(Registry.LocalMachine,
                    new string[] { "SOFTWARE", "Eucalyptus Systems", "Eucalyptus" }, key);
            }
            catch (Exception e)
            {
                EucaLogger.Exception("Could not delete registry value", e);
                return;
            }
        }

        public static object GetSvcRegistryValue(string key)
        {
            if (key == null)
                return null;
            try
            {
                return Com.Eucalyptus.SystemsUtil.GetRegistryValue(Registry.LocalMachine,
                    new string[] { "SOFTWARE", "Eucalyptus Systems", "Eucalyptus" }, key);
            }
            catch (Exception e)
            {
                EucaLogger.Exception("Could not retrieve registry value", e);
                return null;
            }
        }

        public static string Encrypt(string input)
        {
            //encrypt AD password properly
            byte[] data = new ASCIIEncoding().GetBytes(input.Trim());
            RijndaelManaged c = new RijndaelManaged();
            byte[] k = Convert.FromBase64String(EucaConstant.dummy);
            byte[] kv =Convert.FromBase64String(EucaConstant.dummyV);
            ICryptoTransform enc = c.CreateEncryptor(k, kv);
            MemoryStream ms = new MemoryStream();
            CryptoStream cs = new CryptoStream(ms, enc, CryptoStreamMode.Write);
            cs.Write(data, 0, data.Length);
            cs.FlushFinalBlock();
            cs.Close();

            string output = Convert.ToBase64String(ms.ToArray());
            ms.Close();
            return output;
        }

        public static string Decrypt(string input)
        {
            byte[] c = Convert.FromBase64String(input);
            RijndaelManaged crypt = new RijndaelManaged();
            byte[] k = Convert.FromBase64String(EucaConstant.dummy);
            byte[] kv = Convert.FromBase64String(EucaConstant.dummyV);
            ICryptoTransform enc = crypt.CreateDecryptor(k, kv);
            MemoryStream ms = new MemoryStream(c);
            CryptoStream cs = new CryptoStream(ms, enc, CryptoStreamMode.Read);

            byte[] data = new byte[c.Length];
            int dataLength = cs.Read(data, 0, data.Length);

            string output = new ASCIIEncoding().GetString(data, 0, dataLength);
            cs.Close();
            ms.Close();
            return output;
        }

    }

    /*public struct Win32_CommandResult
    {
        public int ExitCode;
        public string Stdout;
        public string Stderr;
    }*/
    /*
    public class WMIUtil
    {
        public static ManagementObject QueryLocalWMI(string query)
        {
            return QueryLocalWMI(null, query);
        }

        public static ManagementObject QueryLocalWMI(string scope, string query)
        {
            ManagementObjectCollection col = QueryLocalWMICollection(scope, query);
            ManagementObject retObj = null;
            foreach (ManagementObject obj in col)
                retObj = obj;

            col.Dispose();            
            return retObj;
        }

        public static ManagementObjectCollection QueryLocalWMICollection(string query)
        {
            return QueryLocalWMICollection(null, query);
        }

        private const int RETRY = 60;
        private const int PAUSE_SEC_BETWEEN_RETRY = 1;
        public static ManagementObjectCollection QueryLocalWMICollection(string scope, string query)
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
                    EucaLogger.Warning("WMI service is not responding; will retry");
                    System.Threading.Thread.Sleep(PAUSE_SEC_BETWEEN_RETRY * 1000);
                    continue;
                }
            } while (numTrial++ < RETRY);

            if (!connected || ms == null || !ms.IsConnected)
                throw new EucaException("Cannot establish connection to the management provider");

            ManagementObjectSearcher searcher = new ManagementObjectSearcher(ms, new ObjectQuery(query));
            ManagementObjectCollection result = searcher.Get();
            searcher.Dispose();
            return result;
        }
    }*/
}
