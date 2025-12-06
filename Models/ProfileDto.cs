using System.ComponentModel.DataAnnotations;

namespace Verifactu.Portal.Models;

public sealed class ProfileDto
{
    [Required]
    public required string TenantId { get; init; }

    [Required]
    [Display(Name = "Razón social")]
    public required string CompanyName { get; set; }

    [Required]
    [Display(Name = "NIF")]
    public required string Nif { get; set; }

    [Required]
    [EmailAddress]
    [Display(Name = "Correo electrónico")]
    public required string Email { get; set; }
}
