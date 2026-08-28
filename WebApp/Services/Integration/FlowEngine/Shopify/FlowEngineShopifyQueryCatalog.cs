namespace WebApp.Services.Integration.FlowEngine;

public sealed class FlowEngineShopifyQueryCatalog : IFlowEngineShopifyQueryCatalog
{
    public string BuildGetProductsQuery(bool includeInventoryItem, bool includeMetafields)
    {
        var inventoryFields = includeInventoryItem
            ? """
                                  inventoryItem {
                                    id
                                    tracked
                                  }
              """
            : string.Empty;

        var metafieldsFields = includeMetafields
            ? """
                                metafields(first: 20) {
                                  edges {
                                    node {
                                      namespace
                                      key
                                      type
                                      value
                                    }
                                  }
                                }
              """
            : string.Empty;

        return $$"""
                 query ShopifyGetProducts($first: Int!, $after: String, $query: String) {
                   products(first: $first, after: $after, query: $query, sortKey: UPDATED_AT, reverse: false) {
                     pageInfo {
                       hasNextPage
                       endCursor
                     }
                     edges {
                       node {
                         id
                         legacyResourceId
                         title
                         handle
                         status
                         vendor
                         productType
                         tags
                         createdAt
                         updatedAt
                         variants(first: 100) {
                           edges {
                             node {
                               id
                               sku
                               barcode
                               title
                               price
                               compareAtPrice
                 {{inventoryFields}}
                             }
                           }
                         }
                         options {
                           name
                           values
                         }
                         images(first: 50) {
                           edges {
                             node {
                               url
                               altText
                             }
                           }
                         }
                 {{metafieldsFields}}
                       }
                     }
                   }
                 }
                 """;
    }

    public string ScopesCheckQuery => """
                                       query ShopifyScopesCheck {
                                         shop {
                                           name
                                           myshopifyDomain
                                         }
                                         currentAppInstallation {
                                           accessScopes {
                                             handle
                                           }
                                         }
                                       }
                                       """;

    public string CurrentAccessScopesQuery => """
                                               query ShopifyCurrentAccessScopes {
                                                 currentAppInstallation {
                                                   accessScopes {
                                                     handle
                                                   }
                                                 }
                                               }
                                               """;

    public string FetchOrderQuery => """
                                      query ShopifyFetchOrder($id: ID!) {
                                        order(id: $id) {
                                          id
                                          legacyResourceId
                                          name
                                          createdAt
                                          updatedAt
                                          cancelledAt
                                          test
                                          displayFinancialStatus
                                          displayFulfillmentStatus
                                          customer {
                                            id
                                            firstName
                                            lastName
                                            email
                                            phone
                                          }
                                          billingAddress {
                                            firstName
                                            lastName
                                            company
                                            address1
                                            address2
                                            city
                                            province
                                            zip
                                            countryCodeV2
                                            phone
                                          }
                                          shippingAddress {
                                            firstName
                                            lastName
                                            company
                                            address1
                                            address2
                                            city
                                            province
                                            zip
                                            countryCodeV2
                                            phone
                                          }
                                          totalShippingPriceSet {
                                            shopMoney {
                                              amount
                                              currencyCode
                                            }
                                          }
                                          shippingLines(first: 10) {
                                            edges {
                                              node {
                                                title
                                                code
                                                originalPriceSet {
                                                  shopMoney {
                                                    amount
                                                    currencyCode
                                                  }
                                                }
                                                discountedPriceSet {
                                                  shopMoney {
                                                    amount
                                                    currencyCode
                                                  }
                                                }
                                                currentDiscountedPriceSet {
                                                  shopMoney {
                                                    amount
                                                    currencyCode
                                                  }
                                                }
                                              }
                                            }
                                          }
                                          lineItems(first: 100) {
                                            pageInfo {
                                              hasNextPage
                                              endCursor
                                            }
                                            edges {
                                              node {
                                                id
                                                sku
                                                name
                                                quantity
                                                originalTotalSet {
                                                  shopMoney {
                                                    amount
                                                    currencyCode
                                                  }
                                                }
                                                discountedTotalSet {
                                                  shopMoney {
                                                    amount
                                                    currencyCode
                                                  }
                                                }
                                                variant {
                                                  id
                                                  sku
                                                  title
                                                  legacyResourceId
                                                }
                                              }
                                            }
                                          }
                                        }
                                      }
                                      """;

