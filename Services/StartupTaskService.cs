using Windows.ApplicationModel;

namespace WinToastRelay.Services;

public sealed class StartupTaskService
{
    private const string TaskId = "WinToastRelayStartupTask";

    public async Task<bool> IsEnabledAsync()
    {
        try
        {
            var task = await StartupTask.GetAsync(TaskId);
            return task.State is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
        }
        catch { return false; }
    }

    public async Task<bool> SetEnabledAsync(bool enabled)
    {
        try
        {
            var task = await StartupTask.GetAsync(TaskId);
            if (enabled)
            {
                var state = await task.RequestEnableAsync();
                return state is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
            }

            task.Disable();
            return false;
        }
        catch { return false; }
    }
}
