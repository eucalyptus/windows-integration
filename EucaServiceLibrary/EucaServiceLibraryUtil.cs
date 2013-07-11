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
    public class EucaServiceLibraryUtil
    {
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
}