    public string FetchOrdersByDateQuery => """
                                             query ShopifyFetchOrdersByDate($first: Int!, $after: String, $query: String!) {
                                               orders(first: $first, after: $after, query: $query, sortKey: CREATED_AT, reverse: false) {
                                                 pageInfo {
                                                   hasNextPage
                                                   endCursor
                                                 }
                                                 edges {
                                                   node {
                                                     id
                                                     legacyResourceId
                                                     name
                                                     createdAt
                                                     updatedAt
                                                     cancelledAt
                                                     test
                                                     displayFinancialStatus
                                                     displayFulfillmentStatus
                                                   }
                                                 }
                                               }
                                             }
                                             """;

    public string ValidateOrdersByDateQuery => """
                                                query ShopifyValidateOrdersByDate($first: Int!, $after: String, $query: String!) {
                                                  orders(first: $first, after: $after, query: $query, sortKey: CREATED_AT, reverse: false) {
                                                    pageInfo {
                                                      hasNextPage
                                                      endCursor
                                                    }
                                                    edges {
                                                      node {
                                                        id
                                                        legacyResourceId
                                                        name
                                                        createdAt
                                                        updatedAt
                                                        cancelledAt
                                                        test
                                                        displayFinancialStatus
                                                        displayFulfillmentStatus
                                                        customer {
                                                          id
                                                          firstName
                                                          lastName
                                                          email
                                                          phone
                                                        }
                                                        billingAddress {
                                                          firstName
                                                          lastName
                                                          company
                                                          address1
                                                          address2
                                                          city
                                                          province
                                                          zip
                                                          countryCodeV2
                                                          phone
                                                        }
                                                        shippingAddress {
                                                          firstName
                                                          lastName
                                                          company
                                                          address1
                                                          address2
                                                          city
                                                          province
                                                          zip
                                                          countryCodeV2
                                                          phone
                                                        }
                                                        totalShippingPriceSet {
                                                          shopMoney {
                                                            amount
                                                            currencyCode
                                                          }
                                                        }
                                                        shippingLines(first: 10) {
                                                          edges {
                                                            node {
                                                              title
                                                              code
                                                              originalPriceSet {
                                                                shopMoney {
                                                                  amount
                                                                  currencyCode
                                                                }
                                                              }
                                                              discountedPriceSet {
                                                                shopMoney {
                                                                  amount
                                                                  currencyCode
                                                                }
                                                              }
                                                              currentDiscountedPriceSet {
                                                                shopMoney {
                                                                  amount
                                                                  currencyCode
                                                                }
                                                              }
                                                            }
                                                          }
                                                        }
                                                        lineItems(first: 100) {
                                                          pageInfo {
                                                            hasNextPage
                                                            endCursor
                                                          }
                                                          edges {
                                                            node {
                                                              id
                                                              sku
                                                              name
                                                              quantity
                                                              originalTotalSet {
                                                                shopMoney {
                                                                  amount
                                                                  currencyCode
                                                                }
                                                              }
                                                              discountedTotalSet {
                                                                shopMoney {
                                                                  amount
                                                                  currencyCode
                                                                }
                                                              }
                                                              variant {
                                                                id
                                                                sku
                                                                title
                                                                legacyResourceId
                                                              }
                                                            }
                                                          }
                                                        }
                                                      }
                                                    }
                                                  }
                                                }
                                                """;

    public string TagsAddMutation => """
                                      mutation ShopifyTagsAdd($id: ID!, $tags: [String!]!) {
                                        tagsAdd(id: $id, tags: $tags) {
                                          node {
                                            id
                                          }
                                          userErrors {
                                            field
                                            message
                                          }
                                        }
                                      }
                                      """;

    public string FetchFulfillmentOrdersQuery => """
                                                  query ShopifyFetchFulfillmentOrders($id: ID!, $after: String) {
                                                    order(id: $id) {
                                                      id
                                                      fulfillmentOrders(first: 50, after: $after) {
                                                        pageInfo {
                                                          hasNextPage
                                                          endCursor
                                                        }
                                                        edges {
                                                          node {
                                                            id
                                                            status
                                                            requestStatus
                                                          }
                                                        }
                                                      }
                                                    }
                                                  }
                                                  """;

    public string FulfillmentCreateMutation => """
                                                mutation ShopifyFulfillmentCreate($fulfillment: FulfillmentInput!) {
                                                  fulfillmentCreate(fulfillment: $fulfillment) {
                                                    fulfillment {
                                                      id
                                                      status
                                                    }
                                                    userErrors {
                                                      field
                                                      message
                                                    }
                                                  }
                                                }
                                                """;

    public string OrderCloseMutation => """
                                         mutation ShopifyOrderClose($input: OrderCloseInput!) {
                                           orderClose(input: $input) {
                                             order {
                                               id
                                               closed
                                             }
                                             userErrors {
                                               field
                                               message
                                             }
                                           }
                                         }
                                         """;
}
