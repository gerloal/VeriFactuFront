using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using Verifactu.Portal.Models;
using Verifactu.Portal.Options;
using Verifactu.Portal.Services;

namespace Verifactu.Portal.Pages.Remote;

[Authorize]
public sealed class ConsultasModel : PageModel
{
    private readonly VerifactuApiClient _apiClient;
    private readonly ILogger<ConsultasModel> _logger;
    private readonly VerifactuApiOptions _options;
    private RemoteUserStatusDto? _remoteStatus;

    public ConsultasModel(VerifactuApiClient apiClient, ILogger<ConsultasModel> logger, IOptions<VerifactuApiOptions> options)
    {
        _apiClient = apiClient;
        _logger = logger;
        _options = options.Value;
    }

    [BindProperty]
    public ConsultaInputModel Input { get; set; } = new();

    public bool CanUseRemoteCertificate { get; private set; }

    public RemoteConsultationResponseDto? Result { get; private set; }

    public IList<RemoteConsultationRecordViewModel> Records { get; private set; } = new List<RemoteConsultationRecordViewModel>();

    public string? ErrorMessage { get; private set; }

    public string? RawResponseDecoded { get; private set; }

    public string? StoredMessage { get; private set; }

    public string? RemoteClientNif { get; private set; }

    public string? RemoteClientNombreRazon { get; private set; }

    public async Task<IActionResult> OnGetAsync()
    {
        await InitializeRemoteStatusAsync().ConfigureAwait(false);
        EnsureDefaultPeriod();
        return Page();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        await InitializeRemoteStatusAsync().ConfigureAwait(false);
        EnsureDefaultPeriod();

        if (!CanUseRemoteCertificate)
        {
            return Page();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        var remoteStatus = _remoteStatus ?? await _apiClient.GetRemoteUserStatusAsync().ConfigureAwait(false);
        _remoteStatus = remoteStatus;
        RemoteClientNif = NormalizeNif(remoteStatus.IdEmisorFactura);
        RemoteClientNombreRazon = ResolveObligadoNombre(remoteStatus);

        if (string.IsNullOrWhiteSpace(remoteStatus.TenantId))
        {
            ErrorMessage = "No se pudo determinar el tenant asociado al usuario.";
            return Page();
        }
        
        if (string.IsNullOrWhiteSpace(_options.AeatConsultaEndpoint))
        {
            ErrorMessage = "No hay un endpoint de consulta AEAT configurado. Contacta con el administrador.";
            return Page();
        }

        var request = BuildRequest(remoteStatus);

        try
        {
            Result = await _apiClient.ExecuteRemoteConsultationAsync(request).ConfigureAwait(false);
            if (Result is null)
            {
                ErrorMessage = "La API no devolvió resultado.";
                return Page();
            }

            Records = BuildRecords(Result.Registros);

            if (!string.IsNullOrWhiteSpace(Result.RawResponseBase64))
            {
                try
                {
                    var bytes = Convert.FromBase64String(Result.RawResponseBase64);
                    RawResponseDecoded = Encoding.UTF8.GetString(bytes);
                }
                catch (FormatException)
                {
                    RawResponseDecoded = null;
                }
            }

            if (Result.Stored is not null)
            {
                if (!string.IsNullOrWhiteSpace(Result.Stored.Status) && Result.Stored.Status.Equals("STORED", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(Result.ConsultaId))
                {
                    StoredMessage = $"Consulta almacenada con ID {Result.ConsultaId}.";
                }
                else if (!string.IsNullOrWhiteSpace(Result.Stored.Status))
                {
                    StoredMessage = $"Estado de almacenamiento: {Result.Stored.Status}.";
                }
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "No se pudo ejecutar la consulta AEAT para el tenant {TenantId}", remoteStatus.TenantId);
            ErrorMessage = "No se pudo realizar la consulta en este momento. Inténtalo de nuevo más tarde.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error procesando la respuesta de consulta remota para el tenant {TenantId}", remoteStatus.TenantId);
            ErrorMessage = "Se produjo un error inesperado procesando la respuesta de AEAT.";
        }

        return Page();
    }

    private async Task InitializeRemoteStatusAsync()
    {
        try
        {
            _remoteStatus = await _apiClient.GetRemoteUserStatusAsync().ConfigureAwait(false);
            CanUseRemoteCertificate = _remoteStatus.RemoteCertificateEnabled;
            ViewData[nameof(RemoteUserStatusDto.TenantId)] = _remoteStatus.TenantId;
            RemoteClientNif = NormalizeNif(_remoteStatus.IdEmisorFactura);
            RemoteClientNombreRazon = ResolveObligadoNombre(_remoteStatus);
            if (!CanUseRemoteCertificate)
            {
                ErrorMessage = "Tu usuario no tiene habilitado el certificado remoto.";
            }
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "No se pudo comprobar el estado del certificado remoto.");
            ErrorMessage = "No se pudo comprobar el estado del certificado remoto.";
            CanUseRemoteCertificate = false;
            _remoteStatus = null;
            RemoteClientNif = null;
            RemoteClientNombreRazon = null;
        }
    }

