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
    abstract class UserDataHandler
    {
        private List<String> _lines = null;
        protected List<String> Lines
        {
            get
            {
                if (_lines == null)
                {
                    try
                    {
                        _lines = new List<string>();
                        using (StreamReader sr = new StreamReader(File.OpenRead(_userDataFile)))
                        {
                            String line = null;
                            while ((line = sr.ReadLine()) != null)
                            {
                                line = line.Trim();
                                if(line.Length>0)
                                    _lines.Add(line);
                            }

                        }
                        return _lines;
                    }
                    catch (Exception ex)
                    {
                        _lines = null;
                        return _lines;
                    }
                }
                else
                    return _lines;
            }
        }

        String[] STARTING_TAGS = { "<script>", "<powershell>", "<eucalyptus>", "<include>" };
        String[] ENDING_TAGS = { "</script>", "</powershell>", "</eucalyptus>", "</include>" };
              
        protected IEnumerable<String> AsMultiLines
        {
            get
            {
                /*
             * <script> do something </script> 
             *  --> <script>
             *      do something
             *      </script>
             */
                List<String> formattedLines = new List<string>();
                foreach (String s in Lines)
                {
                    String line = s.Trim();
                    if (STARTING_TAGS.Where(tag => line.ToLower().StartsWith(tag)).Count() > 0)
                    {
                        String tag = line.Substring(0, line.IndexOf(">") + 1);
                        line = line.Remove(0, line.IndexOf(">") + 1);
                        formattedLines.Add(tag);
                    }

                    if (ENDING_TAGS.Where(tag => line.ToLower().EndsWith(tag)).Count() > 0)
                    {
                        String tag = line.Substring(line.LastIndexOf("<"));
                        line = line.Remove(line.LastIndexOf("<"));
                        formattedLines.Add(line);
                        formattedLines.Add(tag);
                        line = null;
                    }
                    if (line != null && line.Length > 0)
                        formattedLines.Add(line);
                }
                return formattedLines;
            }
        }

        protected IEnumerable<String> AsMultiLinesWithoutTag
        {
            get
            {
                return this.AsMultiLines
                    .Where(line => !(STARTING_TAGS.Where(tag => tag.Equals(line.ToLower())).Any() ||
                        (ENDING_TAGS.Where(tag => tag.Equals(line.ToLower())).Any())));
            }
        }

        private String _userDataFile=null;
        protected String UserDataFile
        {
            get
            {
                return _userDataFile;
            }
        }


        public void HandleUserData(String userDataFile)
        {
            this._userDataFile = userDataFile;
            this.Handle();
        }

        abstract protected void Handle();
    }
}
