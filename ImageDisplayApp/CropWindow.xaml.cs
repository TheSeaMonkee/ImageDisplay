// SPDX-License-Identifier: MIT
// Copyright (c) 2026 SeaMonkee. Developed by SeaMonkee and Claude (Anthropic).

using System.Windows;

namespace ImageDisplayApp
{
    /// <summary>
    /// Four sliders that trim the edges of the cloned window. Changes apply live to the overlay, so the effect
    /// can be seen while dragging.
    /// </summary>
    public partial class CropWindow : Window
    {
        private readonly MainWindow _main;
        private bool _updating = true; // true until the first SyncFromMain: ignores events raised while the XAML loads

        public CropWindow(MainWindow main, Window owner)
        {
            InitializeComponent();
            _main = main;
            Owner = owner;
            SyncFromMain();
        }

        /// <summary>Shows the crop the main window actually holds (it may have clamped what was asked for).</summary>
        private void SyncFromMain()
        {
            _updating = true;

            CropFraction crop = _main.Crop;
            sldLeft.Value = Math.Round(crop.Left * 100);
            sldTop.Value = Math.Round(crop.Top * 100);
            sldRight.Value = Math.Round(crop.Right * 100);
            sldBottom.Value = Math.Round(crop.Bottom * 100);

            txtLeft.Text = sldLeft.Value + "%";
            txtTop.Text = sldTop.Value + "%";
            txtRight.Text = sldRight.Value + "%";
            txtBottom.Text = sldBottom.Value + "%";

            _updating = false;
        }

        private void Slider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_updating)
            {
                return;
            }

            _main.SetCrop(new CropFraction(
                sldLeft.Value / 100.0,
                sldTop.Value / 100.0,
                sldRight.Value / 100.0,
                sldBottom.Value / 100.0));

            SyncFromMain();
        }

        private void Reset_Click(object sender, RoutedEventArgs e)
        {
            _main.SetCrop(CropFraction.None);
            SyncFromMain();
        }

        private void Done_Click(object sender, RoutedEventArgs e) => Close();
    }
}
