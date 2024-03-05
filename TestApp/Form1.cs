using Device_Insize7130usb;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TestApp
{
    /// <summary>
    ///  ПЕрвая задача - проверить базовые возможности устройства
    ///     Подключение устройства
    ///     Отключение  устройства
    ///     Обработка новых данных
    ///     ... что-то еще.
    ///  Дальше:
    ///         улучшение...
    /// </summary>
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            devInsize7130usb1.SerialPortNumber = 1;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            DevInsize7130usb.StatusCodes status = devInsize7130usb1.StartConnection();
            label1.Text = status.ToString();
        }

        private void button3_Click(object sender, EventArgs e)
        {
            DevInsize7130usb.StatusCodes status = devInsize7130usb1.StopConnection();
            label2.Text = status.ToString();
        }

        private void devInsize7130usb1_Connected()
        {
            label3.Text = "Connected";
        }

        private void devInsize7130usb1_Disconnected()
        {
            label3.Text = "No connection";
        }

        private void devInsize7130usb1_NewDataAvaible()
        {
            //
        }

        private void devInsize7130usb1_NotSupported()
        {
            // Устройство не поддерживается
        }
    }
}