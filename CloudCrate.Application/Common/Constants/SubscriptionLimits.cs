using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.Common.Constants;

public static class SubscriptionLimits
{
    public const int FreeCrateLimit = 1;
    public const int ProCrateLimit = 3;

    public static int GetCrateLimit(SubscriptionPlan plan) =>
        plan switch
        {
            SubscriptionPlan.Free => FreeCrateLimit,
            SubscriptionPlan.Pro => ProCrateLimit,
            _ => FreeCrateLimit
        };
}