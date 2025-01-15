using HalconDotNet;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace WpfApp1
{

    class MainVM
    {
        public HImage DisplayImg { get; set; }

        public MainVM()
        {
            DisplayImg = new HImage(@"C:\Users\Lenovo\Downloads\123.png");
        }
    }
}
