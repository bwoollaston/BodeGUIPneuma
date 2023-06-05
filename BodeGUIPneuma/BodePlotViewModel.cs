using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using OxyPlot;
using OxyPlot.Series;

namespace BodeGUIPneuma
{
    public class BodePlotViewModel : ViewModelBase
    {
        public BodePlotViewModel()
        {
            Title = "Bode Plot";
            Points = new ObservableCollection<DataPoint>();
            Threshold = new ObservableCollection<DataPoint>();
        }
        private string _title;
        public string Title
        {
            get
            {
                return _title;
            }
            set
            {
                _title = value;
                OnPropertyChanged();
            }
        }
        private ObservableCollection<DataPoint> _points;
        public ObservableCollection<DataPoint> Points
        {
            get { return _points; }
            set { _points = value; OnPropertyChanged(); }
        }
        private ObservableCollection<DataPoint> _threshold;
        public ObservableCollection<DataPoint> Threshold
        {
            get { return _threshold; }
            set { _threshold = value; OnPropertyChanged(); }
        }
    }
}