    private void EnsureDefaultPeriod()
    {
        if (string.IsNullOrWhiteSpace(Input.Ejercicio))
        {
            Input.Ejercicio = DateTime.UtcNow.Year.ToString(CultureInfo.InvariantCulture);
        }

        if (string.IsNullOrWhiteSpace(Input.Periodo))
        {
            Input.Periodo = DateTime.UtcNow.ToString("MM", CultureInfo.InvariantCulture);
        }

    }

    private RemoteConsultationRequestDto BuildRequest(RemoteUserStatusDto status)
    {
        var tenantId = NormalizeString(status.TenantId) ?? NormalizeString(_options.TenantId) ?? string.Empty;
        var obligadoNif = NormalizeNif(Input.NifObligado) ?? NormalizeNif(status.IdEmisorFactura);
        var filtroContraparteNif = NormalizeNif(Input.ClienteNif);
        var obligadoNombre = NormalizeString(RemoteClientNombreRazon) ?? ResolveObligadoNombre(status);

        var request = new RemoteConsultationRequestDto
        {
            TenantId = tenantId ?? string.Empty,
            ConsultaId = NormalizeString(Input.ConsultaId),
            Endpoint = _options.AeatConsultaEndpoint!.Trim(),
            StoreResult = Input.GuardarResultado,
            DatosAdicionalesRespuesta = new RemoteConsultationAdditionalDataDto
            {
                MostrarNombreRazonEmisor = Input.MostrarNombreEmisor ? "S" : "N",
                MostrarSistemaInformatico = Input.MostrarSistemaInformatico ? "S" : "N"
            },
            RequestContext = new RemoteConsultationContextDto
            {
                Ejercicio = NormalizeString(Input.Ejercicio),
                Periodo = NormalizeString(Input.Periodo),
                NifObligado = obligadoNif
            }
        };

        var cabecera = new RemoteConsultationCabeceraDto
        {
            IdVersion = "1.0"
        };

        if (!string.IsNullOrWhiteSpace(obligadoNif) || !string.IsNullOrWhiteSpace(obligadoNombre))
        {
            cabecera.ObligadoEmision = new RemoteConsultationPersonaDto
            {
                Nif = obligadoNif,
                NombreRazon = obligadoNombre
            };
        }

        request.Cabecera = cabecera;

        var filtro = new RemoteConsultationFiltroDto();
        var hasFiltro = false;

        if (!string.IsNullOrWhiteSpace(Input.NumeroSerie))
        {
            filtro.NumSerieFactura = NormalizeString(Input.NumeroSerie);
            hasFiltro = true;
        }

        if (!string.IsNullOrWhiteSpace(Input.ReferenciaExterna))
        {
            filtro.RefExterna = NormalizeString(Input.ReferenciaExterna);
            hasFiltro = true;
        }

        if (!string.IsNullOrWhiteSpace(Input.ClavePaginacion))
        {
            filtro.ClavePaginacion = new RemoteConsultationClaveDto
            {
                NumSerieFactura = NormalizeString(Input.ClavePaginacion)
            };
            hasFiltro = true;
        }

        if (!string.IsNullOrWhiteSpace(filtroContraparteNif))
        {
            filtro.Contraparte = new RemoteConsultationPersonaDto
            {
                Nif = filtroContraparteNif
            };
            hasFiltro = true;
        }

        if (!string.IsNullOrWhiteSpace(Input.Ejercicio) || !string.IsNullOrWhiteSpace(Input.Periodo))
        {
            filtro.PeriodoImputacion = new RemoteConsultationPeriodoDto
            {
                Ejercicio = NormalizeString(Input.Ejercicio),
                Periodo = NormalizeString(Input.Periodo)
            };
            hasFiltro = true;
        }

        if (Input.FechaDesde.HasValue || Input.FechaHasta.HasValue)
        {
            filtro.FechaExpedicionFactura = new RemoteConsultationFechaFiltroDto
            {
                RangoFechaExpedicion = new RemoteConsultationRangoFechaDto
                {
                    Desde = Input.FechaDesde?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    Hasta = Input.FechaHasta?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
                }
            };

            hasFiltro = true;
        }

        if (!string.IsNullOrWhiteSpace(Input.SistemaInformaticoNombre) || !string.IsNullOrWhiteSpace(Input.SistemaInformaticoVersion))
        {
            filtro.SistemaInformatico = new RemoteConsultationSistemaInformaticoDto
            {
                NombreSistemaInformatico = NormalizeString(Input.SistemaInformaticoNombre),
                Version = NormalizeString(Input.SistemaInformaticoVersion)
            };
            hasFiltro = true;
        }

        if (hasFiltro)
        {
            request.FiltroConsulta = filtro;
        }

        return request;
    }

