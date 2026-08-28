using System.Globalization;
using System.Text.Json;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineCentraQueryCatalog : IFlowEngineCentraQueryCatalog
{
    public string GetFetchOrderQuery() =>
        """
        query Order($id: String!) {
          order(id: $id) {
            id
            number
            status
            createdAt
            store { id }
            market { name }
            shippingAddress {
              firstName
              lastName
              address1
              address2
              city
              zipCode
              email
              phoneNumber
              country { code }
            }
            ... on DirectToConsumerOrder {
              internalComment
              attributes {
                elements {
                  ... on AttributeStringElement {
                    value
                    key
                  }
                }
              }
              totals {
                shipping { value }
                shippingTaxIncluded { value }
                handling { value }
                discounts { value }
              }
              paymentHistory {
                entryType
                status
                externalReference
                value { value }
                paramsJSON
              }
              paymentMethod { name }
            }
            ... on WholesaleOrder {
              account {
                attributes {
                  elements {
                    ... on AttributeStringElement {
                      description
                      value
                    }
                  }
                }
              }
              shippingAddress { companyName }
              buyer { email }
            }
            grandTotal {
              value
              currency { code }
            }
            lines {
              id
              productVariant { variantNumber }
              quantity
              lineValue { value }
              taxPercent
              unitOriginalPrice { value }
              allocations { quantity }
            }
          }
        }
        """;

    public string GetFetchOrdersByDateQuery() =>
        """
        query OrdersByDatePaginated($from: DateTimeTz, $to: DateTimeTz, $limit: Int, $page: Int) {
          orders(where: { createdAt: { from: $from, to: $to } }, limit: $limit, page: $page) {
            id
            number
            status
            createdAt
            store { id }
            market { name }
          }
        }
        """;

    public string GetFetchReturnQuery() =>
        """
        query Return($id: [Int!]) {
          returns(where: {id: $id}) {
            ... on DirectToConsumerReturn {
              id
              createdAt
              returnStatus
              returnedToStock
              store { id }
              shipment {
                shippingAddress {
                  firstName
                  lastName
                  address1
                  address2
                  zipCode
                  city
                  country { code }
                  email
                  phoneNumber
                }
              }
              refundPaymentHistory {
                entryType
                value { value }
                paramsJSON
                paymentMethod
              }
              grandTotal {
                value
                currency { code }
              }
              lines {
                orderLine {
                  lineValue(includingTax: false) { value }
                  returnedQuantity
                  productVariant { variantNumber }
                }
              }
              totals {
                shipping { value }
                shippingTaxRules { taxIncluded { value } }
                handling { value }
                handlingTaxRules { taxIncluded { value } }
                returnCost { value }
                returnCostTaxRules { taxIncluded { value } }
                discounts { value }
                discountTaxRules { taxIncluded { value } }
              }
              comment
              order {
                id
                number
                market { name }
                shippingAddress { country { code } }
                paymentHistory {
                  entryType
                  status
                  externalReference
                  value { value }
                  paramsJSON
                }
                ... on DirectToConsumerOrder {
                  attributes {
                    elements {
                      ... on AttributeStringElement {
                        key
                        value
                      }
                    }
                  }
                  paymentMethod { name }
                }
              }
            }
          }
        }
        """;

    public string GetFetchReturnsByDateQuery() =>
        """
        query ReturnsByDatePaginated($from: DateTimeTz, $to: DateTimeTz, $limit: Int, $page: Int) {
          returns(where: { createdAt: { from: $from, to: $to } }, limit: $limit, page: $page) {
            ... on DirectToConsumerReturn {
              id
              createdAt
              returnStatus
              returnedToStock
              store { id }
              shipment {
                shippingAddress {
                  firstName
                  lastName
                  address1
                  address2
                  zipCode
                  city
                  country { code }
                  email
                  phoneNumber
                }
              }
              refundPaymentHistory {
                entryType
                value { value }
                paramsJSON
                paymentMethod
              }
              grandTotal {
                value
                currency { code }
              }
              lines {
                orderLine {
                  lineValue(includingTax: false) { value }
                  returnedQuantity
                  productVariant { variantNumber }
                }
              }
              totals {
                shipping { value }
                shippingTaxRules { taxIncluded { value } }
                handling { value }
                handlingTaxRules { taxIncluded { value } }
                returnCost { value }
                returnCostTaxRules { taxIncluded { value } }
                discounts { value }
                discountTaxRules { taxIncluded { value } }
              }
              comment
              order {
                id
                number
                market { name }
                shippingAddress { country { code } }
                paymentHistory {
                  entryType
                  status
                  externalReference
                  value { value }
                  paramsJSON
                }
                attributes {
                  elements {
                    key
                    value
                    description
                  }
                }
                paymentMethod { name }
              }
            }
          }
        }
        """;

    public string GetSendOrdersByDateQuery() =>
        """
        query OrdersByDatePaginatedFull($from: DateTimeTz, $to: DateTimeTz, $limit: Int, $page: Int) {
          orders(where: { createdAt: { from: $from, to: $to } }, limit: $limit, page: $page) {
            id
            number
            status
            createdAt
            store { id }
            market { name }
            shippingAddress {
              firstName
              lastName
              companyName
              address1
              address2
              city
              zipCode
              email
              phoneNumber
              country { code }
            }
            paymentHistory {
              entryType
              status
              externalReference
              value { value }
              paramsJSON
            }
            grandTotal {
              value
              currency { code }
            }
            lines {
              productVariant { variantNumber }
              quantity
              lineValue { value }
              taxPercent
              unitOriginalPrice { value }
            }
            ... on DirectToConsumerOrder {
              internalComment
              attributes {
                elements {
                  ... on AttributeStringElement {
                    key
                    value
                    description
                  }
                }
              }
              totals {
                shipping { value }
                shippingTaxIncluded { value }
                handling { value }
                discounts { value }
              }
              paymentMethod { name }
            }
            ... on WholesaleOrder {
              account {
                attributes {
                  elements {
                    ... on AttributeStringElement {
                      key
                      value
                      description
                    }
                  }
                }
              }
            }
          }
        }
        """;

    public string GetSendOrderByIdQuery() =>
        """
        query Order($id: String!) {
          order(id: $id) {
            id
            number
            status
            createdAt
            store { id }
            market { name }
            shippingAddress {
              firstName
              lastName
              companyName
              address1
              address2
              city
              zipCode
              email
              phoneNumber
              country { code }
            }
            paymentHistory {
              entryType
              status
              externalReference
              value { value }
              paramsJSON
            }
            grandTotal {
              value
              currency { code }
            }
            lines {
              productVariant { variantNumber }
              quantity
              lineValue { value }
              taxPercent
              unitOriginalPrice { value }
            }
            ... on DirectToConsumerOrder {
              internalComment
              attributes {
                elements {
                  ... on AttributeStringElement {
                    key
                    value
                    description
                  }
                }
              }
              totals {
                shipping { value }
                shippingTaxIncluded { value }
                handling { value }
                discounts { value }
              }
              paymentMethod { name }
            }
            ... on WholesaleOrder {
              account {
                attributes {
                  elements {
                    ... on AttributeStringElement {
                      key
                      value
                      description
                    }
                  }
                }
              }
            }
          }
        }
        """;

    public string GetSendOrderByLookupQuery() =>
        """
        query OrdersByIdLookup($id: [String!], $limit: Int) {
          orders(where: { id: $id }, limit: $limit) {
            id
            number
            status
            createdAt
            store { id }
            market { name }
            shippingAddress {
              firstName
              lastName
              companyName
              address1
              address2
              city
              zipCode
              email
              phoneNumber
              country { code }
            }
            paymentHistory {
              entryType
              status
              externalReference
              value { value }
              paramsJSON
            }
            grandTotal {
              value
              currency { code }
            }
            lines {
              productVariant { variantNumber }
              quantity
              lineValue { value }
              taxPercent
              unitOriginalPrice { value }
            }
            ... on DirectToConsumerOrder {
              internalComment
              attributes {
                elements {
                  ... on AttributeStringElement {
                    key
                    value
                    description
                  }
                }
              }
              totals {
                shipping { value }
                shippingTaxIncluded { value }
                handling { value }
                discounts { value }
              }
              paymentMethod { name }
            }
            ... on WholesaleOrder {
              account {
                attributes {
                  elements {
                    ... on AttributeStringElement {
                      key
                      value
                      description
                    }
                  }
                }
              }
            }
          }
        }
        """;

    public string GetSendReturnsByDateQuery() =>
        """
        query ReturnsByDatePaginated($from: DateTimeTz, $to: DateTimeTz, $limit: Int, $page: Int) {
          returns(where: { createdAt: { from: $from, to: $to } }, limit: $limit, page: $page) {
            id
            createdAt
            returnStatus
            returnedToStock
            store { id }
            shipment {
              shippingAddress {
                firstName
                lastName
                address1
                address2
                zipCode
                city
                country { code }
                email
                phoneNumber
                companyName
              }
            }
            refundPaymentHistory {
              entryType
              value { value }
              paramsJSON
              paymentMethod
            }
            grandTotal {
              value
              currency { code }
            }
            lines {
              orderLine {
                lineValue(includingTax: false) { value }
                returnedQuantity
                productVariant { variantNumber }
              }
            }
            totals {
              shipping { value }
              shippingTaxRules { taxIncluded { value } }
              handling { value }
              handlingTaxRules { taxIncluded { value } }
              returnCost { value }
              returnCostTaxRules { taxIncluded { value } }
              discounts { value }
              discountTaxRules { taxIncluded { value } }
            }
            comment
            order {
              id
              number
              market { name }
              shippingAddress { country { code } }
              paymentHistory {
                entryType
                status
                externalReference
                value { value }
                paramsJSON
              }
              attributes {
                elements {
                  key
                  value
                  description
                }
              }
              paymentMethod { name }
            }
          }
        }
        """;

    public string GetSendReturnByIdQuery() =>
        """
        query Return($id: [Int!]) {
          returns(where: {id: $id}) {
            id
            createdAt
            returnStatus
            returnedToStock
            store { id }
            shipment {
              shippingAddress {
                firstName
                lastName
                address1
                address2
                zipCode
                city
                country { code }
                email
                phoneNumber
                companyName
              }
            }
            refundPaymentHistory {
              entryType
              value { value }
              paramsJSON
              paymentMethod
            }
            grandTotal {
              value
              currency { code }
            }
            lines {
              orderLine {
                lineValue(includingTax: false) { value }
                returnedQuantity
                productVariant { variantNumber }
              }
            }
            totals {
              shipping { value }
              shippingTaxRules { taxIncluded { value } }
              handling { value }
              handlingTaxRules { taxIncluded { value } }
              returnCost { value }
              returnCostTaxRules { taxIncluded { value } }
              discounts { value }
              discountTaxRules { taxIncluded { value } }
            }
            comment
            order {
              id
              number
              market { name }
              shippingAddress { country { code } }
              paymentHistory {
                entryType
                status
                externalReference
                value { value }
                paramsJSON
              }
              attributes {
                elements {
                  key
                  value
                  description
                }
              }
              paymentMethod { name }
            }
          }
        }
        """;

    public string GetShipmentOrdersByDateQuery(bool includeShippedQuantity)
    {
        var shippedQuantityField = includeShippedQuantity ? "\n                  shippedQuantity" : string.Empty;
        return $$"""
                 query ShipmentOrdersByDate($from: DateTimeTz, $to: DateTimeTz, $limit: Int, $page: Int) {
                   orders(where: { createdAt: { from: $from, to: $to } }, limit: $limit, page: $page) {
                     id
                     number
                     status
                     createdAt
                     store {
                       id
                     }
                     shipments {
                       isPaid
                       isGoodToGo
                       isCaptured
                     }
                     lines {
                       id
                       quantity{{shippedQuantityField}}
                       allocations {
                         quantity
                       }
                     }
                   }
                 }
                 """;
    }

    public string GetShipmentOrderByIdQuery(bool includeShippedQuantity)
    {
        var shippedQuantityField = includeShippedQuantity ? "\n                  shippedQuantity" : string.Empty;
        return $$"""
                 query ShipmentOrderById($id: String!) {
                   order(id: $id) {
                     id
                     number
                     status
                     createdAt
                     store {
                       id
                     }
                     shipments {
                       isPaid
                       isGoodToGo
                       isCaptured
                     }
                     lines {
                       id
                       quantity{{shippedQuantityField}}
                       allocations {
                         quantity
                       }
                     }
                   }
                 }
                 """;
    }

    public string GetShipmentOrdersByStatusQuery(IReadOnlyList<string> statuses, bool includeShippedQuantity)
    {
        var shippedQuantityField = includeShippedQuantity ? "\n                  shippedQuantity" : string.Empty;
        var statusLiteral = statuses.Count == 0
            ? "\"CONFIRMED\", \"PROCESSING\""
            : string.Join(", ", statuses.Select(status => JsonSerializer.Serialize(status)));

        return $$"""
                 query ShipmentOrdersByStatus($limit: Int, $page: Int) {
                   orders(where: { status: [{{statusLiteral}}] }, limit: $limit, page: $page) {
                     id
                     number
                     status
                     createdAt
                     store {
                       id
                     }
                     shipments {
                       isPaid
                       isGoodToGo
                       isCaptured
                     }
                     lines {
                       id
                       quantity{{shippedQuantityField}}
                       allocations {
                         quantity
                       }
                     }
                   }
                 }
                 """;
    }

    public string GetCreateShipmentQuery()
        => """
           mutation createShipment($orderId: String!, $lines: [ShipmentLineInput!]!) {
             createShipment(
               input: {
                 order: { id: $orderId }
                 lines: $lines
                 capture: true
               }
             ) {
               userErrors { message }
               userWarnings { message }
               shipment {
                 id
                 number
                 isCaptured
                 isShipped
                 isGoodToGo
                 isPaid
               }
             }
           }
           """;

    public string GetCreateShipmentWithCaptureQuery()
        => """
           mutation createShipmentWithCapturing($orderId: String!, $lines: [ShipmentLineInput!]!, $capture: Boolean!) {
             createShipment(
               input: {
                 order: { id: $orderId }
                 lines: $lines
                 capture: $capture
               }
             ) {
               userErrors { message }
               userWarnings { message }
               shipment {
                 id
                 number
                 isCaptured
                 isShipped
                 isGoodToGo
                 isPaid
               }
             }
           }
           """;

    public string GetCaptureShipmentQuery()
        => """
           mutation captureShipment($id: Int!) {
             captureShipment(id: $id) {
               userErrors { message }
               userWarnings { message }
               shipment {
                 id
                 number
                 isCaptured
                 isShipped
                 isGoodToGo
                 isPaid
               }
             }
           }
           """;

    public string GetCompleteShipmentQuery()
        => """
           mutation completeShipment($id: Int!, $sendEmail: Boolean!) {
             completeShipment(
               id: $id
               input: {
                 sendEmail: $sendEmail
               }
             ) {
               userErrors { message }
               shipment {
                 id
                 number
                 isCaptured
                 isShipped
                 isGoodToGo
                 isPaid
               }
             }
           }
           """;

    public string GetUpdateShipmentMarkPaidQuery()
        => """
           mutation updateShipmentMarkPaid($id: Int!) {
             updateShipment(
               id: $id
               input: {
                 isPaid: true
               }
             ) {
               userErrors { message }
               userWarnings { message }
               shipment {
                 id
                 number
                 isCaptured
                 isShipped
                 isGoodToGo
                 isPaid
               }
             }
           }
           """;

    public string GetUpdateShipmentGoodToGoQuery()
        => """
           mutation updateShipmentGoodToGo($id: Int!) {
             updateShipment(
               id: $id
               input: {
                 isGoodToGo: true
               }
             ) {
               userErrors { message }
               userWarnings { message }
               shipment {
                 id
                 number
                 isCaptured
                 isShipped
                 isGoodToGo
                 isPaid
               }
             }
           }
           """;

    public string GetOrderShipmentsQuery()
        => """
           query getOrderShipments($id: String!) {
             order(id: $id) {
               shipments {
                 id
                 number
                 isCaptured
                 isShipped
                 isPaid
                 isGoodToGo
               }
             }
           }
           """;

    public string GetCancelOrderLinesWholesaleQuery(IReadOnlyList<FlowEngineShipmentLineInput> lines, bool includeStockAction)
    {
        var cancellationLines = BuildCancellationLinesLiteral(lines, includeStockAction);
        return $$"""
                 mutation updateWholesaleCancel($orderNumber: Int!, $cancellationComment: String!) {
                   updateWholesaleOrder(
                     order: { number: $orderNumber }
                     input: {
                       cancelLines: {{cancellationLines}}
                       cancellationComment: $cancellationComment
                     }
                   ) {
                     userErrors { message }
                   }
                 }
                 """;
    }

    public string GetCancelOrderLinesDirectToConsumerQuery(IReadOnlyList<FlowEngineShipmentLineInput> lines, bool includeStockAction)
    {
        var cancellationLines = BuildCancellationLinesLiteral(lines, includeStockAction);
        return $$"""
                 mutation updateDtcCancel($orderNumber: Int!, $cancellationComment: String!) {
                   updateDirectToConsumerOrder(
                     order: { number: $orderNumber }
                     input: {
                       cancelLines: {{cancellationLines}}
                       cancellationComment: $cancellationComment
                     }
                   ) {
                     userErrors { message }
                   }
                 }
                 """;
    }

    private static string BuildCancellationLinesLiteral(IReadOnlyList<FlowEngineShipmentLineInput> lines, bool includeStockAction)
    {
        var payloads = lines
            .Where(line => !string.IsNullOrWhiteSpace(line.OrderLineId))
            .Select(line =>
            {
                var trimmedId = line.OrderLineId.Trim();
                var lineIdValue = int.TryParse(trimmedId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var numericId)
                    ? numericId.ToString(CultureInfo.InvariantCulture)
                    : JsonSerializer.Serialize(trimmedId);
                var fields = new List<string>
                {
                    $"line: {{ id: {lineIdValue} }}",
                    $"quantity: {line.Quantity.ToString(CultureInfo.InvariantCulture)}"
                };

                if (includeStockAction)
                    fields.Add("stockAction: REMOVE_FROM_STOCK");

                return "{ " + string.Join(" ", fields) + " }";
            })
            .ToList();

        return payloads.Count == 0 ? "[]" : "[" + string.Join(", ", payloads) + "]";
    }
}
