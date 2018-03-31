using StatisticalFrowOnSeries;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.DataVisualization.Charting;

namespace forms
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
            chart.ChartAreas[0].AxisY.IsStartedFromZero = false;
        }   

        public void AddCandle(Candle c)
        {
            chart.Series[0].Points.AddY(c.High, c.Low, c.Open, c.Close);
        }

        public void AddIndi(int index, double c)
        {
            chart.Series[index].Points.AddY(c);
        }
        public void AddIndi(int index, double[] c)
        {
            chart.Series[index].Points.AddY(c[0],c[1]);
        }
    }
}
