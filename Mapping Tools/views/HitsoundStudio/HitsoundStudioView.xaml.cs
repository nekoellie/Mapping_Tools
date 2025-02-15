﻿using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Mapping_Tools.Classes.HitsoundStuff;
using Mapping_Tools.Classes.SystemTools;
using Mapping_Tools.Viewmodels;
using NAudio.Wave;
using NAudio.Vorbis;
using System.Text;
using System.Globalization;

namespace Mapping_Tools.Views {
    /// <summary>
    /// Interactielogica voor HitsoundCopierView.xaml
    /// </summary>
    public partial class HitsoundStudioView : UserControl, ISavable<HitsoundStudioVM> {
        private readonly BackgroundWorker backgroundWorker;
        private HitsoundStudioVM Settings;

        private bool suppressEvents = false;

        private List<HitsoundLayer> selectedLayers;
        private HitsoundLayer selectedLayer;

        public string AutoSavePath => Path.Combine(MainWindow.AppDataPath, "hsstudioproject.json");

        public string DefaultSaveFolder => Path.Combine(MainWindow.AppDataPath, "Hitsound Studio Projects");

        public HitsoundStudioView() {
            InitializeComponent();
            Width = MainWindow.AppWindow.content_views.Width;
            Height = MainWindow.AppWindow.content_views.Height;
            backgroundWorker = (BackgroundWorker) FindResource("backgroundWorker");
            Settings = new HitsoundStudioVM();
            DataContext = Settings;
            LayersList.SelectedIndex = 0;
            Num_Layers_Changed();
            GetSelectedLayers();
            ProjectManager.LoadProject(this, message: false);
        }

        private void BackgroundWorker_DoWork(object sender, DoWorkEventArgs e) {
            var bgw = sender as BackgroundWorker;
            Make_Hitsounds((Arguments) e.Argument, bgw, e);
        }

        private void BackgroundWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e) {
            if( e.Error != null ) {
                MessageBox.Show(string.Format("{0}{1}{2}", e.Error.Message, Environment.NewLine, e.Error.StackTrace), "Error");
            }
            else {
                progress.Value = 0;
            }
            start.IsEnabled = true;
            startish.IsEnabled = true;
        }

        private void BackgroundWorker_ProgressChanged(object sender, ProgressChangedEventArgs e) {
            progress.Value = e.ProgressPercentage;
        }

        private struct Arguments {
            public string ExportFolder;
            public string BaseBeatmap;
            public Sample DefaultSample;
            public List<HitsoundLayer> HitsoundLayers;
            public bool Debug;
            public Arguments(string exportFolder, string baseBeatmap, Sample defaultSample, List<HitsoundLayer> hitsoundLayers, bool debug) {
                ExportFolder = exportFolder;
                BaseBeatmap = baseBeatmap;
                DefaultSample = defaultSample;
                HitsoundLayers = hitsoundLayers;
                Debug = debug;
            }
        }

