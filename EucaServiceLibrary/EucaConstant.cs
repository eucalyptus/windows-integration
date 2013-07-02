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
namespace Com.Eucalyptus.Windows
{
    public class EucaConstant
    {
        public const string EucaDriveName = "A:\\";        
        public static readonly string EucaChannelFileName = string.Format("{0}eucalog.xml", EucaDriveName);

        public static string ProgramRoot
        {
            get
            {// find the default installation location from registry
                string programRoot = null;
                try
                {
                    programRoot = (string)EucaServiceLibraryUtil.GetSvcRegistryValue("InstallLocation");
                    return programRoot;
                }
                catch (Exception)
                {
                   return Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles)+"\\Eucalyptus";
                }
            }
        }

        public static string EucaLogFileName
        {
            get
            {
                if(ProgramRoot==null)
                    return ".\\eucalog.txt";
                else
                    return ProgramRoot + "\\eucalog.txt";
            }
        }

        public static bool JustLaunched = false;


        public static readonly string EucaDriverSourceDir = EucaDriveName + "drivers";
        public static readonly string EucaDriverDestinationDir = ProgramRoot + "\\drivers";
        public const string dummy = "m+1eSOgk8tYU5Y4gUfk75rzL9y6/TK06a4FHkJBM/CI=";
        public const string dummyV = "Swqt3fqaSBj8gIbiZbrQDQ==";
    }
}