    private IList<RemoteConsultationRecordViewModel> BuildRecords(IList<JsonElement> registros)
    {
        var results = new List<RemoteConsultationRecordViewModel>();
        if (registros is null)
        {
            return results;
        }

        foreach (var registro in registros)
        {
            results.Add(new RemoteConsultationRecordViewModel
            {
                Serie = ReadString(registro, ["idFactura", "numSerieFacturaEmisor"], ["numSerieFacturaEmisor"], ["serie"]),
                Numero = ReadString(registro, ["idFactura", "numFactura"], ["numFactura"], ["numero"]),
                Fecha = ReadString(registro, ["fechaExpedicionFactura"], ["datosFactura", "fechaExpedicionFactura"]),
                Estado = ReadString(registro, ["estadoRegistro"], ["estadoFactura"]),
                Importe = ReadString(registro, ["datosFactura", "importeTotalFactura"], ["importeTotal"], ["importeTotalFactura"]),
                Huella = ReadString(registro, ["huellaDigital"], ["huella"]),
                Cliente = ReadString(registro, ["contraparte", "nif"], ["contraparte", "idOtro"], ["destinatario", "nif"]),
                RawJson = JsonSerializer.Serialize(registro, new JsonSerializerOptions { WriteIndented = true })
            });
        }

        return results;
    }

    private static string? ReadString(JsonElement element, params string[][] paths)
    {
        foreach (var path in paths)
        {
            if (TryTraverse(element, path, out var target))
            {
                switch (target.ValueKind)
                {
                    case JsonValueKind.String:
                        return target.GetString();
                    case JsonValueKind.Number:
                        if (target.TryGetDecimal(out var dec))
                        {
                            return dec.ToString("0.##", CultureInfo.InvariantCulture);
                        }

                        return target.GetRawText();
                    case JsonValueKind.True:
                    case JsonValueKind.False:
                        return target.GetBoolean().ToString();
                    default:
                        return target.GetRawText();
                }
            }
        }

        return null;
    }

    private static bool TryTraverse(JsonElement element, ReadOnlySpan<string> path, out JsonElement target)
    {
        target = element;
        foreach (var segment in path)
        {
            if (target.ValueKind != JsonValueKind.Object || !target.TryGetProperty(segment, out target))
            {
                target = default;
                return false;
            }
        }

        return true;
    }

    public sealed class ConsultaInputModel
    {
        [Display(Name = "Número de serie")]
        public string? NumeroSerie { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Fecha desde")]
        public DateTime? FechaDesde { get; set; }

        [DataType(DataType.Date)]
        [Display(Name = "Fecha hasta")]
        public DateTime? FechaHasta { get; set; }

        [Display(Name = "Ejercicio")]
        [StringLength(4, ErrorMessage = "El ejercicio debe tener 4 dígitos.")]
        public string? Ejercicio { get; set; }

        [Display(Name = "Periodo")]
        [StringLength(2, ErrorMessage = "El periodo debe tener 2 dígitos.")]
        public string? Periodo { get; set; }

        [Display(Name = "NIF contraparte (opcional)")]
        public string? ClienteNif { get; set; }

        [Display(Name = "Referencia externa")]
        public string? ReferenciaExterna { get; set; }

        [Display(Name = "Clave paginación")]
        public string? ClavePaginacion { get; set; }

        [Display(Name = "ID consulta")]
        public string? ConsultaId { get; set; }

        [Display(Name = "Mostrar nombre emisor")]
        public bool MostrarNombreEmisor { get; set; }

        [Display(Name = "Mostrar sistema informático")]
        public bool MostrarSistemaInformatico { get; set; }

        [Display(Name = "Guardar resultado en S3")]
        public bool GuardarResultado { get; set; }

        [Display(Name = "Nombre sistema informático")]
        public string? SistemaInformaticoNombre { get; set; }

        [Display(Name = "Versión sistema informático")]
        public string? SistemaInformaticoVersion { get; set; }

        [Display(Name = "Idioma sistema informático")]
        public string? SistemaInformaticoIdioma { get; set; }

        [Display(Name = "NIF obligado")]
        public string? NifObligado { get; set; }
    }

    public sealed class RemoteConsultationRecordViewModel
    {
        public string? Serie { get; init; }
        public string? Numero { get; init; }
        public string? Fecha { get; init; }
        public string? Estado { get; init; }
        public string? Importe { get; init; }
        public string? Huella { get; init; }
        public string? Cliente { get; init; }
        public string RawJson { get; init; } = string.Empty;
    }

    private static string? NormalizeString(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string? NormalizeNif(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim().ToUpperInvariant();

    private string? ResolveObligadoNombre(RemoteUserStatusDto status)
    {
        var candidate = NormalizeString(status.NombreRazon) ?? NormalizeString(status.NombreEmisorFactura);
        if (!string.IsNullOrWhiteSpace(candidate))
        {
            return candidate;
        }

        candidate = NormalizeString(User?.FindFirstValue("name"));
        if (!string.IsNullOrWhiteSpace(candidate))
        {
            return candidate;
        }

        var given = NormalizeString(User?.FindFirstValue("given_name"));
        var family = NormalizeString(User?.FindFirstValue("family_name"));

        if (!string.IsNullOrWhiteSpace(given) || !string.IsNullOrWhiteSpace(family))
        {
            return string.Join(" ", new[] { given, family }.Where(static x => !string.IsNullOrWhiteSpace(x)));
        }

        return null;
    }
}
