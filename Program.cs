using GpuPreference;

static class Program
{
    [STAThread]
    static void Main()
    {
        const string MutexName = "Global\\GpuPreference_SingleInstance";
        using var mutex = new Mutex(initiallyOwned: true, MutexName, out bool created);
        if (!created)
        {
            MessageBox.Show("GPU 偏好管理已在运行中。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        ApplicationConfiguration.Initialize();
        CategoryStore.Load();
        CategoryStore.PurgeStale(GpuRegistry.ListEntries().Select(e => e.Exe));
        Application.Run(new MainForm());
    }
}
