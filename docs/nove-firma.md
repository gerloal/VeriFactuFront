# Firma local para tenants NOVERIFACTU

Este documento resume cómo funciona el circuito de firma local de facturas para tenants configurados como `NOVERIFACTU`.

## Condiciones de despliegue

1. El tenant debe estar marcado en el backend con `TenantSystemType = NOVERIFACTU` (la UI usa `ITenantContext.GetSystemTypeAsync()` para detectar esto).
2. Los tenants `VERIFACTU` no muestran controles de firma local: la operación se gestiona a través del backend y la AEAT.
3. La página `Pages/Batches/Details` muestra un panel exclusivo con el selector de certificados, el listado de facturas y botones para descargar/firmar el XML de cada elemento.

## Flujo de firma

1. El usuario selecciona un certificado disponible (lista ofrecida por `browserCertificateService`).
2. Al pulsar **Descargar XML** se llama al handler `OnPostDownloadXmlAsync`, que obtiene el XML base64 de la factura (`VerifactuApiClient.GetInvoiceXmlAsync`).
3. El botón **Firmar y guardar** descarga (si es necesario) el XML, calcula el SHA-256 (hex + base64), invoca `browserCertificateService.sign(...)` y construye el payload `{ tenantId, batchId, itemId, facturaId, xmlSignedBase64, hashSha256Hex, metadata }`.
4. Ese payload se envía a `OnPostSubmitSignedAsync`, que delega en `VerifactuApiClient.SubmitSignedInvoiceAsync` y POST `/signed`.
5. La tabla refleja el estado de cada factura (`Sin firmar`, `XML descargado`, `Firmada`).

## Pruebas manuales recomendadas

1. Configurar un tenant con `SystemType = NOVERIFACTU` y acceder a `Batches/Details`.
2. Asegurarse de que el panel de firma local y los botones `Descargar XML` / `Firmar y guardar` están visibles.
3. Instalar/activar el conector de certificados local o habilitar el mock (`window.__browserPkiEnableMock = true`).
4. Crear un lote con facturas y, desde los detalles, descargar el XML de una factura.
5. Firmarla usando un certificado válido; revisar que `hashSha256Hex` real y la metadata se envían al backend (`/signed`).
6. Verificar que el backend responde `200` y que la fila muestra el estado `Firmada`.
7. Repetir sin certificado para verificar mensajes de error.

## Notas de implementación

- `invoiceSigner.js` comparte utilidades con `batchUpload.js` (hash SHA-256, base64, `browserCertificateService`).
- El selector de certificados almacena atributos `data-subject`, `data-issuer` y `data-thumbprint` para enviarlos como metadata.
- Los handlers Razor devuelven JSON con mensajes amigables para que la UI los refleje.
- El layout expone `window.__isNoVerifactuTenant` para que el script se autoexcluya en tenants Verifactu.

## Limitaciones

- Solo se puede firmar una factura a la vez desde el navegador.
- El backend debe validar `hashSha256Hex` y la firma CAdES independientemente de la UI.
- Si no hay proveedor de certificados, el panel muestra un aviso y se deshabilitan las acciones de firma.
