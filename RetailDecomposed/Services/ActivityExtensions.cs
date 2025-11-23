using System.Diagnostics;

namespace RetailDecomposed.Services;

/// <summary>
/// Extension methods for Activity to enhance telemetry capabilities.
/// </summary>
public static class ActivityExtensions
{
    /// <summary>
    /// Records an exception in the activity with standard tags.
    /// </summary>
    public static void RecordException(this Activity? activity, Exception exception)
    {
        if (activity == null || exception == null)
            return;

        activity.SetTag("exception.type", exception.GetType().FullName);
        activity.SetTag("exception.message", exception.Message);
        activity.SetTag("exception.stacktrace", exception.StackTrace);

        if (exception.InnerException != null)
        {
            activity.SetTag("exception.inner_type", exception.InnerException.GetType().FullName);
            activity.SetTag("exception.inner_message", exception.InnerException.Message);
        }
    }
}
