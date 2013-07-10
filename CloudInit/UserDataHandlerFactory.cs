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
using System.IO.Compression;

namespace Com.Eucalyptus.Windows
{
    class UserDataHandlerFactory
    {
        private static UserDataHandlerFactory _instance = new UserDataHandlerFactory();
        private UserDataHandlerFactory() { }
        public static UserDataHandlerFactory Instance
        {
            get
            {
                return _instance;
            }
        }

        internal UserDataHandler GetHandler(String userDataFile)
        {
            /*
             *  <script> </script>   (from ec2config)
                <powershell> </powershell>    (from ec2config)
                <eucalyptus> </eucalyptus>    : for euca-specific parameter passing
                <include> </include> : (optional) a list of URLs (that's to be processed with the same rule)
                zipped directory
             *
             */
            try
            {
                tryDecompress(userDataFile, CloudInit.CloudInitDirectory);
                return new ZipHandler();
            }
            catch (Exception ex)
            {
                // the user-data is not a zipped file
                UserDataHandler dataHandler = null;
                using (StreamReader sr = new StreamReader(File.OpenRead(userDataFile)))
                {
                    String line = null;
                    while ((line = sr.ReadLine()) != null)
                    {
                        line = line.Trim();
                        if (line.Length > 0)
                        {
                            line = line.ToLower();
                            if (line.StartsWith("<script>"))
                                dataHandler = new ScriptHandler();
                            else if (line.StartsWith("<powershell>"))
                                dataHandler = new PowershellHandler();
                            else if (line.StartsWith("<eucalyptus>"))
                                dataHandler = new EucalyptusParameterHandler();
                            else if (line.StartsWith("<include>"))
                                dataHandler = new IncludeHandler();
                            else
                                dataHandler = new BogusHandler();
                            break;
                        }
                    }
                }
                if (dataHandler == null)
                    throw new EucaException("User-data is in unknown format");
                return dataHandler;
            }
        }   

        void tryDecompress(String userDataFile, String decompresssedDir)
        {
            EucaFileUtil.Unzip(decompresssedDir, userDataFile);
        }
    }
}
