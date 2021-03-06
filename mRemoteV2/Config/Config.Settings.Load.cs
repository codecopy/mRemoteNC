using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using System.Xml;
using Microsoft.VisualBasic;
using mRemoteNC.App;
using My;
using System.Linq;
using WeifenLuo.WinFormsUI.Docking;

//using mRemoteNC.Runtime;

//using System.Environment;

namespace mRemoteNC.Config
{
    namespace SettingsManager
    {
        public class Load
        {
            #region Public Properties

            private frmMain _MainForm;

            public frmMain MainForm
            {
                get { return this._MainForm; }
                set { this._MainForm = value; }
            }

            #endregion Public Properties

            #region Public Methods

            public Load(frmMain MainForm)
            {
                this._MainForm = MainForm;
            }

            public void Load_Settings()
            {
                try
                {
                    // Migrate settings from previous version
                    if (Settings.Default.DoUpgrade)
                    {
                        Settings.Default.Upgrade();
                        Settings.Default.DoUpgrade = false;

                        // Clear pending update flag
                        // This is used for automatic updates, not for settings migration, but it
                        // needs to be cleared here because we know that we just updated.
                        Settings.Default.UpdatePending = false;
                        Settings.Default.Save();
                    }

                    mRemoteNC.SupportedCultures.InstantiateSingleton();
                    if (Settings.Default.OverrideUICulture != "" &&
                        mRemoteNC.SupportedCultures.IsNameSupported((string)Settings.Default.OverrideUICulture))
                    {
                        System.Threading.Thread.CurrentThread.CurrentUICulture =
                            new System.Globalization.CultureInfo((string)Settings.Default.OverrideUICulture);
                        Runtime.Log.InfoFormat("Override Culture: {0}/{1}",
                                               System.Threading.Thread.CurrentThread.CurrentUICulture.Name,
                                               System.Threading.Thread.CurrentThread.CurrentUICulture.NativeName);
                    }

                    this._MainForm.WindowState = FormWindowState.Normal;
                    if (Settings.Default.MainFormState == FormWindowState.Normal)
                    {
                        if (!Settings.Default.MainFormLocation.IsEmpty)
                        {
                            this._MainForm.Location = Settings.Default.MainFormLocation;
                        }
                        if (!Settings.Default.MainFormSize.IsEmpty)
                        {
                            this._MainForm.Size = Settings.Default.MainFormSize;
                        }
                    }
                    else
                    {
                        if (!Settings.Default.MainFormRestoreLocation.IsEmpty)
                        {
                            this._MainForm.Location = Settings.Default.MainFormRestoreLocation;
                        }
                        if (!Settings.Default.MainFormRestoreSize.IsEmpty)
                        {
                            this._MainForm.Size = Settings.Default.MainFormRestoreSize;
                        }
                    }
                    if (Settings.Default.MainFormState == FormWindowState.Maximized)
                    {
                        this._MainForm.WindowState = FormWindowState.Maximized;
                    }

                    // Make sure the form is visible on the screen
                    const int minHorizontal = 300;
                    const int minVertical = 150;
                    System.Drawing.Rectangle screenBounds = Screen.FromHandle(this._MainForm.Handle).Bounds;
                    System.Drawing.Rectangle newBounds = this._MainForm.Bounds;

                    if (newBounds.Right < screenBounds.Left + minHorizontal)
                    {
                        newBounds.X = screenBounds.Left + minHorizontal - newBounds.Width;
                    }
                    if (newBounds.Left > screenBounds.Right - minHorizontal)
                    {
                        newBounds.X = screenBounds.Right - minHorizontal;
                    }
                    if (newBounds.Bottom < screenBounds.Top + minVertical)
                    {
                        newBounds.Y = screenBounds.Top + minVertical - newBounds.Height;
                    }
                    if (newBounds.Top > screenBounds.Bottom - minVertical)
                    {
                        newBounds.Y = screenBounds.Bottom - minVertical;
                    }

                    this._MainForm.Location = newBounds.Location;

                    if (Settings.Default.MainFormKiosk == true)
                    {
                        frmMain.defaultInstance.fullscreenManager.EnterFullscreen();
                    }

                    if (Settings.Default.UseCustomPuttyPath)
                    {
                        Connection.PuttyBase.PuttyPath = Settings.Default.CustomPuttyPath;
                    }
                    else
                    {
                        Connection.PuttyBase.PuttyPath =
                            (new Microsoft.VisualBasic.ApplicationServices.WindowsFormsApplicationBase()).Info.
                                DirectoryPath + "\\PuTTYNG.exe";
                    }

                    if (Settings.Default.ShowSystemTrayIcon)
                    {
                        Runtime.NotificationAreaIcon = new Tools.Controls.NotificationAreaIcon();
                    }

                    if (Settings.Default.AutoSaveEveryMinutes > 0)
                    {
                        this._MainForm.tmrAutoSave.Interval =
                            System.Convert.ToInt32(Settings.Default.AutoSaveEveryMinutes * 60000);
                        this._MainForm.tmrAutoSave.Enabled = true;
                    }

                    Settings.Default.ConDefaultPassword =
                        Security.Crypt.Decrypt(Settings.Default.ConDefaultPassword,
                                               AppInfo.General.EncryptionKey);

                    LoadPanelsFromXML();
                    LoadExternalAppsFromXML();
                    LoadQuickTextsFromXML();

                    if (Settings.Default.ResetToolbars == false&&!Settings.Default.FirstStart)
                    {
                        LoadToolbarsFromSettings();
                    }
                    else
                    {
                        SetToolbarsDefault();
                    }
                }
                catch (Exception ex)
                {
                    Runtime.Log.Error("Loading settings failed" + Constants.vbNewLine + ex.Message);
                    //mC.AddMessage(Messages.MessageClass.ErrorMsg, "Loading settings failed" & vbNewLine & ex.Message, True)
                }
            }

