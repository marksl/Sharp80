﻿/// Sharp 80 (c) Matthew Hamilton
/// Licensed Under GPL v3. See license.txt for details.

using System;
using System.IO;
using System.Windows.Forms;

namespace Sharp80
{
    internal static class Dialogs
    {
        internal delegate void DialogDelegate();

        /// <summary>
        /// Guarantee that AfterShowDialog will always be invoked
        /// after BeforeShowDialog. Nesting is possible.
        /// </summary>
        public static event DialogDelegate BeforeShowDialog;
        public static event DialogDelegate AfterShowDialog;

        private static IWin32Window Parent { get; set; }

        public static void Initialize(IWin32Window Parent)
        {
            Dialogs.Parent = Parent;
        }

        // MESSAGE BOXES

        public static bool AskYesNo(string Question, string Caption = ProductInfo.PRODUCT_NAME)
        {
            System.Diagnostics.Debug.Assert(MainForm.IsUiThread);

            bool res;
            try
            {
                BeforeShowDialog?.Invoke();
                switch (MessageBox.Show(Parent, Question, Caption, MessageBoxButtons.YesNo, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1))
                {
                    case DialogResult.Yes:
                        res = true;
                        break;
                    default:
                        res = false;
                        break;
                }
            }
            finally
            {
                AfterShowDialog?.Invoke();
            }
            return res;
        }
        public static bool? AskYesNoCancel(string Question, string Caption = ProductInfo.PRODUCT_NAME)
        {
            System.Diagnostics.Debug.Assert(MainForm.IsUiThread);

            bool? res;
            BeforeShowDialog?.Invoke();
            switch (MessageBox.Show(Parent, Question, Caption, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question, MessageBoxDefaultButton.Button1))
            {
                case DialogResult.Yes:
                    res = true;
                    break;
                case DialogResult.No:
                    res = false;
                    break;
                default:
                    res = null;
                    break;
            }
            AfterShowDialog?.Invoke();
            return res;
        }
        public static void InformUser(string Information, string Caption = "Sharp 80")
        {
            System.Diagnostics.Debug.Assert(MainForm.IsUiThread);

            try
            {
                BeforeShowDialog?.Invoke();
                MessageBox.Show(Parent, Information, Caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            finally
            {
                AfterShowDialog?.Invoke();
            }
        }
        public static void AlertUser(string Alert, string Caption = ProductInfo.PRODUCT_NAME)
        {
            System.Diagnostics.Debug.Assert(MainForm.IsUiThread);

            try
            {
                BeforeShowDialog?.Invoke();
                MessageBox.Show(Parent, Alert, Caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            finally
            {
                AfterShowDialog?.Invoke();
            }
        }

        // PATHS AND FILE DIALOGS

        public static string UserSelectFile(bool Save, string DefaultPath, string Title, string Filter, string DefaultExtension, bool SelectFileInDialog)
        {
            System.Diagnostics.Debug.Assert(MainForm.IsUiThread);

            string dir = DefaultPath.Length > 0 ? Path.GetDirectoryName(DefaultPath) :
                                                  Storage.DocsPath;

            FileDialog dialog;

            if (Save)
            {
                dialog = new SaveFileDialog()
                {
                    OverwritePrompt = true
                };
            }
            else
            {
                dialog = new OpenFileDialog()
                {
                    Multiselect = false,
                    CheckPathExists = true
                };
            }
            dialog.Title = Title;
            dialog.FileName = (SelectFileInDialog && DefaultPath.Length > 0) ? Path.GetFileName(DefaultPath) : string.Empty;
            dialog.InitialDirectory = dir;
            dialog.ValidateNames = true;
            dialog.Filter = Filter;
            dialog.AddExtension = true;
            dialog.DefaultExt = DefaultExtension;
            dialog.CheckFileExists = !Save;
            dialog.CheckPathExists = true;

            DialogResult dr;
            try
            {
                BeforeShowDialog?.Invoke();
                dr = dialog.ShowDialog(Parent);
            }
            finally
            {
                AfterShowDialog?.Invoke();
            }

            string path = dialog.FileName;

            if (dr == DialogResult.OK && path.Length > 0)
                if (Save || File.Exists(path))
                    return path;

            return string.Empty;
        }
        public static string GetFilePath(string DefaultPath)
        {
            System.Diagnostics.Debug.Assert(MainForm.IsUiThread);

            return UserSelectFile(Save: false,
                                  DefaultPath: DefaultPath,
                                  Title: "Select File",
                                  Filter: "TRS-80 Files (*.cmd; *.bas; *.txt)|*.cmd;*.bas;*.txt|All Files (*.*)|*.*",
                                  DefaultExtension: "cmd",
                                  SelectFileInDialog: true);
        }
        public static string GetCommandFilePath(string DefaultPath)
        {
            System.Diagnostics.Debug.Assert(MainForm.IsUiThread);

            return UserSelectFile(Save: false,
                                  DefaultPath: DefaultPath,
                                  Title: "Select CMD File",
                                  Filter: "TRS-80 CMD Files (*.cmd)|*.cmd|All Files (*.*)|*.*",
                                  DefaultExtension: "cmd",
                                  SelectFileInDialog: true);
        }
        public static string GetSnapshotFile(string DefaultPath, bool Save)
        {
            System.Diagnostics.Debug.Assert(MainForm.IsUiThread);

            if (string.IsNullOrWhiteSpace(DefaultPath))
                DefaultPath = Path.Combine(Storage.AppDataPath, @"Snapshots/");

            return UserSelectFile(Save: Save,
                                  DefaultPath: DefaultPath,
                                  Title: Save ? "Save Snapshot File" : "Load Snapshot File",
                                  Filter: "TRS-80 Snapshot Files (*.snp)|*.snp|All Files (*.*)|*.*",
                                  DefaultExtension: "snp",
                                  SelectFileInDialog: !Save);
        }
        public static string GetAssemblyFile(string DefaultPath, bool Save)
        {
            System.Diagnostics.Debug.Assert(MainForm.IsUiThread);

            return UserSelectFile(Save: Save,
                                  DefaultPath: DefaultPath,
                                  Title: Save ? "Save Assembly File" : "Load Assembly File",
                                  Filter: "Z80 Assembly Files (*.asm)|*.asm|Text Files (*.txt)|*.txt|All Files (*.*)|*.*",
                                  DefaultExtension: "asm",
                                  SelectFileInDialog: true);
        }
        public static string GetTapeFilePath(string DefaultPath, bool Save)
        {
            System.Diagnostics.Debug.Assert(MainForm.IsUiThread);

            return UserSelectFile(Save: Save,
                                  DefaultPath: DefaultPath,
                                  Title: Save ? "Save Tape File" : "Load Tape File",
                                  Filter: "Cassette Files (*.cas)|*.cas|All Files (*.*)|*.*",
                                  DefaultExtension: "cas",
                                  SelectFileInDialog: true);
        }

        // TEXT FILE LAUNCHING

        public static void ShowTextFile(string Path)
        {
            if (Path.ToUpper().EndsWith(".TXT"))
                System.Diagnostics.Process.Start(Path);
        }

        // CLIPBOARD

        public static string ClipboardText
        {
            get => Clipboard.GetText(TextDataFormat.Text);
            set => Clipboard.SetText(value, TextDataFormat.Text);
        }
    }
}
