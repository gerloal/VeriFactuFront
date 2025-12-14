(function setupInvoiceSigner() {
    const root = document.querySelector('[data-invoice-signer-root]');
    const isNoverifactu = window.__isNoVerifactuTenant === true || window.__isNoVerifactuTenant === 'true';

    if (!root || !isNoverifactu) {
        return;
    }

    const batchId = root.dataset.batchId;
    const tenantId = (root.dataset.tenantId || '').trim();
    const tokenInput = document.querySelector('input[name="__RequestVerificationToken"]');
    const antiForgeryToken = tokenInput ? tokenInput.value : '';
    const certificateSelect = document.querySelector('[data-invoice-signer-select]');
    const certificateRefresh = document.querySelector('[data-invoice-signer-refresh]');
    const certificateStatus = document.querySelector('[data-invoice-signer-status]');

    const handlerBase = window.location.pathname;
    const state = {
        certificates: [],
        loadingCertificates: false
    };

    const STATUS_STYLES = {
        pending: 'bg-secondary',
        downloading: 'bg-info',
        signed: 'bg-success',
        error: 'bg-danger'
    };

    function formatCertificateLabel(cert) {
        const subject = cert.subject || cert.displayName || 'Certificado';
        const issuer = cert.issuer ? ` — ${cert.issuer}` : '';
        const thumbprint = cert.thumbprint ? ` (${cert.thumbprint})` : '';
        return `${subject}${issuer}${thumbprint}`;
    }

    function setCertificateStatus(message, variant) {
        if (!certificateStatus) {
            return;
        }

        certificateStatus.textContent = message || '';
        certificateStatus.classList.toggle('text-danger', variant === 'danger');
        certificateStatus.classList.toggle('text-muted', !variant);
    }

    async function loadCertificates(force = false) {
        if (state.loadingCertificates) {
            return;
        }

        if (!force && state.certificates.length > 0) {
            return;
        }

        if (!certificateSelect || !window.browserCertificateService) {
            setCertificateStatus('Servicio de certificados no disponible.', 'danger');
            return;
        }

        state.loadingCertificates = true;
        setCertificateStatus('Buscando certificados disponibles...');

        try {
            await window.browserCertificateService.ensureAvailability();
            const certificates = await window.browserCertificateService.listCertificates();
            state.certificates = Array.isArray(certificates) ? certificates : [];
            renderCertificates();
        } catch (error) {
            console.warn('[InvoiceSigner] No se pudieron cargar certificados', error);
            state.certificates = [];
            certificateSelect.innerHTML = '<option value="">No hay certificados disponibles</option>';
            setCertificateStatus(error?.message || 'No se pudo acceder al conector de certificados.', 'danger');
        } finally {
            state.loadingCertificates = false;
        }
    }

    function renderCertificates() {
        if (!certificateSelect) {
            return;
        }

        certificateSelect.innerHTML = '';

        const placeholder = document.createElement('option');
        placeholder.value = '';
        placeholder.textContent = state.certificates.length ? 'Selecciona un certificado' : 'No hay certificados detectados';
        certificateSelect.appendChild(placeholder);

        state.certificates.forEach((certificate) => {
            const option = document.createElement('option');
            const id = certificate.id || certificate.thumbprint || certificate.subject || '';
            option.value = id;
            option.textContent = formatCertificateLabel(certificate);
            if (certificate.subject) {
                option.dataset.subject = certificate.subject;
            }
            if (certificate.issuer) {
                option.dataset.issuer = certificate.issuer;
            }
            if (certificate.thumbprint) {
                option.dataset.thumbprint = certificate.thumbprint;
            }
            certificateSelect.appendChild(option);
        });

        setCertificateStatus(state.certificates.length
            ? 'Selecciona el certificado con el que firmarás las facturas.'
            : 'Instala o activa el conector de certificados para firmar localmente.');
    }

    function arrayBufferToBase64(buffer) {
        const bytes = new Uint8Array(buffer);
        let binary = '';
        const chunkSize = 0x8000;
        for (let i = 0; i < bytes.length; i += chunkSize) {
            const chunk = bytes.subarray(i, i + chunkSize);
            binary += String.fromCharCode.apply(null, chunk);
        }
        return window.btoa(binary);
    }

    function arrayBufferToHex(buffer) {
        const view = new DataView(buffer);
        const hexParts = [];
        for (let i = 0; i < view.byteLength; i += 4) {
            const value = view.getUint32(i);
            hexParts.push(value.toString(16).padStart(8, '0'));
        }
        return hexParts.join('');
    }

    function base64ToArrayBuffer(base64) {
        const binary = window.atob(base64);
        const length = binary.length;
        const buffer = new ArrayBuffer(length);
        const bytes = new Uint8Array(buffer);
        for (let i = 0; i < length; i += 1) {
            bytes[i] = binary.charCodeAt(i);
        }
        return buffer;
    }

    async function computeHash(buffer) {
        if (!window.crypto?.subtle) {
            throw new Error('Tu navegador no soporta las APIs criptográficas necesarias.');
        }

        const digest = await window.crypto.subtle.digest('SHA-256', buffer);
        return {
            hex: arrayBufferToHex(digest),
            base64: arrayBufferToBase64(digest)
        };
    }

    function updateRowStatus(row, stateKey, message) {
        const badge = row.querySelector('[data-invoice-sign-badge]');
        const detail = row.querySelector('[data-invoice-sign-detail]');
        if (!badge) {
            return;
        }

        badge.textContent = message || (stateKey === 'signed' ? 'Firmada' : 'Sin firmar');
        badge.className = `badge ${STATUS_STYLES[stateKey] || STATUS_STYLES.pending}`;
        if (detail) {
            detail.textContent = message
                ?? (stateKey === 'signed'
                    ? 'Factura firmada y almacenada en el backend.'
                    : stateKey === 'downloading'
                        ? 'Descargando XML para firmar...'
                        : stateKey === 'error'
                            ? 'Error de firma.'
                            : '');
        }
    }

    function getHandlerUrl(handler) {
        return `${handlerBase}?handler=${handler}`;
    }

    async function fetchXml(itemId) {
        const formData = new FormData();
        if (antiForgeryToken) {
            formData.set('__RequestVerificationToken', antiForgeryToken);
        }
        formData.set('itemId', itemId);

        const response = await fetch(getHandlerUrl('DownloadXml'), {
            method: 'POST',
            body: formData,
            credentials: 'same-origin'
        });

        if (!response.ok) {
            const error = await extractErrorMessage(response);
            throw new Error(error || 'No se pudo descargar el XML.');
        }

        return response.json();
    }

    async function extractErrorMessage(response) {
        try {
            const data = await response.json();
            return data?.message;
        } catch {
            return response.statusText;
        }
    }

    async function submitSignedPayload(payload) {
        const response = await fetch(getHandlerUrl('SubmitSigned'), {
            method: 'POST',
            credentials: 'same-origin',
            headers: {
                'Content-Type': 'application/json',
                'RequestVerificationToken': antiForgeryToken,
                'X-Requested-With': 'XMLHttpRequest'
            },
            body: JSON.stringify(payload)
        });

        if (!response.ok) {
            const error = await extractErrorMessage(response);
            throw new Error(error || 'No se pudo guardar la factura firmada.');
        }

        return response.json();
    }

    function buildMetadata(option) {
        const metadata = {
            signedAt: new Date().toISOString(),
            certificateId: option?.value || undefined,
            certificateThumbprint: option?.dataset?.thumbprint || undefined,
            certificateSubject: option?.dataset?.subject || undefined,
            certificateIssuer: option?.dataset?.issuer || undefined
        };

        Object.keys(metadata).forEach((key) => {
            if (metadata[key] === undefined) {
                delete metadata[key];
            }
        });

        return metadata;
    }

    async function handleDownload(row) {
        const itemId = row.dataset.invoiceItemId;
        const badgeMessage = 'Descargando XML...';
        updateRowStatus(row, 'downloading', badgeMessage);

        try {
            const payload = await fetchXml(itemId);
            const xmlBase64 = payload?.xmlBase64 || '';
            if (!xmlBase64) {
                throw new Error('El backend no devolvió el XML.');
            }

            row.dataset.invoiceXml = xmlBase64;
            if (payload.facturaId) {
                row.dataset.invoiceFacturaId = payload.facturaId;
            }

            updateRowStatus(row, 'pending', 'XML descargado.');
        } catch (error) {
            updateRowStatus(row, 'error', error?.message);
            throw error;
        }
    }

    async function handleSign(row) {
        const downloadButton = row.querySelector('button[data-invoice-action="download"]');
        const signButton = row.querySelector('button[data-invoice-action="sign"]');
        const badgeMessage = 'Firmando factura...';
        updateRowStatus(row, 'downloading', badgeMessage);

        try {
            let xmlBase64 = row.dataset.invoiceXml;
            if (!xmlBase64) {
                await handleDownload(row);
                xmlBase64 = row.dataset.invoiceXml;
            }

            const buffer = base64ToArrayBuffer(xmlBase64 || '');
            const hash = await computeHash(buffer);

            const selectedOption = certificateSelect?.selectedOptions[0];
            const certificateId = selectedOption?.value?.trim();
            if (!certificateId) {
                throw new Error('Selecciona un certificado antes de firmar.');
            }

            if (!window.browserCertificateService) {
                throw new Error('El servicio de certificados no está disponible.');
            }

            const signatureResult = await window.browserCertificateService.sign(buffer, certificateId, {
                hashAlgorithm: 'SHA-256',
                format: 'CAdES'
            });

            if (!signatureResult || !signatureResult.signature) {
                throw new Error('El proveedor no devolvió la firma.');
            }

            const metadata = buildMetadata(selectedOption);
            metadata.payloadHashBase64 = hash.base64;

            const payload = {
                tenantId: tenantId || undefined,
                batchId,
                itemId: row.dataset.invoiceItemId,
                facturaId: row.dataset.invoiceFacturaId || undefined,
                xmlSignedBase64: signatureResult.signature,
                hashSha256Hex: hash.hex,
                metadata
            };

            await submitSignedPayload(payload);
            updateRowStatus(row, 'signed', 'Firma almacenada correctamente.');
        } catch (error) {
            updateRowStatus(row, 'error', error?.message);
            throw error;
        } finally {
            if (signButton) {
                signButton.disabled = false;
            }
            if (downloadButton) {
                downloadButton.disabled = false;
            }
        }
    }

    root.addEventListener('click', async (event) => {
        const button = event.target.closest('button[data-invoice-action]');
        if (!button) {
            return;
        }

        event.preventDefault();
        const action = button.dataset.invoiceAction;
        const row = button.closest('[data-invoice-row]');
        if (!row) {
            return;
        }

        button.disabled = true;

        try {
            if (action === 'download') {
                await handleDownload(row);
            } else if (action === 'sign') {
                await handleSign(row);
            }
        } catch (error) {
            console.error('[InvoiceSigner]', error);
        } finally {
            button.disabled = false;
        }
    });

    certificateRefresh?.addEventListener('click', () => loadCertificates(true));

    loadCertificates(true).catch((error) => {
        console.warn('[InvoiceSigner] Error iniciando certificados', error);
    });
})();