        private void Make_Hitsounds(Arguments arg, BackgroundWorker worker, DoWorkEventArgs _) {
            if (arg.Debug) {
                // Convert the multiple layers into packages that have the samples from all the layers at one specific time
                List<SamplePackage> samplePackages = HitsoundConverter.ZipLayers(arg.HitsoundLayers, arg.DefaultSample);
                UpdateProgressBar(worker, 20);

                // Convert the packages to hitsounds that fit on an osu standard map
                CompleteHitsounds completeHitsounds = HitsoundConverter.GetCompleteHitsounds(samplePackages);
                UpdateProgressBar(worker, 40);

                int samples = 0;
                foreach (CustomIndex ci in completeHitsounds.CustomIndices) {
                    foreach (HashSet<SampleGeneratingArgs> h in ci.Samples.Values) {
                        if (h.Any(o => SampleImporter.ValidateSampleArgs(o))) {
                            samples++;
                        }
                    }
                }
                UpdateProgressBar(worker, 60);

                int greenlines = 0;
                int lastIndex = -1;
                foreach (HitsoundEvent hit in completeHitsounds.Hitsounds) {
                    if (hit.CustomIndex != lastIndex) {
                        lastIndex = hit.CustomIndex;
                        greenlines++;
                    }
                }
                UpdateProgressBar(worker, 100);

                MessageBox.Show(string.Format("Number of sample indices: {0}, Number of samples: {1}, Number of greenlines: {2}", completeHitsounds.CustomIndices.Count, samples, greenlines));
            } 
            else {
                var allSampleArgs = arg.HitsoundLayers.Select(o => o.SampleArgs).Concat(new SampleGeneratingArgs[] { arg.DefaultSample.SampleArgs });
                var loadedSamples = SampleImporter.ImportSamples(allSampleArgs);
                UpdateProgressBar(worker, 20);

                // Convert the multiple layers into packages that have the samples from all the layers at one specific time
                List<SamplePackage> samplePackages = HitsoundConverter.ZipLayers(arg.HitsoundLayers, arg.DefaultSample);
                UpdateProgressBar(worker, 40);

                // Convert the packages to hitsounds that fit on an osu standard map
                CompleteHitsounds completeHitsounds = HitsoundConverter.GetCompleteHitsounds(samplePackages, loadedSamples);
                UpdateProgressBar(worker, 60);

                // Delete all files in the export folder before filling it again
                DirectoryInfo di = new DirectoryInfo(arg.ExportFolder);
                foreach (FileInfo file in di.GetFiles()) {
                    file.Delete();
                }
                UpdateProgressBar(worker, 80);

                // Export the hitsound .osu and sound samples
                HitsoundExporter.ExportCompleteHitsounds(arg.ExportFolder, arg.BaseBeatmap, completeHitsounds, loadedSamples);
                UpdateProgressBar(worker, 99);

                // Open export folder
                Process.Start(arg.ExportFolder);
            }

            UpdateProgressBar(worker, 100);
        }

        private void UpdateProgressBar(BackgroundWorker worker, int progress) {
            if (worker != null && worker.WorkerReportsProgress) {
                worker.ReportProgress(progress);
            }
        }

        private void Startish_Click(object sender, RoutedEventArgs e) {
            backgroundWorker.RunWorkerAsync(new Arguments(MainWindow.ExportPath, Settings.BaseBeatmap, Settings.DefaultSample, Settings.HitsoundLayers.ToList(), true));
            startish.IsEnabled = false;
        }

        private void Start_Click(object sender, RoutedEventArgs e) {
            if (Settings.BaseBeatmap == null || Settings.DefaultSample == null) {
                MessageBox.Show("Please import a base beatmap and default hitsound first.");
                return;
            }
            backgroundWorker.RunWorkerAsync(new Arguments(MainWindow.ExportPath, Settings.BaseBeatmap, Settings.DefaultSample, Settings.HitsoundLayers.ToList(), false));
            start.IsEnabled = false;
        }

        private void GetSelectedLayers() {
            selectedLayers = new List<HitsoundLayer>();

            if (LayersList.SelectedItems.Count == 0) {
                selectedLayer = null;
                return;
            }

            foreach (HitsoundLayer hsl in LayersList.SelectedItems) {
                selectedLayers.Add(hsl);
            }

            selectedLayer = selectedLayers[0];
        }

        private void SelectedSamplePathBrowse_Click(object sender, RoutedEventArgs e) {
            try {
                string path = IOHelper.SampleFileDialog();
                if (path != "") {
                    SelectedSamplePathBox.Text = path;
                }
            } catch (Exception) { }
        }

        private void SelectedImportSamplePathBrowse_Click(object sender, RoutedEventArgs e) {
            try {
                string path = IOHelper.SampleFileDialog();
                if (path != "") {
                    SelectedImportSamplePathBox.Text = path;
                }
            } catch (Exception) { }
        }

        private void SelectedImportPathBrowse_Click(object sender, RoutedEventArgs e) {
            try {
                string[] paths = IOHelper.BeatmapFileDialog();
                if (paths.Length != 0) {
                    SelectedImportPathBox.Text = paths[0];
                    }
            } catch (Exception) { }
        }

        private void SelectedImportPathLoad_Click(object sender, RoutedEventArgs e) {
            try {
                string path = IOHelper.CurrentBeatmap();
                if (path != "") {
                    SelectedImportPathBox.Text = path;
                }
            } catch (Exception) { }
        }

