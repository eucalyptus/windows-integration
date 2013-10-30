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
    class IncludeHandler : UserDataHandler
    {
        /*
         * <include> 
         *    http://myhost/user-data1
         *    http://myhost/user-data2
         * </include>
         * 
         */
        override protected void Handle()
        {
            var urls = this.AsMultiLinesWithoutTag;
            var validUrls = urls.Where(url =>
                url.ToLower().StartsWith("http://"));
            List<String> localUserData = new List<String>();
            foreach (String url in validUrls)
            {
                String filePath = String.Format("{0}\\{1}.txt",
                    CloudInit.CloudInitDirectory, Guid.NewGuid().ToString().Substring(0, 8));
                try{
                    EucaUtil.Curl(url, filePath);
                    localUserData.Add(filePath);
                }catch(Exception ex){
                    EucaLogger.Exception(String.Format("Failed to download from the include url {0}", url), ex);
                    continue;
                }
            }

            foreach (String userDataFile in localUserData)
            {
                UserDataHandler handler = null;
                try
                {
                    handler = UserDataHandlerFactory.Instance.GetHandler(userDataFile);
                }
                catch (Exception ex)
                {
                    EucaLogger.Exception("Unable to find the right handler for include file", ex);
                }

                try
                {
                    handler.HandleUserData(userDataFile);
                }
                catch (Exception ex)
                {
                    EucaLogger.Exception("Failed to handle the user data", ex);
                }
            }

            foreach (String userDataFile in localUserData)
                File.Delete(userDataFile);
        }
    }
}
