using System.Net;
using System.Text;
using System.Text.Json;
using PowerMonitor.Core.Schedule;

namespace PowerMonitor.Tests;

public class YasnoTests
{
    // Shape of the live answer; the groups and hours are made up.
    private const string Planned = """
        {
          "1.1": {
            "today": {
              "slots": [
                {"start": 0, "end": 120, "type": "Definite"},
                {"start": 120, "end": 720, "type": "NotPlanned"},
                {"start": 720, "end": 960, "type": "Definite"},
                {"start": 960, "end": 1080, "type": "Possible"},
                {"start": 1080, "end": 1440, "type": "NotPlanned"}
              ],
              "date": "2026-11-24T00:00:00+02:00",
              "status": "ScheduleApplies"
            },
            "tomorrow": {"slots": [], "date": "2026-11-25T00:00:00+02:00", "status": "WaitingForSchedule"},
            "updatedOn": "2026-11-23T21:45:03+00:00"
          },
          "2.1": {
            "today": {"slots": [], "date": "2026-11-24T00:00:00+02:00", "status": "NoOutages"},
            "tomorrow": {"slots": [], "date": "2026-11-25T00:00:00+02:00", "status": "EmergencyShutdowns"},
            "updatedOn": "2026-11-23T21:45:03+00:00"
          }
        }
        """;

    private static string WithSlots(string slots) => """
        {"1.1": {"today": {"slots": [SLOTS], "date": "2026-11-24T00:00:00+02:00", "status": "ScheduleApplies"}}}
        """.Replace("SLOTS", slots);

    private static JsonElement Json(string text) => JsonDocument.Parse(text).RootElement.Clone();

    [Fact]
    public void Parses_slots_of_one_group_and_skips_unpublished_days()
    {
        var days = YasnoProvider.ParsePlanned(Json(Planned), "1.1");

        var today = Assert.Single(days);
        Assert.Equal(new DateOnly(2026, 11, 24), today.Day);
        Assert.Equal(DayStatus.Applies, today.Status);
        Assert.Equal(3, today.Outages.Count);
        Assert.Equal(KyivTime.At(today.Day, 0), today.Outages[0].Start);
        Assert.Equal(KyivTime.At(today.Day, 120), today.Outages[0].End);
        Assert.False(today.Outages[0].Possible);
        Assert.True(today.Outages[2].Possible);
        Assert.NotNull(today.UpdatedAt);
    }

    [Fact]
    public void Reads_no_outages_and_emergency_days()
    {
        var days = YasnoProvider.ParsePlanned(Json(Planned), "2.1");

        Assert.Equal(2, days.Count);
        Assert.Equal(DayStatus.NoOutages, days[0].Status);
        Assert.Empty(days[0].Outages);
        Assert.Equal(DayStatus.Emergency, days[1].Status);
    }

    [Fact]
    public void Rejects_a_missing_group_and_a_bad_group_id()
    {
        Assert.Throws<ScheduleSourceException>(() => YasnoProvider.ParsePlanned(Json(Planned), "9.1"));
        Assert.Throws<ScheduleSourceException>(() => YasnoProvider.ParsePlanned(Json(Planned), "1.1/../x"));
    }

    [Theory]
    [InlineData("""{"start": 100, "end": 50, "type": "Definite"}""")]
    [InlineData("""{"start": -5, "end": 50, "type": "Definite"}""")]
    [InlineData("""{"start": 0, "end": 1500, "type": "Definite"}""")]
    [InlineData("""{"start": 0, "end": 60, "type": "Surprise"}""")]
    [InlineData("""{"start": "0", "end": 60, "type": "Definite"}""")]
    public void Rejects_malformed_slots(string slot)
    {
        var payload = WithSlots(slot);

        Assert.Throws<ScheduleSourceException>(() => YasnoProvider.ParsePlanned(Json(payload), "1.1"));
    }

