using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Text;
using System.Windows.Forms;
using System.Management;
using System.IO;
using Microsoft.Win32;

namespace Com.Eucalyptus.Windows
{
    public partial class EucaConfigForm : Form
    {
        private string[] EUCALYPTUS_REGISTRY_PATH = new string[]{
                    "SOFTWARE", "Eucalyptus Systems", "Eucalyptus"};

        private ADConfiguration _adConfig;
        public EucaConfigForm()
        {
            try
            {
                string installLocation = (string)SystemsUtil.GetRegistryValue(Registry.LocalMachine, EUCALYPTUS_REGISTRY_PATH, "InstallLocation");
                EucaLogger.LogLocation = string.Format("{0}\\eucalog.txt", installLocation);
            }
            catch (Exception e)
            {
                MessageBox.Show(string.Format("Unexpected exception thrown: {0}", e.Message));
                return;
            }

            InitializeComponent();
        //    using (System.Drawing.Bitmap bm = new System.Drawing.Bitmap(EucaConstant.ProgramRoot+"\\euca.png"))
          //      this.Icon = System.Drawing.Icon.FromHandle(bm.GetHicon());       
            InitPanel();
        }

        private void InitPanel()
        {
            /// check if the instance is a member of an AD
            ///            
            bool partOfDomain = false;
            string domain = null;
            try
            {
                using (ManagementObject comObj = WMIUtil.QueryLocalWMI("Select * from win32_computersystem"))
                {
                    partOfDomain = (bool)comObj["PartOfDomain"];
                    domain = (string)comObj["Domain"];
                }

                if (partOfDomain)
                {
                    labelADStatus.Text = string.Format("domain member of {0}", domain);
                    // buttonUnregister.Enabled = true;
                    // buttonUnregister.Visible = true;
                }
                else
                    labelADStatus.Text = "not a member of a domain";
            }
            catch (Exception e)
            {
                EucaLogger.Exception("Can't determine if the host is a part of domain", e);
                labelADStatus.Text = "error-AD status could not be determined";
            }

            object tmp = EucaServiceLibraryUtil.GetSvcRegistryValue("ADAddress");
            if (tmp != null)
                _adConfig.ADAddress = (string)tmp;
            tmp = EucaServiceLibraryUtil.GetSvcRegistryValue("ADUsername");
            if (tmp != null)
                _adConfig.ADUsername = (string)tmp;
            tmp = EucaServiceLibraryUtil.GetSvcRegistryValue("ADPassword");
            {
                if (tmp != null)
                    _adConfig.ADPassword = SystemsUtil.Decrypt((string)tmp);
            }
            tmp = EucaServiceLibraryUtil.GetSvcRegistryValue("ADOU");
            if (tmp != null)
                _adConfig.ADOU = (string)tmp;

            if (_adConfig.ADAddress != null)
                textBoxADAddress.Text = _adConfig.ADAddress;
            if (_adConfig.ADUsername != null)
                textBoxADUsername.Text = _adConfig.ADUsername;
            if (_adConfig.ADPassword != null)
                textBoxADPassword.Text = _adConfig.ADPassword;
            if (_adConfig.ADOU != null)
                textBoxADOU.Text = _adConfig.ADOU;


            /// bring up the list of users/groups that has RD permission on the image
            string[] rdPermissionUsers = null;
            try
            {
                rdPermissionUsers = this.GetRDPermissionUsers();
            }
            catch (Exception e) {
                EucaLogger.Exception("Could not enumerate RD users/groups from registry",e);
            }

            if (rdPermissionUsers != null)
            {
                foreach (string name in rdPermissionUsers)
                {
                    listBoxRDPermission.Items.Add(name);
                }
            }
            buttonApply.Enabled = false;

            try
            {
                int selected = (int) SystemsUtil.GetRegistryValue(Registry.LocalMachine, EUCALYPTUS_REGISTRY_PATH, "FormatDrives");
                if (selected == 1)
                    checkBoxFormatDrives.Checked = true;
                else
                    checkBoxFormatDrives.Checked = false;
            }
            catch (Exception e)
            {
                EucaLogger.Exception("Couldn't check 'FormatDrives' option from registry", e);
            }
        }
        
