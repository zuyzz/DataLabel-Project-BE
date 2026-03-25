using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

public static class ExportHelpers
{
    public static List<ConsensusObject>? ParseConsensusPayload(string payload)
    {
        try
        {
            using var doc = JsonDocument.Parse(payload);
            var root = doc.RootElement;

            if (!root.TryGetProperty("bboxes", out var bboxesElement) ||
                bboxesElement.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            var objects = new List<ConsensusObject>();

            foreach (var obj in bboxesElement.EnumerateArray())
            {
                objects.Add(new ConsensusObject
                {
                    Label = obj.GetProperty("Label").GetString() ?? "",
                    X = obj.GetProperty("X").GetDouble(),
                    Y = obj.GetProperty("Y").GetDouble(),
                    W = obj.GetProperty("Width").GetDouble(),
                    H = obj.GetProperty("Height").GetDouble()
                });
            }

            return objects;
        }
        catch (JsonException)
        {
            // Invalid JSON
            return null;
        }
        catch (Exception)
        {
            // Missing fields or wrong format
            return null;
        }
    }

    public static (int Width, int Height) ParseImageDimensions(string metadata)
    {
        try
        {
            using var doc = JsonDocument.Parse(metadata);
            var width = 0;
            var height = 0;

            if (doc.RootElement.TryGetProperty("width", out var w))
                width = w.GetInt32();
            if (doc.RootElement.TryGetProperty("height", out var h))
                height = h.GetInt32();

            return (width, height);
        }
        catch
        {
            return (0, 0);
        }
    }

    public static string ExtractFileName(string storageUri)
    {
        try
        {
            var uri = new Uri(storageUri);
            return Path.GetFileName(uri.LocalPath);
        }
        catch
        {
            return storageUri;
        }
    }
}

public class ConsensusObject
{
    public string Label { get; set; } = null!;
    public double X { get; set; }
    public double Y { get; set; }
    public double W { get; set; }
    public double H { get; set; }
}