            public void SetToolbarsDefault()
            {
                try
                {
                    MainForm.tsContainer.SuspendLayout();
                    ClearToolStrips();
                    //Placeholder: Add new toolbar here
                    ToolStripPanelFromString("top").Join(MainForm.tsQuickTexts, 0);
                    ToolStripPanelFromString("top").Join(MainForm.tsExternalTools, 0);
                    ToolStripPanelFromString("top").Join(MainForm.tsQuickConnect, 0);
                    ToolStripPanelFromString("top").Join(MainForm.msMain, 0);
                    MainForm.msMain.Visible = true;
                    MainForm.tsQuickConnect.Visible = true;
                    MainForm.tsExternalTools.Visible = false;
                    MainForm.tsQuickTexts.Visible = false;
                    MainForm.tsContainer.ResumeLayout(true);
                }
                catch (Exception ex)
                {
                    Runtime.Log.Error("SetToolbarsDefault failed" + Constants.vbNewLine + ex);
                    SetToolbarsDefault();
                }
            }

            private void LoadToolbarsFromSettings()
            {
                try
                {
                    var dic = new Dictionary<ToolStrip, ToolStripConfig>
                    {
                        //Placeholder: ToolBar
                        {MainForm.tsExternalTools, ToolStripConfig.FromXMLString(Settings.Default.tsExternalTools)},
                        {MainForm.tsQuickConnect, ToolStripConfig.FromXMLString(Settings.Default.tsQuickConnect)},
                        {MainForm.msMain, ToolStripConfig.FromXMLString(Settings.Default.msMain)},
                        //{MainForm.ToolStrip1, ToolStripConfig.FromXMLString(Settings.Default.ToolStrip1)},
                        {MainForm.tsQuickTexts, ToolStripConfig.FromXMLString(Settings.Default.tsQuickTexts)}
                    }.OrderBy(pair => pair.Value.Parent)
                    .ThenBy(pair => pair.Value.Visible)
                    .ThenBy(pair => pair.Value.Row)
                    .ThenByDescending(pair => pair.Value.Index);

                    if (dic.Any(pair => pair.Value.IsBroken))
                    {
                        SetToolbarsDefault();
                        return;
                    }

                    MainForm.tsContainer.SuspendLayout();
                    ClearToolStrips();
                    foreach (var config in dic)
                    {
                        config.Key.Dock = config.Value.DockStyle;
                        config.Key.Visible = config.Value.Visible;

                        ToolStripPanelFromString(config.Value.Parent).Join(config.Key, config.Value.Row);
                    }
                    foreach (var toolStripConfig in dic)
                    {
                        toolStripConfig.Key.Location = toolStripConfig.Value.Location;
                        toolStripConfig.Key.Left = toolStripConfig.Value.Left;
                        toolStripConfig.Key.Top = toolStripConfig.Value.Top;
                    }
                    MainForm.tsContainer.ResumeLayout(true);
                }
                catch (Exception ex)
                {
                    Runtime.Log.Error("LoadToolbarsFromSettings failed" + Constants.vbNewLine + ex);
                    SetToolbarsDefault();
                }
            }