        private void DefaultSampleBrowse_Click(object sender, RoutedEventArgs e) {
            try {
                string path = IOHelper.SampleFileDialog();
                if (path != "") {
                    Settings.DefaultSample.SampleArgs.Path = path;
                    DefaultSamplePathBox.Text = path;
                    }
            } catch (Exception) { }
        }

        private void BaseBeatmapBrowse_Click(object sender, RoutedEventArgs e) {
            try {
                string[] paths = IOHelper.BeatmapFileDialog();
                if (paths.Length != 0) {
                    Settings.BaseBeatmap = paths[0];
                    }
            } catch (Exception) { }
        }

        private void BaseBeatmapLoad_Click(object sender, RoutedEventArgs e) {
            try {
                string path = IOHelper.CurrentBeatmap();
                if (path != "") {
                    Settings.BaseBeatmap = path;
                    }
            } catch (Exception) { }
        }

        private void ReloadFromSource_Click(object sender, RoutedEventArgs e) {
            try {
                var seperatedByImportArgsForReloading = new Dictionary<ImportReloadingArgs, List<HitsoundLayer>>(new ImportReloadingArgsComparer());

                foreach (var layer in selectedLayers) {
                    var reloadingArgs = layer.ImportArgs.GetImportReloadingArgs();
                    if (seperatedByImportArgsForReloading.TryGetValue(reloadingArgs, out List<HitsoundLayer> value)) {
                        value.Add(layer);
                    } else {
                        seperatedByImportArgsForReloading.Add(reloadingArgs, new List<HitsoundLayer>() { layer });
                    }
                }

                foreach (var pair in seperatedByImportArgsForReloading) {
                    var reloadingArgs = pair.Key;
                    var layers = pair.Value;

                    var importedLayers = HitsoundImporter.ImportReloading(reloadingArgs);

                    layers.ForEach(o => o.Reload(importedLayers));
                }
            } catch (Exception ex) { Console.WriteLine(ex.Message); Console.WriteLine(ex.StackTrace); }
        }

        private void LayersList_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (suppressEvents) return;

