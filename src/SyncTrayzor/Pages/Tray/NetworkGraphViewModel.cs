using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using Stylet;
using SyncTrayzor.Syncthing;
using SyncTrayzor.Utils;
using System;
using System.Linq;

namespace SyncTrayzor.Pages.Tray
{
    public class NetworkGraphViewModel : Screen, IDisposable
    {
        private static readonly DateTime epoch = DateTime.UtcNow; // Some arbitrary value in the past
        private static readonly TimeSpan window = TimeSpan.FromMinutes(15);

        private const double minYValue = 1024 * 100; // 100 KBit/s

        private readonly ISyncthingManager syncthingManager;

        private readonly LinearAxis yAxis;
        private readonly LinearAxis xAxis;

        private readonly LineSeries inboundSeries;
        private readonly LineSeries outboundSeries;

        public PlotModel OxyPlotModel { get; } = new();
        public bool ShowGraph { get; private set; }

        public string MaxYValue { get; private set; }

        public NetworkGraphViewModel(ISyncthingManager syncthingManager)
        {
            this.syncthingManager = syncthingManager;

            OxyPlotModel.PlotAreaBorderColor = OxyColors.LightGray;

            xAxis = new LinearAxis()
            {
                Position = AxisPosition.Bottom,
                IsZoomEnabled = false,
                IsPanEnabled = false,
                IsAxisVisible = false,
                MajorGridlineColor = OxyColors.Gray,
                MajorGridlineStyle = LineStyle.Dash,
            };
            OxyPlotModel.Axes.Add(xAxis);

            yAxis = new LinearAxis()
            {
                Position = AxisPosition.Right,
                IsZoomEnabled = false,
                IsPanEnabled = false,
                IsAxisVisible = false,
                AbsoluteMinimum = -1, // Leave a little bit of room for the line to draw
            };
            OxyPlotModel.Axes.Add(yAxis);

            inboundSeries = new LineSeries()
            {
                Color = OxyColors.Red,
            };
            OxyPlotModel.Series.Add(inboundSeries);

            outboundSeries = new LineSeries()
            {
                Color = OxyColors.Green,
            };
            OxyPlotModel.Series.Add(outboundSeries);

            ResetToEmptyGraph();

            Update(this.syncthingManager.TotalConnectionStats);
            this.syncthingManager.TotalConnectionStatsChanged += TotalConnectionStatsChanged;
            this.syncthingManager.StateChanged += SyncthingStateChanged;
            ShowGraph = this.syncthingManager.State == SyncthingState.Running;
        }

        protected override void OnActivate()
        {
            base.OnActivate();
            OxyPlotModel.InvalidatePlot(true);
        }

        private void SyncthingStateChanged(object sender, SyncthingStateChangedEventArgs e)
        {
            if (e.OldState == SyncthingState.Running)
            {
                ResetToEmptyGraph();
            }

            ShowGraph = e.NewState == SyncthingState.Running;
        }

        private void ResetToEmptyGraph()
        {
            var now = DateTime.UtcNow;
            var earliest = (now - window - epoch).TotalSeconds;
            var latest = (now - epoch).TotalSeconds;

            // Put points on the far left, so we get a line from them
            inboundSeries.Points.Clear();
            inboundSeries.Points.Add(new DataPoint(earliest, 0));
            inboundSeries.Points.Add(new DataPoint(latest, 0));

            outboundSeries.Points.Clear();
            outboundSeries.Points.Add(new DataPoint(earliest, 0));
            outboundSeries.Points.Add(new DataPoint(latest, 0));

            xAxis.Minimum = earliest;
            xAxis.Maximum = latest;

            yAxis.Maximum = minYValue;
            MaxYValue = FormatUtils.BytesToHuman(minYValue) + "/s";

            if (IsActive)
                OxyPlotModel.InvalidatePlot(true);
        }

        private void TotalConnectionStatsChanged(object sender, ConnectionStatsChangedEventArgs e)
        {
            Update(e.TotalConnectionStats);
        }

        private void Update(SyncthingConnectionStats stats)
        {
            var now = DateTime.UtcNow;
            double earliest = (now - window - epoch).TotalSeconds;

            Update(earliest, inboundSeries, stats.InBytesPerSecond);
            Update(earliest, outboundSeries, stats.OutBytesPerSecond);

            xAxis.Minimum = earliest;
            xAxis.Maximum = (now - epoch).TotalSeconds;

            // This increases the value to the nearest 1024 boundary
            double maxValue = inboundSeries.Points.Concat(outboundSeries.Points).Max(x => x.Y);
            double roundedMax;
            if (maxValue > minYValue)
            {
                double factor = Math.Pow(1024, (int)Math.Log(maxValue, 1024));
                roundedMax = Math.Ceiling(maxValue / factor) * factor;
            }
            else
            {
                roundedMax = minYValue;
            }

            // Give the graph a little bit of headroom, otherwise the line gets chopped
            yAxis.Maximum = roundedMax * 1.05;
            MaxYValue = FormatUtils.BytesToHuman(roundedMax) + "/s";

            if (IsActive)
                OxyPlotModel.InvalidatePlot(true);
        }

        private void Update(double earliest, LineSeries series, double bytesPerSecond)
        {
            // Keep one data point below 'earliest'

            int i = 0;
            for (; i < series.Points.Count && series.Points[i].X < earliest; i++) { }
            i--;
            if (i > 0)
            {
                series.Points.RemoveRange(0, i);
            }

            series.Points.Add(new DataPoint((DateTime.UtcNow - epoch).TotalSeconds, bytesPerSecond));
        }

        public void Dispose()
        {
            syncthingManager.TotalConnectionStatsChanged -= TotalConnectionStatsChanged;
            syncthingManager.StateChanged -= SyncthingStateChanged;
        }
    }
}
