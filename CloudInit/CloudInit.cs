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
    public class CloudInit
    {
        internal const String CloudInitDirectory = "C:\\Scratch";
        public void Init()
        {
            string userDataFile = null;
            try
            {
                userDataFile = CloudInit.CloudInitDirectory + "\\user-data";
                EucaUtil.GetUserData(userDataFile);
                if (!File.Exists(userDataFile))
                    throw new EucaException("User data file not found");
                if ((new FileInfo(userDataFile)).Length <= 0)
                    throw new EucaException("Invalid user data file");

            }
            catch (Exception ex)
            {
                EucaLogger.Debug("Unable to download the user-data");
                throw ex;
            }

            // detect the contents
            UserDataHandler handler = null;
            try
            {
                handler = UserDataHandlerFactory.Instance.GetHandler(userDataFile);
            }
            catch (Exception ex)
            {
                EucaLogger.Exception("Unable to find the handler for matching user-data contents", ex);
                return;
            }
            // invoke handler
            try
            {
                handler.HandleUserData(userDataFile);
            }
            catch (Exception e)
            {
                EucaLogger.Exception("User data handler threw exception", e);
            }
            // return
        }
    }
}
