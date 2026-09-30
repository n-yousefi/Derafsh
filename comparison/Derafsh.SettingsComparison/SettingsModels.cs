using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Derafsh;

namespace Derafsh.SettingsComparison;

[Table("StoreSettings")]
public sealed class StoreSettingsDto
{
    [Key] public int Id { get; set; }
    public string StoreName { get; set; } = string.Empty;
    public string Currency { get; set; } = string.Empty;
    public string TimeZone { get; set; } = string.Empty;

    [ChildCollection(nameof(StoreContactDto.StoreSettingsId))]
    public List<StoreContactDto>? Contacts { get; set; } = [];

    [ChildCollection(nameof(ShippingZoneDto.StoreSettingsId))]
    public List<ShippingZoneDto>? ShippingZones { get; set; } = [];

    [ChildCollection(nameof(NotificationRecipientDto.StoreSettingsId))]
    public List<NotificationRecipientDto>? NotificationRecipients { get; set; } = [];
}

[Table("StoreContact")]
public sealed class StoreContactDto
{
    [Key] public int Id { get; set; }
    public int StoreSettingsId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

[Table("ShippingZone")]
public sealed class ShippingZoneDto
{
    [Key] public int Id { get; set; }
    public int StoreSettingsId { get; set; }
    public string CountryCode { get; set; } = string.Empty;
    public decimal Fee { get; set; }
}

[Table("NotificationRecipient")]
public sealed class NotificationRecipientDto
{
    [Key] public int Id { get; set; }
    public int StoreSettingsId { get; set; }
    public string Email { get; set; } = string.Empty;
}
