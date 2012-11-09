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
using System.Text;
using Microsoft.Win32;
using System.Management;
namespace Com.Eucalyptus.Windows
{   
    class Program
    {
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
            catch (Exception e)
            {                
                return null;
            }
        }
        public static int SpawnProcessAndWait(string exe, string args)
        {
            using (System.Diagnostics.Process proc = new System.Diagnostics.Process())
            {
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.FileName = exe;
                proc.StartInfo.Arguments = args;              
                proc.Start();
                proc.WaitForExit();
                return proc.ExitCode;
            }
        }

        static void Main(string[] args)
        {
            object tmp =  GetRegistryValue("InstallLocation");
            if (tmp == null)
                return;
            string programRoot = (string)tmp;

            string exe = string.Format("{0}\\EucaConfiguration.exe", programRoot);
            SpawnProcessAndWait(exe, null);
        }
    }
}
