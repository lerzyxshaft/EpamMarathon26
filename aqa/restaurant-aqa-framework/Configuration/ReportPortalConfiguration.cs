using System.Runtime.CompilerServices;

namespace Restaurant.Aqa.Configuration;

internal static class ReportPortalConfiguration
{
    [ModuleInitializer]
    internal static void Configure()
    {
        _ = AqaConfiguration.Current;
        var endpoint = Environment.GetEnvironmentVariable("RP_ENDPOINT");
        if (!string.IsNullOrWhiteSpace(endpoint))
        {
            Environment.SetEnvironmentVariable("ReportPortal_Server_Url", endpoint);
            Environment.SetEnvironmentVariable(
                "ReportPortal_Server_ApiKey",
                Environment.GetEnvironmentVariable("RP_API_KEY"));
            Environment.SetEnvironmentVariable(
                "ReportPortal_Server_Authentication_Uuid",
                Environment.GetEnvironmentVariable("RP_API_KEY"));
            Environment.SetEnvironmentVariable(
                "ReportPortal_Server_Project",
                Environment.GetEnvironmentVariable("RP_PROJECT"));
            Environment.SetEnvironmentVariable(
                "ReportPortal_Launch_Name",
                Environment.GetEnvironmentVariable("RP_LAUNCH") ?? "Restaurant AQA");
            Environment.SetEnvironmentVariable("ReportPortal_Enabled", "true");
        }
    }
}