    [Fact]
    public void Rejects_a_date_without_an_offset_and_too_many_slots_or_groups()
    {
        var noOffset = """
            {"1.1": {"today": {"slots": [], "date": "2026-11-24T00:00:00", "status": "NoOutages"}}}
            """;
        Assert.Throws<ScheduleSourceException>(() => YasnoProvider.ParsePlanned(Json(noOffset), "1.1"));

        var slots = string.Join(",", Enumerable.Repeat("""{"start": 0, "end": 10, "type": "Definite"}""", 201));
        Assert.Throws<ScheduleSourceException>(() => YasnoProvider.ParsePlanned(Json(WithSlots(slots)), "1.1"));

        var groups = string.Join(",", Enumerable.Range(1, 201).Select(n => $"\"{n}.1\": {{}}"));
        Assert.Throws<ScheduleSourceException>(() => YasnoProvider.ParsePlanned(Json($"{{{groups}}}"), "1.1"));
    }

    [Fact]
    public void Slot_times_follow_the_kyiv_wall_clock_across_a_clock_change()
    {
        // Kyiv leaves summer time on 2026-10-25: 05:00 that day is still the wall clock, not 5 h of elapsed time.
        var day = new DateOnly(2026, 10, 25);
        var payload = """
            {"1.1": {"today": {"slots": [{"start": 300, "end": 360, "type": "Definite"}],
                               "date": "2026-10-25T00:00:00+03:00", "status": "ScheduleApplies"}}}
            """;

        var outage = Assert.Single(Assert.Single(YasnoProvider.ParsePlanned(Json(payload), "1.1")).Outages);

        Assert.Equal(day, KyivTime.DayOf(outage.Start));
        Assert.Equal(TimeSpan.FromHours(2), outage.Start.Offset);
    }

    [Fact]
    public void Parses_directory_choices_and_drops_unsafe_ones()
    {
        var choices = YasnoAddressLookup.ParseChoices(Json("""
            [{"id": 911, "value": "вул. Хрещатик"},
             {"id": "12; DROP", "value": "bad id"},
             {"id": 5, "value": "  spaced\u200b   out\u0007  "},
             {"id": 6, "value": 17},
             "junk"]
            """));

        Assert.Equal(2, choices.Count);
        Assert.Equal(new AddressChoice("911", "вул. Хрещатик"), choices[0]);
        Assert.Equal("spaced out", choices[1].Label);
    }

    [Fact]
    public void Parses_the_group_of_an_address()
    {
        Assert.Equal("20.1", YasnoAddressLookup.ParseGroup(Json("""{"group": 20, "subgroup": 1}""")));
        Assert.Null(YasnoAddressLookup.ParseGroup(Json("""{"group": 0, "subgroup": 1}""")));
        Assert.Null(YasnoAddressLookup.ParseGroup(Json("""{"group": 20}""")));
        Assert.Null(YasnoAddressLookup.ParseGroup(Json("""{"title": "EntryNotFoundException"}""")));
    }

    [Fact]
    public void Matches_house_numbers_the_way_people_type_them()
    {
        var found = AddressText.Pick(
            [new("1", "8А"), new("2", "8"), new("3", "80"), new("4", "18")],
            "8-а");

        Assert.Equal(["1"], found.Select(c => c.Id).ToArray());
        Assert.Equal(["2", "1", "3"], AddressText.Pick([new("1", "8А"), new("2", "8"), new("3", "80")], "8").Select(c => c.Id).ToArray());
    }

