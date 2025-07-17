namespace CloudCrate.Application.Email;

public class InviteEmailModel
{
    public string CrateName { get; }
    public string InviteLink { get; }

    public InviteEmailModel(string crateName, string inviteLink)
    {
        CrateName = crateName;
        InviteLink = inviteLink;
    }
}