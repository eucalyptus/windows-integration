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
using Microsoft.Win32;

namespace Com.Eucalyptus.Windows
{
    class EucalyptusParameterHandler : UserDataHandler
    {
        /*
         * <eucalyptus> 
         * key1:value1
         * key2:value2
         * </eucalytpus>
         * 
         * <eucalyptus> key1:value1, key2:value2 </eucalyptus>
         */
        override protected void Handle()
        {
            var keyValues = this.AsMultiLinesWithoutTag
                .Where(line => line.Split(':').Length == 2)
                .Select(line => new KeyValuePair<String, String>(line.Split(':')[0], line.Split(':')[1]));

            foreach (var kv in keyValues)
            {
                try
                {
                    SetEucaRegistryValue(kv.Key, kv.Value);
                    EucaLogger.Debug(String.Format("Eucalyptus registry updated: {0}-{1}", kv.Key, kv.Value)); 
                }
                catch (Exception e)
                {
                    EucaLogger.Exception("Could not set registry value", e);
                }
            }
        }

        private void SetEucaRegistryValue(string key, object value)
        {
            if (key == null || value == null)
                return;
            try
            {
                Com.Eucalyptus.SystemsUtil.SetRegistryValue(Registry.LocalMachine,
                    new string[] { "SOFTWARE", "Eucalyptus Systems", "Eucalyptus" }, key, value, true);
            }
            catch (Exception e)
            {
                throw e;
            }
        }
    }
}
