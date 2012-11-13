
windows-integration
===================

Verison 1.02   11/14/2012


This package was created using ICSharpCode.SharpZipLib.dll version 860
from http://www.icsharpcode.net/opensource/sharpziplib/Download.aspx.



Eucalyptus Windows Integration tool


1. git clone  windows-integration. 

2. If neccessry, copy ICSharpCode.SharpZipLib.dll to  windows-integration/EucaWindowsService

  2a. Start ->Programs -> Visual Studio 2010.

  2b. Nagivate to <Github_path>/windows-integration

      Ensure the  windows-integration/EucaWindowsService->References has
      "ICSharpCode.SharpZipLib" and it is not highlighted with  YELLOW BANG "!".

      If may be necessary to delete ICSharpCode.SharpZipLib  property 
      and add it again:

     DELETE:

     - High Lite "windows-integration/EucaWindowsService->References"
     - Right Click on ICSharpCode.SharpZipLib, and "Delete" 

     ADD:

     - High Lite "windows-integration/EucaWindowsService->References"
     - Right Click -> "Add references" 
     - Navigate to windows-integration/EucaWindowsService 
     - select "ICSharpCode.SharpZipLib.dll " 

3. Build using "Build" pull down in VS. 

4. The executables are windows-integration/EucalyptusPackage/Debug 

      Windows 2008 and later: EucalyptusWindowsIntegration.msi  
      Windows 2003:           setup.exe


5.  Installing Eucalyptus on Windows 2008 x64 (and later)
    requires signed drivers to be disabled :

      bcdedit -set loadoptions DISABLE_INTEGRITY_CHECKS
      bcdedit -set TESTSIGNING ON 

 
     