using CloudCrate.Domain.Enums;

namespace CloudCrate.Application.DTOs.Admin.Request;

public class UpdateUserPlanRequest
{
    public SubscriptionPlan Plan { get; set; }
}