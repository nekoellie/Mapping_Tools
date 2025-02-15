﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace Mapping_Tools.Classes.BeatmapHelper {
    class Editor {
        string BeatmapPath { get; set; }
        public Beatmap Beatmap { get; set; }

        public Editor(List<string> lines) {
            Beatmap = new Beatmap(lines);
        }

        public Editor(string path) {
            BeatmapPath = path;
            Beatmap = new Beatmap(ReadFile(BeatmapPath));
        }

        public List<string> ReadFile(string path) {
            // Get contents of the file
            var lines = File.ReadAllLines(path);
            return new List<string>(lines);
        }

        public void SaveFile(string path) {
            SaveFile(path, Beatmap.GetLines());
        }

        public void SaveFile(List<string> lines) {
            SaveFile(BeatmapPath, lines);
        }

        public void SaveFile() {
            SaveFile(BeatmapPath, Beatmap.GetLines());
        }

        public static void SaveFile(string path, List<string> lines) {
            if (!File.Exists(path)) {
                File.Create(path).Dispose();
            }

            File.WriteAllLines(path, lines);
        }

        public string GetBeatmapFolder() {
            return Directory.GetParent(BeatmapPath).FullName;
        }

        public static string GetBeatmapFolder(string path)
        {
            return Directory.GetParent(path).FullName;
        }
    }
}
