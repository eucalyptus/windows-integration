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
using System.Linq;
using System.Net;
using System.IO;

namespace Com.Eucalyptus
{
    public class EucaUtil
    {
        public static void GetUserData(String fileToDownload)
        {
            try
            {
                byte[] userData = GetUserData();
                using(BinaryWriter bw = new BinaryWriter(File.Open(fileToDownload, FileMode.Create), Encoding.Default))
                {
                    bw.Write(userData);
                }
            }
            catch (Exception e)
            {
                throw e;
            }
        }

        public static byte[] GetUserData()
        {
            HttpWebRequest httpReq =
             (HttpWebRequest)WebRequest.Create(EucaConstant.UserDataUrl);
            HttpWebResponse response = (HttpWebResponse)httpReq.GetResponse();
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new EucaException("Invalid response code from server: " + response.StatusCode);
            }
            Stream stream = response.GetResponseStream();
            byte[] data = null;
            using (BinaryReader reader = new BinaryReader(stream))
            {
                data = reader.ReadBytes((int)response.ContentLength);
            }
            if (data != null && data.Length != response.ContentLength)
            {
                throw new EucaException("data length doesn't match");
            }

            return data;
        }

    }
}
