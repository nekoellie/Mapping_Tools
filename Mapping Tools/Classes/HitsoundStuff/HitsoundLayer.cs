﻿
using Mapping_Tools.Classes.BeatmapHelper;
using Mapping_Tools.Classes.MathUtil;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace Mapping_Tools.Classes.HitsoundStuff {
    public class HitsoundLayer : INotifyPropertyChanged {
        private string name;
        public string Name {
            get { return name; }
            set {
                if (name != value) {
                    name = value;
                    NotifyPropertyChanged("Name");
                }
            }
        }

        private SampleSet sampleSet;
        public SampleSet SampleSet {
            get { return sampleSet; }
            set {
                if (sampleSet != value) {
                    sampleSet = value;
                    NotifyPropertyChanged("SampleSet");
                }
            }
        }

        private Hitsound hitsound;
        public Hitsound Hitsound {
            get { return hitsound; }
            set {
                if (hitsound != value) {
                    hitsound = value;
                    NotifyPropertyChanged("Hitsound");
                }
            }
        }

        private int priority;
        public int Priority {
            get { return priority; }
            set {
                if (priority != value) {
                    priority = value;
                    NotifyPropertyChanged("Priority");
                }
            }
        }

        private LayerImportArgs importArgs;
        public LayerImportArgs ImportArgs {
            get { return importArgs; }
            set {
                if (importArgs != value) {
                    importArgs = value;
                    NotifyPropertyChanged("ImportArgs");
                }
            }
        }

        private SampleGeneratingArgs sampleArgs;
        public SampleGeneratingArgs SampleArgs {
            get { return sampleArgs; }
            set {
                if (sampleArgs != value) {
                    sampleArgs = value;
                    NotifyPropertyChanged("SampleArgs");
                }
            }
        }

        private List<double> times;
        public List<double> Times {
            get { return times; }
            set {
                if (times != value) {
                    times = value;
                    NotifyPropertyChanged("Times");
                }
            }
        }

        public string SampleSetString { get => GetSampleSetString(); set => SetSampleSetString(value); }

        private void SetSampleSetString(string value) {
            SampleSet =  (SampleSet)Enum.Parse(typeof(SampleSet), value);
            NotifyPropertyChanged("SampleSetString");
        }

        private string GetSampleSetString() {
            return SampleSet.ToString();
        }

        public string HitsoundString { get => GetHitsoundString(); set => SetHitsoundString(value); }

        private void SetHitsoundString(string value) {
            Hitsound = (Hitsound)Enum.Parse(typeof(Hitsound), value);
            NotifyPropertyChanged("HitsoundString");
        }

        private string GetHitsoundString() {
            return Hitsound.ToString();
        }

        public int SampleSetComboBoxIndex { get => GetSampleSetComboBoxIndex(); set => SetSampleSetComboBoxIndex(value); }

        private void SetSampleSetComboBoxIndex(int value) {
            SampleSet = (SampleSet)(value + 1);
        }

        private int GetSampleSetComboBoxIndex() {
            return (int)SampleSet - 1;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void NotifyPropertyChanged(string propName) {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propName));
        }

        public HitsoundLayer() {
            Name = "";
            SampleSet = SampleSet.Normal;
            Hitsound = Hitsound.Normal;
            importArgs = new LayerImportArgs();
            sampleArgs = new SampleGeneratingArgs();
            Times = new List<double>();
        }

        public HitsoundLayer(string name, ImportType importType, SampleSet sampleSet, Hitsound hitsound, string samplePath) {
            Name = name;
            SampleSet = sampleSet;
            Hitsound = hitsound;
            importArgs = new LayerImportArgs(importType);
            sampleArgs = new SampleGeneratingArgs(samplePath);
            Times = new List<double>();
        }

        public HitsoundLayer(string name, ImportType importType, SampleSet sampleSet, Hitsound hitsound, SampleGeneratingArgs sampleArgs) {
            Name = name;
            SampleSet = sampleSet;
            Hitsound = hitsound;
            importArgs = new LayerImportArgs(importType);
            this.sampleArgs = sampleArgs;
            Times = new List<double>();
        }

        public HitsoundLayer(string name, SampleSet sampleSet, Hitsound hitsound, SampleGeneratingArgs sampleArgs, LayerImportArgs importArgs) {
            Name = name;
            SampleSet = sampleSet;
            Hitsound = hitsound;
            this.importArgs = importArgs;
            this.sampleArgs = sampleArgs;
            Times = new List<double>();
        }

        public void SetPriority(int priority) {
            Priority = priority;
        }

        public void Reload(List<HitsoundLayer> layers) {
            List<HitsoundLayer> sameLayer = layers.FindAll(o => ImportArgs.ReloadCompatible(o.ImportArgs));

            Times.Clear();
            foreach (HitsoundLayer hsl in sameLayer) {
                Times.AddRange(hsl.Times);
            }
            Times.OrderBy(o => o);
            NotifyPropertyChanged("Times");
        }
    }
}
