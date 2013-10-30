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
using System.Linq;
using System.Text;
using System.IO;
namespace Com.Eucalyptus.Windows
{
    class ZipHandler : UserDataHandler
    {
        private String _unzippedDir = null;
        internal String UnzippedDir 
        {
            private set
            {
                this._unzippedDir = value;
            }
            get{
                return this._unzippedDir;
            }
        }
        internal ZipHandler(String _unzippedDir)
        {
            this.UnzippedDir = _unzippedDir;
        }

        /****
         * Contents in the unzipped directory
         * script: script file to be handled by ScriptHandler
         * powershell: powershell file to be handled by ScriptHandler
         * eucalyptus: eucalyptus file to be handled by EucalyptusParameterHandler
         * include: include file to be handled by IncludeHandler
         * and any other resource files to be used by script/powershell handlers (exe, dll, etc)
         ****/
        override protected void Handle()
        {
            if (!Directory.Exists(UnzippedDir))
            {
                EucaLogger.Error(String.Format("Can't find the unzipped directory {0}", UnzippedDir));
                return;
            }
            else if (File.Exists(UserDataFile))
            {
                try
                {
                    EucaFileUtil.Unzip(UnzippedDir, UserDataFile);
                }
                catch (Exception ex)
                {
                    EucaLogger.Exception(String.Format("Failed to unzip {0} into {1}", UserDataFile, UnzippedDir), ex);
                    return;
                }
            }

            foreach (String filePath in Directory.GetFiles(UnzippedDir))
            {
                String fileName = Path.GetFileName(filePath).ToLower();
                UserDataHandler handler = null;
                if (fileName.Equals("script") || fileName.Equals("powershell"))
                    handler = new ScriptHandler();
                else if (fileName.Equals("eucalyptus"))
                    handler = new EucalyptusParameterHandler();
                else if (fileName.Equals("include"))
                    handler = new IncludeHandler();
                else
                {
                    EucaLogger.Debug(String.Format("unknown file: {0}", fileName));
                    continue;
                }

                try
                {
                    handler.HandleUserData(filePath);
                    EucaLogger.Debug(String.Format("Successfully handled the contents in {0}", fileName));
                }
                catch (Exception ex)
                {
                    EucaLogger.Exception(String.Format("failed to handle the file {0}", fileName), ex);
                }
            }
        }
    }
}
