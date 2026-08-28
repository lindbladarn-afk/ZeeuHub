# Jeeves Invoices - Domain Hints (for SQL generation)

This document contains domain-specific hints used by the AI SQL generator for invoices.
Keep it plain-text and deterministic. Avoid customer-specific data.

## Core table
- Invoices: `dbo.ft` (invoice/ledger rows)

## Data warehouse finance fact
- Prefer `dbo.q_zu_bi_fsg` for analytics over raw invoice tables when it exists in the schema.
- Revenue per month: group `[Invoice Row SUM]` by `[Invoice Date]` at month level.
- Customer key: `[Customer No]`; use the customer dimension for the customer name when the fact view does not expose one.
- Do not use `[Amount to Pay]` to total invoice rows: it is an invoice-header amount and can repeat across several invoice rows.

## Key columns (common)
Invoices (`dbo.ft`)
- `ft.FaktNr`: invoice number
- `ft.FtgNr`: customer number
- `ft.Saljare`: salesperson
- `ft.FaktDat`: invoice date (used as due date until real due date is available)
- `ft.FaktRadSummaInklMoms`: invoice row amount incl VAT
- `ft.ForetagKod`: company code (filter when CompanyCode/ForetagKod is provided)

Customers (`dbo.fr`)
- `fr.ForetagKod + fr.FtgNr` can be joined to `ft.ForetagKod + ft.FtgNr`
- `fr.FtgNamn`: customer name

## Query patterns
- Total unpaid/overdue invoices (current implementation):
  - Group by `ft.FaktNr`
  - Amount: SUM(`ft.FaktRadSummaInklMoms`)
  - Date: MAX(`ft.FaktDat`)
  - Customer: MAX(`ft.FtgNr`) and optionally join to customer name (`dbo.fr`)
- Business rule:
  - "Open invoice" means "unpaid invoice" in ZeeU terminology.
  - Use `ft.AttBetalaBelopp > 0` to identify open/unpaid invoices.
  - Use `ft.AttBetalaBelopp <= 0` to identify paid/closed invoices.
- Overdue logic (temporary):
  - Use `ft.FaktDat` as the date to compare to today if no explicit due date exists.
  - (Future improvement: use a real due date column when available.)
