using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace Verifactu.Portal.Models;

public sealed class ProfileDto
{
    [Required]
    public string TenantId { get; init; } = string.Empty;

    [Required]
    [Display(Name = "Razón social")]
    public string CompanyName { get; set; } = string.Empty;

    [Required]
    [Display(Name = "NIF")]
    public string Nif { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [Display(Name = "Correo electrónico")]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Tipo de sistema")]
    [JsonPropertyName("tipoSistema")]
    public string? TipoSistema { get; init; }

    [JsonPropertyName("usaCertificadoRemotoVerifactu")]
    public bool UsaCertificadoRemotoVerifactu { get; init; }

    [JsonPropertyName("idEmisorFactura")]
    public string? IdEmisorFactura { get; init; }

    [JsonPropertyName("nombreRazon")]
    public string? NombreRazon { get; init; }

    [JsonIgnore]
    public TenantSystemType SystemType => TenantSystemTypeExtensions.FromValue(TipoSistema);
}