            GetSelectedLayers();
            UpdateEditingField();
        }

        private void UpdateEditingField() {
            if (selectedLayers.Count == 0) { return; }

            suppressEvents = true;

            // Populate the editing fields
            SelectedNameBox.Text = selectedLayers.AllToStringOrDefault(o => o.Name);
            SelectedSampleSetBox.Text = selectedLayers.AllToStringOrDefault(o => o.SampleSetString);
            SelectedHitsoundBox.Text = selectedLayers.AllToStringOrDefault(o => o.HitsoundString);
            TimesBox.Text = selectedLayers.AllToStringOrDefault(o => o.Times, HitsoundLayerExtension.DoubleListToStringConverter);

            SelectedSamplePathBox.Text = selectedLayers.AllToStringOrDefault(o => o.SampleArgs.Path);
            SelectedSampleBankBox.Text = selectedLayers.AllToStringOrDefault(o => o.SampleArgs.Bank);
            SelectedSamplePatchBox.Text = selectedLayers.AllToStringOrDefault(o => o.SampleArgs.Patch);
            SelectedSampleInstrumentBox.Text = selectedLayers.AllToStringOrDefault(o => o.SampleArgs.Instrument);
            SelectedSampleKeyBox.Text = selectedLayers.AllToStringOrDefault(o => o.SampleArgs.Key);
            SelectedSampleLengthBox.Text = selectedLayers.AllToStringOrDefault(o => o.SampleArgs.Length, CultureInfo.InvariantCulture);
            SelectedSampleVelocityBox.Text = selectedLayers.AllToStringOrDefault(o => o.SampleArgs.Velocity);

            SelectedImportTypeBox.Text = selectedLayers.AllToStringOrDefault(o => o.ImportArgs.ImportType);
            SelectedImportPathBox.Text = selectedLayers.AllToStringOrDefault(o => o.ImportArgs.Path);
            SelectedImportXCoordBox.Text = selectedLayers.AllToStringOrDefault(o => o.ImportArgs.X, CultureInfo.InvariantCulture);
            SelectedImportYCoordBox.Text = selectedLayers.AllToStringOrDefault(o => o.ImportArgs.Y, CultureInfo.InvariantCulture);
            SelectedImportSamplePathBox.Text = selectedLayers.AllToStringOrDefault(o => o.ImportArgs.SamplePath);
            SelectedImportBankBox.Text = selectedLayers.AllToStringOrDefault(o => o.ImportArgs.Bank);
            SelectedImportPatchBox.Text = selectedLayers.AllToStringOrDefault(o => o.ImportArgs.Patch);
            SelectedImportKeyBox.Text = selectedLayers.AllToStringOrDefault(o => o.ImportArgs.Key);
            SelectedImportLengthBox.Text = selectedLayers.AllToStringOrDefault(o => o.ImportArgs.Length, CultureInfo.InvariantCulture);
            SelectedImportLengthRoughnessBox.Text = selectedLayers.AllToStringOrDefault(o => o.ImportArgs.LengthRoughness, CultureInfo.InvariantCulture);
            SelectedImportVelocityBox.Text = selectedLayers.AllToStringOrDefault(o => o.ImportArgs.Velocity);
            SelectedImportVelocityRoughnessBox.Text = selectedLayers.AllToStringOrDefault(o => o.ImportArgs.VelocityRoughness, CultureInfo.InvariantCulture);


            // Update visibility
            if (selectedLayers.Any(o => o.SampleArgs.UsesSoundFont)) {
                SoundFontArgsPanel.Visibility = Visibility.Visible;
            } else {
                SoundFontArgsPanel.Visibility = Visibility.Collapsed;
            }
            if (selectedLayers.Any(o => o.ImportArgs.ImportType == ImportType.Stack)) {
                SelectedStackPanel.Visibility = Visibility.Visible;
            } else {
                SelectedStackPanel.Visibility = Visibility.Collapsed;
            }
            if (selectedLayers.Any(o => o.ImportArgs.ImportType == ImportType.Hitsounds)) {
                SelectedHitsoundsPanel.Visibility = Visibility.Visible;
            } else {
                SelectedHitsoundsPanel.Visibility = Visibility.Collapsed;
            }
            if (selectedLayers.Any(o => o.ImportArgs.ImportType == ImportType.MIDI)) {
                SelectedMIDIPanel.Visibility = Visibility.Visible;
            } else {
                SelectedMIDIPanel.Visibility = Visibility.Collapsed;
            }
            if (selectedLayers.Any(o => o.ImportArgs.CanImport)) {
                ImportArgsPanel.Visibility = Visibility.Visible;
            } else {
                ImportArgsPanel.Visibility = Visibility.Collapsed;
            }

            suppressEvents = false;
        }

        void HitsoundLayer_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
            try {
                SampleGeneratingArgs args = selectedLayer.SampleArgs;
                var mainOutputStream = SampleImporter.ImportSample(args);

                if (mainOutputStream == null) {
                    MessageBox.Show("Could not load the specified sample.");
                    return;
                }

                WaveOutEvent player = new WaveOutEvent();

                player.Init(mainOutputStream.GetSampleProvider());
                player.PlaybackStopped += PlayerStopped;

                player.Play();
            }
            catch (FileNotFoundException) { MessageBox.Show("Could not find the specified sample."); }
            catch (DirectoryNotFoundException) { MessageBox.Show("Could not find the specified sample's directory."); }
            catch (Exception ex) { Console.WriteLine(ex.Message); Console.WriteLine(ex.StackTrace); }
        }

        void PlayerStopped(object sender, StoppedEventArgs e) {
            ((WaveOutEvent)sender).Dispose();
            GC.Collect();
        }

        private void Num_Layers_Changed() {
            if (Settings.HitsoundLayers.Count == 0) {
                FirstGrid.ColumnDefinitions[0].Width = new GridLength(0);
                EditPanel.IsEnabled = false;
            } else if (FirstGrid.ColumnDefinitions[0].Width.Value < 100) {
                FirstGrid.ColumnDefinitions[0].Width = new GridLength(1, GridUnitType.Star);
                FirstGrid.ColumnDefinitions[2].Width = new GridLength(2, GridUnitType.Star);
                EditPanel.IsEnabled = true;
            }
        }

        private void Add_Click(object sender, RoutedEventArgs e) {
            try {
                HitsoundLayerImportWindow importWindow = new HitsoundLayerImportWindow(Settings.HitsoundLayers.Count);
                importWindow.ShowDialog();

                LayersList.SelectedItems.Clear();
                foreach (HitsoundLayer layer in importWindow.HitsoundLayers) {
                    if (layer != null) {
                        Settings.HitsoundLayers.Add(layer);
                        LayersList.SelectedItems.Add(layer);
                    }
                }
                
                RecalculatePriorities();
                Num_Layers_Changed();
                GetSelectedLayers();
            } catch (Exception ex) {
                MessageBox.Show(ex.Message);
            }
        }

        private void Delete_Click(object sender, RoutedEventArgs e) {
            try {
                // Ask for confirmation
                MessageBoxResult messageBoxResult = MessageBox.Show("Are you sure?", "Confirm deletion", MessageBoxButton.YesNo);
                if (messageBoxResult != MessageBoxResult.Yes) { return; }

                if (selectedLayers.Count == 0 || selectedLayers == null) { return; }

                suppressEvents = true;

                int index = Settings.HitsoundLayers.IndexOf(selectedLayer);

                foreach (HitsoundLayer hsl in selectedLayers) {
                    Settings.HitsoundLayers.Remove(hsl);
                }
                suppressEvents = false;

                LayersList.SelectedIndex = Math.Max(Math.Min(index - 1, Settings.HitsoundLayers.Count - 1), 0);

                RecalculatePriorities();
                Num_Layers_Changed();

            } catch (Exception ex) { Console.WriteLine(ex.Message); Console.WriteLine(ex.StackTrace); }
        }

        private void Raise_Click(object sender, RoutedEventArgs e) {
            try {
                suppressEvents = true;

                int selectedIndex = Settings.HitsoundLayers.IndexOf(selectedLayer);
                List<HitsoundLayer> moveList = new List<HitsoundLayer>();
                foreach (HitsoundLayer hsl in selectedLayers) {
                    moveList.Add(hsl);
                }

                foreach (HitsoundLayer hsl in Settings.HitsoundLayers) {
                    if (moveList.Contains(hsl)) {
                        moveList.Remove(hsl);
                    }
                    else
                        break;
                }

                foreach (HitsoundLayer hsl in moveList) {
                    int index = Settings.HitsoundLayers.IndexOf(hsl);

                    //Dont move left if it is the first item in the list or it is not in the list
                    if (index <= 0)
                        continue;

                    //Swap with this item with the one to its left
                    Settings.HitsoundLayers.Remove(hsl);
                    Settings.HitsoundLayers.Insert(index - 1, hsl);
                }

                LayersList.SelectedItems.Clear();
                foreach (HitsoundLayer hsl in selectedLayers) {
                    LayersList.SelectedItems.Add(hsl);
                }

                suppressEvents = false;

                RecalculatePriorities();
                GetSelectedLayers();
            } catch (Exception ex) { Console.WriteLine(ex.Message); Console.WriteLine(ex.StackTrace); }
        }

        private void Lower_Click(object sender, RoutedEventArgs e) {
            try {
                suppressEvents = true;

                int selectedIndex = Settings.HitsoundLayers.IndexOf(selectedLayer);
                List<HitsoundLayer> moveList = new List<HitsoundLayer>();
                foreach (HitsoundLayer hsl in selectedLayers) {
                    moveList.Add(hsl);
                }

                for (int i = Settings.HitsoundLayers.Count - 1; i >= 0; i--) {
                    HitsoundLayer hsl = Settings.HitsoundLayers[i];
                    if (moveList.Contains(hsl)) {
                        moveList.Remove(hsl);
                    }
                    else
                        break;
                }

                for (int i = moveList.Count - 1; i >= 0; i--) {
                    HitsoundLayer hsl = moveList[i];
                    int index = Settings.HitsoundLayers.IndexOf(hsl);

                    //Dont move left if it is the first item in the list or it is not in the list
                    if (index >= Settings.HitsoundLayers.Count - 1)
                        continue;

                    //Swap with this item with the one to its left
                    Settings.HitsoundLayers.Remove(hsl);
                    Settings.HitsoundLayers.Insert(index + 1, hsl);
                }

                LayersList.SelectedItems.Clear();
                foreach (HitsoundLayer hsl in selectedLayers) {
                    LayersList.SelectedItems.Add(hsl);
                }

                suppressEvents = false;

                RecalculatePriorities();
                GetSelectedLayers();
            } catch (Exception ex) { Console.WriteLine(ex.Message); Console.WriteLine(ex.StackTrace); }
        }

        private void RecalculatePriorities() {
            for (int i = 0; i < Settings.HitsoundLayers.Count; i++) {
                Settings.HitsoundLayers[i].SetPriority(i);
            }
        }

        private void SelectedNameBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (suppressEvents) return;

            string t = (sender as TextBox).Text;
            foreach (HitsoundLayer hitsoundLayer in selectedLayers) {
                hitsoundLayer.Name = t;
            }
        }

        private void SelectedSampleSetBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (suppressEvents) return;

            string t = ((sender as ComboBox).SelectedItem as ComboBoxItem).Content.ToString();
            foreach (HitsoundLayer hitsoundLayer in selectedLayers) {
                hitsoundLayer.SampleSetString = t;
            }
        }

        private void SelectedHitsoundBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (suppressEvents) return;

            string t = ((sender as ComboBox).SelectedItem as ComboBoxItem).Content.ToString();
            foreach (HitsoundLayer hitsoundLayer in selectedLayers) {
                hitsoundLayer.HitsoundString = t;
            }
        }

        private void TimesBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (suppressEvents) return;
            if ((sender as TextBox).GetBindingExpression(TextBox.TextProperty).HasValidationError) return;

            try {
                List<double> t = (sender as TextBox).Text.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Select(o => double.Parse(o)).OrderBy(o => o).ToList();

                foreach (HitsoundLayer hitsoundLayer in selectedLayers) {
                    hitsoundLayer.Times = t;
                }
            } catch (Exception ex) { Console.WriteLine(ex.Message); Console.WriteLine(ex.StackTrace); }
        }

        private void SelectedSamplePathBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (suppressEvents) return;

            string t = (sender as TextBox).Text;
            foreach (HitsoundLayer hitsoundLayer in selectedLayers) {
                hitsoundLayer.SampleArgs.Path = t;
            }
            UpdateEditingField();
        }

        private void SelectedSampleBankBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (suppressEvents) return;

            int t = (sender as TextBox).GetInt(-1);
            foreach (HitsoundLayer hitsoundLayer in selectedLayers) {
                hitsoundLayer.SampleArgs.Bank = t;
            }
        }

        private void SelectedSamplePatchBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (suppressEvents) return;

            int t = (sender as TextBox).GetInt(-1);
            foreach (HitsoundLayer hitsoundLayer in selectedLayers) {
                hitsoundLayer.SampleArgs.Patch = t;
            }
        }

        private void SelectedSampleInstrumentBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (suppressEvents) return;

            int t = (sender as TextBox).GetInt(-1);
            foreach (HitsoundLayer hitsoundLayer in selectedLayers) {
                hitsoundLayer.SampleArgs.Instrument = t;
            }
        }

        private void SelectedSampleKeyBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (suppressEvents) return;

            int t = (sender as TextBox).GetInt(-1);
            foreach (HitsoundLayer hitsoundLayer in selectedLayers) {
                hitsoundLayer.SampleArgs.Key = t;
            }
        }

        private void SelectedSampleLengthBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (suppressEvents) return;

            double t = (sender as TextBox).GetDouble(-1);
            foreach (HitsoundLayer hitsoundLayer in selectedLayers) {
                hitsoundLayer.SampleArgs.Length = t;
            }
        }

        private void SelectedSampleVelocityBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (suppressEvents) return;

            int t = (sender as TextBox).GetInt(-1);
            foreach (HitsoundLayer hitsoundLayer in selectedLayers) {
                hitsoundLayer.SampleArgs.Velocity = t;
            }
        }

        private void SelectedImportTypeBox_SelectionChanged(object sender, SelectionChangedEventArgs e) {
            if (suppressEvents) return;

            string t = ((sender as ComboBox).SelectedItem as ComboBoxItem).Content.ToString();
            ImportType type = (ImportType)Enum.Parse(typeof(ImportType), t);
            foreach (HitsoundLayer hitsoundLayer in selectedLayers) {
                hitsoundLayer.ImportArgs.ImportType = type;
            }
            UpdateEditingField();
        }

        private void SelectedImportPathBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (suppressEvents) return;

            string t = (sender as TextBox).Text;
            foreach (HitsoundLayer hitsoundLayer in selectedLayers) {
                hitsoundLayer.ImportArgs.Path = t;
            }
        }

        private void SelectedImportXCoordBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (suppressEvents) return;

            double t = (sender as TextBox).GetDouble(-1);
            foreach (HitsoundLayer hitsoundLayer in selectedLayers) {
                hitsoundLayer.ImportArgs.X = t;
            }
        }

        private void SelectedImportYCoordBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (suppressEvents) return;

            double t = (sender as TextBox).GetDouble(-1);
            foreach (HitsoundLayer hitsoundLayer in selectedLayers) {
                hitsoundLayer.ImportArgs.Y = t;
            }
        }

        private void SelectedImportSamplePathBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (suppressEvents) return;

            string t = (sender as TextBox).Text;
            foreach (HitsoundLayer hitsoundLayer in selectedLayers) {
                hitsoundLayer.ImportArgs.SamplePath = t;
            }
        }

        private void SelectedImportBankBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (suppressEvents) return;

            int t = (sender as TextBox).GetInt(-1);
            foreach (HitsoundLayer hitsoundLayer in selectedLayers) {
                hitsoundLayer.ImportArgs.Bank = t;
            }
        }

        private void SelectedImportPatchBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (suppressEvents) return;

            int t = (sender as TextBox).GetInt(-1);
            foreach (HitsoundLayer hitsoundLayer in selectedLayers) {
                hitsoundLayer.ImportArgs.Patch = t;
            }
        }

        private void SelectedImportKeyBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (suppressEvents) return;

            int t = (sender as TextBox).GetInt(-1);
            foreach (HitsoundLayer hitsoundLayer in selectedLayers) {
                hitsoundLayer.ImportArgs.Key = t;
            }
        }

        private void SelectedImportLengthBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (suppressEvents) return;

            int t = (sender as TextBox).GetInt(-1);
            foreach (HitsoundLayer hitsoundLayer in selectedLayers) {
                hitsoundLayer.ImportArgs.Length = t;
            }
        }

        private void SelectedImportVelocityBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (suppressEvents) return;

            int t = (sender as TextBox).GetInt(-1);
            foreach (HitsoundLayer hitsoundLayer in selectedLayers) {
                hitsoundLayer.ImportArgs.Velocity = t;
            }
        }

        private void SelectedImportLengthRoughnessBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (suppressEvents) return;

            double t = (sender as TextBox).GetDouble(1);
            foreach (HitsoundLayer hitsoundLayer in selectedLayers) {
                hitsoundLayer.ImportArgs.LengthRoughness = t;
            }
        }

        private void SelectedImportVelocityRoughnessBox_TextChanged(object sender, TextChangedEventArgs e) {
            if (suppressEvents) return;

            double t = (sender as TextBox).GetDouble(1);
            foreach (HitsoundLayer hitsoundLayer in selectedLayers) {
                hitsoundLayer.ImportArgs.VelocityRoughness = t;
            }
        }

        public HitsoundStudioVM GetSaveData() {
            return Settings;
        }

        public void SetSaveData(HitsoundStudioVM saveData) {
            suppressEvents = true;

            Settings = saveData;
            DataContext = Settings;

            suppressEvents = false;

            LayersList.SelectedIndex = 0;
            Num_Layers_Changed();
        }
    }
}
