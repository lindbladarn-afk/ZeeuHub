using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using WebApp.Models.Integration;

namespace WebApp.Services.Integration.Akeneo
{
    public class AkeneoExportService : IAkeneoExportService
    {
        private const string DefaultFileName = "PIM2Jvs.xml";
        private readonly IAkeneoClient _client;

        public AkeneoExportService(IAkeneoClient client)
        {
            _client = client;
        }

        public async Task<AkeneoExportResult> ExportProductsXmlAsync(int limit, string? fileName, CancellationToken ct = default)
        {
            var products = await _client.FetchProductsAsync(limit, ct);
            var safeName = ResolveExportFileName(fileName);

            var xml = BuildXml(products);
            return new AkeneoExportResult
            {
                Scope = "all",
                FileName = safeName,
                Xml = xml,
                Count = products.Count
            };
        }

        public async Task<AkeneoExportResult> ExportProductsXmlAsync(IReadOnlyList<string> skus, int limit, string? fileName, CancellationToken ct = default)
        {
            var requestedSkus = (skus ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            var products = await _client.FetchProductsBySkusAsync(requestedSkus, limit, ct);
            var safeName = ResolveExportFileName(fileName);

            var xml = BuildXml(products);
            var foundSkus = products
                .Select(product => product.Identifier)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!.Trim())
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var notFoundSkus = requestedSkus
                .Where(sku => !foundSkus.Contains(sku))
                .ToList();

            return new AkeneoExportResult
            {
                Scope = "skus",
                FileName = safeName,
                Xml = xml,
                Count = products.Count,
                RequestedSkus = requestedSkus,
                NotFoundSkus = notFoundSkus
            };
        }

        private static string BuildXml(IReadOnlyList<AkeneoProduct> products)
        {
            var root = new XElement("products");

            foreach (var p in products)
            {
                var articleNo = FirstNonEmpty(p.ArtNr, p.Identifier) ?? string.Empty;
                var product = new XElement("product", new XAttribute("ArtNr", articleNo));

                foreach (var element in BuildKnownElements(p))
                    product.Add(element);

                foreach (var attribute in p.Attributes
                             .Where(entry => !ShouldSkipAttribute(entry.Key)))
                {
                    product.Add(new XElement(attribute.Key, attribute.Value ?? string.Empty));
                }

                root.Add(product);
            }

            var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), root);
            var settings = new XmlWriterSettings
            {
                Indent = true,
                Encoding = new UTF8Encoding(false),
                OmitXmlDeclaration = false,
                NewLineHandling = NewLineHandling.Entitize
            };

            using var sw = new Utf8StringWriter();
            using var writer = XmlWriter.Create(sw, settings);
            doc.Save(writer);
            writer.Flush();
            return sw.ToString();
        }

        private static IEnumerable<XElement> BuildKnownElements(AkeneoProduct product)
        {
            yield return CreateElement("ArtBeskr", product.ArtBeskr);
            yield return CreateElement("ArtBeskrSpec", product.ArtBeskrSpec);
            yield return CreateElement("ArtKat", product.ArtKat);
            yield return CreateElement("ArtKod", GetAttribute(product, "ArtKod"));
            yield return CreateElement("ArtNrEAN", FirstNonEmpty(product.ArtNrEan, GetAttribute(product, "ArtNrEAN")));
            yield return CreateElement("ArtProdKonto", GetAttribute(product, "ArtProdKonto"));
            yield return CreateElement("ArtRitnNr", product.ArtRitnNr);
            yield return CreateElement("ArtStatNr", GetAttribute(product, "ArtStatNr"));
            yield return CreateElement("ArtVikt", GetAttribute(product, "ArtVikt"));
            yield return CreateElement("ItemStatusCode", GetAttribute(product, "ItemStatusCode"));
            yield return CreateElement("LevNr", GetAttribute(product, "LevNr"));
            yield return CreateElement("MSRPEUR", GetAttribute(product, "MSRPEUR"));
            yield return CreateElement("MSRPSEK", GetAttribute(product, "MSRPSEK"));
            yield return CreateElement("MSRPUSD", GetAttribute(product, "MSRPUSD"));
            yield return CreateElement("VaruGruppKod", product.VaruGruppKod);
        }

        private static bool ShouldSkipAttribute(string key)
        {
            return key.Equals("ArtNr", StringComparison.OrdinalIgnoreCase)
                || key.Equals("ArtBeskr", StringComparison.OrdinalIgnoreCase)
                || key.Equals("ArtBeskrSpec", StringComparison.OrdinalIgnoreCase)
                || key.Equals("ArtKat", StringComparison.OrdinalIgnoreCase)
                || key.Equals("ArtKod", StringComparison.OrdinalIgnoreCase)
                || key.Equals("ArtNrEAN", StringComparison.OrdinalIgnoreCase)
                || key.Equals("ArtProdKonto", StringComparison.OrdinalIgnoreCase)
                || key.Equals("ArtRitnNr", StringComparison.OrdinalIgnoreCase)
                || key.Equals("ArtStatNr", StringComparison.OrdinalIgnoreCase)
                || key.Equals("ArtVikt", StringComparison.OrdinalIgnoreCase)
                || key.Equals("ItemStatusCode", StringComparison.OrdinalIgnoreCase)
                || key.Equals("LevNr", StringComparison.OrdinalIgnoreCase)
                || key.Equals("MSRPEUR", StringComparison.OrdinalIgnoreCase)
                || key.Equals("MSRPSEK", StringComparison.OrdinalIgnoreCase)
                || key.Equals("MSRPUSD", StringComparison.OrdinalIgnoreCase)
                || key.Equals("VaruGruppKod", StringComparison.OrdinalIgnoreCase);
        }

        private static string? GetAttribute(AkeneoProduct product, string key)
            => product.Attributes.TryGetValue(key, out var value) ? value : null;

        private static string? FirstNonEmpty(params string?[] values)
            => values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

        private static XElement CreateElement(string name, string? value)
            => new(name, value ?? string.Empty);

        private static string ResolveExportFileName(string? fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
                return DefaultFileName;

            var normalized = EnsureXmlExtension(fileName);
            return string.Equals(normalized, DefaultFileName, StringComparison.OrdinalIgnoreCase)
                ? DefaultFileName
                : DefaultFileName;
        }

        private static string EnsureXmlExtension(string fileName)
        {
            return fileName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)
                ? fileName
                : fileName + ".xml";
        }

        private sealed class Utf8StringWriter : StringWriter
        {
            public override Encoding Encoding => Encoding.UTF8;
        }
    }
}
