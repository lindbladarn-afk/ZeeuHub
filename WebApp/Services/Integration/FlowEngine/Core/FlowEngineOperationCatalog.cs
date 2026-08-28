using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineOperationCatalog : IFlowEngineOperationCatalog
{
    private static readonly IReadOnlyList<FlowEngineOperationDefinition> Operations = new List<FlowEngineOperationDefinition>
    {
        new()
        {
            Key = FlowEngineOperationType.ConfigValidate.ToString(),
            Operation = FlowEngineOperationType.ConfigValidate,
            Section = FlowEngineSectionKeys.Config,
            Label = "FlowEngine config validate",
            Summary = "Validerar portalens aktiva native FlowEngine-konfiguration for Jeeves, Centra, Shopify och Akeneo i ett enda jobb.",
            Slice = "Drift och konfiguration",
            Readiness = FlowEngineModuleReadiness.InProgress
        },
        new()
        {
            Key = FlowEngineOperationType.CheckOrders.ToString(),
            Operation = FlowEngineOperationType.CheckOrders,
            Section = FlowEngineSectionKeys.Centra,
            Label = "Centra check orders",
            Summary = "Datumstyrd kontroll av vilka Centra-ordrar som finns i Jeeves, med found/missing/deleted/error i jobbhistoriken.",
            Slice = "Jobbsubstrat + Centra lasfloden",
            Readiness = FlowEngineModuleReadiness.InProgress
        },
        new()
        {
            Key = FlowEngineOperationType.CentraFetchOrder.ToString(),
            Operation = FlowEngineOperationType.CentraFetchOrder,
            Section = FlowEngineSectionKeys.Centra,
            Label = "Centra fetch order",
            Summary = "Hamtar en enskild Centra-order som raw GraphQL payload direkt i portalens jobbhistorik.",
            Slice = "Centra lasfloden",
            Readiness = FlowEngineModuleReadiness.InProgress
        },
        new()
        {
            Key = FlowEngineOperationType.CentraFetchOrders.ToString(),
            Operation = FlowEngineOperationType.CentraFetchOrders,
            Section = FlowEngineSectionKeys.Centra,
            Label = "Centra fetch orders",
            Summary = "Datumstyrd eller ranged fetch av Centra-order med dag-for-dag-output och samma 7-dagarsguard som ovriga rangekommandon.",
            Slice = "Centra lasfloden",
            Readiness = FlowEngineModuleReadiness.InProgress
        },
        new()
        {
            Key = FlowEngineOperationType.CentraFetchReturn.ToString(),
            Operation = FlowEngineOperationType.CentraFetchReturn,
            Section = FlowEngineSectionKeys.Centra,
            Label = "Centra fetch return",
            Summary = "Hamtar en enskild Centra-retur som raw GraphQL payload direkt i FlowEngine-modulen.",
            Slice = "Centra lasfloden",
            Readiness = FlowEngineModuleReadiness.InProgress
        },
        new()
        {
            Key = FlowEngineOperationType.CentraFetchReturns.ToString(),
            Operation = FlowEngineOperationType.CentraFetchReturns,
            Section = FlowEngineSectionKeys.Centra,
            Label = "Centra fetch returns",
            Summary = "Datumstyrd eller ranged fetch av Centra-returer med dag-for-dag-output och samma rangeguard som originalet.",
            Slice = "Centra lasfloden",
            Readiness = FlowEngineModuleReadiness.InProgress
        },
        new()
        {
            Key = FlowEngineOperationType.GetCustomerAddresses.ToString(),
            Operation = FlowEngineOperationType.GetCustomerAddresses,
            Section = FlowEngineSectionKeys.Jeeves,
            Label = "Jeeves delivery addresses",
            Summary = "Hamtar leveransadresser i portalens egen jobbmodell och visar output direkt i modulen.",
            Slice = "Jeeves lasfloden",
            Readiness = FlowEngineModuleReadiness.Available
        },
        new()
        {
            Key = FlowEngineOperationType.GetOrders.ToString(),
            Operation = FlowEngineOperationType.GetOrders,
            Section = FlowEngineSectionKeys.Jeeves,
            Label = "Jeeves get orders",
            Summary = "Hamta orderrader via c_extordernr eller c_ordernr med samma typed jobbmodell som originalet.",
            Slice = "Jeeves lasfloden",
            Readiness = FlowEngineModuleReadiness.Available
        },
        new()
        {
            Key = FlowEngineOperationType.OrderExists.ToString(),
            Operation = FlowEngineOperationType.OrderExists,
            Section = FlowEngineSectionKeys.Jeeves,
            Label = "Jeeves order exists",
            Summary = "Snabb kontroll om en extern order redan finns i Jeeves innan skarpa send-floden.",
            Slice = "Jeeves lasfloden",
            Readiness = FlowEngineModuleReadiness.Available
        },
        new()
        {
            Key = FlowEngineOperationType.GetProduct.ToString(),
            Operation = FlowEngineOperationType.GetProduct,
            Section = FlowEngineSectionKeys.Jeeves,
            Label = "Jeeves product lookup",
            Summary = "Artikeluppslag kor nu native i portalen med typed command och jobbhistorik.",
            Slice = "Jeeves lasfloden",
            Readiness = FlowEngineModuleReadiness.Available
        },
        new()
        {
            Key = FlowEngineOperationType.GetArtStatus.ToString(),
            Operation = FlowEngineOperationType.GetArtStatus,
            Section = FlowEngineSectionKeys.Jeeves,
            Label = "Jeeves art status",
            Summary = "Batchkontroll av artikelstatus for import order-grid och export.",
            Slice = "Jeeves lasfloden",
            Readiness = FlowEngineModuleReadiness.Available
        },
        new()
        {
            Key = FlowEngineOperationType.ImportOrder.ToString(),
            Operation = FlowEngineOperationType.ImportOrder,
            Section = FlowEngineSectionKeys.Jeeves,
            Label = "Jeeves import order",
            Summary = "Portalform, paritetsnara validering, delivery address-resolution, PDF-review och Jeeves-sendning kor nu native i modulen.",
            Slice = "Jeeves skrivfloden",
            Readiness = FlowEngineModuleReadiness.InProgress
        },
        new()
        {
            Key = FlowEngineOperationType.AkeneoProducts.ToString(),
            Operation = FlowEngineOperationType.AkeneoProducts,
            Section = FlowEngineSectionKeys.Akeneo,
            Label = "Akeneo export",
            Summary = "XML-export for valda SKU:er via portalens egen Akeneo-klient och jobbmodell.",
            Slice = "Akeneo",
            Readiness = FlowEngineModuleReadiness.InProgress
        },
        new()
        {
            Key = FlowEngineOperationType.AkeneoAllProducts.ToString(),
            Operation = FlowEngineOperationType.AkeneoAllProducts,
            Section = FlowEngineSectionKeys.Akeneo,
            Label = "Akeneo all products",
            Summary = "Full Akeneo-export via portalens egen Akeneo-klient med samma XML-format som den befintliga exporttjansten.",
            Slice = "Akeneo",
            Readiness = FlowEngineModuleReadiness.InProgress
        },
        new()
        {
            Key = FlowEngineOperationType.AkeneoSendToShopify.ToString(),
            Operation = FlowEngineOperationType.AkeneoSendToShopify,
            Section = FlowEngineSectionKeys.Akeneo,
            Label = "Akeneo send to Shopify",
            Summary = "Dry run-paritet med originalkommandot: bygger onskat Shopify-utkast fran Akeneo, laser nuvarande Shopify-produkt via SKU och sparar diff/warnings i jobbhistoriken.",
            Slice = "Akeneo",
            Readiness = FlowEngineModuleReadiness.InProgress
        },
        new()
        {
            Key = FlowEngineOperationType.AkeneoSendToCentra.ToString(),
            Operation = FlowEngineOperationType.AkeneoSendToCentra,
            Section = FlowEngineSectionKeys.Akeneo,
            Label = "Akeneo send to Centra",
            Summary = "Dry run-paritet for Centra-handoffen: bygger samma Akeneo XML som exporten och sparar file preview, urval och summering i jobbhistoriken.",
            Slice = "Akeneo",
            Readiness = FlowEngineModuleReadiness.InProgress
        },
        new()
        {
            Key = FlowEngineOperationType.ShopifyScopesCheck.ToString(),
            Operation = FlowEngineOperationType.ShopifyScopesCheck,
            Section = FlowEngineSectionKeys.Shopify,
            Label = "Shopify scopes-check",
            Summary = "Verifierar butik, granted scopes och vilka Shopify-kommandokategorier som ar korbara innan skarp drift.",
            Slice = "Shopify",
            Readiness = FlowEngineModuleReadiness.InProgress
        },
        new()
        {
            Key = FlowEngineOperationType.ShopifyGetProducts.ToString(),
            Operation = FlowEngineOperationType.ShopifyGetProducts,
            Section = FlowEngineSectionKeys.Shopify,
            Label = "Shopify get-products",
            Summary = "Laser produkter via GraphQL med limit, fri query och updated-since direkt i portalens jobbhistorik.",
            Slice = "Shopify",
            Readiness = FlowEngineModuleReadiness.InProgress
        },
        new()
        {
            Key = FlowEngineOperationType.ShopifyFetchOrder.ToString(),
            Operation = FlowEngineOperationType.ShopifyFetchOrder,
            Section = FlowEngineSectionKeys.Shopify,
            Label = "Shopify fetch order",
            Summary = "Hamtar en full Shopify-order via id/gid och visar payloaden direkt i FlowEngine-modulen.",
            Slice = "Shopify",
            Readiness = FlowEngineModuleReadiness.InProgress
        },
        new()
        {
            Key = FlowEngineOperationType.ShopifyFetchOrders.ToString(),
            Operation = FlowEngineOperationType.ShopifyFetchOrders,
            Section = FlowEngineSectionKeys.Shopify,
            Label = "Shopify fetch orders",
            Summary = "Datumstyrd eller ranged fetch av Shopify-order med samma 7-dagarsguard som originalet.",
            Slice = "Shopify",
            Readiness = FlowEngineModuleReadiness.InProgress
        },
        new()
        {
            Key = FlowEngineOperationType.ShopifyValidateOrder.ToString(),
            Operation = FlowEngineOperationType.ShopifyValidateOrder,
            Section = FlowEngineSectionKeys.Shopify,
            Label = "Shopify validate order",
            Summary = "Korar originalets ordervalidering mot en enskild Shopify-order och sparar beslutet i jobbhistoriken.",
            Slice = "Shopify",
            Readiness = FlowEngineModuleReadiness.InProgress
        },
        new()
        {
            Key = FlowEngineOperationType.ShopifyValidateOrders.ToString(),
            Operation = FlowEngineOperationType.ShopifyValidateOrders,
            Section = FlowEngineSectionKeys.Shopify,
            Label = "Shopify validate orders",
            Summary = "Datumstyrd eller ranged batchvalidering av Shopify-order med typed decisions i portalens jobbmodell.",
            Slice = "Shopify",
            Readiness = FlowEngineModuleReadiness.InProgress
        },
        new()
        {
            Key = FlowEngineOperationType.ShopifyCheckOrders.ToString(),
            Operation = FlowEngineOperationType.ShopifyCheckOrders,
            Section = FlowEngineSectionKeys.Shopify,
            Label = "Shopify check orders",
            Summary = "Datumstyrd batch som jamfor Shopify-order mot Jeeves via ext order-nr och markerar found, missing och error.",
            Slice = "Shopify",
            Readiness = FlowEngineModuleReadiness.InProgress
        },
        new()
        {
            Key = FlowEngineOperationType.ShopifySendOrder.ToString(),
            Operation = FlowEngineOperationType.ShopifySendOrder,
            Section = FlowEngineSectionKeys.Shopify,
            Label = "Shopify send order",
            Summary = "Enskild Shopify-order som valideras, mappas till Jeeves och kan dry-runas eller skickas skarpt med SentToJeeves-tagg.",
            Slice = "Shopify",
            Readiness = FlowEngineModuleReadiness.InProgress
        },
        new()
        {
            Key = FlowEngineOperationType.ShopifySendOrders.ToString(),
            Operation = FlowEngineOperationType.ShopifySendOrders,
            Section = FlowEngineSectionKeys.Shopify,
            Label = "Shopify send orders",
            Summary = "Batchsanding for en dag, latest-day eller ett UTC-intervall med samma 7-dagarsguard, exists-check och taggning som originalet.",
            Slice = "Shopify",
            Readiness = FlowEngineModuleReadiness.InProgress
        },
        new()
        {
            Key = FlowEngineOperationType.SendOrder.ToString(),
            Operation = FlowEngineOperationType.SendOrder,
            Section = FlowEngineSectionKeys.Centra,
            Label = "Centra send order",
            Summary = "Enskild Centra-order som valideras, mappas till Jeeves och kan dry-runas eller skickas skarpt med samma idempotensregler som batchsparet.",
            Slice = "Centra skrivfloden",
            Readiness = FlowEngineModuleReadiness.InProgress
        },
        new()
        {
            Key = FlowEngineOperationType.SendOrders.ToString(),
            Operation = FlowEngineOperationType.SendOrders,
            Section = FlowEngineSectionKeys.Centra,
            Label = "Centra send orders",
            Summary = "Datumstyrd dry run eller skarp batch som validerar Centra-ordrar, mappar till Jeeves och hoppar over existing/ineligible i jobbhistoriken.",
            Slice = "Centra skrivfloden",
            Readiness = FlowEngineModuleReadiness.InProgress
        },
        new()
        {
            Key = FlowEngineOperationType.CreateShipments.ToString(),
            Operation = FlowEngineOperationType.CreateShipments,
            Section = FlowEngineSectionKeys.Centra,
            Label = "Centra create shipments",
            Summary = "Datumstyrd shipment-batch som gor Jeeves preflight, line planning, Centra shipment-mutationer och store-specifika workflowsteg.",
            Slice = "Shipment-floden",
            Readiness = FlowEngineModuleReadiness.InProgress
        },
        new()
        {
            Key = FlowEngineOperationType.CreateShipment.ToString(),
            Operation = FlowEngineOperationType.CreateShipment,
            Section = FlowEngineSectionKeys.Centra,
            Label = "Centra create shipment",
            Summary = "Enskild shipment-korning for ett Order ID med samma Jeeves gate, allocation-planering och store-specifika workflowsteg som batchflodena.",
            Slice = "Shipment-floden",
            Readiness = FlowEngineModuleReadiness.InProgress
        },
        new()
        {
            Key = FlowEngineOperationType.CreateShipmentsPending.ToString(),
            Operation = FlowEngineOperationType.CreateShipmentsPending,
            Section = FlowEngineSectionKeys.Centra,
            Label = "Centra create shipments pending",
            Summary = "Statusstyrd pending-batch for CONFIRMED/PROCESSING som kor samma Jeeves gate, line planning och shipment-workflows utan datumfilter.",
            Slice = "Shipment-floden",
            Readiness = FlowEngineModuleReadiness.InProgress
        },
        new()
        {
            Key = FlowEngineOperationType.SendReturns.ToString(),
            Operation = FlowEngineOperationType.SendReturns,
            Section = FlowEngineSectionKeys.Centra,
            Label = "Centra send returns",
            Summary = "Datumstyrd dry run eller skarp batch som validerar Centra-returer, mappar dem till Jeeves och hoppar over redan skickade returer.",
            Slice = "Centra skrivfloden",
            Readiness = FlowEngineModuleReadiness.InProgress
        },
        new()
        {
            Key = FlowEngineOperationType.SendReturn.ToString(),
            Operation = FlowEngineOperationType.SendReturn,
            Section = FlowEngineSectionKeys.Centra,
            Label = "Centra send return",
            Summary = "Enskild Centra-retur som valideras, mappas till Jeeves och kan dry-runas eller skickas skarpt med samma duplicate-handling som batchsparet.",
            Slice = "Centra skrivfloden",
            Readiness = FlowEngineModuleReadiness.InProgress
        },
        new()
        {
            Key = FlowEngineOperationType.CompleteOrder.ToString(),
            Operation = FlowEngineOperationType.CompleteOrder,
            Section = FlowEngineSectionKeys.Shopify,
            Label = "Shopify complete order",
            Summary = "Enskild Shopify-order med samma Jeeves gate, fulfillment-orderfiltrering och valfri close order som batchkommandona.",
            Slice = "Shopify",
            Readiness = FlowEngineModuleReadiness.InProgress
        },
        new()
        {
            Key = FlowEngineOperationType.CompleteOrders.ToString(),
            Operation = FlowEngineOperationType.CompleteOrders,
            Section = FlowEngineSectionKeys.Shopify,
            Label = "Shopify complete orders",
            Summary = "Datumstyrd Shopify-batch som hamtar orders for ett UTC-datum, verifierar Jeeves-status och skapar fulfillment native i portalen.",
            Slice = "Shopify",
            Readiness = FlowEngineModuleReadiness.InProgress
        },
        new()
        {
            Key = FlowEngineOperationType.CompleteOrdersPending.ToString(),
            Operation = FlowEngineOperationType.CompleteOrdersPending,
            Section = FlowEngineSectionKeys.Shopify,
            Label = "Shopify complete orders pending",
            Summary = "Pending-batch som hittar Shopify-order markerade som skickade till Jeeves men inte shippade, verifierar Jeeves-status och skapar fulfillment native i portalen.",
            Slice = "Shopify",
            Readiness = FlowEngineModuleReadiness.InProgress
        }
    };

    public IReadOnlyList<FlowEngineOperationDefinition> GetAll()
    {
        return Operations;
    }
}
