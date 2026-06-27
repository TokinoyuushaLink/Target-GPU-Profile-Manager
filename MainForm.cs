using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

namespace GpuPreference;

public class MainForm : Form
{
    readonly ListBox  _catList;
    readonly ListView _lv;

    static readonly string[] PrefLabels = ["系统默认", "省电（核显）", "高性能（独显）"];

    string? _selectedCategory = null;  // null = 未分类视图, 非null = 具体分类名

    public MainForm()
    {
        Text            = "GPU 偏好管理";
        Size            = new Size(820, 520);
        StartPosition   = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.Sizable;

        // Form 自身双缓冲
        DoubleBuffered = true;

        // ── 顶部工具栏 ───────────────────────────────────────────
        var toolbar = new Panel { Dock = DockStyle.Top, Height = 32 };

        var lblCat    = new Label  { Text = "分类", AutoSize = true, Font = new Font(Font, FontStyle.Bold) };
        var btnAddCat = new Button { Text = "+", Width = 26 };
        var btnDelCat = new Button { Text = "−", Width = 26 };
        var btnAdd    = new Button { Text = "添加程序", AutoSize = true };
        var btnDel    = new Button { Text = "删除选中", AutoSize = true };

        btnAddCat.Click += OnAddCategory;
        btnDelCat.Click += OnDeleteCategory;
        btnAdd.Click    += OnAdd;
        btnDel.Click    += OnDelete;

        toolbar.Controls.AddRange([lblCat, btnAddCat, btnDelCat, btnAdd, btnDel]);

        void LayoutToolbar()
        {
            int h = toolbar.ClientSize.Height;
            int cy(Control c) => (h - c.Height) / 2;
            lblCat.Location    = new Point(6, cy(lblCat));
            btnAddCat.Location = new Point(lblCat.Right + 6,        cy(btnAddCat));
            btnDelCat.Location = new Point(btnAddCat.Right + 2,     cy(btnDelCat));
            btnDel.Location    = new Point(toolbar.Width - 4 - btnDel.Width, cy(btnDel));
            btnAdd.Location    = new Point(btnDel.Left - 4 - btnAdd.Width,   cy(btnAdd));
        }
        toolbar.SizeChanged += (_, _) => LayoutToolbar();

        // ── 左右分割容器 ─────────────────────────────────────────
        var split = new SplitContainer { Dock = DockStyle.Fill, FixedPanel = FixedPanel.Panel1 };
        Load += (_, _) =>
        {
            split.Panel1MinSize    = 80;
            split.Panel2MinSize    = 200;
            split.SplitterDistance = 180;
        };
        // SplitContainer 双缓冲
        SetDoubleBuffered(split);

        // ── 左侧分类列表 ─────────────────────────────────────────
        _catList = new ListBox
        {
            Dock           = DockStyle.Fill,
            BorderStyle    = BorderStyle.None,
            IntegralHeight = false,
            SelectionMode  = SelectionMode.MultiExtended,
        };
        _catList.SelectedIndexChanged += OnCategoryChanged;
        _catList.MouseDown            += OnCatRightClick;
        split.Panel1.Controls.Add(_catList);

        // ── 右侧 ListView ────────────────────────────────────────
        _lv = new ListView
        {
            Dock          = DockStyle.Fill,
            View          = View.Details,
            FullRowSelect = true,
            GridLines     = true,
            MultiSelect   = true,
        };
        // ListView 双缓冲（Win32 扩展样式）
        SetDoubleBuffered(_lv);

        _lv.Columns.Add("APP路径", -1);
        _lv.Columns.Add("备注",    -1);
        _lv.Columns.Add("选项菜单", -1);

        void ResizeColumns()
        {
            const int prefW = 120;
            int total = _lv.ClientSize.Width;
            int note  = total * 25 / 100;
            int path  = total - prefW - note;
            if (path < 50) return;
            _lv.BeginUpdate();
            _lv.Columns[0].Width = path;
            _lv.Columns[1].Width = note;
            _lv.Columns[2].Width = prefW;
            _lv.EndUpdate();
        }
        _lv.SizeChanged += (_, _) => ResizeColumns();

        _lv.MouseClick += OnListViewClick;
        _lv.KeyDown    += OnListViewKeyDown;
        _lv.DragEnter  += OnDragEnter;
        _lv.DragDrop   += OnDragDrop;

        split.Panel2.Controls.Add(_lv);

        // ── 组装 ─────────────────────────────────────────────────
        Controls.Add(split);
        Controls.Add(toolbar);

        // AllowDrop 在消息泵第一次空闲时设置，此时 OLE 已完全初始化
        EventHandler? idleHandler = null;
        idleHandler = (_, _) =>
        {
            _lv.AllowDrop = true;
            Application.Idle -= idleHandler;
        };
        Application.Idle += idleHandler;

        LayoutToolbar();
        Refresh();
    }