            private void ClearToolStrips()
            {
                MainForm.tsContainer.RightToolStripPanel.Controls.Clear();
                MainForm.tsContainer.LeftToolStripPanel.Controls.Clear();
                MainForm.tsContainer.TopToolStripPanel.Controls.Clear();
                MainForm.tsContainer.BottomToolStripPanel.Controls.Clear();
            }

            private ToolStripPanel ToolStripPanelFromString(string Panel)
            {
                switch (Panel.ToLower())
                {
                    case "top":
                        return MainForm.tsContainer.TopToolStripPanel;
                    case "bottom":
                        return MainForm.tsContainer.BottomToolStripPanel;
                    case "left":
                        return MainForm.tsContainer.LeftToolStripPanel;
                    case "right":
                        return MainForm.tsContainer.RightToolStripPanel;
                    default:
                        return MainForm.tsContainer.TopToolStripPanel;
                }
            }

            public void LoadPanelsFromXML()
            {
                try
                {
                    Runtime.Windows.treePanel = null;
                    Runtime.Windows.configPanel = null;
                    Runtime.Windows.errorsPanel = null;

                    while (MainForm.pnlDock.Contents.Count > 0)
                    {
                        ((DockContent)MainForm.pnlDock.Contents[0]).Close();
                    }

                    Runtime.Startup.CreatePanels();

                    string oldPath =
                        (string)
                        (System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData) + "\\" +
                         (new Microsoft.VisualBasic.ApplicationServices.WindowsFormsApplicationBase()).Info.ProductName +
                         "\\" + mRemoteNC.AppInfo.Settings.LayoutFileName);
                    string newPath =
                        (string)
                        (mRemoteNC.AppInfo.Settings.SettingsPath + "\\" + mRemoteNC.AppInfo.Settings.LayoutFileName);
                    if (File.Exists(newPath))
                    {
                        MainForm.pnlDock.LoadFromXml(newPath, GetContentFromPersistString);

                    }
                    else 
                    {
                        if ((!AppInfo.General.IsPortable)&&File.Exists(oldPath))
                        {
                            MainForm.pnlDock.LoadFromXml(oldPath, GetContentFromPersistString);
                        }
                        else
                        {
                            Runtime.Startup.SetDefaultLayout();
                        }
                    }
                }
                catch (Exception ex)
                {
                    Runtime.Log.Error("LoadPanelsFromXML failed" + Constants.vbNewLine + ex.Message);
                    //mC.AddMessage(Messages.MessageClass.ErrorMsg, "LoadPanelsFromXML failed" & vbNewLine & ex.Message, True)
                }
            }

            public void LoadQuickTextsFromXML()
            {
                string oldPath =
                    (string)
                    (System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData) + "\\" +
                     (new Microsoft.VisualBasic.ApplicationServices.WindowsFormsApplicationBase()).Info.ProductName +
                     "\\" + mRemoteNC.AppInfo.Settings.QuickTextsFilesName);
                string newPath =
                    (string)
                    (mRemoteNC.AppInfo.Settings.SettingsPath + "\\" + mRemoteNC.AppInfo.Settings.QuickTextsFilesName);
                XmlDocument xDom = new XmlDocument();
                if (File.Exists(newPath))
                {
                    xDom.Load(newPath);
                }
                else 
                {
                    if ((!AppInfo.General.IsPortable) && File.Exists(oldPath))
                    {
                        xDom.Load(oldPath);
                    }
                    else
                    {
                        return;
                    }
                }

