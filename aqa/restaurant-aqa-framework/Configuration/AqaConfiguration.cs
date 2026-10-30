namespace Restaurant.Aqa.Configuration;

public sealed class AqaConfiguration
{
    private AqaConfiguration()
    {
        FrontendUrl = Get("AQA_FRONTEND_URL", "https://d1n6pycnjfkr4j.cloudfront.net").TrimEnd('/');
        ApiUrl = Get("AQA_API_URL", "https://44hns6u8zf.execute-api.us-west-2.amazonaws.com/develop").TrimEnd('/');
        LiveTestsEnabled = bool.TryParse(Get("AQA_RUN_LIVE_TESTS", "false"), out var enabled) && enabled;
        OwnerEmail = Get("AQA_OWNER_EMAIL", string.Empty);
        OwnerPassword = Get("AQA_OWNER_PASSWORD", string.Empty);
        OtherCustomerEmail = Get("AQA_OTHER_CUSTOMER_EMAIL", string.Empty);
        OtherCustomerPassword = Get("AQA_OTHER_CUSTOMER_PASSWORD", string.Empty);
        TestTableId = Get("AQA_TEST_TABLE_ID", string.Empty);
        TestDishId = Get("AQA_TEST_DISH_ID", string.Empty);
    }

    public static AqaConfiguration Current { get; } = Load();

    public string FrontendUrl { get; }
    public string ApiUrl { get; }
    public bool LiveTestsEnabled { get; }
    public string OwnerEmail { get; }
    public string OwnerPassword { get; }
    public string OtherCustomerEmail { get; }
    public string OtherCustomerPassword { get; }
    public string TestTableId { get; }
    public string TestDishId { get; }

    public bool HasLiveTestSettings =>
        LiveTestsEnabled &&
        !string.IsNullOrWhiteSpace(OwnerEmail) &&
        !string.IsNullOrWhiteSpace(OwnerPassword) &&
        !string.IsNullOrWhiteSpace(TestTableId) &&
        !string.IsNullOrWhiteSpace(TestDishId);

    public bool HasOtherCustomer =>
        !string.IsNullOrWhiteSpace(OtherCustomerEmail) &&
        !string.IsNullOrWhiteSpace(OtherCustomerPassword);

    private static AqaConfiguration Load()
    {
        LoadEnvironmentFile();
        return new AqaConfiguration();
    }

    private static string Get(string name, string fallback) =>
        Environment.GetEnvironmentVariable(name) ?? fallback;

    private static void LoadEnvironmentFile()
    {
        var directory = new DirectoryInfo(Environment.CurrentDirectory);
        while (directory is not null)
        {
            var filePath = Path.Combine(directory.FullName, ".env");
            if (File.Exists(filePath))
            {
                foreach (var line in File.ReadLines(filePath))
                {
                    var trimmed = line.Trim();
                    if (trimmed.Length == 0 || trimmed.StartsWith('#'))
                    {
                        continue;
                    }

                    var separator = trimmed.IndexOf('=');
                    if (separator <= 0)
                    {
                        continue;
                    }

                    var name = trimmed[..separator].Trim();
                    var value = trimmed[(separator + 1)..].Trim().Trim('"', '\'');
                    if (Environment.GetEnvironmentVariable(name) is null)
                    {
                        Environment.SetEnvironmentVariable(name, value);
                    }
                }

                return;
            }

            directory = directory.Parent;
        }
    }
}