    // 通过反射对任意控件开启双缓冲
    static void SetDoubleBuffered(Control c)
    {
        typeof(Control)
            .GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(c, true);
    }

    // ── 刷新 ─────────────────────────────────────────────────────

    public new void Refresh()
    {
        RefreshCategoryList();
        RefreshListView();
    }

    void RefreshCategoryList()
    {
        var prev = _catList.SelectedItem as string;
        _catList.Items.Clear();
        _catList.Items.Add("未分类");
        foreach (var cat in CategoryStore.Categories)
            _catList.Items.Add(cat.Name);

        int idx = prev != null ? _catList.Items.IndexOf(prev) : 0;
        _catList.SelectedIndex = idx >= 0 ? idx : 0;
    }

    void RefreshListView()
    {
        var all = GpuRegistry.ListEntries();
        _lv.BeginUpdate();
        _lv.Items.Clear();
        foreach (var e in all)
        {
            var cat = CategoryStore.GetCategory(e.Exe);
            if (_selectedCategory == null && cat != null) continue;
            else if (_selectedCategory != null && cat != _selectedCategory) continue;

            var item = new ListViewItem(e.Exe);
            item.SubItems.Add(CategoryStore.GetNote(e.Exe) ?? "");
            item.SubItems.Add(PrefLabels[(int)e.Pref]);
            item.Tag = e;
            _lv.Items.Add(item);
        }
        _lv.EndUpdate();
    }

    // ── 分类栏事件 ───────────────────────────────────────────────

    void OnCategoryChanged(object? sender, EventArgs e)
    {
        // 多选或选中"全部"时不过滤
        if (_catList.SelectedItems.Count != 1)
        {
            _selectedCategory = null;
        }
        else
        {
            var sel = _catList.SelectedItem as string;
            _selectedCategory = (sel == "未分类" || sel == null) ? null : sel;
        }
        RefreshListView();
    }

    void OnCatRightClick(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Right) return;
        int idx = _catList.IndexFromPoint(e.Location);
        if (idx < 0) return;
        if (_catList.Items[idx] as string == "未分类") return;

        // 右键点到未选中项时，切换为单选该项
        if (!_catList.SelectedIndices.Contains(idx))
        {
            _catList.ClearSelected();
            _catList.SelectedIndex = idx;
        }

        var menu = new ContextMenuStrip();

        if (_catList.SelectedItems.Count == 1)
        {
            var catName = _catList.SelectedItem as string ?? "";
            var miRename = new ToolStripMenuItem("重命名");
            miRename.Click += (_, _) => RenameCategory(catName);
            menu.Items.Add(miRename);
            menu.Items.Add(new ToolStripSeparator());
        }

