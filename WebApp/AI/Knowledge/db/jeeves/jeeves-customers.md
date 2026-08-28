# Jeeves Customers - Domain Hints (for SQL generation)

This document contains domain-specific hints used by the AI SQL generator.
Keep it plain-text and deterministic. Avoid customer-specific data.

## Credit limit (kundkredit)
- Credit limit is stored in `dbo.kus.kundkredlim` (not `dbo.fr.aktiekap`).
- When questions mention "kreditlimit", "kreditgräns", or "credit limit", prefer `kus.kundkredlim`.
- If both `fr` (customer) and `kus` exist, use `fr` for customer name and `kus` for credit limit.

## Typical joins (if columns exist)
- Customer number (pick ONLY columns that exist in schema):
  - `oh.FtgNr` (order header) ↔ `fr.FtgNr` (customer)
  - Prefer `kus.FtgNr` to join with `fr.FtgNr`
  - Use `kus.KundNr` ONLY if it exists in schema (do NOT assume it exists)
- Company code:
  - Use `ForetagKod` on all tables if present.
