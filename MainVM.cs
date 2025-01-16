using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Input;

namespace WpfApp1
{

    class MainVM
    {
        public HImage DisplayImg { get; set; }

        public ObservableCollection<HImage> Imgs { get; set; } = new ObservableCollection<HImage>();

        public MainVM()
        {
            Imgs.Add(new HImage(@"C:\Users\Lenovo\Downloads\123.png"));
            Imgs.Add(new HImage(@"C:\Users\Lenovo\Downloads\456.png"));
            Imgs.Add(new HImage(@"C:\Users\Lenovo\Downloads\并集.png"));
            Imgs.Add(new HImage(@"C:\Users\Lenovo\Downloads\交集.png"));
            Imgs.Add(new HImage(@"C:\Users\Lenovo\Downloads\差集.png"));
            DisplayImg = Imgs[0];
        }
    }
}
