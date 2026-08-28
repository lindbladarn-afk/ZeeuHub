Bank reconciliation module notes
================================

Purpose
-------

This note tracks what the bank reconciliation module is becoming while we keep the current UI and workflow simple.

We are intentionally not rebuilding the page into a complex rules screen. The module stays a single menu item, but the content can switch by mode:

- overview
- classification
- posting / coding
- reconciliation
- transaction detail

Current status
--------------

The module now has its own controller and keeps the old route alive:

- new controller: `WebApp/Controllers/BankReconciliation/BankReconciliationController.cs`
- old route alias still works: `/Integration/BankReconciliation`

The ingestion and matching flow already exists and is still the base of the module:

- CAMT upload
- CAMT parsing
- invoice matching
- manual match persistence
- AI suggestion flow
- demo scenarios

What we added now
-----------------

We added a separate transaction classification layer so we can expand the module without changing the current UI behavior.

The workspace now presents one four-step workflow:

- source
- review types and coding
- match
- complete and lock

Classification and coding are grouped into one review step. The complete step now validates the result on the server and locks the reconciliation. Reopening requires a reason and is written to the audit trail. Completing the reconciliation still does not post accounting entries to Jeeves.
The classification mode now shows global type cards for the whole transaction set, and clicking a card filters the transaction list down to that type without leaving the module.
The classifier now also carries a suggested account and cost center per type so the next step can be a lightweight coding view instead of a separate admin screen.
We also accept `.nda` uploads as camt.053 containers because some customer deliveries use that filename even though the content is XML.
We now persist a coding rule matrix per company and bank account, with versioning and conflict detection, so the coding view can be edited and saved without changing the rest of the workflow.
The current load order is explicit: bank account specific type rows first, then the bank account's `DEF`, then company default type rows, and finally the company default `DEF`.
The selected transaction in the work panel now shows the same effective coding suggestion, so the match view and coding view point at the same account and cost center.
Recommendations, auto-match and AI suggestions now reuse the same invoice-matching gate, so internal transfers and other non-invoice types do not get a second competing interpretation.
The invoice list now uses page/pageSize in the live customer path and in the hub-owned supplier path, so the UI only renders 20 rows at a time.
The transaction list also uses 20 rows per page, so the overview and workflow panels stay consistent. The top transaction totals are recalculated for the current filtered set, while the classification cards and manual-review queue stay global so they do not depend on the active page.
Supplier invoice candidates are loaded from hub-owned SQL instead of a Jeeves stored procedure, but they still land in the same invoice contract that the matcher and UI already understand.
The demo invoice list now respects the selected classification filter as well, so supplier-payment views no longer show the same invoices as customer-payment views.

New model:

- `WebApp/Models/Integration/BankReconciliation/BankReconciliationClassificationModels.cs`

New classifier:

- `WebApp/Services/Integration/BankReconciliation/BankReconciliationTransactionClassifier.cs`

Persistent coding rules:

- `WebApp/Services/Integration/BankReconciliation/CodingRules/BankReconciliationCodingRuleService.cs`
- `WebApp/Services/Integration/BankReconciliation/CodingRules/IBankReconciliationCodingRuleService.cs`

Parsed transactions now carry both:

- the new classification object
- the legacy `Group` and `ClassificationRule` fields

That means the UI can stay as-is while future views can read the richer classification data.

Classification contract
-----------------------

The first version of the module classification uses these values:

- `DEF` as the default
- `Bankinbetalningar`
- `Räntekonto`
- `Överföring konto`
- `Leverantörsbetalning`
- `Autogiro`
- `Kontantuttag`
- `Bankavgift`
- `Skattebetalning`

The classifier currently uses CAMT signals and text hints from remittance/debtor data.

Legacy compatibility
--------------------

The existing module UI still depends on legacy grouping.

So we keep the old values in place:

- customer inpayments still map to `Kundinbetalningar`
- supplier payouts still map to `Leverantorsutbetalningar`
- everything else falls back to `Ovrigt`

The new classification is extra data, not a breaking change.

Where it is wired
-----------------

The new classification is used in:

- CAMT parser
- demo transaction mapping
- JSON sent to the bank reconciliation UI

The tests already cover:

- customer payment classification
- internal transfer classification
- interest classification
- default fallback
- parser behavior

