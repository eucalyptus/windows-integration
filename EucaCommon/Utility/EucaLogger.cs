/*************************************************************************
 * Copyright 2010-2012 Eucalyptus Systems, Inc.
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
#define EUCA_DEBUG
#define CONSOLE_OUT
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading;
using System.Xml.Serialization;

namespace Com.Eucalyptus
{    
    public class EucaLogger
    {
        private EucaLogger()
	    { }
        private static int _logSizeKB = 1024;
        public static int LogFileSize {
            set { _logSizeKB = value; }
        }

        private static string _logLocation = "log.txt";
        public static string LogLocation
        {
            set { _logLocation = value; }
            get { return _logLocation; }
        }

        private static EucaLogger _instance = new EucaLogger();
        static public EucaLogger Instance
	    {
            get{ return _instance;  }
	    }

        public static void Warning(string msg)
        {
            Instance.LogWarning(msg);
        }

        public void LogWarning(String msg)
        {
            Log(LOG_WARNING, msg);
        }

        public static void Info(string msg)
        {
            Instance.LogInfo(msg);
        }

        public void LogInfo(String msg)
        {
            Log(LOG_INFO, msg);
        }

        public static void Fatal(string msg)
        {
            Instance.LogFatal(msg);
        }

        public void LogFatal(String msg)
        {
            Log(LOG_FATAL, msg);
        }

        public static void Debug(string msg)
        {
            Instance.LogDebug(msg);
        }

        public void LogDebug(String msg)
        {
            Log(LOG_DEBUG, msg);
        }   

        public static void Exception(string msg, Exception e){
            Exception(LOG_WARNING, msg, e);
        }

        public static void Exception(int logType, String msg, Exception e)
        {
            Instance.LogException(logType, msg, e);
        }

        public static void Exception(int logType, Exception e)
        {
            Instance.LogException(logType, null, e);
        }

        public void LogException(int logType, String msg, Exception e)
        {
            StringBuilder sb = new StringBuilder();
            if (msg != null)
            {
                sb.Append(msg);
                sb.AppendLine();
            }

            Exception ie = e;
            string dash = "-";
            while(ie != null)
            {
                sb.Append(dash+" "+ie.Message);
                sb.AppendLine();
                dash += "-";
                ie = ie.InnerException;
            }
            sb.Append("STACK TRACE --- ");
            sb.AppendLine();
            sb.Append(e.StackTrace);
            
            Log(logType, sb.ToString());
        }

        public static void DevDebug(String msg)
        {
            EucaLogger.Instance.Log(EucaLogger.LOG_DEVDEBUG, msg);
        }
        
        // Eucalyptus logging message format
        // [DateTime] [Proc_ID] [LogType] Message
	    public void Log(int logType, String msg)
	    {
		    //String timedMsg = msg + " at " + (DateTime.Now).ToString();
            String eucaMsg = null; 
    		
		    try{
		    switch(logType)
		    {
		    case LOG_INFO:
                eucaMsg= string.Format("[{0}] [{1}] [{2}] {3}",
                    DateTime.Now.ToString(), System.Diagnostics.Process.GetCurrentProcess().Id, "EUCAINFO", msg);
#if CONSOLE_OUT
                Console.Error.WriteLine(eucaMsg);
               Console.Error.Flush();
#endif
			    break;
    			
		    case LOG_WARNING:
                eucaMsg = string.Format("[{0}] [{1}] [{2}] {3}",
                    DateTime.Now.ToString(), System.Diagnostics.Process.GetCurrentProcess().Id, "EUCAWARN", msg);
#if CONSOLE_OUT
               Console.Error.WriteLine(eucaMsg);
               Console.Error.Flush();
#endif
                break;
    			
		    case LOG_FATAL:

                //Console.WriteLine("CRITICAL: " + timedMsg);
			    eucaMsg = string.Format("[{0}] [{1}] [{2}] {3}",
                    DateTime.Now.ToString(), System.Diagnostics.Process.GetCurrentProcess().Id, "EUCAFATAL", msg);
#if CONSOLE_OUT
              Console.Error.WriteLine(eucaMsg);
               Console.Error.Flush();
#endif
                break;	

            case LOG_DEBUG:
                eucaMsg = string.Format("[{0}] [{1}] [{2}] {3}",
                    DateTime.Now.ToString(), System.Diagnostics.Process.GetCurrentProcess().Id, "EUCADEBUG", msg);
#if CONSOLE_OUT
                Console.Error.WriteLine(eucaMsg);
                Console.Error.Flush();
#endif
                break;

            case LOG_DEVDEBUG:
                   eucaMsg = string.Format("[{0}] [{1}] [{2}] {3}",
                     DateTime.Now.ToString(), System.Diagnostics.Process.GetCurrentProcess().Id, "DEVDEBUG", msg);
#if CONSOLE_OUT
               Console.Error.WriteLine(eucaMsg);
               Console.Error.Flush();
#endif
                break;

		    default:
                eucaMsg = string.Format("[{0}] [{1}] [{2}] {3}",
                    DateTime.Now.ToString(), System.Diagnostics.Process.GetCurrentProcess().Id, "UNKNOWN", msg);
#if CONSOLE_OUT
               Console.Error.WriteLine(eucaMsg);
               Console.Error.Flush();
#endif
			    break;
		    }
		    }catch(IOException e)
		    {
                eucaMsg = "Couldn't construct a log message: "+e.Message;
		    }

            WriteLogFile(eucaMsg);
	    }

        private int _sizeCheckFlag = 0;
        private const int NUM_LOG_ARCHIVE = 3;
        private void WriteLogFile(String msg)
        {
            lock (this)
            {
                // check the size of current log file and rename it if that exceeds the thresholds
                if (_sizeCheckFlag++ % 10 == 0)
                {
                    _sizeCheckFlag = 0;
                    if (File.Exists(_logLocation) && (new FileInfo(_logLocation)).Length >= _logSizeKB*1024)
                    {
                        for (int i = NUM_LOG_ARCHIVE-1; i > 0; i--)
                        {
                            /// Delete File  i
                            string fileName = string.Format("{0}.{1}", _logLocation, i);
                            if (File.Exists(fileName))
                                File.Delete(fileName);

                            string fileToMove = null;
                            fileToMove = string.Format("{0}.{1}", _logLocation, i - 1);
                            if(File.Exists(fileToMove))
                                File.Copy(fileToMove, fileName);
                        }

                        string nextFile = string.Format("{0}.0", _logLocation);
                        if (File.Exists(nextFile))
                            File.Delete(nextFile);
                        File.Move(_logLocation, nextFile);                        
                    }
                }

                try
                {                 
                    using (StreamWriter sw = new StreamWriter(
                        new FileStream(_logLocation,FileMode.Append, FileAccess.Write)))
                    {
                        sw.WriteLine(msg);
                        sw.Flush();
                        sw.Close();
                    }                    
                }
                catch (IOException e)
                { ;}
            }
        }
        	
	    public const int LOG_WARNING = 0;
        public const int LOG_FATAL = 1;
        public const int LOG_INFO = 2;
        public const int LOG_DEBUG = 3;
        public const int LOG_DEVDEBUG= 4;
        public const int LOG_TYPE_UNKNOWN = 10;
    }
}