        private void buttonApply_Click(object sender, EventArgs e)
        {
            string adAddr = textBoxADAddress.Text;
            if (adAddr != null && adAddr.Length == 0)
                adAddr = null;
            string adUsername = textBoxADUsername.Text;
            if (adUsername != null && adUsername.Length == 0)
                adUsername = null;
            string adPassword = textBoxADPassword.Text;
            if (adPassword != null && adPassword.Length == 0)
                adPassword = null;
            string adPasswordConfirm = textBoxPwdConfirm.Text;
            if (adPasswordConfirm != null && adPasswordConfirm.Length == 0)
                adPasswordConfirm = null;

            string adOu = textBoxADOU.Text;
            if (adOu != null && adOu.Length == 0)
                adOu = null;

            if (!(adAddr == null && adUsername == null && adPassword == null && adOu == null))
            {// user has put something in the config window
                if (adAddr == null || adUsername == null || adPassword == null)
                {
                    MessageBox.Show("AD address, username, and password are required to enable AD join");
                    return;
                }
            }         
            else
            {
                MessageBox.Show("AD address, username, and password are required to enable AD join");
                return;
            }

            if (adPassword != adPasswordConfirm)
            {
                MessageBox.Show("Passwords do not match!");
                return;
            }

            bool updated = false;
            if (adAddr.Trim() != (string) EucaServiceLibraryUtil.GetSvcRegistryValue("ADAddress"))
            {
                EucaServiceLibraryUtil.SetSvcRegistryValue("ADAddress", adAddr.Trim());
                updated = true;
            }
            if (adUsername.Trim() != (string)EucaServiceLibraryUtil.GetSvcRegistryValue("ADUsername"))
            {
                EucaServiceLibraryUtil.SetSvcRegistryValue("ADUsername", adUsername.Trim());
                updated = true;
            }

            //encrypt AD password properly
            if (adPassword.Trim() != (string)EucaServiceLibraryUtil.GetSvcRegistryValue("ADPassword"))
            {
                string data = SystemsUtil.Encrypt(adPassword.Trim());
                EucaServiceLibraryUtil.SetSvcRegistryValue("ADPassword", data);
                updated = true;
            }

            if (adOu != null)
            {
                if (adOu.Trim() != (string)EucaServiceLibraryUtil.GetSvcRegistryValue("ADOU"))
                {
                    EucaServiceLibraryUtil.SetSvcRegistryValue("ADOU", adOu.Trim());
                    updated = true;
                }
            }
            if (updated)
            {
                MessageBox.Show("AD information is updated succesfully");
                buttonApply.Enabled = false;
            }
            else
                MessageBox.Show("No change has been saved");
            
        }
        /* this method is deprecated */
        private void buttonUnregister_Click(object sender, EventArgs e)
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
                        MessageBox.Show("This instance is not a member of any domain");
                        return;
                    }

