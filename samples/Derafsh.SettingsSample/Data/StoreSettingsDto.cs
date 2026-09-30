using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Derafsh;

namespace Derafsh.SettingsSample.Data;

[Table("StoreSettings")]
public sealed class StoreSettingsDto
{
    [Key, DatabaseGenerated(DatabaseGeneratedOption.None)]
    public int Id { get; set; }

    [Required, StringLength(120)]
    [Display(Name = "Store name")]
    public string StoreName { get; set; } = string.Empty;

    [Required, StringLength(3, MinimumLength = 3)]
    public string Currency { get; set; } = "EUR";

    [Required, StringLength(80)]
    [Display(Name = "Time zone")]
    public string TimeZone { get; set; } = "Europe/Amsterdam";

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
    [Key]
    public int Id { get; set; }

    public int StoreSettingsId { get; set; }

    [Required, StringLength(20)]
    public string Type { get; set; } = "Phone";

    [Required, StringLength(160)]
    public string Value { get; set; } = string.Empty;
}

[Table("ShippingZone")]
public sealed class ShippingZoneDto
{
    [Key]
    public int Id { get; set; }

    public int StoreSettingsId { get; set; }

    [Required, StringLength(2, MinimumLength = 2)]
    [Display(Name = "Country code")]
    public string CountryCode { get; set; } = string.Empty;

    [Range(0, 100000)]
    public decimal Fee { get; set; }
}

[Table("NotificationRecipient")]
public sealed class NotificationRecipientDto
{
    [Key]
    public int Id { get; set; }

    public int StoreSettingsId { get; set; }

    [Required, EmailAddress, StringLength(200)]
    public string Email { get; set; } = string.Empty;
}
