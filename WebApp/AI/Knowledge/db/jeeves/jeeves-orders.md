# Jeeves Orders - Domain Hints (for SQL generation)

This document contains domain-specific hints used by the AI SQL generator.
Keep it plain-text and deterministic. Avoid customer-specific data.

## Core tables (typical)
- Order header: `dbo.oh`
- Order lines: `dbo.orp`
- Customers: `dbo.fr`

## Key columns (common)
Order header (`dbo.oh`)
- `oh.OrderNr`: order number (numeric)
- `oh.OrderNrAlfa`: order number (text)
- `oh.OrdDatum`: order date
- `oh.OrdSumInklMoms`: order total incl VAT
- `oh.OrdSumExklMoms`: order total excl VAT
- `oh.FtgNr`: customer number
- `oh.Saljare`: salesperson
- `oh.ForetagKod`: company code (filter when CompanyCode/ForetagKod is provided)

Order lines (`dbo.orp`)
- `orp.OrderNr`: order number (join key to `oh.OrderNr`)
- `orp.OrdRadNr`: line number
- `orp.ArtNr`: article number
- `orp.OrdAntal`: quantity
- `orp.vb_RadVardeInklMoms`: line amount incl VAT
- `orp.vb_RadVardeExklMoms`: line amount excl VAT
- `orp.ForetagKod`: company code

Customers (`dbo.fr`)
- Join: `fr.ForetagKod + fr.FtgNr` -> `oh.ForetagKod + oh.FtgNr`
- `fr.FtgNamn`: customer name

## Query patterns
- Largest orders:
  - Sort by `oh.OrdSumInklMoms` DESC
  - Include order number, customer, date, amount
- Top selling products:
  - Aggregate from `dbo.orp` by `orp.ArtNr`
  - Sum amount and sum quantity
- Filter by company:
  - If CompanyCode exists, filter `ForetagKod = CompanyCode` on relevant tables

