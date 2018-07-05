﻿using System;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace LocalizationUE4
{
    public partial class MainFrame : Form
    {
        //
        // DATA
        //

        public FindDialog findDlg = null;
        public InternalFormat data = null;

        //
        // Constructors and destructor
        //

        public MainFrame()
        {
            InitializeComponent();

            findDlg = new FindDialog();

            Application.Idle += new EventHandler(OnIdle);
        }

        //
        // File actions (Open, Save, Import and Export)
        //

        private void OnOpen(object sender, EventArgs e)
        {
            if (openDlg.ShowDialog(this) == DialogResult.OK)
            {
                status.Text = "Loading... Please wait.";
                data = new InternalFormat();

                string FileName = openDlg.FileName;
                string DirName = Path.GetDirectoryName(FileName);
                string Title = Path.GetFileNameWithoutExtension(FileName);
                string FileText = "";
                byte[] FileData = null;

                try
                {
                    FileText = File.ReadAllText(FileName);
                    data.LoadFromManifest(FileName, FileText);

                    string metaname = Path.ChangeExtension(FileName, "locmeta");
                    FileData = File.ReadAllBytes(metaname);
                    data.LoadFromLocMeta(FileData);

                    var dirs = Directory.GetDirectories(DirName);
                    foreach (var subdir in dirs)
                    {
                        string culture = subdir.Replace(DirName + Path.DirectorySeparatorChar, "");
                        string name = Path.Combine(subdir, Title + ".archive");
                        FileText = File.ReadAllText(name);
                        data.LoadFromArchive(culture, FileText);
                    }
                }
                catch (Exception ex)
                {
                    data = null;
                    MessageBox.Show(this, ex.Message, "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                UpdateAll();
                status.Text = "All files loaded.";
            }
        }

        private void OnSave(object sender, EventArgs e)
        {
            if (data == null)
                return;
            if (saveDlg.ShowDialog(this) == DialogResult.OK)
            {
                status.Text = "Saving... Please wait.";

                string FileName = saveDlg.FileName;
                string DirName = Path.GetDirectoryName(FileName);
                string Title = Path.GetFileNameWithoutExtension(FileName);
                string FileText = "";

                try
                {
                    FileText = data.SaveToManifest();
                    File.WriteAllText(FileName, FileText, Encoding.Unicode);

                    foreach (var culture in data.Cultures)
                    {
                        string dname = Path.Combine(DirName, culture);
                        string fname = Path.Combine(dname, Title + ".archive");
                        FileText = data.SaveToArchive(culture);
                        Directory.CreateDirectory(dname);
                        File.WriteAllText(fname, FileText, Encoding.Unicode);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }

                status.Text = "All files saved.";
            }
        }

        private void OnImport(object sender, EventArgs e)
        {
            if (importDlg.ShowDialog(this) == DialogResult.OK)
            {
                status.Text = "Importing... Please wait.";
                try
                {
                    data = ExcelConvert.Import(importDlg.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                UpdateAll();
                status.Text = "Import finished.";
            }
        }

        private void OnExport(object sender, EventArgs e)
        {
            if (data == null)
                return;
            status.Text = "Exporting... Please wait.";
            try
            {
                ExcelConvert.Export(data);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, ex.Message, "Error!", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            status.Text = "Export finished.";
        }

        //
        // Other menu actions
        //

        private void OnShowFind(object sender, EventArgs e)
        {
            if (findDlg.Visible)
                findDlg.Hide();
            else
                findDlg.Show(this);
        }

        private void OnAbout(object sender, EventArgs e)
        {
            AboutDialog dlg = new AboutDialog();
            dlg.ShowDialog(this);
        }

        private void OnExit(object sender, EventArgs e)
        {
            Close();
        }

        //
        // Form events
        //

        private void OnCultureChanged(object sender, EventArgs e)
        {
            UpdateLocaleListTranslation();
            OnSelectedIndexChanged(sender, e);
        }

        private void OnSelectedIndexChanged(object sender, EventArgs e)
        {
            if (dataGrid.SelectedRows.Count > 0)
            {
                DataGridViewRow row = dataGrid.SelectedRows[0];
                InternalKey key = (InternalKey)row.Tag;
                if (key != null)
                {
                    namespaceEdit.Text = row.Cells[1].Value.ToString();
                    keyEdit.Text = key.Key;
                    pathEdit.Text = key.Path;
                    translationEdit.Text = row.Cells[4].Value.ToString();
                }
            }
            else
            {
                namespaceEdit.Text = "";
                keyEdit.Text = "";
                pathEdit.Text = "";
                translationEdit.Text = "";
            }
        }

        private void OnTranslationKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Return && e.Control == true)
            {
                if (dataGrid.SelectedRows.Count > 0)
                {
                    string culture = cultureCombo.SelectedItem.ToString();
                    var row = dataGrid.SelectedRows[0];
                    InternalKey key = (InternalKey)row.Tag;
                    if (key != null)
                    {
                        row.Cells[4].Value = translationEdit.Text;
                        key.SetTranslationForCulture(culture, translationEdit.Text);
                    }
                }
                e.SuppressKeyPress = true;
            }
        }

        private void OnTranslationFocused(object sender, EventArgs e)
        {
            status.Text = "Press 'Ctrl' + 'Enter' to store changes.";
        }

        public void OnIdle(object sender, EventArgs e)
        {
            saveMenuBtn.Enabled = (data != null);
            saveToolBtn.Enabled = (data != null);
            exportMenuBtn.Enabled = (data != null);
            exportToolBtn.Enabled = (data != null);
            findMenuBtn.Checked = findDlg.Visible;
            findToolBtn.Checked = findDlg.Visible;
        }

        //
        // Update methods
        //

        private void UpdateAll()
        {
            UpdateCultureCombo();
            UpdateLocaleListWithoutTranslation();
            UpdateLocaleListTranslation();
            OnSelectedIndexChanged(this, null);
        }

        private void UpdateCultureCombo()
        {
            if (data == null)
            {
                cultureCombo.Items.Clear();
                nativeCulture.Text = "Native Culture";
            }
            else
            {
                cultureCombo.BeginUpdate();
                cultureCombo.Items.Clear();
                foreach (string s in data.Cultures)
                    cultureCombo.Items.Add(s);
                cultureCombo.SelectedIndex = 0;
                cultureCombo.EndUpdate();
                nativeCulture.Text = "Native Culture: [" + data.NativeCulture + "]";
            }
        }

        private void UpdateLocaleListWithoutTranslation()
        {
            dataGrid.SuspendLayout();
            dataGrid.Rows.Clear();
            if (data != null)
            {
                int index = 0;
                foreach (var ns in data.Subnamespaces)
                {
                    foreach (var rec in ns.Children)
                    {
                        foreach(var key in rec.Keys)
                        {
                            dataGrid.Rows.Add(new string[]
                            {
                                (index + 1).ToString(),
                                ns.Name, key.Key,
                                "", ""
                            });
                            dataGrid.Rows[index].Tag = key;
                            index++;
                        }
                    }
                }
                rowCount.Text = "Rows: " + dataGrid.RowCount.ToString();
            }
            else
            {
                rowCount.Text = "Rows";
            }
            dataGrid.ResumeLayout();
        }

        private void UpdateLocaleListTranslation()
        {
            dataGrid.SuspendLayout();
            if (data != null)
            {
                string culture = cultureCombo.SelectedItem.ToString();
                bool native = (culture == data.NativeCulture);

                dataGrid.Columns[3].HeaderText = native ? "Source" : "Native Culture";

                foreach (DataGridViewRow item in dataGrid.Rows)
                {
                    InternalKey key = (InternalKey)item.Tag;
                    item.Cells[3].Value = native ? key.parent.Source : key.GetTranslationForCulture(data.NativeCulture);
                    item.Cells[4].Value = key.GetTranslationForCulture(culture);
                }
            }
            dataGrid.ResumeLayout();
        }

        //
        // Find methods
        //

        public int LoopIndex(int index, int count, bool down)
        {
            if (count < 1)
                return 0;
            index += down ? 1 : -1;
            if (index < 0)
                return count - 1;
            if (index < count)
                return index;
            return 0;
        }

        private bool IsMatch(int index, string pattern, bool matchCase)
        {
            RegexOptions options = matchCase ? RegexOptions.None : RegexOptions.IgnoreCase;
            if (index >= 0 && index < dataGrid.Rows.Count)
            {
                var row = dataGrid.Rows[index];
                string source = row.Cells[3].Value.ToString();
                string translation = row.Cells[4].Value.ToString();
                bool source_match = Regex.IsMatch(source, pattern, options);
                bool translation_match = Regex.IsMatch(translation, pattern, options);
                return source_match || translation_match;
            }
            return false;
        }

        public void FindNext(string text, bool directionDown, bool wholeWords, bool matchCase)
        {
            int count = dataGrid.Rows.Count;

            if (count < 1 || text.Trim() == "")
                return;

            int findIndex = 0; 
            int stopIndex = dataGrid.RowCount - 1;
            if (dataGrid.SelectedRows.Count > 0)
            {
                stopIndex = findIndex = dataGrid.SelectedRows[0].Index;
                findIndex = LoopIndex(findIndex, count, directionDown);
            }

            string pattern = wholeWords ? string.Format("\\b{0}\\b", text) : text;

            int resultIndex = -1;
            while (findIndex != stopIndex)
            {
                if (IsMatch(findIndex, pattern, matchCase))
                {
                    resultIndex = findIndex;
                    break;
                }
                else
                    findIndex = LoopIndex(findIndex, count, directionDown);
            }

            if (resultIndex >= 0 && resultIndex < count)
            {
                var row = dataGrid.Rows[resultIndex];
                row.Selected = true;
                dataGrid.CurrentCell = row.Cells[0];
            }
            else
                System.Media.SystemSounds.Beep.Play();
        }
    }
}