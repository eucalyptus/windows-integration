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

namespace Com.Eucalyptus.Windows
{
    class ScriptHandler : UserDataHandler
    {
        override protected void Handle()
        {
            EucaLogger.Debug("Script Handler invoked");
            List<String> cmdLines = new List<string>();
            foreach (String s in Lines)
            {
                String cmdLine = s.Trim();
                if (cmdLine.ToLower().StartsWith("<script>"))
                   cmdLine = cmdLine.Remove(0, "<script>".Length);
                bool endOfScript = false;
                if (cmdLine.ToLower().EndsWith("</script>"))
                {
                    cmdLine = cmdLine.Remove(cmdLine.Length - "<script>".Length, "<script>".Length);
                    endOfScript = true;
                }
                if(cmdLine.Length>0)
                    cmdLines.Add(cmdLine);
                if (endOfScript)
                    break;
            }

            ExecuteAll(cmdLines);
        }

        private void ExecuteAll(List<String> cmdLines)
        {
            EucaLogger.Debug(string.Format("Starting to execute {0} command lines", cmdLines.Count));

            foreach (String cmdLine in cmdLines)
            {
                String[] parts = cmdLine.Split(null);
                if (parts.Length <= 0)
                {
                    EucaLogger.Debug(String.Format("Unable to parse command line: {0}", cmdLine));
                    continue;
                }
                String exec = parts[0];
                String args = null;
                if (parts.Length > 1)
                   args = String.Join(" ", parts, 1, parts.Length - 1);
                EucaLogger.Debug(String.Format("Executing {0} {1}", exec, args));
                try
                {
                    Win32_CommandResult result = SystemsUtil.SpawnProcessAndWait(exec, args);
                    EucaLogger.Debug(String.Format("Execution finished with exit code={0}", result.ExitCode));
                }
                catch (Exception ex)
                {
                    EucaLogger.Debug("Execution failed");
                    EucaLogger.Debug(ex.ToString());
                }
            }

        }

    }
}
