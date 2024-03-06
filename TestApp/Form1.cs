using Device_Insize7130usb;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using static Device_Insize7130usb.DevInsize7130usb;

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

        private void Button1_Click(object sender, EventArgs e)
        {
            if( uint.TryParse(textBox1.Text, out uint value))
            {
                if (value > 0)
                {
                    devInsize7130usb1.SerialPortNumber = value;
                }
            }
        }

        private void Button2_Click(object sender, EventArgs e)
        {
            DevInsize7130usb.StatusCodes status = devInsize7130usb1.StartConnection();
            label1.Text = status.ToString();
        }

        private void Button3_Click(object sender, EventArgs e)
        {
            DevInsize7130usb.StatusCodes status = devInsize7130usb1.StopConnection();
            label2.Text = status.ToString();
        }

        private void DevInsize7130usb1_Connected()
        {
            _ = this.Invoke(new Action(() =>
            { label3.Text = "Connected"; }
            ));
            
        }

        private void DevInsize7130usb1_Disconnected()
        {
            _ = this.Invoke(new Action(() =>
            { label3.Text = "No connection"; }
            ));
        }

        private void DevInsize7130usb1_NewDataAvaible()
        {
            //
            while (devInsize7130usb1.QueueMeasuredData.Count > 1) 
            {
                if (!devInsize7130usb1.IsConnected )
                    { return; }
                devInsize7130usb1.QueueMeasuredData.Dequeue();
            }
            if (!devInsize7130usb1.IsConnected)
            { return; }
            _ = this.Invoke(new Action(() =>
            {
                MeasuredData measureData = devInsize7130usb1.QueueMeasuredData.Dequeue();
                //label4.Text = measureData.Time.ToString("mm.ss.tttt")// devInsize7130usb1.QueueMeasuredData.Dequeue().ToString();
                //label4.Text = measureData.Time.ToString() + " " + measureData.Value.ToString();// devInsize7130usb1.QueueMeasuredData.Dequeue().ToString();
                //label4.Text = measureData.Time.ToString("mm.ss.fffff") + " " + measureData.Value.ToString();// devInsize7130usb1.QueueMeasuredData.Dequeue().ToString();
                label4.Text = measureData.Value.ToString("f4");// devInsize7130usb1.QueueMeasuredData.Dequeue().ToString();
                //textBox2.Text +="\n"+ label4.Text;
            }
            ));
        }

        private void DevInsize7130usb1_NotSupported()
        {
            // Устройство не поддерживается
        }



        private void DevInsize7130usb1_StatusConnectionChanged(DevInsize7130usb.ConnectionStates newStatus, DevInsize7130usb.ChangeStatusReasons reason)
        {
            // изменился статус подключения
            //if( i)
            _ = this.Invoke(new Action(() =>
                { label5.Text = newStatus.ToString() + " : " + reason.ToString(); }
            ));
            
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            // Закрываем подключение
            //devInsize7130usb1.StopConnection(); 
        }

        private void Form1_FormClosing_1(object sender, FormClosingEventArgs e)
        {
            if(devInsize7130usb1.IsConnected)
            {
                _ = MessageBox.Show("Нужно остановить передачу");
                e.Cancel = true;
                return;
            }
            //devInsize7130usb1.StopConnection();
            Thread.Sleep(1000);

        }

        private void button4_Click(object sender, EventArgs e)
        {
            DialogResult result = MessageBox.Show("Вы уверены что хотите изменить положение нуля датчика?",
                "Подтвердить изменение нуля", 
                MessageBoxButtons.YesNo, 
                MessageBoxIcon.Question , MessageBoxDefaultButton.Button2);
            //
            if (result == DialogResult.Yes) 
            {
                // Координата нуля записывается в eeprom датчика, поэтому не следует 
                // постоянно сбрасывать нуль в датчике.
                // Для временного получения относительных координат, проще корректировать
                // текуший ноль в программе.
                // Установка нуля в датчике имеет смысл, когда нужно привязать координаты датчика
                // к какому-то постоянному положению, определяемому механикой.
                devInsize7130usb1.Zepo(); 
            }
        }
    }
}