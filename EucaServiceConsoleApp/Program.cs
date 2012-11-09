using System;
using System.Collections.Generic;
using System.Text;
using System.Reflection;
using System.IO;
namespace EucaServiceConsoleApp
{
    class Program
    {
        static void Main(string[] args)
        {
            try
            {
                Assembly eucaDll= Assembly.LoadFrom(".\\EucaService.dll");
                Type tEucaBoot = eucaDll.GetType("Com.Eucalyptus.Windows.EucaServiceLibrary.Bootstrapper");
                object bootObj = Activator.CreateInstance(tEucaBoot);
                MethodInfo mEuca = tEucaBoot.GetMethod("DoBootstrap");
                mEuca.Invoke(bootObj, new object[]{".\\WinInstance.xml"});
            }
            catch (Exception e)
            {
                string errorMsg = string.Format("Error from the bootstrapping process \n -- {0}\n   --{1}", e.Message, e.StackTrace);
                LogError(errorMsg);
            }
                        
            Console.ReadLine();
        }

        static void LogError(string msg)
        {
            WriteMsgToFile(".\\error.txt", msg);
        }

        static void WriteMsgToFile(string file, string msg)
        {
            using (StreamWriter sw = new StreamWriter(file, true))
            {
                sw.WriteLine(msg);
            }
        }
    }
}