                    DialogResult answer = MessageBox.Show(string.Format("Detach this instance from domain({0})?", domain),
                        "Detach from domain",MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                    if (answer == DialogResult.No)
                        return;                  

                    ManagementBaseObject paramIn = comObj.GetMethodParameters("UnjoinDomainOrWorkgroup");

                    paramIn["Password"] = null; // this won't delete computer record from AD controller
                    paramIn["UserName"] = null;
                    paramIn["FUnjoinOptions"] = (UInt32)0x00; // Default, no option

                    ManagementBaseObject paramOut = comObj.InvokeMethod("UnjoinDomainOrWorkgroup", paramIn, null);
                    UInt32 retVal = (UInt32)paramOut["ReturnValue"];
                    if (retVal == 0)
                    {
                        EucaLogger.Info("The instance was successfully detached from the domain");
                        MessageBox.Show("The instance was successfully detached from the domain");
                        string hostnameFile = EucaConstant.ProgramRoot + "\\hostname";
                        if (File.Exists(hostnameFile))
                        {
                            try{  File.Delete(hostnameFile);                        }
                            catch (Exception)   { ; }
                        }
                        System.Environment.Exit(0);
                    }
                    else
                    {
                        EucaLogger.Warning(string.Format("Could not detach the instance: exit code={0}", retVal));
                        MessageBox.Show(string.Format("Could not detach the instance: exit code={0}", retVal));
                        return;
                    }
                }
            }
            catch (Exception ie)
            {
                EucaLogger.Exception("Can't detach the instance from the domain",ie);
            }
        }    

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            if (buttonApply.Enabled)
            {
                DialogResult answer = MessageBox.Show("The changes were not saved. Do you want to close anyway?","Confirmation", 
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer == DialogResult.No)
                    return;                 
            }

            System.Environment.Exit(0);
        }

        // text = Forget
        private void buttonClear_Click(object sender, EventArgs e)
        {
            DialogResult answer = MessageBox.Show("Do you want to remove all AD records from the system?",
                  "AD clean-up", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer == DialogResult.No)
                return;                  
            try
            {
                EucaServiceLibraryUtil.DeleteSvcRegistryValue("ADAddress");
                EucaServiceLibraryUtil.DeleteSvcRegistryValue("ADUsername");
                EucaServiceLibraryUtil.DeleteSvcRegistryValue("ADPassword");
                EucaServiceLibraryUtil.DeleteSvcRegistryValue("ADOU");
                
                textBoxADAddress.Text = "";
                textBoxADUsername.Text = "";
                textBoxADPassword.Text = "";
                textBoxADOU.Text = "";
                buttonApply.Enabled = false;

                MessageBox.Show("Active Directory information is cleared");             
            }
            catch (Exception ie)
            {
                EucaLogger.Exception("Can't forget the AD information", ie);
            }
        }

        private void textBoxADAddress_TextChanged(object sender, EventArgs e)
        {
            if (!buttonApply.Enabled)
                buttonApply.Enabled = true;
        }

        private void textBoxADUsername_TextChanged(object sender, EventArgs e)
        {
            if (!buttonApply.Enabled)
                buttonApply.Enabled = true;
        }

        private void textBoxADPassword_TextChanged(object sender, EventArgs e)
        {
            if (!buttonApply.Enabled)
                buttonApply.Enabled = true;
        }

        private void textBoxADOU_TextChanged(object sender, EventArgs e)
        {
            if (!buttonApply.Enabled)
                buttonApply.Enabled = true;
        }

        private void EucaConfigForm_Load(object sender, EventArgs e)
        {
        }


        private void buttonAddRDP_Click(object sender, EventArgs e)
        {
            string strUser = textBoxRDPUsername.Text;
            if (strUser == null)
                return;
            strUser = strUser.Trim();
            if (strUser == null || strUser.Length <= 2)
            {
                MessageBox.Show("User/group should be specified");
                return;
            }

            if(!strUser.Contains("\\"))
            {
                MessageBox.Show("Should specify either localhost or domain");
                return;
            }

            if(listBoxRDPermission.Items.Contains(strUser))
            {
                MessageBox.Show("The user/group is already in the authorized list");
                return;
            }

            listBoxRDPermission.Items.Add(strUser);
            textBoxRDPUsername.Text = "";

            if (!buttonRDPermissionApply.Enabled)
                buttonRDPermissionApply.Enabled = true;
        }

        private void buttonADPermissionRemove_Click(object sender, EventArgs e)
        {
            if (listBoxRDPermission.Items.Count <= 0)
                return;

            List<string> selected = new List<string>(10);
            foreach (string str in listBoxRDPermission.SelectedItems)
                selected.Add(str);

            foreach (string str in selected)
                listBoxRDPermission.Items.Remove(str);

            if (selected.Count > 0 && !buttonRDPermissionApply.Enabled)
                buttonRDPermissionApply.Enabled = true;
        }


        private void textBoxRDPUsername_GotFocus(object sender, EventArgs e)
        {
            listBoxRDPermission.ClearSelected();
        }

        private void listBoxRDPUsername_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (listBoxRDPermission.SelectedItems.Count > 0)
                buttonADPermissionRemove.Enabled = true;
            else
                buttonADPermissionRemove.Enabled = false;
        }

        private void buttonRDPermissionClose_Click(object sender, EventArgs e)
        {
            if (buttonRDPermissionApply.Enabled)
            {
                DialogResult answer = MessageBox.Show("The changes were not saved. Do you want to close anyway?", "Confirmation",
                  MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer == DialogResult.No)
                    return;                 
            }
            System.Environment.Exit(0);
        }

        private void buttonRDPermissionApply_Click(object sender, EventArgs e)
        {
            /// compare user/group list in registry with the list in GUI
            /// 
            string[] users = new string[listBoxRDPermission.Items.Count];
            int i = 0;
            foreach (string s in listBoxRDPermission.Items)
                users[i++] = s.Trim();
            
            try
            {
                ClearRDPermissionUserRegistry();
                AddRDPermissionUsers(users);
                MessageBox.Show("Remote desktop permission updated successfully");
            }
            catch (Exception ex)
            {
                MessageBox.Show("[FAILED] Couldn't save users/groups to the Windows registry!");
                EucaLogger.Exception("Couldn't save users/groups to the Windows registry", ex);
                return;
            }
            buttonRDPermissionApply.Enabled = false;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        /// <exception cref="Exception"/>
        private string[] GetRDPermissionUsers()
        {
            RegistryKey regKey =
                Registry.LocalMachine.OpenSubKey("SOFTWARE").OpenSubKey("Eucalyptus Systems").
                OpenSubKey("Eucalyptus").OpenSubKey("RDP", false);

            string[] usernames = regKey.GetValueNames();
            regKey.Close();

            return usernames;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="username"></param>
        /// <exception cref="Exception"/>
        private void AddRDPermissionUsers(string[] username)
        {
            if (username == null || username.Length == 0)
                return;
            RegistryKey regKey =
                Registry.LocalMachine.OpenSubKey("SOFTWARE").OpenSubKey("Eucalyptus Systems").
                OpenSubKey("Eucalyptus").OpenSubKey("RDP", true);

            foreach (string s in username)
                regKey.SetValue(s, "");
            
            regKey.Flush();
            regKey.Close();
        }

       
        /// <summary>
        /// 
        /// </summary>
        /// <exception cref="Exception"/>
        private void ClearRDPermissionUserRegistry()
        {
            RegistryKey regKey =
                Registry.LocalMachine.OpenSubKey("SOFTWARE").OpenSubKey("Eucalyptus Systems").
                OpenSubKey("Eucalyptus").OpenSubKey("RDP", true);

            foreach(string value in regKey.GetValueNames())
                regKey.DeleteValue(value);
            
            regKey.Flush();
            regKey.Close();
        }

        private void checkBoxFormatDrives_CheckedChanged(object sender, EventArgs e)
        {
            bool formatDrive = this.checkBoxFormatDrives.Checked;
            try
            {
                if (formatDrive)
                    SystemsUtil.SetRegistryValue(Registry.LocalMachine, EUCALYPTUS_REGISTRY_PATH, "FormatDrives", 1, false);
                else
                    SystemsUtil.SetRegistryValue(Registry.LocalMachine, EUCALYPTUS_REGISTRY_PATH, "FormatDrives", 0, false);
            }
            catch (Exception ie)
            {
                EucaLogger.Exception("Couldn't set registry value for 'FormatDrives'", ie);
            }
        }

        private string SysprepAnswerFile {
            get
            {
                string installLocation = (string)
                    SystemsUtil.GetRegistryValue(Registry.LocalMachine, EUCALYPTUS_REGISTRY_PATH, "InstallLocation");
                string answerPath = null;
                if(installLocation.EndsWith("\\"))
                    answerPath = string.Format("{0}sysprep_answers\\", installLocation);
                else
                    answerPath = string.Format("{0}\\sysprep_answers\\", installLocation);

                if (OSEnvironment.OS_Name == OSEnvironment.Enum_OsName.S2008)
                {
                    if (OSEnvironment.Is64bit)
                        answerPath += "answer_server2008_amd64.xml";
                    else
                        answerPath += "answer_server2008_x86.xml";
                }
                else if (OSEnvironment.OS_Name == OSEnvironment.Enum_OsName.S2008R2 ||
                    OSEnvironment.OS_Name == OSEnvironment.Enum_OsName.S2012)
                    answerPath += "answer_server2008r2_amd64.xml";
                else if (OSEnvironment.OS_Name == OSEnvironment.Enum_OsName.Win7)
                {
                    if (OSEnvironment.Is64bit)
                        answerPath += "answer_win7_amd64.xml";
                    else
                        answerPath += "answer_win7_x86.xml";
                }
                else
                    answerPath = null;

                return answerPath;
            }
        }

        private void buttonSysprep_Click(object sender, EventArgs e)
        {
         //   IMPLEMENT_SYS_PREP;
            try
            {
                DialogResult answer = MessageBox.Show("Sysprep should be performed only when you are finished with VM setup. Do you want to run the sysprep now?",
                  "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question);

                if (answer == DialogResult.No)
                    return;    

                string answerPath = this.SysprepAnswerFile;
                if (answerPath == null)
                {
                    MessageBox.Show("Sysprep is not supported on this Windows version. Please check with the administration manual to find the supported OS",
                        "WARNING");
                    return;
                }

                if (!File.Exists(answerPath))
                {
                    MessageBox.Show(string.Format("Sysprep answer file is not found ({0})", answerPath));
                    return;
                }

                string sysprepDest = string.Format("C:\\{0}", (new FileInfo(answerPath)).Name);
                if (File.Exists(sysprepDest))
                    File.Delete(sysprepDest);

                File.Copy(answerPath, sysprepDest);
                string sysprepExec = string.Format("{0}\\sysprep\\sysprep.exe",
                    Environment.GetFolderPath(Environment.SpecialFolder.System));

                string arg = string.Format("/generalize /oobe /quit /unattend:\"{0}\"", sysprepDest);
                EucaLogger.Debug("sysprep argument: " + arg);

                System.Diagnostics.Process proc = new System.Diagnostics.Process();
                proc.StartInfo.UseShellExecute = true;

                Win32_CommandResult result = SystemsUtil.SpawnProcessAndWait(sysprepExec, arg);                
                if (result.ExitCode != 0)
                {
                    MessageBox.Show(string.Format("Sysprep returned exit code: {0}", result.ExitCode));
                    EucaLogger.Debug(string.Format("Sysprep exit code: {0}, stdout: {1}, stderr: {2}", result.ExitCode, result.Stdout, result.Stderr));
                    return;
                }
                else
                {
                    MessageBox.Show("Sysprep finished successfully. You can shutdown the VM and register it with Eucalyptus front-end.");
                    System.Environment.Exit(0);
                }
            }
            catch (Exception ie)
            {
                MessageBox.Show(string.Format("Unexpected exception thrown: {0}", ie.Message), "WARNING");
                EucaLogger.Exception("Unexpected exception thrown while running sysprep", ie);
                return;
            }
        }

        private void buttonAnswerFile_Click(object sender, EventArgs e)
        {
            try
            {
                string answerPath = this.SysprepAnswerFile;
                if (!File.Exists(answerPath))
                {
                    MessageBox.Show(string.Format("Sysprep answer file is not found ({0})", answerPath));
                    return;
                }
                Win32_CommandResult result = SystemsUtil.SpawnProcessAndWait("notepad.exe", answerPath);
            }
            catch (Exception ie) {
                MessageBox.Show(string.Format("Unexpected exception thrown: {0}", ie.Message), "WARNING");
                EucaLogger.Exception("Unexpected exception thrown while opening sysprep answer file", ie);
                return;
            }
        }
    }

    public struct ADConfiguration
    {
        public string ADAddress { get; set; }
        public string ADUsername { get; set; }
        public string ADPassword { get; set; }
        public string ADOU { get; set; }
    }
}
