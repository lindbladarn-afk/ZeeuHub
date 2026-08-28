// Handles Intelligence HTTP calls, NDJSON progress streaming, and safe transport errors.
window.ZeeUAIQueryClient = (() => {
    const requestTimeoutMs = 120000;
    let activeQueryController = null;

    class AiTransportError extends Error {
        constructor(message, code = 'network_error', status = null, canRetry = true) {
            super(message);
            this.name = 'AiTransportError';
            this.code = code;
            this.status = status;
            this.canRetry = canRetry;
        }
    }

    const getAntiForgery = () => {
        const tokenInput = document.querySelector('#__af input[name="__RequestVerificationToken"]');
        return tokenInput?.value || null;
    };

    const requestHeaders = (includeJson = true) => {
        const token = getAntiForgery();
        return {
            ...(includeJson ? { 'Content-Type': 'application/json' } : {}),
            ...(token ? { RequestVerificationToken: token } : {})
        };
    };

    const readErrorMessage = async (response) => {
        try {
            const payload = await response.json();
            return payload?.message || payload?.title || `HTTP ${response.status}`;
        } catch {
            return `HTTP ${response.status}`;
        }
    };

    const fetchWithTimeout = async (url, options = {}, suppliedController = null) => {
        const controller = suppliedController || new AbortController();
        let timedOut = false;
        const timeout = window.setTimeout(() => {
            timedOut = true;
            controller.abort();
        }, requestTimeoutMs);
        try {
            return await fetch(url, { ...options, signal: controller.signal });
        } catch (error) {
            if (error?.name === 'AbortError') {
                if (!timedOut) {
                    throw new AiTransportError(
                        'Analysen avbröts.',
                        'cancelled',
                        null,
                        false);
                }
                throw new AiTransportError(
                    'Analysen tog för lång tid. Försök med en mer avgränsad fråga.',
                    'timeout',
                    null,
                    true);
            }
            throw new AiTransportError(
                navigator.onLine
                    ? 'Kunde inte nå ZeeU Intelligence. Försök igen om en stund.'
                    : 'Du verkar sakna nätverksanslutning. Anslut och försök igen.',
                navigator.onLine ? 'network_error' : 'offline',
                null,
                true);
        } finally {
            window.clearTimeout(timeout);
        }
    };

    const requestJson = async (url, options = {}) => {
        const response = await fetchWithTimeout(url, options);
        if (!response.ok) {
            throw new AiTransportError(
                await readErrorMessage(response),
                'http_error',
                response.status,
                response.status >= 500);
        }
        return await response.json();
    };

    const query = async (question, context, onProgress) => {
        const controller = new AbortController();
        activeQueryController?.abort();
        activeQueryController = controller;

        try {
            const response = await fetchWithTimeout('/AI/query-stream', {
                method: 'POST',
                headers: requestHeaders(),
                body: JSON.stringify({
                    question,
                    source: context?.source || 'intelligence',
                    dataSourceKey: context?.dataSourceKey || null
                })
            }, controller);

            if (!response.ok) {
                throw new AiTransportError(
                    await readErrorMessage(response),
                    'http_error',
                    response.status,
                    response.status >= 500);
            }
            if (!response.body) {
                throw new AiTransportError(
                    'Webbläsaren kunde inte läsa analysens statusflöde.',
                    'stream_unavailable',
                    response.status,
                    true);
            }

            const reader = response.body.getReader();
            const decoder = new TextDecoder();
            let buffer = '';
            let result = null;

            const consumeLine = (line) => {
                if (!line.trim()) return;

                let streamEvent;
                try {
                    streamEvent = JSON.parse(line);
                } catch {
                    throw new AiTransportError(
                        'Ett ofullständigt svar togs emot från ZeeU Intelligence.',
                        'invalid_stream',
                        response.status,
                        true);
                }

                if (streamEvent.type === 'progress' && streamEvent.progress) {
                    onProgress?.(streamEvent.progress);
                } else if (streamEvent.type === 'result' && streamEvent.result) {
                    result = streamEvent.result;
                }
            };

            while (true) {
                const { value, done } = await reader.read();
                buffer += decoder.decode(value || new Uint8Array(), { stream: !done });
                const lines = buffer.split('\n');
                buffer = lines.pop() || '';
                lines.forEach(consumeLine);
                if (done) break;
            }

            if (buffer.trim()) consumeLine(buffer);
            if (!result) {
                throw new AiTransportError(
                    'Analysen avslutades utan ett fullständigt svar.',
                    'incomplete_response',
                    response.status,
                    true);
            }

            return result;
        } catch (error) {
            if (controller.signal.aborted && error?.code !== 'timeout') {
                throw new AiTransportError('Analysen avbröts.', 'cancelled', null, false);
            }
            throw error;
        } finally {
            if (activeQueryController === controller) activeQueryController = null;
        }
    };

    const cancelActiveQuery = () => activeQueryController?.abort();

    const manualQuery = (sql) => requestJson('/AI/manual-query', {
        method: 'POST',
        headers: requestHeaders(),
        body: JSON.stringify({ sql })
    });

    const setQuotaDecision = (choice) => requestJson('/AI/quota-decision', {
        method: 'POST',
        headers: requestHeaders(),
        body: JSON.stringify({ choice })
    });

    const getQuotaStatus = () => requestJson('/AI/quota-status', { method: 'GET' });

    const submitFeedback = (responseId, rating, comment) => requestJson('/AI/feedback', {
        method: 'POST',
        headers: requestHeaders(),
        body: JSON.stringify({ responseId, rating, comment: comment || null })
    });

    return {
        AiTransportError,
        query,
        cancelActiveQuery,
        manualQuery,
        setQuotaDecision,
        getQuotaStatus,
        submitFeedback
    };
})();
