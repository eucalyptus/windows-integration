#define EUCA_DEBUG
using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Threading;
using System.Xml.Serialization;
namespace Com.Eucalyptus.Windows
{
    public sealed class LogTools
    {
        private LogTools()
	    {
            // delete previous channel
            try
            {
                if (File.Exists(EucaConstant.EucaChannelFileName))
                {
                    File.Delete(EucaConstant.EucaChannelFileName);
                }
            }
            catch (Exception)
            {                ;            }

            string delimiter = string.Format("=================== Log at {0} ======================", DateTime.Now.ToString());
            WriteLogFile(LOG_INFO,delimiter);
            
           /* if (File.Exists(EucaConstant.EucaLogFileName))
            {
                string backup = EucaConstant.EucaLogFileName+".old";
                if (File.Exists(backup))
                    File.Delete(backup);
                File.Move(EucaConstant.EucaLogFileName, backup);
            }*/
        }

        private static LogTools _instance = new LogTools();
        static public LogTools Instance
	    {
            get
            {
                return _instance;
            }
	    }

        public static void PrintExceptionMessages(Exception e)
        {
            string dbgMsg = "";
            Exception ie = e;

            while (ie != null)
            {
                dbgMsg += string.Format("{0}-{1}{2}", ie.Message, ie.Source, Environment.NewLine);
                ie = ie.InnerException;
            }
            Debug(dbgMsg);
        }

        private bool _enableHostPrefix = false;
        public bool EnableHostPrefix { get { return _enableHostPrefix; } set { _enableHostPrefix = value; } }

        private String _logLocation;
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

        public static void Critical(string msg)
        {
            Instance.LogCritical(msg);
        }

        public void LogCritical(String msg)
        {
            Log(LOG_CRITICAL, msg);
        }

        public static void Debug(string msg)
        {
            Instance.LogDebug(msg);
        }

        public void LogDebug(String msg)
        {
            Log(LOG_DEBUG, msg);
        }

      

        public static void Exception(String msg, Exception e)
        {
            Instance.LogException(msg, e);
        }

        public static void Exception(Exception e)
        {
            Instance.LogException(null, e);
        }

        public void LogException(String msg, Exception e)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine();
            if(msg != null)
                sb.Append(msg+Environment.NewLine);

            Exception ie = e;

            string dash = "-";
            while(ie != null)
            {
                sb.Append(dash+" "+ie.Message + Environment.NewLine);
                dash += "-";
                ie = ie.InnerException;
            }

            sb.Append("STACK TRACE ---"+Environment.NewLine);
            sb.Append(e.StackTrace);
            Log(LOG_EXCEPTION, sb.ToString());
        }


        public void Log(String msg)
        {
            Log(LOG_TYPE_UNKNOWN, msg);
        }

	    public void Log(int logType, String msg)
	    {
		//    Console.WriteLine("LOG: "+msg);
            String timedMsg = msg + " at " + (DateTime.Now).ToString();
    		
		    try{
		    switch(logType)
		    {
		    case LOG_INFO:
#if CONSOLE_OUT
                Console.WriteLine("INFO: " + timedMsg);
#endif
			    WriteLogFile(LOG_INFO, "INFO: "+timedMsg);
			    break;
    			
		    case LOG_WARNING:

#if CONSOLE_OUT
    Console.WriteLine("WARNING: "+timedMsg);
#endif
            WriteLogFile(LOG_WARNING, "WARNING: "+timedMsg);
			    break;
    			
		    case LOG_CRITICAL:

                //Console.WriteLine("CRITICAL: " + timedMsg);
#if CONSOLE_OUT
                Console.WriteLine("========================================");
    Console.WriteLine("CRITICAL: "+timedMsg);
                Console.WriteLine("========================================");
#endif
			    WriteLogFile(LOG_CRITICAL, "CRITICAL: "+timedMsg);
			    break;			

            case LOG_DEBUG:
#if CONSOLE_OUT
                Console.WriteLine("DEBUG: " + timedMsg);
#endif
                WriteLogFile(LOG_DEBUG, "DEBUG: " + timedMsg);
                break;

           case LOG_EXCEPTION:
#if CONSOLE_OUT
                Console.WriteLine("EXCEPTION: " + timedMsg);
#endif
                WriteLogFile(LOG_EXCEPTION, "EXCEPTION: " + timedMsg);
                break;

		    default:
				    ;
			    break;
		    }
		    }catch(IOException e)
		    {
                ;
		    }
	    }

        public void SetLogLocation(String location)
        {
            _logLocation = location;
        }

        private void WriteLogFile(int logType, String msg)
        {
            lock (this)
            {
                try
                {                 
                    using (StreamWriter sw = new StreamWriter(EucaConstant.EucaLogFileName, true))
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

        //LogEuca function is currently not possible because there's no two-way channel between vm and nc.
        public static void LogEuca(string msg)
        {
            LogEuca(int.MinValue, msg, true);
        }
        public static void LogEuca(int code, string msg)
        {
            LogEuca(code, msg, true);
        }
        public static void LogEuca(string msg, bool commit)
        {
            LogEuca(int.MinValue, msg, commit);
        }

        private class EucaBufferedLog {
            public int Code{ get; set;}
            public string Message { get; set; }
        }
        private static List<EucaBufferedLog> _eucaLogbuf = null;
        private static object _logLock = new object();
        public static void LogEuca(int code, string msg, bool commit)
        {
            lock (_logLock)
            {
                if (_eucaLogbuf == null)
                    _eucaLogbuf = new List<EucaBufferedLog>();
                EucaBufferedLog log = new EucaBufferedLog();
                log.Code = code;
                log.Message = msg;
                _eucaLogbuf.Add(log);

                // in the worst case, {deserilization, memory copy, serialization, file delete/copy} can happen
                // for every message
                if (commit)
                    CommitEucaLog();       
             }
        }
        
        public static void CommitEucaLog()
        {
            try
            {
                Logs newLog = new Logs();
                newLog.Log = new LogsLog[_eucaLogbuf.Count];

                int i = 0;
                foreach (EucaBufferedLog buf in _eucaLogbuf)
                {
                    newLog.Log[i] = new LogsLog();
                    if (buf.Code != int.MinValue)
                    {
                        newLog.Log[i].code = buf.Code;
                        newLog.Log[i].codeSpecified = true;
                    }
                    else
                        newLog.Log[i].codeSpecified = false;

                    newLog.Log[i].Value = buf.Message;
                    i++;
                }

                // write back to the xml file
                if (File.Exists(EucaConstant.EucaChannelFileName))
                    File.Delete(EucaConstant.EucaChannelFileName);

                using (Stream s = File.Create(EucaConstant.EucaChannelFileName))
                {
                    XmlSerializer xs = new XmlSerializer(typeof(Logs));
                    xs.Serialize(s, newLog);
                    s.Flush();
                    s.Close();
                }
            }
            catch (Exception e)
            {
                Warning("Could not commit EucaLog");
                Exception(e);
            }
        }
    	
	    public const int LOG_WARNING = 0;
        public const int LOG_CRITICAL = 1;
        public const int LOG_INFO = 2;
        public const int LOG_DEBUG = 3;
        public const int LOG_EXCEPTION = 4;
        public const int LOG_TYPE_UNKNOWN = 10;
    }
}
