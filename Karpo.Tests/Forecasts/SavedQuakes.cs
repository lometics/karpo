// quakes: saved answers of the USGS's feed (docs\widgets.md §240). Sample is a real one, the past
// week's of 2.5 and up as the feed gave it on 2026-10-19 at 17:40:45 UTC, cut to six earthquakes —
// New Mexico's 3.3, the Rat Islands' 4.2 under two networks' ids, Romania's 4.5, Greece's 4.6,
// Russia's 6.9 and the Mid-Atlantic Ridge's 6.8 — and a quarry blast from the all-magnitudes feed,
// verbatim. Feed builds one of its own shape from a test's events.

using System.Globalization;
using System.Text;

namespace Karpo.Tests.Forecasts;

internal static class SavedQuakes
{
    /// <summary>When the feed was generated: what "now" is for the sample.</summary>
    public static readonly DateTime Generated = new(2026, 10, 19, 17, 40, 45, DateTimeKind.Utc);

    /// <summary>Zürich, where the sample's distances are from.</summary>
    public static WeatherOptions Zurich() => new() { Place = "Zürich, Switzerland", Latitude = 47.37, Longitude = 8.54 };

    public const string Sample = """
        {"type":"FeatureCollection","metadata":{"generated":1792431645000,"url":"https://earthquake.usgs.gov/earthquakes/feed/v1.0/summary/2.5_week.geojson","title":"USGS Magnitude 2.5+ Earthquakes, Past Week","status":200,"api":"2.7.0","count":332},"features":[{"type":"Feature","properties":{"mag":3.3,"place":"23 km SSW of Lovington, New Mexico","time":1792430728209,"updated":1792431109040,"tz":null,"url":"https://earthquake.usgs.gov/earthquakes/eventpage/us6000u3fl","detail":"https://earthquake.usgs.gov/earthquakes/feed/v1.0/detail/us6000u3fl.geojson","felt":null,"cdi":null,"mmi":null,"alert":null,"status":"reviewed","tsunami":0,"sig":168,"net":"us","code":"6000u3fl","ids":",us6000u3fl,","sources":",us,","types":",origin,phase-data,","nst":40,"dmin":0.436,"rms":0.67,"gap":101,"magType":"mb_lg","type":"earthquake","title":"M 3.3 - 23 km SSW of Lovington, New Mexico"},"geometry":{"type":"Point","coordinates":[-103.4715,32.759,6.52]},"id":"us6000u3fl"},{"type":"Feature","properties":{"mag":4.2,"place":"Rat Islands, Aleutian Islands, Alaska","time":1792427265503,"updated":1792431124487,"tz":null,"url":"https://earthquake.usgs.gov/earthquakes/eventpage/us6000u3de","detail":"https://earthquake.usgs.gov/earthquakes/feed/v1.0/detail/us6000u3de.geojson","felt":null,"cdi":null,"mmi":2.886,"alert":null,"status":"reviewed","tsunami":0,"sig":271,"net":"us","code":"6000u3de","ids":",aka2026usthoq,us6000u3de,","sources":",ak,us,","types":",origin,phase-data,shakemap,","nst":64,"dmin":0.432,"rms":0.89,"gap":163,"magType":"mb","type":"earthquake","title":"M 4.2 - Rat Islands, Aleutian Islands, Alaska"},"geometry":{"type":"Point","coordinates":[178.3686,51.5183,52.174]},"id":"us6000u3de"},{"type":"Feature","properties":{"mag":4.5,"place":"2 km W of Lunca Jariștei, Romania","time":1792399917571,"updated":1792427049301,"tz":null,"url":"https://earthquake.usgs.gov/earthquakes/eventpage/us6000u3bs","detail":"https://earthquake.usgs.gov/earthquakes/feed/v1.0/detail/us6000u3bs.geojson","felt":18,"cdi":3.4,"mmi":null,"alert":null,"status":"reviewed","tsunami":0,"sig":318,"net":"us","code":"6000u3bs","ids":",us6000u3bs,","sources":",us,","types":",dyfi,origin,phase-data,","nst":134,"dmin":1.864,"rms":0.56,"gap":33,"magType":"mb","type":"earthquake","title":"M 4.5 - 2 km W of Lunca Jariștei, Romania"},"geometry":{"type":"Point","coordinates":[26.2168,45.481,124.754]},"id":"us6000u3bs"},{"type":"Feature","properties":{"mag":4.6,"place":"6 km NNE of Sámi, Greece","time":1792195070544,"updated":1792266596073,"tz":null,"url":"https://earthquake.usgs.gov/earthquakes/eventpage/us6000u2ww","detail":"https://earthquake.usgs.gov/earthquakes/feed/v1.0/detail/us6000u2ww.geojson","felt":6,"cdi":4.1,"mmi":null,"alert":null,"status":"reviewed","tsunami":0,"sig":328,"net":"us","code":"6000u2ww","ids":",us6000u2ww,","sources":",us,","types":",dyfi,moment-tensor,origin,phase-data,","nst":67,"dmin":1.476,"rms":0.97,"gap":85,"magType":"mb","type":"earthquake","title":"M 4.6 - 6 km NNE of Sámi, Greece"},"geometry":{"type":"Point","coordinates":[20.6863,38.305,10]},"id":"us6000u2ww"},{"type":"Feature","properties":{"mag":6.9,"place":"76 km NE of Ozernovskiy, Russia","time":1792121731925,"updated":1792208509338,"tz":null,"url":"https://earthquake.usgs.gov/earthquakes/eventpage/us6000u2mq","detail":"https://earthquake.usgs.gov/earthquakes/feed/v1.0/detail/us6000u2mq.geojson","felt":2,"cdi":5.3,"mmi":5.631,"alert":"green","status":"reviewed","tsunami":0,"sig":734,"net":"us","code":"6000u2mq","ids":",us6000u2mq,","sources":",us,","types":",dyfi,ground-failure,losspager,moment-tensor,origin,phase-data,shakemap,","nst":188,"dmin":1.155,"rms":0.8,"gap":22,"magType":"mww","type":"earthquake","title":"M 6.9 - 76 km NE of Ozernovskiy, Russia"},"geometry":{"type":"Point","coordinates":[157.2701,51.9848,135]},"id":"us6000u2mq"},{"type":"Feature","properties":{"mag":6.8,"place":"central Mid-Atlantic Ridge","time":1791894187248,"updated":1791905973040,"tz":null,"url":"https://earthquake.usgs.gov/earthquakes/eventpage/us6000u214","detail":"https://earthquake.usgs.gov/earthquakes/feed/v1.0/detail/us6000u214.geojson","felt":null,"cdi":null,"mmi":0,"alert":"green","status":"reviewed","tsunami":1,"sig":711,"net":"us","code":"6000u214","ids":",attmuiel,us6000u214,pt26286001,","sources":",at,us,pt,","types":",impact-link,losspager,moment-tensor,origin,phase-data,shakemap,","nst":98,"dmin":9.022,"rms":0.77,"gap":74,"magType":"mww","type":"earthquake","title":"M 6.8 - central Mid-Atlantic Ridge"},"geometry":{"type":"Point","coordinates":[-19.4308,-0.4085,10]},"id":"us6000u214"},{"type":"Feature","properties":{"mag":1.87,"place":"5 km NW of Boron, CA","time":1792366257250,"updated":1792423605778,"tz":null,"url":"https://earthquake.usgs.gov/earthquakes/eventpage/ci41349135","detail":"https://earthquake.usgs.gov/earthquakes/feed/v1.0/detail/ci41349135.geojson","felt":null,"cdi":null,"mmi":null,"alert":null,"status":"reviewed","tsunami":0,"sig":54,"net":"ci","code":"41349135","ids":",ci41349135,","sources":",ci,","types":",nearby-cities,origin,phase-data,scitech-link,","nst":47,"dmin":0.1025,"rms":0.15,"gap":39,"magType":"ml","type":"quarry blast","title":"M 1.9 Quarry Blast - 5 km NW of Boron, CA"},"geometry":{"type":"Point","coordinates":[-117.684833333333,35.0366666666667,-0.82]},"id":"ci41349135"}],"bbox":[-179.9252,-58.1423,-2.51,179.8713,67.374,599.104]}
        """;

