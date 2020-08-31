using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Xamarin.Forms;

namespace EventSender
{
    public partial class MainPage : ContentPage
    {
        private int _count = 1;
        public MainPage()
        {
            InitializeComponent();
        }

        private BinaryWriter _writer;

        void OnConnectClick(object sender, EventArgs e)
        {
            try
            {
                LastSwipe.Text = "Connect";
                var client = new TcpClient(IpAddress.Text, Convert.ToInt32(Port.Text));
                var stream = client.GetStream();
                _writer = new BinaryWriter(stream);
            }
            catch (Exception exp)
            {
                LastSwipe.Text = exp.Message;
            }
        }

        void OnSwiped(int id)
        {
            try
            {
                _count++;
                _writer.Write(id);
            }
            catch (Exception exp)
            {
                LastSwipe.Text = exp.Message;
            }
        }

        void OnSwipedLeft(object sender, SwipedEventArgs e)
        {
            LastSwipe.Text = "LastSwipe: Left";
            OnSwiped(1);
        }
        void OnSwipedRight(object sender, SwipedEventArgs e)
        {
            LastSwipe.Text = "LastSwipe: Right";
            OnSwiped(2);
        }
        void OnSwipedUp(object sender, SwipedEventArgs e)
        {

            LastSwipe.Text = "LastSwipe: Up";
            OnSwiped(3);
        }
        void OnSwipedDown(object sender, SwipedEventArgs e)
        {
            LastSwipe.Text = "LastSwipe: Down";
            OnSwiped(4);
        }
    }
}
