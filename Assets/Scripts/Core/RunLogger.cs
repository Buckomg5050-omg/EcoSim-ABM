using System;
using System.IO;
using UnityEngine;

public sealed class RunLogger : IDisposable
{
    private StreamWriter writer;
    private int flushEvery;
    private int counter;
    public string LogPath { get; private set; }

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
    }

    public void LogTick(int tick, int agents)
    {
        if (writer == null) return;
        writer.WriteLine($"{Time.time:F3},{tick},{agents}");
        counter++;
        if (counter % flushEvery == 0) writer.Flush();
    }

    public void Close()
    {
        if (writer != null)
        {
            writer.Flush();
            writer.Dispose();
            writer = null;
        }
    }

    public void Dispose() => Close();
}
