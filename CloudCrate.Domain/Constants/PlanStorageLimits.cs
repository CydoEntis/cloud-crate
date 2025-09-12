using CloudCrate.Domain.Enums;

namespace CloudCrate.Domain.Constants;

public static class PlanStorageLimits
{
    public const long Free = 5L * 1024 * 1024 * 1024; // 5 GB
    public const long Mini = 100L * 1024 * 1024 * 1024; // 100 GB
    public const long Standard = 250L * 1024 * 1024 * 1024; // 250 GB
    public const long Max = 500L * 1024 * 1024 * 1024; // 500 GB

    public static long GetLimit(SubscriptionPlan plan) => plan switch
    {
        SubscriptionPlan.Mini => Mini,
        SubscriptionPlan.Standard => Standard,
        SubscriptionPlan.Max => Max,
        _ => Free
    };
}