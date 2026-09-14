using Microsoft.Extensions.Configuration;

namespace CrmRegistrationGateway.Configuration;

public sealed class CrmFieldMapping
{
    public string LeadEntitySet { get; init; } = "leads";
    public string LeadId { get; init; } = "leadid";
    public string Subject { get; init; } = "subject";
    public string CompanyName { get; init; } = "companyname";
    public string FirstName { get; init; } = "firstname";
    public string LastName { get; init; } = "lastname";
    public string Email { get; init; } = "emailaddress1";
    public string Department { get; init; } = "twbs_department";
    public string JobTitle { get; init; } = "twbs_isunvani";
    public string City { get; init; } = "twbs_sehir";
    public string WorkPhone { get; init; } = "telephone1";
    public string MobilePhone { get; init; } = "mobilephone";

    public static CrmFieldMapping FromConfiguration(IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        return new CrmFieldMapping
        {
            LeadEntitySet = Read(configuration, "CRM_LEAD_ENTITY_SET", "CrmMapping:LeadEntitySet", "leads"),
            LeadId = Read(configuration, "CRM_LEAD_ID_FIELD", "CrmMapping:LeadId", "leadid"),
            Subject = Read(configuration, "CRM_SUBJECT_FIELD", "CrmMapping:Subject", "subject"),
            CompanyName = Read(configuration, "CRM_COMPANY_NAME_FIELD", "CrmMapping:CompanyName", "companyname"),
            FirstName = Read(configuration, "CRM_FIRST_NAME_FIELD", "CrmMapping:FirstName", "firstname"),
            LastName = Read(configuration, "CRM_LAST_NAME_FIELD", "CrmMapping:LastName", "lastname"),
            Email = Read(configuration, "CRM_EMAIL_FIELD", "CrmMapping:Email", "emailaddress1"),
            Department = Read(configuration, "CRM_DEPARTMENT_FIELD", "CrmMapping:Department", "twbs_department"),
            JobTitle = Read(configuration, "CRM_JOB_TITLE_FIELD", "CrmMapping:JobTitle", "twbs_isunvani"),
            City = Read(configuration, "CRM_CITY_FIELD", "CrmMapping:City", "twbs_sehir"),
            WorkPhone = Read(configuration, "CRM_WORK_PHONE_FIELD", "CrmMapping:WorkPhone", "telephone1"),
            MobilePhone = Read(configuration, "CRM_MOBILE_PHONE_FIELD", "CrmMapping:MobilePhone", "mobilephone")
        };
    }

    public void Validate()
    {
        var requiredNames = new[]
        {
            LeadEntitySet,
            LeadId,
            Subject,
            CompanyName,
            FirstName,
            LastName,
            Email,
            Department,
            JobTitle,
            City,
            WorkPhone,
            MobilePhone
        };

        if (requiredNames.Any(name => !IsValidLogicalName(name)))
        {
            throw new InvalidOperationException("One or more CRM field mapping values are invalid.");
        }

    }

    private static bool IsValidLogicalName(string value) =>
        !string.IsNullOrWhiteSpace(value) &&
        value.All(character => char.IsLetterOrDigit(character) || character == '_');

    private static string Read(
        IConfiguration configuration,
        string environmentName,
        string sectionName,
        string defaultValue) =>
        (configuration[environmentName] ?? configuration[sectionName] ?? defaultValue).Trim();

}
