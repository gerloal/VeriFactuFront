using System.Text.Json.Serialization;

namespace Verifactu.Portal.Models;

public sealed class RemoteConsultationRequestDto
{
    [JsonPropertyName("tenantId")]
    public string TenantId { get; set; } = string.Empty;

    [JsonPropertyName("endpoint")]
    public string Endpoint { get; set; } = string.Empty;

    [JsonPropertyName("consultaId")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ConsultaId { get; set; }

    [JsonPropertyName("storeResult")]
    public bool StoreResult { get; set; }

    [JsonPropertyName("cabecera")]
    public RemoteConsultationCabeceraDto Cabecera { get; set; } = new();

    [JsonPropertyName("filtroConsulta")]
    public RemoteConsultationFiltroDto FiltroConsulta { get; set; } = new();

    [JsonPropertyName("datosAdicionalesRespuesta")]
    public RemoteConsultationAdditionalDataDto DatosAdicionalesRespuesta { get; set; } = new();

    [JsonPropertyName("requestContext")]
    public RemoteConsultationContextDto RequestContext { get; set; } = new();
}

public sealed class RemoteConsultationCabeceraDto
{
    [JsonPropertyName("obligadoEmision")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RemoteConsultationContraparteDto? ObligadoEmision { get; set; }

    [JsonPropertyName("destinatario")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RemoteConsultationContraparteDto? Destinatario { get; set; }
}

public sealed class RemoteConsultationContraparteDto
{
    [JsonPropertyName("nombreRazon")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NombreRazon { get; set; }

    [JsonPropertyName("nif")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Nif { get; set; }

    [JsonPropertyName("idOtro")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? IdOtro { get; set; }
}

public sealed class RemoteConsultationFiltroDto
{
    [JsonPropertyName("numSerieFactura")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NumSerieFactura { get; set; }

    [JsonPropertyName("fechaExpedicionFactura")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RemoteConsultationFechaFiltroDto? FechaExpedicionFactura { get; set; }

    [JsonPropertyName("contraparte")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RemoteConsultationContraparteDto? Contraparte { get; set; }

    [JsonPropertyName("periodoImputacion")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RemoteConsultationPeriodoDto? PeriodoImputacion { get; set; }

    [JsonPropertyName("refExterna")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? RefExterna { get; set; }

    [JsonPropertyName("clavePaginacion")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ClavePaginacion { get; set; }

    [JsonPropertyName("sistemaInformatico")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public RemoteConsultationSistemaInformaticoDto? SistemaInformatico { get; set; }
}

public sealed class RemoteConsultationFechaFiltroDto
{
    [JsonPropertyName("fechaDesde")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FechaDesde { get; set; }

    [JsonPropertyName("fechaHasta")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? FechaHasta { get; set; }
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

public sealed class RemoteConsultationSistemaInformaticoDto
{
    [JsonPropertyName("nombre")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Nombre { get; set; }

    [JsonPropertyName("version")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Version { get; set; }

    [JsonPropertyName("idioma")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Idioma { get; set; }
}

public sealed class RemoteConsultationAdditionalDataDto
{
    [JsonPropertyName("mostrarNombreEmisor")]
    public bool MostrarNombreEmisor { get; set; }

    [JsonPropertyName("mostrarSistemaInformatico")]
    public bool MostrarSistemaInformatico { get; set; }
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