        var miDelete = new ToolStripMenuItem("删除分类");
        miDelete.Click += (_, _) => OnDeleteCategory(null, EventArgs.Empty);
        menu.Items.Add(miDelete);
        menu.Show(Cursor.Position);
    }

    void OnAddCategory(object? sender, EventArgs e)
    {
        var name = InputDialog.Show(this, "新建分类", "分类名称：");
        if (string.IsNullOrWhiteSpace(name)) return;
        CategoryStore.AddCategory(name.Trim());
        Refresh();
    }

    void OnDeleteCategory(object? sender, EventArgs e)
    {
        var cats = _catList.SelectedItems.Cast<string>()
            .Where(s => s != "未分类").ToList();
        if (cats.Count == 0) return;

        string msg = cats.Count == 1
            ? $"删除分类「{cats[0]}」？\n（其中的程序变为未分类，不影响 GPU 偏好）"
            : $"删除选中的 {cats.Count} 个分类？\n（其中的程序变为未分类，不影响 GPU 偏好）";

        if (MessageBox.Show(msg, "删除分类", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        foreach (var cat in cats)
            CategoryStore.DeleteCategory(cat);
        _selectedCategory = null;
        Refresh();
    }

    void RenameCategory(string catName)
    {
        var newName = InputDialog.Show(this, "重命名分类", "新名称：", catName);
        if (string.IsNullOrWhiteSpace(newName) || newName == catName) return;
        CategoryStore.RenameCategory(catName, newName.Trim());
        Refresh();
    }

    void DeleteCategory(string catName)
    {
        var r = MessageBox.Show(
            $"删除分类「{catName}」？\n（其中的程序变为未分类，不影响 GPU 偏好）",
            "删除分类", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
        if (r != DialogResult.Yes) return;
        CategoryStore.DeleteCategory(catName);
        _selectedCategory = null;
        Refresh();
    }

    // ── ListView 事件 ────────────────────────────────────────────

    void OnListViewClick(object? sender, MouseEventArgs e)
    {
        var hit = _lv.HitTest(e.Location);
        if (hit.Item is null) return;

        bool isNoteCol = hit.SubItem == hit.Item.SubItems[1];
        bool isMenuCol = hit.SubItem == hit.Item.SubItems[2];

        if (e.Button == MouseButtons.Right || (e.Button == MouseButtons.Left && isMenuCol))
            ShowEntryMenu(hit.Item);
        else if (e.Button == MouseButtons.Left && isNoteCol)
            EditNote(hit.Item);
    }

    void EditNote(ListViewItem item)
    {
        if (item.Tag is not GpuEntry entry) return;
        var current = CategoryStore.GetNote(entry.Exe) ?? "";
        var newNote = InputDialog.Show(this, "编辑备注", "备注：", current);
        if (newNote == null) return;
        CategoryStore.SetNote(entry.Exe, newNote.Trim());
        item.SubItems[1].Text = newNote.Trim();
    }

    void ShowEntryMenu(ListViewItem item)
    {
        if (item.Tag is not GpuEntry entry) return;

        var menu = new ContextMenuStrip();

        var selected = _lv.SelectedItems.Count > 1
            ? _lv.SelectedItems.Cast<ListViewItem>().Select(i => i.Tag as GpuEntry).OfType<GpuEntry>().ToList()
            : [entry];

        for (int i = 0; i < PrefLabels.Length; i++)
        {
            var pref  = (GpuPref)i;
            var label = PrefLabels[i];
            var mi    = new ToolStripMenuItem(label) { Checked = selected.Count == 1 && entry.Pref == pref };
            mi.Click += (_, _) => { foreach (var en in selected) GpuRegistry.SetEntry(en.Exe, pref); RefreshListView(); };
            menu.Items.Add(mi);
        }

        menu.Items.Add(new ToolStripSeparator());

        var entries = _lv.SelectedItems.Count > 1
            ? _lv.SelectedItems.Cast<ListViewItem>().Select(i => i.Tag as GpuEntry).OfType<GpuEntry>().ToList()
            : [entry];

        var moveMenu = new ToolStripMenuItem("移动到分类");
        var miNoCat  = new ToolStripMenuItem("（未分类）")
            { Checked = entries.Count == 1 && CategoryStore.GetCategory(entry.Exe) == null };
        miNoCat.Click += (_, _) => { foreach (var en in entries) CategoryStore.ClearCategory(en.Exe); Refresh(); };
        moveMenu.DropDownItems.Add(miNoCat);

        if (CategoryStore.Categories.Any())
        {
            moveMenu.DropDownItems.Add(new ToolStripSeparator());
            foreach (var cat in CategoryStore.Categories)
            {
                var catName = cat.Name;
                var mi = new ToolStripMenuItem(catName)
                    { Checked = entries.Count == 1 && CategoryStore.GetCategory(entry.Exe) == catName };
                mi.Click += (_, _) => { foreach (var en in entries) CategoryStore.SetCategory(en.Exe, catName); Refresh(); };
                moveMenu.DropDownItems.Add(mi);
            }
        }

        menu.Items.Add(moveMenu);
        menu.Items.Add(new ToolStripSeparator());
        var del = new ToolStripMenuItem("删除此条目");
        del.Click += (_, _) => ConfirmDelete(entry);
        menu.Items.Add(del);

        menu.Show(Cursor.Position);
    }

    void OnListViewKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Delete && _lv.SelectedItems.Count > 0)
            DeleteSelected();
    }

    // ── 拖放导入 ─────────────────────────────────────────────────

    void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data?.GetDataPresent(DataFormats.FileDrop) != true) return;
        var files = e.Data.GetData(DataFormats.FileDrop) as string[];
        if (files?.Any(f => f.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
                         || f.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase)) == true)
            e.Effect = DragDropEffects.Copy;
    }

    void OnDragDrop(object? sender, DragEventArgs e)
    {
        var files = e.Data?.GetData(DataFormats.FileDrop) as string[];
        if (files == null) return;
        foreach (var file in files)
        {
            if (file.EndsWith(".lnk", StringComparison.OrdinalIgnoreCase))
            {
                var target = ResolveLnk(file);
                if (target != null && target.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    AddExe(target);
            }
            else if (file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                AddExe(file);
            }
        }
    }

    static string? ResolveLnk(string lnkPath)
    {
        try
        {
            var shell = new ShellLink();
            var persist = (IPersistFile)shell;
            persist.Load(lnkPath, 0);
            var link = (IShellLinkW)shell;
            var buf = new char[260];
            link.GetPath(buf, buf.Length, IntPtr.Zero, 0);
            var path = new string(buf).TrimEnd('\0');
            Marshal.ReleaseComObject(shell);
            return string.IsNullOrEmpty(path) ? null : path;
        }
        catch { return null; }
    }

    // ── 添加 / 删除 ──────────────────────────────────────────────

    async void OnAdd(object? sender, EventArgs e)
    {
        var picked = await Task.Factory.StartNew(() =>
        {
            using var dlg = new OpenFileDialog
            {
                Title  = "选择要添加的程序",
                Filter = "可执行文件 (*.exe)|*.exe|所有文件|*.*",
            };
            return dlg.ShowDialog() == DialogResult.OK ? dlg.FileName : null;
        }, CancellationToken.None, TaskCreationOptions.None, StaTaskScheduler.Instance);

        if (picked == null) return;
        AddExe(picked);
    }

    void AddExe(string path)
    {
        using var prefDlg = new PrefSelectDialog(Path.GetFileName(path));
        if (prefDlg.ShowDialog(this) != DialogResult.OK) return;

        GpuRegistry.SetEntry(path, prefDlg.SelectedPref);
        if (!string.IsNullOrEmpty(_selectedCategory))
            CategoryStore.SetCategory(path, _selectedCategory);

        // 首次添加时用文件名作为默认备注，之后可自由修改
        if (string.IsNullOrEmpty(CategoryStore.GetNote(path)))
            CategoryStore.SetNote(path, Path.GetFileNameWithoutExtension(path));

        Refresh();
    }

    void OnDelete(object? sender, EventArgs e)
    {
        if (_lv.SelectedItems.Count == 0)
        {
            MessageBox.Show("请先选择一条记录", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        DeleteSelected();
    }

    void DeleteSelected()
    {
        var entries = _lv.SelectedItems.Cast<ListViewItem>()
            .Select(i => i.Tag as GpuEntry).OfType<GpuEntry>().ToList();
        if (entries.Count == 0) return;

        string msg = entries.Count == 1
            ? $"确认删除：\n{entries[0].Exe}"
            : $"确认删除选中的 {entries.Count} 条记录？";

        if (MessageBox.Show(msg, "删除确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;

        foreach (var entry in entries)
        {
            GpuRegistry.DeleteEntry(entry.Exe);
            CategoryStore.RemoveExe(entry.Exe);
        }
        Refresh();
    }

    void ConfirmDelete(GpuEntry entry)
    {
        if (MessageBox.Show($"确认删除：\n{entry.Exe}", "删除确认",
            MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
        GpuRegistry.DeleteEntry(entry.Exe);
        CategoryStore.RemoveExe(entry.Exe);
        Refresh();
    }
}

// ── 偏好选择对话框 ────────────────────────────────────────────────

public class PrefSelectDialog : Form
{
    public GpuPref SelectedPref { get; private set; } = GpuPref.HighPerformance;

    public PrefSelectDialog(string fileName)
    {
        Text            = "选择 GPU 偏好";
        Size            = new Size(300, 180);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox     = false;
        MinimizeBox     = false;
        StartPosition   = FormStartPosition.CenterParent;

        var label = new Label { Text = $"为 {fileName} 选择 GPU 偏好：", AutoSize = true, Location = new Point(12, 12) };
        var combo = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Location = new Point(12, 38), Width = 260 };
        combo.Items.AddRange(["系统默认", "省电（核显）", "高性能（独显）"]);
        combo.SelectedIndex = 2;

        var btnOk     = new Button { Text = "确定", DialogResult = DialogResult.OK,    Location = new Point(100, 100), Width = 80 };
        var btnCancel = new Button { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(192, 100), Width = 80 };
        btnOk.Click += (_, _) => SelectedPref = (GpuPref)combo.SelectedIndex;

        AcceptButton = btnOk;
        CancelButton = btnCancel;
        Controls.AddRange([label, combo, btnOk, btnCancel]);
    }
}

// ── 单行输入对话框 ────────────────────────────────────────────────

public static class InputDialog
{
    public static string? Show(IWin32Window? owner, string title, string prompt, string defaultValue = "")
    {
        using var form = new Form
        {
            Text            = title,
            Size            = new Size(320, 150),
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox     = false,
            MinimizeBox     = false,
            StartPosition   = FormStartPosition.CenterParent,
        };
        var label     = new Label   { Text = prompt, AutoSize = true, Location = new Point(12, 12) };
        var textBox   = new TextBox { Text = defaultValue, Location = new Point(12, 34), Width = 280 };
        var btnOk     = new Button  { Text = "确定", DialogResult = DialogResult.OK,    Location = new Point(120, 70), Width = 80 };
        var btnCancel = new Button  { Text = "取消", DialogResult = DialogResult.Cancel, Location = new Point(212, 70), Width = 80 };

        form.AcceptButton = btnOk;
        form.CancelButton = btnCancel;
        form.Controls.AddRange([label, textBox, btnOk, btnCancel]);

        return form.ShowDialog(owner) == DialogResult.OK ? textBox.Text : null;
    }
}

// ── Shell COM（解析 .lnk）────────────────────────────────────────

[ComImport, Guid("00021401-0000-0000-C000-000000000046")]
class ShellLink { }

[ComImport, Guid("000214F9-0000-0000-C000-000000000046"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IShellLinkW
{
    void GetPath([Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] char[] pszFile, int cch, IntPtr pfd, uint fFlags);
    void GetIDList(out IntPtr ppidl);
    void SetIDList(IntPtr pidl);
    void GetDescription([Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] char[] pszName, int cch);
    void SetDescription([MarshalAs(UnmanagedType.LPWStr)] string pszName);
    void GetWorkingDirectory([Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] char[] pszDir, int cch);
    void SetWorkingDirectory([MarshalAs(UnmanagedType.LPWStr)] string pszDir);
    void GetArguments([Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] char[] pszArgs, int cch);
    void SetArguments([MarshalAs(UnmanagedType.LPWStr)] string pszArgs);
    void GetHotkey(out short pwHotkey);
    void SetHotkey(short wHotkey);
    void GetShowCmd(out int piShowCmd);
    void SetShowCmd(int iShowCmd);
    void GetIconLocation([Out, MarshalAs(UnmanagedType.LPArray, SizeParamIndex = 1)] char[] pszIconPath, int cch, out int piIcon);
    void SetIconLocation([MarshalAs(UnmanagedType.LPWStr)] string pszIconPath, int iIcon);
    void SetRelativePath([MarshalAs(UnmanagedType.LPWStr)] string pszPathRel, uint dwReserved);
    void Resolve(IntPtr hwnd, uint fFlags);
    void SetPath([MarshalAs(UnmanagedType.LPWStr)] string pszFile);
}

// ── DebugView 日志 ────────────────────────────────────────────────

public static class Log
{
    static readonly string Path = System.IO.Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
        "GpuPref_debug.log");

    public static void Write(string msg)
    {
        try { File.AppendAllText(Path, $"{DateTime.Now:HH:mm:ss.fff} {msg}\n"); }
        catch { }
    }
}
