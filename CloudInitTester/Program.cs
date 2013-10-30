using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.Eucalyptus.Windows
{
    class Program
    {
        static void Main(string[] args)
        {
            CloudInit cInit = new CloudInit();
            cInit.Init();
        }
    }
}
