using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace PowerMonitor.Core.Schedule;

/// <summary>
/// YASNO public API, which serves DTEK Kyiv (region 25, dso 902). Ported from
/// fastiuk/ukraine-energy-monitor (<c>providers/yasno.py</c>).
/// <code>
/// GET /regions/{region}/dsos/{dso}/planned-outages
/// {"1.1": {"today": {"slots": [{"start": 0, "end": 810, "type": "NotPlanned"}, ...],
///                    "date": "2026-10-18T00:00:00+03:00", "status": "ScheduleApplies"},
///          "tomorrow": {..., "status": "WaitingForSchedule"},
///          "updatedOn": "2026-10-18T12:53:46+00:00"}}
/// </code>
/// Slot ends are minutes from local midnight.
/// </summary>
public sealed class YasnoProvider : IScheduleProvider
{
    public const string Host = "app.yasno.ua";
    public const int KyivRegion = 25;
    public const int KyivDso = 902;
    public const string BaseUrl = "https://app.yasno.ua/api/blackout-service/public/shutdowns";
    public const int MaxGroups = 200;
    public const int MaxSlots = 200;

    private static readonly Regex GroupPattern = new(
        @"^[0-9]{1,3}\.[0-9]{1,2}$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ScheduleHttp _http;
    private readonly int _region;
    private readonly int _dso;

    public YasnoProvider(ScheduleHttp http, int region, int dso)
    {
        _http = http;
        _region = region;
        _dso = dso;
    }

    public static bool IsGroup(string? group) => group is not null && GroupPattern.IsMatch(group);

    public async Task<IReadOnlyList<DaySchedule>> FetchAsync(string group, CancellationToken cancellationToken)
    {
        if (!IsGroup(group))
            throw new ScheduleSourceException("The saved outage group is not valid. Look up the address again.");

        var url = new Uri($"{BaseUrl}/regions/{_region}/dsos/{_dso}/planned-outages");
        var payload = await _http.GetJsonAsync(url, cancellationToken);
        return ParsePlanned(payload, group);
    }

    public static IReadOnlyList<DaySchedule> ParsePlanned(JsonElement payload, string group)
    {
        if (!IsGroup(group))
            throw new ScheduleSourceException("The outage group is not valid.");
        if (payload.ValueKind != JsonValueKind.Object)
            throw new ScheduleSourceException("YASNO: expected an object keyed by group.");

        var groups = 0;
        JsonElement entry = default;
        var found = false;
        foreach (var property in payload.EnumerateObject())
        {
            if (++groups > MaxGroups)
                throw new ScheduleSourceException("YASNO: too many groups.");
            if (property.Name == group)
            {
                entry = property.Value;
                found = true;
            }
        }

        if (groups == 0)
            throw new ScheduleSourceException("YASNO: no groups in the answer.");
        if (!found)
            throw new ScheduleSourceException($"YASNO does not list group {group}.");
        if (entry.ValueKind != JsonValueKind.Object)
            throw new ScheduleSourceException($"YASNO: unexpected entry for group {group}.");

        var updated = ReadDate(entry, "updatedOn");
        var days = new List<DaySchedule>();
        foreach (var key in new[] { "today", "tomorrow" })
        {
            if (!entry.TryGetProperty(key, out var item) || item.ValueKind != JsonValueKind.Object)
                continue;

            var status = ReadString(item, "status");
            if (status == "WaitingForSchedule")
                continue;
            if (ReadDate(item, "date") is not { } start)
                throw new ScheduleSourceException($"YASNO: no date for {group} {key}.");

            var day = KyivTime.DayOf(start);
            switch (status)
            {
                case "NoOutages":
                    days.Add(new DaySchedule(day, updated, DayStatus.NoOutages, []));
                    break;
                case "ScheduleApplies":
                    days.Add(new DaySchedule(day, updated, DayStatus.Applies, ParseSlots(day, item)));
                    break;
                case "EmergencyShutdowns":
                    days.Add(new DaySchedule(day, updated, DayStatus.Emergency, []));
                    break;
                default:
                    days.Add(new DaySchedule(day, updated, DayStatus.Unknown, []));
                    break;
            }
        }

        return days.OrderBy(d => d.Day).ToList();
    }

    private static IReadOnlyList<ScheduledOutage> ParseSlots(DateOnly day, JsonElement item)
    {
        if (!item.TryGetProperty("slots", out var slots) || slots.ValueKind != JsonValueKind.Array ||
            slots.GetArrayLength() > MaxSlots)
            throw new ScheduleSourceException("YASNO: unreadable slots.");

        var outages = new List<ScheduledOutage>();
        foreach (var slot in slots.EnumerateArray())
        {
            if (slot.ValueKind != JsonValueKind.Object ||
                !TryInt(slot, "start", out var start) ||
                !TryInt(slot, "end", out var end) ||
                start < 0 || start >= end || end > 1440)
                throw new ScheduleSourceException("YASNO: unreadable slot.");

            var type = ReadString(slot, "type");
            bool possible;
            if (type == "NotPlanned")
                continue;
            if (type == "Definite")
                possible = false;
            else if (type is not null &&
                     (type.StartsWith("Possib", StringComparison.Ordinal) ||
                      type.StartsWith("Probab", StringComparison.Ordinal)))
                possible = true;
            else
                throw new ScheduleSourceException("YASNO: unknown slot type.");

            outages.Add(new ScheduledOutage(KyivTime.At(day, start), KyivTime.At(day, end), possible));
        }

        return ScheduleTimeline.Merge(outages);
    }

    private static bool TryInt(JsonElement element, string name, out int value)
    {
        value = 0;
        return element.TryGetProperty(name, out var property) &&
               property.ValueKind == JsonValueKind.Number &&
               property.TryGetInt32(out value);
    }

    private static string? ReadString(JsonElement element, string name) =>
        element.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static DateTimeOffset? ReadDate(JsonElement element, string name)
    {
        var text = ReadString(element, name);
        if (text is null || text.Length > 40)
            return null;

        // An answer without an offset is ambiguous; refuse it.
        return DateTimeOffset.TryParse(
            text,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var moment) && HasOffset(text)
            ? moment
            : null;
    }

    private static bool HasOffset(string text) =>
        text.EndsWith('Z') || text.LastIndexOfAny(['+', '-']) > 9;
}

/// <summary>Address to group through YASNO's open directory.</summary>
public sealed class YasnoAddressLookup : IAddressLookup
{
    private static readonly Regex HouseNumber = new("[0-9]{1,5}", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private readonly ScheduleHttp _http;
    private readonly int _region;
    private readonly int _dso;

    public YasnoAddressLookup(ScheduleHttp http, int region, int dso)
    {
        _http = http;
        _region = region;
        _dso = dso;
    }

    public const int MinQuery = 3;

    public IReadOnlyList<AddressStep> Steps { get; } =
    [
        new("Street name", "Khreshchatyk", false),
        new("House number", "12", true)
    ];

    public async Task<IReadOnlyList<AddressChoice>> OptionsAsync(
        IReadOnlyList<string> path,
        string query,
        CancellationToken cancellationToken)
    {
        if (path.Count >= Steps.Count)
            throw new ArgumentException("The address is already complete.", nameof(path));

        query = AddressText.CleanQuery(query);
        var house = Steps[path.Count].IsHouse;
        var terms = new List<string>();
        if (house)
        {
            // The directory writes "8А" where people type "8-а": search by the number, filter here.
            var number = HouseNumber.Match(query);
            terms.Add(number.Success ? number.Value : AddressText.StripKind(query).Trim());
        }
        else
        {
            var whole = AddressText.StripKind(query).Trim();
            terms.Add(whole);
            var longest = AddressText.SearchTerm(query);
            if (longest != whole.ToLowerInvariant())
                terms.Add(longest);
        }

        foreach (var term in terms)
        {
            if (term.Length < (house ? 1 : MinQuery))
                continue;

            var url = Url(house ? "houses" : "streets", path, term);
            var found = AddressText.Pick(ParseChoices(await _http.GetJsonAsync(url, cancellationToken)), query);
            if (found.Count > 0)
                return found;
        }

        return [];
    }

    public async Task<string?> ResolveGroupAsync(IReadOnlyList<string> path, CancellationToken cancellationToken)
    {
        if (path.Count != Steps.Count)
            throw new ArgumentException("The address is incomplete.", nameof(path));

        var payload = await _http.GetJsonAsync(Url("group", path, null), cancellationToken);
        return ParseGroup(payload);
    }

    public static IReadOnlyList<AddressChoice> ParseChoices(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Array || payload.GetArrayLength() > 50_000)
            throw new ScheduleSourceException("YASNO addresses: expected a list.");

        var choices = new List<AddressChoice>();
        foreach (var item in payload.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object ||
                !item.TryGetProperty("id", out var idElement) ||
                idElement.ValueKind != JsonValueKind.Number ||
                !idElement.TryGetInt64(out var idNumber) ||
                AddressText.NumericId(idNumber.ToString(CultureInfo.InvariantCulture)) is not { } id ||
                !item.TryGetProperty("value", out var valueElement) ||
                valueElement.ValueKind != JsonValueKind.String ||
                AddressText.CleanLabel(valueElement.GetString()) is not { } label)
                continue;

            choices.Add(new AddressChoice(id, label));
        }

        return choices;
    }

    /// <summary><c>{"group": 19, "subgroup": 1}</c> becomes "19.1"; anything else is no group.</summary>
    public static string? ParseGroup(JsonElement payload)
    {
        if (payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("group", out var group) || !group.TryGetInt32(out var g) ||
            !payload.TryGetProperty("subgroup", out var subgroup) || !subgroup.TryGetInt32(out var s) ||
            g is <= 0 or >= 100 || s is <= 0 or >= 100)
            return null;

        return $"{g}.{s}";
    }

    private Uri Url(string endpoint, IReadOnlyList<string> path, string? query)
    {
        var parts = new List<string>
        {
            $"regionId={_region}",
            $"dsoId={_dso}"
        };
        if (path.Count > 0)
            parts.Add($"streetId={AddressText.RequireId(path[0])}");
        if (path.Count > 1)
            parts.Add($"houseId={AddressText.RequireId(path[1])}");
        if (query is not null)
            parts.Add($"query={Uri.EscapeDataString(query)}");

        return new Uri($"{YasnoProvider.BaseUrl}/addresses/v2/{endpoint}?{string.Join('&', parts)}");
    }
}
