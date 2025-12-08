using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Net.Http;
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

    public string? RemoteEndpoint => _options.RemoteConsultationEndpoint;

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

        if (string.IsNullOrWhiteSpace(_options.RemoteConsultationEndpoint))
        {
            ErrorMessage = "No hay un endpoint de consulta AEAT configurado. Contacta con el administrador.";
            return Page();
        }

        var remoteStatus = _remoteStatus ?? await _apiClient.GetRemoteUserStatusAsync().ConfigureAwait(false);
        _remoteStatus = remoteStatus;

        if (string.IsNullOrWhiteSpace(remoteStatus.TenantId))
        {
            ErrorMessage = "No se pudo determinar el tenant asociado al usuario.";
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

        if (string.IsNullOrWhiteSpace(Input.NifObligado) && _remoteStatus is not null && !string.IsNullOrWhiteSpace(_remoteStatus.TenantId))
        {
            Input.NifObligado = _remoteStatus.TenantId;
        }
    }

    private RemoteConsultationRequestDto BuildRequest(RemoteUserStatusDto status)
    {
        var tenantId = status.TenantId ?? _options.TenantId ?? string.Empty;

        var filtro = new RemoteConsultationFiltroDto
        {
            NumSerieFactura = string.IsNullOrWhiteSpace(Input.NumeroSerie) ? null : Input.NumeroSerie.Trim(),
            RefExterna = string.IsNullOrWhiteSpace(Input.ReferenciaExterna) ? null : Input.ReferenciaExterna.Trim(),
            ClavePaginacion = string.IsNullOrWhiteSpace(Input.ClavePaginacion) ? null : Input.ClavePaginacion.Trim()
        };

        if (!string.IsNullOrWhiteSpace(Input.ClienteNif))
        {
            filtro.Contraparte = new RemoteConsultationContraparteDto
            {
                Nif = Input.ClienteNif!.Trim().ToUpperInvariant()
            };
        }

        if (Input.FechaDesde.HasValue || Input.FechaHasta.HasValue)
        {
            filtro.FechaExpedicionFactura = new RemoteConsultationFechaFiltroDto
            {
                FechaDesde = Input.FechaDesde?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                FechaHasta = Input.FechaHasta?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            };
        }

        if (!string.IsNullOrWhiteSpace(Input.Ejercicio) || !string.IsNullOrWhiteSpace(Input.Periodo))
        {
            filtro.PeriodoImputacion = new RemoteConsultationPeriodoDto
            {
                Ejercicio = string.IsNullOrWhiteSpace(Input.Ejercicio) ? null : Input.Ejercicio.Trim(),
                Periodo = string.IsNullOrWhiteSpace(Input.Periodo) ? null : Input.Periodo.Trim()
            };
        }

        if (!string.IsNullOrWhiteSpace(Input.SistemaInformaticoNombre) || !string.IsNullOrWhiteSpace(Input.SistemaInformaticoVersion) || !string.IsNullOrWhiteSpace(Input.SistemaInformaticoIdioma))
        {
            filtro.SistemaInformatico = new RemoteConsultationSistemaInformaticoDto
            {
                Nombre = string.IsNullOrWhiteSpace(Input.SistemaInformaticoNombre) ? null : Input.SistemaInformaticoNombre.Trim(),
                Version = string.IsNullOrWhiteSpace(Input.SistemaInformaticoVersion) ? null : Input.SistemaInformaticoVersion.Trim(),
                Idioma = string.IsNullOrWhiteSpace(Input.SistemaInformaticoIdioma) ? null : Input.SistemaInformaticoIdioma.Trim()
            };
        }

        var cabecera = new RemoteConsultationCabeceraDto
        {
            ObligadoEmision = string.IsNullOrWhiteSpace(tenantId) ? null : new RemoteConsultationContraparteDto { Nif = tenantId },
            Destinatario = string.IsNullOrWhiteSpace(Input.ClienteNif) ? null : new RemoteConsultationContraparteDto { Nif = Input.ClienteNif!.Trim().ToUpperInvariant() }
        };

        var request = new RemoteConsultationRequestDto
        {
            TenantId = tenantId,
            Endpoint = _options.RemoteConsultationEndpoint?.Trim() ?? string.Empty,
            ConsultaId = string.IsNullOrWhiteSpace(Input.ConsultaId) ? null : Input.ConsultaId.Trim(),
            StoreResult = Input.GuardarResultado,
            Cabecera = cabecera,
            FiltroConsulta = filtro,
            DatosAdicionalesRespuesta = new RemoteConsultationAdditionalDataDto
            {
                MostrarNombreEmisor = Input.MostrarNombreEmisor,
                MostrarSistemaInformatico = Input.MostrarSistemaInformatico
            },
            RequestContext = new RemoteConsultationContextDto
            {
                Ejercicio = string.IsNullOrWhiteSpace(Input.Ejercicio) ? null : Input.Ejercicio.Trim(),
                Periodo = string.IsNullOrWhiteSpace(Input.Periodo) ? null : Input.Periodo.Trim(),
                NifObligado = string.IsNullOrWhiteSpace(Input.NifObligado) ? tenantId : Input.NifObligado!.Trim().ToUpperInvariant()
            }
        };

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

        [Display(Name = "NIF cliente")]
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
        public bool GuardarResultado { get; set; } = true;

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
}