                foreach (XmlElement xEl in xDom.DocumentElement.ChildNodes)
                {
                    Tools.QuickText extA = new Tools.QuickText();
                    extA.DisplayName = xEl.Attributes["DisplayName"].Value;
                    extA.Text = xEl.Attributes["Text"].Value;
                    Runtime.QuickTexts.Add(extA);
                }

                xDom = null;

                frmMain.Default.AddQuickTextsToToolBar();
            }

            public void LoadExternalAppsFromXML()
            {
                string oldPath =
                    (string)
                    (System.Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData) + "\\" +
                     (new Microsoft.VisualBasic.ApplicationServices.WindowsFormsApplicationBase()).Info.ProductName +
                     "\\" + mRemoteNC.AppInfo.Settings.ExtAppsFilesName);
                string newPath =
                    (string)
                    (mRemoteNC.AppInfo.Settings.SettingsPath + "\\" + mRemoteNC.AppInfo.Settings.ExtAppsFilesName);
                XmlDocument xDom = new XmlDocument();
                if (File.Exists(newPath))
                {
                    xDom.Load(newPath);
                }
                else 
                {
                    if ((!AppInfo.General.IsPortable)&&File.Exists(oldPath))
                    {
                        xDom.Load(oldPath);
                    }
                    else
                    {
                        return;
                    }
                }

                foreach (XmlElement xEl in xDom.DocumentElement.ChildNodes)
                {
                    Tools.ExternalTool extA = new Tools.ExternalTool();
                    extA.DisplayName = xEl.Attributes["DisplayName"].Value;
                    extA.FileName = xEl.Attributes["FileName"].Value;
                    extA.Arguments = xEl.Attributes["Arguments"].Value;

                    if (xEl.HasAttribute("WaitForExit"))
                    {
                        extA.WaitForExit = Convert.ToBoolean(xEl.Attributes["WaitForExit"].Value);
                    }

                    if (xEl.HasAttribute("TryToIntegrate"))
                    {
                        extA.TryIntegrate = Convert.ToBoolean(xEl.Attributes["TryToIntegrate"].Value);
                    }

                    Runtime.ExternalTools.Add(extA);
                }

                MainForm.SwitchToolBarText(System.Convert.ToBoolean(Settings.Default.ExtAppsTBShowText));

                xDom = null;

                frmMain.Default.AddExternalToolsToToolBar();
            }

            #endregion Public Methods

            #region Private Methods

            private IDockContent GetContentFromPersistString(string persistString)
            {
                // pnlLayout.xml persistence XML fix for refactoring to mRemoteNC
                if (persistString.StartsWith("mRemote."))
                {
                    persistString = persistString.Replace("mRemote.", "mRemoteNC.");
                }

                try
                {
                    if (persistString == typeof(UI.Window.Config).ToString())
                    {
                        return Runtime.Windows.configPanel;
                    }

                    if (persistString == typeof(UI.Window.Tree).ToString())
                    {
                        return Runtime.Windows.treePanel;
                    }

                    if (persistString == typeof(UI.Window.ErrorsAndInfos).ToString())
                    {
                        return Runtime.Windows.errorsPanel;
                    }

                    if (persistString == typeof(UI.Window.Sessions).ToString())
                    {
                        return Runtime.Windows.sessionsPanel;
                    }

                    if (persistString == typeof(UI.Window.ScreenshotManager).ToString())
                    {
                        return Runtime.Windows.screenshotPanel;
                    }
                }
                catch (Exception ex)
                {
                    Runtime.Log.Error("GetContentFromPersistString failed" + Constants.vbNewLine + ex.Message);
                    //mC.AddMessage(Messages.MessageClass.ErrorMsg, "GetContentFromPersistString failed" & vbNewLine & ex.Message, True)
                }

                return null;
            }

            #endregion Private Methods
        }
    }
}