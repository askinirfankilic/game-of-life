using System;
using System.IO;
using System.Linq;

public static class PatternLoader {
    private const string PATTERN_DIR = "Assets/Data";

    public static int[,] Load(string name, int width, int height) {
        var path = Path.Combine(PATTERN_DIR, name + ".txt");
        var text = ReadText(path);
        return LoadPatternFromText(text, width, height);
    }

    private static string ReadText(string path) {
        try {
            return File.ReadAllText(path);
        }
        catch (Exception e) {
            throw new Exception($"Failed to read pattern file: {path}", e);
        }
    }

    private static int[,] LoadPatternFromText(string text, int width, int height) {
        var lines = text.Split('\n')
            .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("!"))
            .ToArray();

        if (lines.Length == 0)
            return new int[0, 0];

        var patternHeight = lines.Length;
        var patternWidth = lines[0].Length;
        if (patternHeight > height || patternWidth > width) {
            throw new Exception($"Pattern is too large. Max height: {height}, max width: {width}");
        }

        var pattern = new int[height, width];

        for (int y = 0; y < height; y++) {
            var line = y < lines.Length ? lines[y] : "";
            for (int x = 0; x < width; x++) {
                pattern[y, x] = (x < line.Length && line[x] == '*') ? 1 : 0;
            }
        }

        return pattern;
    }
}