(() => {
  const config = {
    amountTolerance: 1.0,
    dateWindowDays: 7
  };

  const normalizeRef = (value) =>
    (value || '')
      .toString()
      .toUpperCase()
      .replace(/\s+/g, '')
      .replace(/[^A-Z0-9]/g, '')
      .trim();

  const normalizeName = (value) =>
    (value || '')
      .toString()
      .toUpperCase()
      .replace(/\s+/g, ' ')
      .replace(/[^A-ZÅÄÖ0-9 ]/g, '')
      .trim();

  const tokenizeName = (value) =>
    normalizeName(value)
      .split(' ')
      .filter((part) => part.length >= 3);

  const parseDate = (value) => {
    if (!value) return null;
    const date = new Date(value);
    return Number.isNaN(date.getTime()) ? null : date;
  };

  const daysBetween = (a, b) => {
    if (!a || !b) return null;
    const diffMs = Math.abs(a.getTime() - b.getTime());
    return Math.round(diffMs / (1000 * 60 * 60 * 24));
  };

  const getTxRefs = (tx) => {
    const refs = [
      tx.reference,
      tx.remittance,
      tx.endToEndId,
      tx.txId,
      tx.acctSvcrRef
    ];
    return refs
      .map((ref) => normalizeRef(ref))
      .filter((ref) => ref.length > 0);
  };

  const getInvoiceRefs = (inv) => {
    const refs = [inv.ocr, inv.invoiceNo, inv.id];
    return refs
      .map((ref) => normalizeRef(ref))
      .filter((ref) => ref.length > 0);
  };

  const getMatchSignals = (tx, inv) => {
    const txRefs = getTxRefs(tx);
    const invRefs = getInvoiceRefs(inv);

    const refExact = invRefs.some((invRef) => txRefs.includes(invRef));
    const refPartial = !refExact && invRefs.some((invRef) => txRefs.some((txRef) => txRef.includes(invRef)));

    const amountExact = (tx.amount || 0) === (inv.amount || 0);
    const amountTolerance = !amountExact && Math.abs((tx.amount || 0) - (inv.amount || 0)) <= config.amountTolerance;
    const currencyMatch = !tx.currency || !inv.currency || tx.currency === inv.currency;

    const txNameTokens = tokenizeName(tx.debtorName);
    const invNameTokens = tokenizeName(inv.customerName);
    const nameMatch = txNameTokens.some((token) => invNameTokens.includes(token));

    const txDate = parseDate(tx.date || tx.valueDate);
    const invDate = parseDate(inv.dueDate);
    const dayDiff = daysBetween(txDate, invDate);
    const dateMatch = dayDiff !== null && dayDiff <= config.dateWindowDays;

    return {
      refExact,
      refPartial,
      amountExact,
      amountTolerance,
      currencyMatch,
      nameMatch,
      dateMatch
    };
  };

  const getConfidence = (signals) => {
    let score = 0;
    if (signals.refExact) score += 60;
    if (signals.refPartial) score += 30;
    if (signals.amountExact) score += 30;
    if (signals.amountTolerance) score += 15;
    if (signals.nameMatch) score += 10;
    if (signals.dateMatch) score += 10;
    if (signals.currencyMatch) score += 5;

    const level = score >= 80 ? 'Hög' : score >= 50 ? 'Medel' : 'Låg';
    return { level, score };
  };

  const getRuleLabel = (signals) => {
    if (signals.refExact && signals.amountExact) return 'OCR/Referens + Belopp';
    if (signals.refExact) return 'OCR/Referens';
    if (signals.amountExact) return 'Belopp';
    if (signals.nameMatch) return 'Betalare';
    if (signals.dateMatch) return 'Datum';
    return 'Manuell';
  };

  const getRuleHelp = (signals) => {
    if (signals.refExact && signals.amountExact) return 'OCR/Referens och belopp matchade fakturan.';
    if (signals.refExact) return 'OCR/Referens matchade fakturan.';
    if (signals.amountExact) return 'Belopp matchade fakturan exakt.';
    if (signals.nameMatch) return 'Betalarnamn matchade kund.';
    if (signals.dateMatch) return 'Datum låg nära förfallodatum.';
    return 'Matchning gjord manuellt.';
  };

  const describeMatch = (tx, inv) => {
    const signals = getMatchSignals(tx, inv);
    const confidence = getConfidence(signals);
    const manual = (tx.matchType || '').toLowerCase() === 'manual';
    const ruleLabel = manual ? 'Manuell' : getRuleLabel(signals);
    const ruleHelp = manual ? 'Matchning gjord manuellt.' : getRuleHelp(signals);
    const adjustedConfidence = manual && confidence.level === 'Låg'
      ? { ...confidence, level: 'Medel' }
      : confidence;
    return {
      signals,
      confidence: adjustedConfidence,
      ruleLabel,
      ruleHelp
    };
  };

  const autoMatch = (transactions, invoices, options) => {
    const { isCredit, getInvoiceRemaining, markMatched } = options;
    const unmatched = transactions.filter((tx) => !tx.matchedInvoiceId && isCredit(tx));

    const tryMatch = (predicate, rule) => {
      unmatched.forEach((tx) => {
        if (tx.matchedInvoiceId) return;
        const candidates = invoices.filter((inv) => predicate(tx, inv));
        if (candidates.length === 1) {
          markMatched(tx, candidates[0], 'auto', rule);
        }
      });
    };

    tryMatch(
      (tx, inv) => {
        const signals = getMatchSignals(tx, inv);
        return signals.refExact && signals.amountExact && signals.currencyMatch && getInvoiceRemaining(inv) >= (tx.amount || 0);
      },
      'reference+amount'
    );

    tryMatch(
      (tx, inv) => {
        const signals = getMatchSignals(tx, inv);
        return signals.refExact && (signals.amountExact || signals.amountTolerance) && getInvoiceRemaining(inv) >= (tx.amount || 0);
      },
      'reference'
    );

    tryMatch(
      (tx, inv) => {
        const signals = getMatchSignals(tx, inv);
        return signals.amountExact && signals.nameMatch && getInvoiceRemaining(inv) >= (tx.amount || 0);
      },
      'amount+name'
    );

    tryMatch(
      (tx, inv) => {
        const signals = getMatchSignals(tx, inv);
        return signals.amountExact && signals.dateMatch && getInvoiceRemaining(inv) >= (tx.amount || 0);
      },
      'amount+date'
    );
  };

  window.BankRecMatching = {
    normalizeRef,
    getMatchSignals,
    describeMatch,
    autoMatch
  };
})();