    /// <summary>A feed of these events, in the feed's own shape; <c>ids</c> as the feed writes them, the id alone where none is given.</summary>
    public static string Feed(params (string Id, double Magnitude, double Latitude, double Longitude, DateTime Utc, string? Ids)[] events)
    {
        var features = new StringBuilder();
        foreach (var (id, magnitude, latitude, longitude, utc, ids) in events)
        {
            var ms = (long)(utc - DateTime.UnixEpoch).TotalMilliseconds;
            var mag = magnitude.ToString(CultureInfo.InvariantCulture);
            var lat = latitude.ToString(CultureInfo.InvariantCulture);
            var lon = longitude.ToString(CultureInfo.InvariantCulture);
            var aliases = ids ?? "," + id + ",";
            features.Append(features.Length == 0 ? "" : ",");
            features.Append($$"""{"type":"Feature","properties":{"mag":{{mag}},"place":"Test place {{id}}","time":{{ms}},"url":"https://earthquake.usgs.gov/earthquakes/eventpage/{{id}}","ids":"{{aliases}}","type":"earthquake"},"geometry":{"type":"Point","coordinates":[{{lon}},{{lat}},10]},"id":"{{id}}"}""");
        }
        return $$"""{"type":"FeatureCollection","metadata":{"count":{{events.Length}}},"features":[{{features}}]}""";
    }
}
