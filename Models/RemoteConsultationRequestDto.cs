using System.Text.Json.Serialization;

namespace Verifactu.Portal.Models;

public sealed class RemoteConsultationRequestDto
{
    [JsonPropertyName("tenantId")]
    public string TenantId { get; set; } = string.Empty;

    [JsonPropertyName("endpoint")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Endpoint { get; set; }

    [JsonPropertyName("consultaId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ConsultaId { get; set; }

    [JsonPropertyName("cabecera")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RemoteConsultationCabeceraDto? Cabecera { get; set; }

    [JsonPropertyName("filtroConsulta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RemoteConsultationFiltroDto? FiltroConsulta { get; set; }

    [JsonPropertyName("datosAdicionalesRespuesta")]
    public RemoteConsultationAdditionalDataDto DatosAdicionalesRespuesta { get; set; } = new();

    [JsonPropertyName("storeResult")]
    public bool StoreResult { get; set; }

    [JsonPropertyName("requestContext")]
    public RemoteConsultationContextDto RequestContext { get; set; } = new();
}

public sealed class RemoteConsultationCabeceraDto
{
    [JsonPropertyName("idVersion")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IdVersion { get; set; }

    [JsonPropertyName("obligadoEmision")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RemoteConsultationPersonaDto? ObligadoEmision { get; set; }

    [JsonPropertyName("destinatario")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RemoteConsultationPersonaDto? Destinatario { get; set; }

    [JsonPropertyName("indicadorRepresentante")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IndicadorRepresentante { get; set; }
}

public sealed class RemoteConsultationPersonaDto
{
    [JsonPropertyName("nombreRazon")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NombreRazon { get; set; }

    [JsonPropertyName("nif")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Nif { get; set; }

    [JsonPropertyName("idOtro")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RemoteConsultationIdOtroDto? IdOtro { get; set; }
}

public sealed class RemoteConsultationIdOtroDto
{
    [JsonPropertyName("codigoPais")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? CodigoPais { get; set; }

    [JsonPropertyName("idType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IdType { get; set; }

    [JsonPropertyName("id")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }
}

public sealed class RemoteConsultationFiltroDto
{
    [JsonPropertyName("periodoImputacion")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RemoteConsultationPeriodoDto? PeriodoImputacion { get; set; }

    [JsonPropertyName("contraparte")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RemoteConsultationPersonaDto? Contraparte { get; set; }

    [JsonPropertyName("fechaExpedicionFactura")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RemoteConsultationFechaFiltroDto? FechaExpedicionFactura { get; set; }

    [JsonPropertyName("numSerieFactura")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NumSerieFactura { get; set; }

    [JsonPropertyName("refExterna")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RefExterna { get; set; }

    [JsonPropertyName("clavePaginacion")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RemoteConsultationClaveDto? ClavePaginacion { get; set; }

    [JsonPropertyName("sistemaInformatico")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RemoteConsultationSistemaInformaticoDto? SistemaInformatico { get; set; }
}

public sealed class RemoteConsultationFechaFiltroDto
{
    [JsonPropertyName("fechaExpedicionFactura")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FechaExpedicionFactura { get; set; }

    [JsonPropertyName("rangoFechaExpedicion")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RemoteConsultationRangoFechaDto? RangoFechaExpedicion { get; set; }
}

public sealed class RemoteConsultationRangoFechaDto
{
    [JsonPropertyName("desde")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Desde { get; set; }

    [JsonPropertyName("hasta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Hasta { get; set; }
}

public sealed class RemoteConsultationPeriodoDto
{
    [JsonPropertyName("ejercicio")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Ejercicio { get; set; }

    [JsonPropertyName("periodo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Periodo { get; set; }
}

public sealed class RemoteConsultationClaveDto
{
    [JsonPropertyName("numSerieFactura")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NumSerieFactura { get; set; }
}

public sealed class RemoteConsultationSistemaInformaticoDto
{
    [JsonPropertyName("nombreSistemaInformatico")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NombreSistemaInformatico { get; set; }

    [JsonPropertyName("version")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Version { get; set; }
}

public sealed class RemoteConsultationAdditionalDataDto
{
    [JsonPropertyName("mostrarNombreRazonEmisor")]
    public string MostrarNombreRazonEmisor { get; set; } = "S";

    [JsonPropertyName("mostrarSistemaInformatico")]
    public string MostrarSistemaInformatico { get; set; } = "S";
}

public sealed class RemoteConsultationContextDto
{
    [JsonPropertyName("ejercicio")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Ejercicio { get; set; }

    [JsonPropertyName("periodo")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Periodo { get; set; }

    [JsonPropertyName("nifObligado")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NifObligado { get; set; }
}
