﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Windows;
//using System.Windows.Controls;
//using System.Windows.Documents;
using System.Windows.Input;
using Windows.UI.Xaml.Controls;
//using System.Windows.Media;
//using System.Windows.Media.Animation;
//using System.Windows.Shapes;

namespace sshmc.MapControls
{
    public partial class CCTV : UserControl
    {
        public CCTV()
        {
            InitializeComponent();
        }

        public void SetBlind()
        {
            this.stbBlind.Begin();
           // throw new NotImplementedException();
        }
    }
}
