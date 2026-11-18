using HRProTools.Web.Services;

namespace HRProTools.Tests;

public sealed class TenantSchemaNamesTests
{
    [Fact]
    public void SchemaNameIsDeterministicAndValidated()
    {
        var id = Guid.Parse("01234567-89ab-cdef-0123-456789abcdef");
        var value = TenantSchemaNames.For(id);
        Assert.Equal("hr_t_0123456789abcdef0123456789abcdef", value);
        Assert.True(TenantSchemaNames.IsValid(value));
        Assert.False(TenantSchemaNames.IsValid("public"));
    }
}
