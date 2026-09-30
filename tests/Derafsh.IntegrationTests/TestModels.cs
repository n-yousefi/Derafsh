using Derafsh;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Derafsh.IntegrationTests;

[Table("DerafshTest_Settings")]
public sealed class SettingsDto
{
    [Key] public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    [ChildCollection(nameof(ContactDto.SettingsId))]
    public List<ContactDto>? Contacts { get; set; } = [];

    [ChildCollection(nameof(ZoneDto.SettingsId))]
    public List<ZoneDto>? Zones { get; set; } = [];
}

[Table("DerafshTest_Contact")]
public sealed class ContactDto
{
    [Key] public int Id { get; set; }
    public int SettingsId { get; set; }
    public string Value { get; set; } = string.Empty;
}

[Table("DerafshTest_Zone")]
public sealed class ZoneDto
{
    [Key] public int Id { get; set; }
    public int SettingsId { get; set; }
    public string Code { get; set; } = string.Empty;
}

[Table("DerafshTest_Customer")]
public sealed class CustomerDto
{
    [Key] public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

[Table("DerafshTest_Order")]
public sealed class OrderDto
{
    [Key] public int Id { get; set; }
    public int CustomerId { get; set; }
    public string Number { get; set; } = string.Empty;

    [Reference(nameof(CustomerId))]
    public CustomerDto? Customer { get; set; }
}

[Table("DerafshTest_KeyOnly")]
public sealed class KeyOnlyDto
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }
}