    [Fact]
    public async Task Looks_up_streets_houses_and_the_group_with_checked_ids()
    {
        var handler = new FakeHandler(request => request.RequestUri!.AbsolutePath switch
        {
            var p when p.EndsWith("/streets") => """[{"id": 911, "value": "вул. Хрещатик"}]""",
            var p when p.EndsWith("/houses") => """[{"id": 41275, "value": "12"}, {"id": 41273, "value": "10,12"}]""",
            _ => """{"group": 20, "subgroup": 1}"""
        });
        using var http = new ScheduleHttp([YasnoProvider.Host], handler);
        var lookup = new YasnoAddressLookup(http, YasnoProvider.KyivRegion, YasnoProvider.KyivDso);

        var streets = await lookup.OptionsAsync([], "вул. Хрещатик", CancellationToken.None);
        var houses = await lookup.OptionsAsync(["911"], "12", CancellationToken.None);
        var group = await lookup.ResolveGroupAsync(["911", "41275"], CancellationToken.None);

        Assert.Equal("911", Assert.Single(streets).Id);
        Assert.Equal("41275", houses[0].Id);
        Assert.Equal("20.1", group);
        Assert.All(handler.Requests, uri => Assert.Equal(YasnoProvider.Host, uri.Host));
        Assert.Contains("regionId=25", handler.Requests[0].Query);
        Assert.Contains("dsoId=902", handler.Requests[0].Query);
        Assert.Contains("streetId=911", handler.Requests[2].Query);
        Assert.Contains("houseId=41275", handler.Requests[2].Query);
        await Assert.ThrowsAsync<ArgumentException>(
            () => lookup.ResolveGroupAsync(["911&evil=1", "1"], CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(
            () => lookup.OptionsAsync(["1", "2"], "x", CancellationToken.None));
    }

    [Fact]
    public async Task Does_not_search_for_too_short_a_street()
    {
        var handler = new FakeHandler(_ => "[]");
        using var http = new ScheduleHttp([YasnoProvider.Host], handler);
        var lookup = new YasnoAddressLookup(http, YasnoProvider.KyivRegion, YasnoProvider.KyivDso);

        var found = await lookup.OptionsAsync([], "вул. Хр", CancellationToken.None);

        Assert.Empty(found);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Fetches_the_schedule_of_a_group()
    {
        var handler = new FakeHandler(_ => Planned);
        using var http = new ScheduleHttp([YasnoProvider.Host], handler);
        var provider = new YasnoProvider(http, YasnoProvider.KyivRegion, YasnoProvider.KyivDso);

        var days = await provider.FetchAsync("1.1", CancellationToken.None);

        Assert.Single(days);
        Assert.EndsWith("/regions/25/dsos/902/planned-outages", Assert.Single(handler.Requests).AbsolutePath);
        await Assert.ThrowsAsync<ScheduleSourceException>(() => provider.FetchAsync("../x", CancellationToken.None));
    }

    [Fact]
    public async Task Http_refuses_other_hosts_plain_http_redirects_errors_and_big_answers()
    {
        var handler = new FakeHandler(_ => "{}");
        using var http = new ScheduleHttp([YasnoProvider.Host], handler);

        await Assert.ThrowsAsync<ScheduleSourceException>(
            () => http.GetJsonAsync(new Uri("https://evil.example/x"), CancellationToken.None));
        await Assert.ThrowsAsync<ScheduleSourceException>(
            () => http.GetJsonAsync(new Uri("http://app.yasno.ua/x"), CancellationToken.None));
        await Assert.ThrowsAsync<ScheduleSourceException>(
            () => http.GetJsonAsync(new Uri("https://app.yasno.ua:8443/x"), CancellationToken.None));
        Assert.Empty(handler.Requests);

        using var redirecting = new ScheduleHttp(
            [YasnoProvider.Host],
            new FakeHandler(_ => "", HttpStatusCode.Found));
        await Assert.ThrowsAsync<ScheduleSourceException>(
            () => redirecting.GetJsonAsync(new Uri("https://app.yasno.ua/x"), CancellationToken.None));

        using var failing = new ScheduleHttp([YasnoProvider.Host], new FakeHandler(_ => "", HttpStatusCode.NotFound));
        await Assert.ThrowsAsync<ScheduleSourceException>(
            () => failing.GetJsonAsync(new Uri("https://app.yasno.ua/x"), CancellationToken.None));

        using var notJson = new ScheduleHttp([YasnoProvider.Host], new FakeHandler(_ => "<html>"));
        await Assert.ThrowsAsync<ScheduleSourceException>(
            () => notJson.GetJsonAsync(new Uri("https://app.yasno.ua/x"), CancellationToken.None));

        using var huge = new ScheduleHttp(
            [YasnoProvider.Host],
            new FakeHandler(_ => "[" + new string('1', ScheduleHttp.MaxBytes) + "]"));
        await Assert.ThrowsAsync<ScheduleSourceException>(
            () => huge.GetJsonAsync(new Uri("https://app.yasno.ua/x"), CancellationToken.None));
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, string> body, HttpStatusCode status = HttpStatusCode.OK)
        : HttpMessageHandler
    {
        public List<Uri> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);
            return Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body(request), Encoding.UTF8, "application/json")
            });
        }
    }
}
