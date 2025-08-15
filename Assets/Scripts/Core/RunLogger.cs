using System;
using System.IO;
using System.Globalization;
using UnityEngine;

public sealed class RunLogger : IDisposable
{
    private StreamWriter writer;
    private StreamWriter eventsWriter;
    private int flushEvery;
    private int counter;
    private int eventLinesSinceFlush;
    public string LogPath { get; private set; }
    public string EventsPath { get; private set; }

    public void StartNew(string prefix, int seed, GridConfig cfg, int flushEvery = 60)
    {
        Close();

        string dir = Path.Combine(Application.persistentDataPath, "Logs");
        Directory.CreateDirectory(dir);

        string ts = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string fname = $"{prefix}_{ts}_seed{seed}_w{cfg.width}_h{cfg.height}.csv";
        LogPath = Path.Combine(dir, fname);

        writer = new StreamWriter(LogPath, append: false);
        this.flushEvery = Mathf.Max(1, flushEvery);
        counter = 0;

        writer.WriteLine("time,tick,agents");
        writer.Flush();

        // Close any previous events writer (if StartNew is called again)
        eventsWriter?.Flush();
        eventsWriter?.Dispose();
        eventsWriter = null;

        // Create sibling events CSV (one per run)
        var eventsDir = Path.GetDirectoryName(LogPath);
        var eventsBase = Path.GetFileNameWithoutExtension(LogPath);
        EventsPath = Path.Combine(eventsDir ?? ".", eventsBase + "_events.csv");
        eventsWriter = new StreamWriter(EventsPath, append: false);
        eventsWriter.WriteLine("time_iso,tick,event,cx,cy,radius,value");
        eventLinesSinceFlush = 0;
    }

    public void LogTick(int tick, int agents)
    {
        if (writer == null) return;
        writer.WriteLine($"{Time.time:F3},{tick},{agents}");
        counter++;
        if (counter % flushEvery == 0) writer.Flush();
    }

    public void LogEvent(int tick, string evt, int cx, int cy, int radius, float value)
    {
        if (eventsWriter == null) return;
        string t = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture); // ISO-8601 UTC
        eventsWriter.WriteLine(string.Format(CultureInfo.InvariantCulture,
            "{0},{1},{2},{3},{4},{5},{6}",
            t, tick, evt, cx, cy, radius, value));
        eventLinesSinceFlush++;
        if (eventLinesSinceFlush >= flushEvery)
        {
            eventsWriter.Flush();
            eventLinesSinceFlush = 0;
        }
    }

    public void Close()
    {
        if (writer != null)
        {
            writer.Flush();
            writer.Dispose();
            writer = null;
        }

        eventsWriter?.Flush();
        eventsWriter?.Dispose();
        eventsWriter = null;
        EventsPath = null;
        eventLinesSinceFlush = 0;
    }

    public void Dispose() => Close();
}
