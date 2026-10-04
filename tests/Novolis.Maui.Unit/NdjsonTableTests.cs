using System.Text.Json;
using Novolis.IO.Ndjson;
using Novolis.Maui.Ndjson;

namespace Novolis.Maui.Unit;

public sealed class NdjsonTableTests
{
    [Test]
    public async Task Infer_PrioritizesLogFieldsAndCapsColumns()
    {
        var record = CreateRecord(
            "{\"message\":\"connected\",\"level\":\"info\",\"timestamp\":\"2026-10-04T22:03:00Z\",\"source\":\"reader\",\"one\":1,\"two\":2,\"three\":3,\"four\":4,\"five\":5,\"six\":6,\"seven\":7,\"eight\":8,\"nine\":9}");

        var columns = NdjsonTableSchema.Infer([new NdjsonRecordDisplay(record)]);

        await Assert.That(columns.Count).IsEqualTo(NdjsonTableSchema.MaximumFieldColumns);
        await Assert.That(columns[0].Name).IsEqualTo("timestamp");
        await Assert.That(columns[1].Name).IsEqualTo("level");
        await Assert.That(columns[2].Name).IsEqualTo("message");
    }

    [Test]
    public async Task Row_UsesCompactValuesAndAnExplicitMissingMarker()
    {
        var display = new NdjsonRecordDisplay(CreateRecord(
            "{\"message\":\"connected\",\"context\":{\"device\":\"reader\"},\"ok\":true}"));
        var row = new NdjsonTableRow(
            display,
            [
                NdjsonTableColumn.ForField("message"),
                NdjsonTableColumn.ForField("context"),
                NdjsonTableColumn.ForField("missing"),
            ]);

        await Assert.That(row.ValueFor("message")).IsEqualTo("connected");
        await Assert.That(row.ValueFor("context")).IsEqualTo("{\"device\":\"reader\"}");
        await Assert.That(row.ValueFor("missing")).IsEqualTo("—");
    }

    private static NdjsonRecord CreateRecord(string json)
    {
        using var document = JsonDocument.Parse(json);
        return new NdjsonRecord(1, 0, document.RootElement.Clone(), null, null);
    }
}