Remaining work
--------------

The four-step workflow, editable coding rules, reconciliation view and durable SQL state are now in place. The next additions should stay separate and only be introduced when there is a reliable source or a clear business need:

1. bank account ownership verification, which remains deliberately deferred
2. an explicit posting/export step if reconciliations are later sent to Jeeves
3. a repeatable browser test in CI for the authenticated happy path

Combined invoice paging
-----------------------

The `Alla typer` view now loads bounded customer and supplier database windows, merges them in one stable due-date order and returns only the requested page. It no longer loads both complete invoice populations into application memory. The combined window is limited to 10,000 rows; unusually large result sets must be filtered to customer or supplier invoices.

Deferred: bank account ownership verification
---------------------------------------------

We are intentionally not requiring users to register bank accounts manually before importing a CAMT file.
The current workflow reads the account metadata from CAMT, binds the reconciliation session to the selected portal company, and matches transactions against invoices loaded from that company's Jeeves database.

The account in the CAMT file is therefore currently treated as source metadata, not as proof that the account belongs to the selected company. This does not block CAMT import, invoice matching, manual matching, or payment bundle suggestions.

Account ownership verification should be reconsidered when a reliable source becomes available, for example:

- maintained company bank accounts in Jeeves
- a bank integration that exposes account ownership
- approved company master data outside Jeeves

When implemented, the check should normalize IBAN and domestic account numbers, compare them without exposing full account details in logs, and return a clear validation result. Until a reliable source exists, silently building an allowlist from uploaded files or requiring manual pre-registration would create false confidence and unnecessary administration.

Rule approach
-------------

The intended rule order is:

1. explicit manual rule
2. bank account specific rule
3. counterpart / text rule
4. type rule
5. default `DEF`

For the persisted coding matrix we now resolve in a simpler effective order:

1. current bank account specific type row
2. current bank account `DEF`
3. company default type row
4. company default `DEF`

Why this shape
--------------

This structure keeps the module easy to follow:

- one module in the menu
- one simple entry point
- one transaction stream
- richer behavior behind the scenes

It also gives us a safe path to add:

- company payments
- interest accounts
- manual account selection
- cost center selection
- more specialized transaction handling

Files to keep in mind
---------------------

- [WebApp/Controllers/BankReconciliation/BankReconciliationController.cs](/Users/alexanderek/ZeeU/ZeeU.CustomerPortal/WebApp/Controllers/BankReconciliation/BankReconciliationController.cs)
- [WebApp/Services/Integration/BankReconciliation/BankReconciliationTransactionClassifier.cs](/Users/alexanderek/ZeeU/ZeeU.CustomerPortal/WebApp/Services/Integration/BankReconciliation/BankReconciliationTransactionClassifier.cs)
- [WebApp/Models/Integration/BankReconciliation/BankReconciliationClassificationModels.cs](/Users/alexanderek/ZeeU/ZeeU.CustomerPortal/WebApp/Models/Integration/BankReconciliation/BankReconciliationClassificationModels.cs)
- [WebApp/Services/Integration/BankReconciliation/Parsing/BankReconciliationCamtParser.cs](/Users/alexanderek/ZeeU/ZeeU.CustomerPortal/WebApp/Services/Integration/BankReconciliation/Parsing/BankReconciliationCamtParser.cs)

Durable storage
---------------

The portal database owns three bank reconciliation tables:

- `Identity.BankReconciliationStates`
- `Identity.BankReconciliationImportRegistries`
- `Identity.BankReconciliationCodingRules`

Keys are scoped by company and use SHA-256 hashes for statement and account identities. Each record has a concurrency version. Existing JSON state, import history and coding rules under `App_Data` are imported idempotently at startup and kept untouched as a fallback copy, but new runtime writes go only to SQL.

A closed state stores the closing user and time, statement fingerprint and coding-rule version. Match changes are rejected until the state is reopened with an audited reason.

Current definition of done
--------------------------

We are in good shape when:

- the module still opens cleanly from the menu
- uploaded CAMT files still parse
- the classification data is visible in the payload
- legacy grouping still works
- complete rejects unmatched and partially matched transactions
- closed state rejects match mutations
- legacy JSON is retained in SQL
- tests cover classification, persistence, paging and lifecycle behavior
