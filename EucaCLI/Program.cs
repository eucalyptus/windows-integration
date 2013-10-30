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
using System.Text;
using System.Management;
namespace Com.Eucalyptus.Windows
{
    class Program
    {
        static int Main(string[] args)
        {
            if (args.Length < 1)
                return -1;

            if (args[0].ToLower() == "-unjoindom")
            {
                try
                {
                    bool partOfDomain = false;
                    string domain = null;
                    using (ManagementObject comObj = WMIUtil.QueryLocalWMI("Select * from win32_computersystem"))
                    {
                        partOfDomain = (bool)comObj["PartOfDomain"];
                        domain = (string)comObj["Domain"];
                        if (!partOfDomain)
                        {// this must be a bug, because the button shouldn't be activated if the instance isn't a domain member
                            //  MessageBox.Show("This instance is not a member of any domain");
                            Console.WriteLine("EucaCLI: The instance is not a member of any domain");
                            return -1;
                        }
                /*        string adAddress = null, adUsername = null, adPassword = null;
                        object tmp = EucaUtil.GetRegistryValue("ADAddress");
                        if (tmp != null)
                            adAddress = (string)tmp;
                        tmp = EucaUtil.GetRegistryValue("ADUsername");
                        if (tmp != null)
                            adUsername = (string)tmp;
                        tmp = EucaUtil.GetRegistryValue("ADPassword");
                        if (tmp != null)
                            adPassword = (string)tmp;

                        if (adUsername == null || adPassword == null || adAddress == null)
                        {
                            Console.WriteLine("EucaCLI: Username/password/ADaddress is not found");
                            return -1;
                        }

                        if (!adUsername.Contains("\\"))
                            adUsername = string.Format("{0}\\{1}", adAddress, adUsername);

                        adPassword = EucaUtil.Decrypt(adPassword);*/
                        ManagementBaseObject paramIn = comObj.GetMethodParameters("UnjoinDomainOrWorkgroup");

                        paramIn["Password"] = null;
                        paramIn["UserName"] = null;
                        paramIn["FUnjoinOptions"] = (UInt32)0x00; // default; No option

                        ManagementBaseObject paramOut = comObj.InvokeMethod("UnjoinDomainOrWorkgroup", paramIn, null);
                        UInt32 retVal = (UInt32)paramOut["ReturnValue"];
                        if (retVal == 0)
                        {
                            Console.WriteLine("SUCCESS");
                            string hostnameFile = EucaConstant.ProgramRoot + "\\hostname";
                            if (System.IO.File.Exists(hostnameFile))
                            {
                                try { System.IO.File.Delete(hostnameFile); }
                                catch (Exception) { ; }
                            }
                            return 0;
                        }
                        else
                        {
                            Console.WriteLine(string.Format("EucaCLI: Could not detach the instance: exit code={0}", retVal));
                            return -1;
                        }
                    }
                }
                catch (Exception ie)
                {
                    Console.WriteLine("EucaCLI: Can't detach the instance from the domain (exception thrown)");
                    return -1;
                }
            }
            else if (args[0].ToLower() == "-setdom")
            {
                try
                {
                    string uname = args[1].Trim();
                    string passwd = args[2].Trim();

                    if (uname == null || passwd == null)
                    {
                        Console.WriteLine("EucaCLI: uname and passwd is null");
                        return -1;
                    }
                    string passwdEnc = SystemsUtil.Encrypt(passwd);

                    EucaServiceLibraryUtil.SetSvcRegistryValue("ADUsername", uname);
                    EucaServiceLibraryUtil.SetSvcRegistryValue("ADPassword", passwdEnc);
                    Console.WriteLine("SUCCESS");
                    return 0;
                }
                catch (Exception e)
                {
                    Console.WriteLine("Exception: " + e.Message);
                    return -1;
                }
            }
            else
            {
                Console.WriteLine("Unknown parameter");
                return -1;
            }

        }
    }
}
