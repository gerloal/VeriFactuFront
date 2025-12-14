using System;

namespace Verifactu.Portal.Models;

public enum TenantSystemType
{
    Unknown = 0,
    Verifactu,
    NoVerifactu
}

public static class TenantSystemTypeExtensions
{
    public static TenantSystemType FromValue(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return TenantSystemType.Unknown;
        }

        return value.Trim().ToUpperInvariant() switch
        {
            "VERIFACTU" => TenantSystemType.Verifactu,
            "NOVERIFACTU" => TenantSystemType.NoVerifactu,
            _ => TenantSystemType.Unknown
        };
    }

    public static string ToDisplayName(this TenantSystemType type)
    {
        return type switch
        {
            TenantSystemType.Verifactu => "VeriFactu",
            TenantSystemType.NoVerifactu => "Sin envío a AEAT",
            _ => "Desconocido"
        };
    }

    public static bool IsVerifactu(this TenantSystemType type) => type == TenantSystemType.Verifactu;
        public static bool IsNoVerifactu(this TenantSystemType type) => type == TenantSystemType.NoVerifactu;
}
