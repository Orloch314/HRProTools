namespace HRProTools.Web.Security;

public static class AppRoles
{
    public const string GlobalAdministrator = "GlobalAdministrator";
    public const string TenantAdministrator = "TenantAdministrator";
    public const string HumanResources = "HumanResources";
    public const string Approver = "Approver";
    public const string Employee = "Employee";

    public static readonly string[] All =
        [GlobalAdministrator, TenantAdministrator, HumanResources, Approver, Employee];
}

public static class AppPolicies
{
    public const string GlobalAdministration = "GlobalAdministration";
    public const string TenantAdministration = "TenantAdministration";
    public const string EmployeeRecords = "EmployeeRecords";
    public const string SalaryRecords = "SalaryRecords";
    public const string EmployeeSelfService = "EmployeeSelfService";
}
