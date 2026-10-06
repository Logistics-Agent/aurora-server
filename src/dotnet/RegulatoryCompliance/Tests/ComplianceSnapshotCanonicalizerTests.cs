using RegulatoryCompliance.Application.Evaluations;

namespace RegulatoryCompliance.Tests;

public sealed class ComplianceSnapshotCanonicalizerTests
{
    [Fact]
    public void Equivalent_snapshots_have_the_same_canonical_json_and_hash()
    {
        var first = Input(
            [
                new CargoEvaluationSnapshot(" Machinery ", "847989", 1, "unit", 10m, 1m, false, null, "crate"),
                new CargoEvaluationSnapshot("Bolts", "731815", 4, "box", 2m, 0.2m, false, null, "carton")
            ],
            [
                new OcrEvaluationSnapshot(Guid.Parse("01900000-0000-7000-8000-000000000002"), "PackingList", "{\"b\":2,\"a\":1}", 0.9m, false),
                new OcrEvaluationSnapshot(Guid.Parse("01900000-0000-7000-8000-000000000001"), "CommercialInvoice", "{\"a\":1,\"b\":2}", 0.95m, false)
            ]);
        var equivalent = Input(
            [
                new CargoEvaluationSnapshot("bolts", "731815", 4, "BOX", 2m, 0.2m, false, null, "CARTON"),
                new CargoEvaluationSnapshot("machinery", "847989", 1, "UNIT", 10m, 1m, false, null, "CRATE")
            ],
            [
                new OcrEvaluationSnapshot(Guid.Parse("01900000-0000-7000-8000-000000000001"), "commercialinvoice", "{\"b\":2,\"a\":1}", 0.95m, false),
                new OcrEvaluationSnapshot(Guid.Parse("01900000-0000-7000-8000-000000000002"), "packinglist", "{\"a\":1,\"b\":2}", 0.9m, false)
            ]);

        var firstCanonical = ComplianceSnapshotCanonicalizer.Canonicalize(first);
        var equivalentCanonical = ComplianceSnapshotCanonicalizer.Canonicalize(equivalent);

        Assert.Equal(firstCanonical.Json, equivalentCanonical.Json);
        Assert.Equal(firstCanonical.SnapshotHash, equivalentCanonical.SnapshotHash);
        Assert.Equal(64, firstCanonical.SnapshotHash.Length);
    }

    [Fact]
    public void Different_ocr_content_changes_the_snapshot_hash()
    {
        var original = ComplianceSnapshotCanonicalizer.Canonicalize(Input([], []));
        var changed = ComplianceSnapshotCanonicalizer.Canonicalize(Input(
            [],
            [new OcrEvaluationSnapshot(
                Guid.Parse("01900000-0000-7000-8000-000000000001"),
                "CommercialInvoice",
                "{\"invoiceNo\":\"INV-2\"}",
                0.95m,
                false)]));

        Assert.NotEqual(original.SnapshotHash, changed.SnapshotHash);
    }

    private static ComplianceEvaluationInput Input(
        IReadOnlyCollection<CargoEvaluationSnapshot> cargo,
        IReadOnlyCollection<OcrEvaluationSnapshot> documents) =>
        new(
            "evaluation-key",
            Guid.Parse("01900000-0000-7000-8000-000000000010"),
            cargo,
            " vn ",
            "sg",
            ["SG", "vn"],
            " sea ",
            documents,
            DateTimeOffset.Parse("2026-09-13T02:00:00Z"),
            "shipment-version-1");
}
