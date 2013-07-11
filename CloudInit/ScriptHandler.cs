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
    class ScriptHandler : UserDataHandler
    {
        override protected void Handle()
        {
            EucaLogger.Debug("Script/Powershell Handler invoked");
            
            List<String> scripts = MakeScriptFragments();

            foreach (String file in scripts)
            {
                if (file.EndsWith(".cmd"))
                    ExecuteCommandLine(file);
                else if (file.EndsWith(".ps1"))
                    ExecutePowershell(file);
                else
                    EucaLogger.Debug("Unknown file format found");
            }
        }

        private void ExecuteCommandLine(String scriptFile)
        {
            String exec = "cmd.exe";
            String args = String.Format("/c {0}", scriptFile);

            EucaLogger.Debug(String.Format("Executing {0} {1}", exec, args));
            try
            {
                Win32_CommandResult result = SystemsUtil.SpawnProcessAndWait(exec, args);
                EucaLogger.Debug(String.Format("Execution finished with exit code={0}", result.ExitCode));
                EucaLogger.Debug(String.Format("Stdout: {0}", result.Stdout));
                EucaLogger.Debug(String.Format("Stderr: {0}", result.Stderr));
            }
            catch (Exception ex)
            {
                EucaLogger.Debug("Execution failed");
                EucaLogger.Debug(ex.ToString());
            }
        }

        private void ExecutePowershell(String powershellFile)
        {
            String exec = "powershell.exe";
            String args = String.Format("-NonInteractive -File {0}", powershellFile);
            EucaLogger.Debug(String.Format("Executing {0} {1}", exec, args));
            try
            {
                Win32_CommandResult result = SystemsUtil.SpawnProcessAndWait(exec, args);
                EucaLogger.Debug(String.Format("Execution finished with exit code={0}", result.ExitCode));
                EucaLogger.Debug(String.Format("Stdout: {0}", result.Stdout));
                EucaLogger.Debug(String.Format("Stderr: {0}", result.Stderr));
            }
            catch (Exception ex)
            {
                EucaLogger.Debug("Execution failed");
                EucaLogger.Debug(ex.ToString());
            }
        }
        /// <summary>
        /// 
        /// </summary>
        /// <returns> the list of file paths that contains the script/powershell </returns>
        enum ParseState { script_open, powershell_open, closed };
        const String ScriptFilesDir = CloudInit.CloudInitDirectory;
        private List<String> MakeScriptFragments()
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
                if (line.StartsWith("<script>", true, null) || line.StartsWith("<powershell>", true, null))
                {
                    String marker = line.Substring(0, line.IndexOf(">") + 1);
                    line = line.Remove(0, line.IndexOf(">")+1);
                    formattedLines.Add(marker);
                }
                if (line.EndsWith("</script>", true, null) || line.EndsWith("</powershell>", true, null))
                {
                    String marker = line.Substring(line.LastIndexOf("<"));
                    line = line.Remove(line.LastIndexOf("<"));
                    formattedLines.Add(line);
                    formattedLines.Add(marker);
                    line = null;
                }
                if (line != null && line.Length > 0)
                    formattedLines.Add(line);
            }

            List<String> scriptFiles = new List<string>();

            ParseState parser = ParseState.closed;
            List<String> contents = new List<string>();
            foreach (String line in formattedLines)
            {
                if (line.ToLower().Equals("<script>"))
                {
                    if (parser != ParseState.closed)
                        throw new EucaException(String.Format("Malformed script: {0}", line));
                    parser = ParseState.script_open;
                    contents.Clear();
                }
                else if (line.ToLower().Equals("<powershell>"))
                {
                    if (parser != ParseState.closed)
                        throw new EucaException(String.Format("Malformed script: {0}", line));
                    parser = ParseState.powershell_open;
                    contents.Clear();
                }
                else if (line.ToLower().Equals("</script>"))
                {
                    if (parser != ParseState.script_open)
                        throw new EucaException(String.Format("Malformed script: {0}", line));
                    parser = ParseState.closed;
                    /// create the scripts file
                    String filePath = String.Format("{0}\\{1}.cmd", ScriptFilesDir,
                        Guid.NewGuid().ToString().Substring(0, 8));
                    WriteScriptFile(contents, filePath);
                    scriptFiles.Add(filePath);
                }
                else if (line.ToLower().Equals("</powershell>"))
                {
                    if (parser != ParseState.powershell_open)
                        throw new EucaException(String.Format("Malformed script: {0}", line));
                    parser = ParseState.closed;
                    /// crate the powershell file
                    /// 
                    String filePath = String.Format("{0}\\{1}.ps1", ScriptFilesDir,
                        Guid.NewGuid().ToString().Substring(0, 8));
                    WriteScriptFile(contents, filePath);
                    scriptFiles.Add(filePath);
                }
                else
                {
                    String cmd = line.Trim();
                    if (cmd.Length > 0)
                        contents.Add(cmd);
                }
            }

            return scriptFiles;
        }
        private void WriteScriptFile(List<String> lines, String filePath)
        {
            using (FileStream fs = new FileStream(filePath, FileMode.Create, FileAccess.Write))
            {
                using (StreamWriter sw = new StreamWriter(fs))
                {
                    foreach (String line in lines)
                        sw.WriteLine(line);
                }
            }
        }
    }
}
