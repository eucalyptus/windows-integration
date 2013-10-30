using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Com.Eucalyptus.Windows
{
    class BogusHandler : UserDataHandler
    {
        override protected void Handle()
        {
            EucaLogger.Debug("Bogus handler");
        }
    }
}
