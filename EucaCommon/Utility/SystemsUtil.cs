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
//using System.Linq;
using System.Text;
using System.Security.Cryptography;
using System.IO;
using System.Xml.Serialization;
using Microsoft.Win32;
using System.Diagnostics;
using System.Net;

namespace Com.Eucalyptus
{
    public class SystemsUtil
    {
        public static void PutSleep(int sec)
        {
            DateTime start = DateTime.Now;
            do
            {
                System.Threading.Thread.Sleep(10);
            } while ((new TimeSpan(DateTime.Now.Ticks - start.Ticks)).TotalMilliseconds < sec * 1000);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="exe"></param>
        /// <param name="args"></param>
        /// <exception cref="EucaException"/>
        /// <returns></returns>
        public static Win32_CommandResult SpawnProcessAndWait(string exe, string args)
        {
            try
            {
                using (System.Diagnostics.Process proc = SpawnProcess(exe, args, null, null, null,null))
                {
                    string stdout = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit();
                    Win32_CommandResult output = new Win32_CommandResult();
                    output.ExitCode = proc.ExitCode;
                    output.Stdout = stdout;
                    output.Stderr = proc.StandardError.ReadToEnd();

                    return output;
                }
            }
            catch (EucaException e)
            {
                throw e;
            }
            catch (Exception e)
            {
                throw new EucaException("Execution failed: " + e.Message, e);
            }            
        }



        /// <summary>
        /// 
        /// </summary>
        /// <param name="exe"></param>
        /// <param name="args"></param>
        /// <exception cref="EucaException"/>
        /// <returns></returns>
        public static Win32_CommandResult SpawnProcessAndWait(string exe, string args, KeyValuePair<string,string>[] env)
        {
            try
            {
                using (System.Diagnostics.Process proc = SpawnProcess(exe, args, null, env, null, null))
                {
                    string stdout = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit();

                    Win32_CommandResult output = new Win32_CommandResult();
                    output.ExitCode = proc.ExitCode;
                    output.Stdout = stdout;
                    output.Stderr = proc.StandardError.ReadToEnd();
                    return output;
                }
            }
            catch (EucaException e)
            {
                throw e;
            }
            catch (Exception e)
            {
                throw new EucaException("Execution failed: " + e.Message, e);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="exe"></param>
        /// <param name="args"></param>
        /// <exception cref="EucaException"/>
        /// <returns></returns>
        public static Win32_CommandResult SpawnProcessAndWait(string exe, string args, string workingDir, KeyValuePair<string, string>[] env)
        {
            try
            {
                using (System.Diagnostics.Process proc = SpawnProcess(exe, args, workingDir, env, null, null))
                {
                    string stdout = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit();

                    Win32_CommandResult output = new Win32_CommandResult();
                    output.ExitCode = proc.ExitCode;
                    output.Stdout = stdout;
                    output.Stderr = proc.StandardError.ReadToEnd();
                    return output;
                }
            }
            catch (EucaException e)
            {
                throw e;
            }
            catch (Exception e)
            {
                throw new EucaException("Execution failed: " + e.Message, e);
            }
        }
        public static System.Diagnostics.Process SpawnProcess(string exe, string args, string workingDir,
            KeyValuePair<string, string>[] env, DataReceivedEventHandler outputHandler, 
            DataReceivedEventHandler errorHandler)
        {
            try
            {
                System.Diagnostics.Process proc = new System.Diagnostics.Process();
                
                proc.StartInfo.UseShellExecute = false;
                if (workingDir != null)
                    proc.StartInfo.WorkingDirectory = workingDir;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.RedirectStandardOutput = true;                
                proc.StartInfo.FileName = exe;
                proc.StartInfo.Arguments = args;
                if (env != null)
                {                    
                    foreach (KeyValuePair<string, string> kv in env)
                    {
                        if (kv.Key.ToLower() == "path")
                        {
                            try
                            {
                                string path = proc.StartInfo.EnvironmentVariables["Path"];
                                if (path != null)
                                    path += string.Format(";{0}", kv.Value);
                                else
                                    path = kv.Value;
                                proc.StartInfo.EnvironmentVariables["Path"] = path;                               
                            }
                            catch (Exception e)
                            {
                                EucaLogger.Exception(EucaLogger.LOG_WARNING, "couldn't set path variable", e);
                            }
                        }else{
                            proc.StartInfo.EnvironmentVariables.Add(kv.Key, kv.Value);
                        }
                    }
                }
                if (outputHandler != null)
                    proc.OutputDataReceived += outputHandler;
                if (errorHandler != null)
                    proc.ErrorDataReceived += errorHandler;

                proc.Start();
                return proc;                
            }
            catch (Exception e)
            {
                throw new EucaException("Execution failed: " + e.Message, e);
            }
        }
     

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <exception cref="EucaException"/>
        public static void SetRegistryValue(RegistryKey baseKey, string[] path, string valueName, object valueValue, bool recursive=false)
        {
            if (baseKey == null || path == null || path.Length <= 0 || valueName == null || valueValue == null)
                throw new EucaException("some parameter is null");
            try
            {
                RegistryKey regKey = baseKey.OpenSubKey(path[0],true);
                if (recursive && regKey == null)
                {   
                    baseKey.CreateSubKey(path[0]);                    
                    regKey = baseKey.OpenSubKey(path[0], true);
                }

                for (int i = 1; i < path.Length; i++)
                {
                    RegistryKey subKey = regKey.OpenSubKey(path[i], true);
                    if (recursive && subKey == null)
                    {
                        regKey.CreateSubKey(path[i]);
                        subKey = regKey.OpenSubKey(path[i], true);
                    }
                    regKey = subKey;
                }
               
                regKey.SetValue(valueName, valueValue);
                regKey.Flush();
                regKey.Close();
            }
            catch (Exception e)
            {
                throw new EucaException("Regitry update failed: " + e.Message, e);
            }
        }

        public static void DeleteRegistryValue(RegistryKey baseKey, string[] path, string valueName)
        {
            if (baseKey == null || path == null || path.Length <= 0 || valueName == null)
                throw new EucaException("some parameter is null");
        
            try
            {
                RegistryKey regKey = baseKey.OpenSubKey(path[0]);
                for (int i = 1; i < path.Length; i++)
                    regKey = regKey.OpenSubKey(path[i],true);

                regKey.DeleteValue(valueName, false);
                regKey.Flush();
                regKey.Close();
            }
            catch (Exception e)
            {
                throw new EucaException("Regitry delete failed: " + e.Message, e);
            }
        }

        public static object GetRegistryValue(RegistryKey baseKey, string[] path, string valueName)
        {
            if (baseKey == null || path == null || path.Length <= 0 || valueName == null)
                throw new EucaException("some parameter is null");
            
            try
            {
                RegistryKey regKey = baseKey.OpenSubKey(path[0]);
                for (int i = 1; i < path.Length; i++)
                {  
                    regKey = regKey.OpenSubKey(path[i]);                   
                    if (regKey == null)
                        throw new EucaException("Couldn't open subkey " + path[i]);
                }
                
                object val = regKey.GetValue(valueName);

                regKey.Close();
                return val;
            }
            catch (Exception e)
            {
                throw new EucaException("Regitry read failed: " + e.Message, e);
            }
        }

        public static string Encrypt(string input)
        {
            //encrypt AD password properly
            byte[] data = new ASCIIEncoding().GetBytes(input.Trim());
            RijndaelManaged c = new RijndaelManaged();
            byte[] k = Convert.FromBase64String(EucaConstant.dummy);
            byte[] kv = Convert.FromBase64String(EucaConstant.dummyV);
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

        public static string SerializeToString<T>(object obj)
        {
            try
            {
                XmlSerializer xs =
                    new XmlSerializer(typeof(T));
                string strXml = null;
                using (MemoryStream ms = new MemoryStream())
                {
                    xs.Serialize(ms, obj);
                    ms.Seek(0, SeekOrigin.Begin);
                    StreamReader sr = new StreamReader(ms);
                    strXml = sr.ReadToEnd();
                    sr.Close();
                }
                return strXml;
            }
            catch (Exception e)
            {
                throw new EucaException("Serialization threw exception", e);
            }
        }

        public static string HostIPAddress
        {
            get
            {
                string hostname = System.Net.Dns.GetHostName();
                bool activeNetFound = false;
                byte[] bIPAddr = null;
                foreach (System.Net.IPAddress addr in Dns.GetHostEntry(hostname).AddressList)
                {
                    if (addr.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        bIPAddr = addr.GetAddressBytes();
                        if (IsValidIP(bIPAddr))
                        {
                            activeNetFound = true;
                            break;
                        }
                    }
                }
                if (!activeNetFound)
                    throw new EucaException("No active IP address found");

                return ToIPString(bIPAddr);
            }
        }

        public static string ToIPString(byte[] ip)
        {
            if (ip == null || ip.Length != 4)
                return null;

            return string.Format("{0}.{1}.{2}.{3}",
                Convert.ToInt16(ip[0]), Convert.ToInt16(ip[1]), Convert.ToInt16(ip[2]), Convert.ToInt16(ip[3]));
        }

        public static bool IsValidIP(byte[] bIPAddr)
        {
            if (bIPAddr[0] == 0 && bIPAddr[1] == 0 && bIPAddr[2] == 0 && bIPAddr[3] == 0)
                return false;

            if (bIPAddr[0] == 127 && bIPAddr[1] == 0 && bIPAddr[2] == 0 && bIPAddr[3] == 1)
                return false;

            if (bIPAddr[0] == 169 && bIPAddr[1] == 254) // automatic private IP address
                return false;

            return true;
        }
    }

    public struct Win32_CommandResult
    {
        public int ExitCode;
        public string Stdout;
        public string Stderr;
    }
}
