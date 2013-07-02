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
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace Com.Eucalyptus.Windows
{
    class Program
    {
        static string _installLocation = null; 

        static void Main(string[] args)
        {
            _installLocation = EucaConstant.ProgramRoot;
            if (_installLocation == null)
            {
                Log("[FAILURE] Can't find the Eucalyptus install location while installing XenPV installer");
                return;
            }         

            string xenPVDir = string.Format("{0}\\xenpv", _installLocation);
            if (Directory.Exists(xenPVDir))
            {
                OSEnvironment.Enum_OsName osName = OSEnvironment.OS_Name;
                if (osName == OSEnvironment.Enum_OsName.Vista || osName == OSEnvironment.Enum_OsName.Win7 ||
                    osName == OSEnvironment.Enum_OsName.S2008 || osName == OSEnvironment.Enum_OsName.S2008R2)
                {
                    string msg = "XenPV installation may fail due to driver signing requirements. Please read admin manual if you encounter the problem.";
                   // Log("OS is Vista or newer; warning message displayed");
                   // MessageBox.Show(msg, "WARNING");
                }
                LaunchEucaPostInstaller();
            }
        }
                
        private static void LaunchEucaPostInstaller()
        {
            string exe = string.Format("{0}\\PostInstallation.exe", _installLocation);
            string xenPVDir = string.Format("{0}\\xenpv", _installLocation);            
            string arg = string.Format("--xenpv \"{0}\"", xenPVDir);
            using (System.Diagnostics.Process proc = new System.Diagnostics.Process())
            {
                proc.StartInfo.UseShellExecute = false;
                proc.StartInfo.RedirectStandardError = true;
                proc.StartInfo.RedirectStandardOutput = true;
                proc.StartInfo.FileName = exe;
                proc.StartInfo.Arguments = arg;

                Log("Starting XenPV driver installation");
                proc.Start();
            }
        }

        private static void Log(string msg)
        {
            string path = (_installLocation != null) ? _installLocation + "\\eucalog_install.txt" : "C:\\eucalog_install.txt";

            using (System.IO.StreamWriter sw = new System.IO.StreamWriter(path, true))
            {
                sw.WriteLine(msg);
            }
        }
    }
}
