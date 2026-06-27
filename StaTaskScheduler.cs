namespace GpuPreference;

// 让 Task 在 STA 线程上执行，供 OpenFileDialog 使用
sealed class StaTaskScheduler : TaskScheduler
{
    public static readonly StaTaskScheduler Instance = new();

    protected override void QueueTask(Task task)
    {
        var t = new Thread(() => TryExecuteTask(task));
        t.SetApartmentState(ApartmentState.STA);
        t.IsBackground = true;
        t.Start();
    }

    protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) => false;
    protected override IEnumerable<Task> GetScheduledTasks() => [];
}
