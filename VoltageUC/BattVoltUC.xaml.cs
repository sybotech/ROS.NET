﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using System.Threading;
using Messages;
using Messages.custom_msgs;
using Ros_CSharp;
using XmlRpc_Wrapper;
using Int32 = Messages.std_msgs.Int32;
using String = Messages.std_msgs.String;
using m = Messages.std_msgs;

namespace BattVoltUC
{
    /// <summary>
    /// Interaction logic for UserControl1.xaml
    /// </summary>
    public partial class BattVolt : UserControl
    {

        Subscriber<m.Float32> sub;

        public BattVolt()
        {
            InitializeComponent();
        }

        private void UserControl_Loaded(object sender, RoutedEventArgs e)
        {

        }

        public void startListening(NodeHandle node)
        {
            sub = node.subscribe<m.Float32>("/Voltage", 1000, callbackVoltMonitor);

        }

        public void callbackVoltMonitor( m.Float32 msg)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (abraCadabra.Visibility != Visibility.Visible)
                    abraCadabra.Visibility = Visibility.Visible;
                textBlock1.Text = msg.data + "v";
            }));

        }

    }
}
