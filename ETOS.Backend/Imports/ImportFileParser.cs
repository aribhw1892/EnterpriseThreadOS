using System.Globalization;
using System.Text;
using ETOS.Backend.Identity;
using ExcelDataReader;

namespace ETOS.Backend.Imports;

public sealed record ParsedImportFile(
    IReadOnlyCollection<string> Headers,
    IReadOnlyCollection<IReadOnlyDictionary<string, string?>> Rows);

public interface IImportFileParser
{
    Task<ParsedImportFile> ParseAsync(
        string originalFileName,
        Stream content,
        int? maxRows,
        CancellationToken cancellationToken);
}

public sealed class CsvImportFileParser : IImportFileParser
{
    public async Task<ParsedImportFile> ParseAsync(
        string originalFileName,
        Stream content,
        int? maxRows,
        CancellationToken cancellationToken)
    {
        var extension = Path.GetExtension(originalFileName);
        if (string.Equals(extension, ".xls", StringComparison.OrdinalIgnoreCase)
            || string.Equals(extension, ".xlsx", StringComparison.OrdinalIgnoreCase))
        {
            return ParseExcel(content, maxRows, cancellationToken);
        }

        if (!string.Equals(extension, ".csv", StringComparison.OrdinalIgnoreCase)
            && !string.Equals(extension, ".txt", StringComparison.OrdinalIgnoreCase))
        {
            throw new RequestValidationException("Only CSV and Excel import files are supported.");
        }

        using var reader = new StreamReader(content, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
        var records = new List<IReadOnlyList<string>>();
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var record = await ReadCsvRecordAsync(reader, cancellationToken);
            if (record is null)
            {
                break;
            }

            records.Add(record);
            if (maxRows is not null && records.Count > maxRows.Value)
            {
                break;
            }
        }

        if (records.Count == 0)
        {
            throw new RequestValidationException("Import file did not contain a header row.");
        }

        var headers = records[0]
            .Select(header => header.Trim())
            .Where(header => !string.IsNullOrWhiteSpace(header))
            .ToList();
        if (headers.Count == 0)
        {
            throw new RequestValidationException("Import file header row did not contain any columns.");
        }

        if (headers.Select(NormalizeKey).Distinct(StringComparer.Ordinal).Count() != headers.Count)
        {
            throw new RequestValidationException("Import file headers must be unique.");
        }

        var rows = records.Skip(1)
            .Select(record => ToRow(headers, record))
            .ToList();

        return new ParsedImportFile(headers, rows);
    }

    private static ParsedImportFile ParseExcel(Stream content, int? maxRows, CancellationToken cancellationToken)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var reader = ExcelReaderFactory.CreateReader(content);
        var records = new List<IReadOnlyList<string>>();
        do
        {
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                var values = new List<string>();
                for (var index = 0; index < reader.FieldCount; index++)
                {
                    values.Add(Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture) ?? string.Empty);
                }

                records.Add(values);
                if (maxRows is not null && records.Count > maxRows.Value)
                {
                    break;
                }
            }

            if (records.Count > 0)
            {
                break;
            }
        }
        while (reader.NextResult());

        if (records.Count == 0)
        {
            throw new RequestValidationException("Import workbook did not contain a header row.");
        }

        var headers = records[0]
            .Select(header => header.Trim())
            .Where(header => !string.IsNullOrWhiteSpace(header))
            .ToList();
        if (headers.Count == 0)
        {
            throw new RequestValidationException("Import workbook header row did not contain any columns.");
        }

        if (headers.Select(NormalizeKey).Distinct(StringComparer.Ordinal).Count() != headers.Count)
        {
            throw new RequestValidationException("Import workbook headers must be unique.");
        }

        var rows = records.Skip(1)
            .Select(record => ToRow(headers, record))
            .ToList();
        return new ParsedImportFile(headers, rows);
    }

    private static async Task<IReadOnlyList<string>?> ReadCsvRecordAsync(
        StreamReader reader,
        CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        var inQuotes = false;
        while (true)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (line is null)
            {
                return builder.Length == 0 ? null : ParseCsvLine(builder.ToString(), ref inQuotes);
            }

            if (builder.Length > 0)
            {
                builder.Append('\n');
            }

            builder.Append(line);
            var parsed = ParseCsvLine(builder.ToString(), ref inQuotes);
            if (!inQuotes)
            {
                return parsed;
            }
        }
    }

    private static IReadOnlyList<string> ParseCsvLine(string line, ref bool inQuotes)
    {
        var values = new List<string>();
        var current = new StringBuilder();
        inQuotes = false;
        for (var index = 0; index < line.Length; index++)
        {
            var character = line[index];
            if (character == '"')
            {
                if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                {
                    current.Append('"');
                    index++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }
            }
            else if (character == ',' && !inQuotes)
            {
                values.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(character);
            }
        }

        values.Add(current.ToString());
        return values;
    }

    private static IReadOnlyDictionary<string, string?> ToRow(IReadOnlyList<string> headers, IReadOnlyList<string> record)
    {
        var row = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        for (var index = 0; index < headers.Count; index++)
        {
            var value = index < record.Count ? record[index].Trim() : null;
            row[headers[index]] = string.IsNullOrWhiteSpace(value) ? null : value;
        }

        return row;
    }

    private static string NormalizeKey(string value) => value.Trim().ToUpperInvariant();
}
