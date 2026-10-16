namespace AcxiomCRM.Models.Enums
{
    public static class RoleConstants
    {
        public const string Admin = "Admin";
        public const string Manager = "Manager";
        public const string SalesExecutive = "SalesExecutive";

        public static readonly string[] AllRoles = [Admin, Manager, SalesExecutive];
    }

    public static class LeadStatuses
    {
        public const string New = "New";
        public const string Contacted = "Contacted";
        public const string Qualified = "Qualified";
        public const string Unqualified = "Unqualified";
        public const string Converted = "Converted";
        public const string Lost = "Lost";

        public static readonly string[] AllStatuses = [New, Contacted, Qualified, Unqualified, Converted, Lost];

        public static bool IsValid(string? status) =>
            !string.IsNullOrWhiteSpace(status) && AllStatuses.Contains(status);

        public static bool CanTransition(string current, string next)
        {
            if (current == next) return true;
            if (current == Converted) return false; // Converted is terminal

            return current switch
            {
                New => next is Contacted or Qualified or Unqualified or Lost,
                Contacted => next is Qualified or Unqualified or Lost,
                Qualified => next is Converted or Unqualified or Lost,
                Unqualified => next is Contacted or Qualified or Lost,
                Lost => next is Contacted or Qualified,
                _ => true
            };
        }
    }

    public static class LeadPriorities
    {
        public const string Low = "Low";
        public const string Medium = "Medium";
        public const string High = "High";
        public const string Critical = "Critical";

        public static readonly string[] All = [Low, Medium, High, Critical];
    }

    public static class LeadSources
    {
        public const string Website = "Website";
        public const string Referral = "Referral";
        public const string LinkedIn = "LinkedIn";
        public const string ColdCall = "Cold Call";
        public const string Campaign = "Campaign";
        public const string Partner = "Partner";
        public const string Other = "Other";

        public static readonly string[] All = [Website, Referral, LinkedIn, ColdCall, Campaign, Partner, Other];
    }

    public static class OpportunityStages
    {
        public const string Qualification = "Qualification";
        public const string Proposal = "Proposal";
        public const string Negotiation = "Negotiation";
        public const string Won = "Won";
        public const string Lost = "Lost";

        public static readonly string[] AllStages = [Qualification, Proposal, Negotiation, Won, Lost];

        public static int DefaultProbability(string stage) => stage switch
        {
            Qualification => 20,
            Proposal => 50,
            Negotiation => 80,
            Won => 100,
            Lost => 0,
            _ => 10
        };

        public static bool IsClosed(string stage) => stage is Won or Lost;
    }

    public static class FollowUpStatuses
    {
        public const string Planned = "Planned";
        public const string Completed = "Completed";
        public const string Missed = "Missed";
        public const string Cancelled = "Cancelled";

        public static readonly string[] All = [Planned, Completed, Missed, Cancelled];
    }

    public static class FollowUpTypes
    {
        public const string Call = "Call";
        public const string Meeting = "Meeting";
        public const string Email = "Email";
        public const string Demo = "Demo";
        public const string CheckIn = "Check-in";

        public static readonly string[] All = [Call, Meeting, Email, Demo, CheckIn];
    }

    public static class ActivityTypes
    {
        public const string Call = "Call";
        public const string Meeting = "Meeting";
        public const string Email = "Email";
        public const string Task = "Task";

        public static readonly string[] All = [Call, Meeting, Email, Task];
    }

    public static class AuditActions
    {
        public const string Login = "Login";
        public const string FailedLogin = "Failed Login";
        public const string Logout = "Logout";
        public const string Create = "Create";
        public const string Update = "Update";
        public const string Delete = "Delete";
        public const string LeadConversion = "Lead Conversion";
        public const string RoleChange = "Role Change";
        public const string Security = "Security";
        public const string AccountLockout = "Account Lockout";
        public const string AccountUnlock = "Account Unlock";
        public const string PasswordReset = "Password Reset";
    }
}
