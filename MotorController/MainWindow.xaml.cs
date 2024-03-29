﻿using MahApps.Metro.Controls;
using MahApps.Metro.IconPacks;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO.Ports;
using System.Security.Permissions;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Input;
using System.Text.RegularExpressions;
using System.Data;
using System.Windows.Controls;

namespace MotorController
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : MetroWindow
    {
        SerialPort ComPort = new SerialPort();//声明一个串口
        private string[] ports;//可用串口数组
        private bool recStaus = true;//接收状态字
        private bool ComPortIsOpen = false;//COM口开启状态字，在打开/关闭串口中使用，这里没有使用自带的ComPort.IsOpen，因为在串口突然丢失的时候，ComPort.IsOpen会自动false，逻辑混乱
        private bool Listening = false;//用于检测是否没有执行完invoke相关操作，仅在单线程收发使用，但是在公共代码区有相关设置，所以未用#define隔离
        private bool WaitClose = false;//invoke里判断是否正在关闭串口是否正在关闭串口，执行Application.DoEvents，并阻止再次invoke ,解决关闭串口时，程序假死，具体参见http://news.ccidnet.com/art/32859/20100524/2067861_4.html 仅在单线程收发使用，但是在公共代码区有相关设置，所以未用#define隔离
        public static string Data, frequency,Mid;
        public static int mx = 0, num = 0, flag;
        List<Customer> comList = new List<Customer>();//可用串口集合
        DispatcherTimer autoSendTick = new DispatcherTimer();//定时发送



        public MainWindow()
        {
            InitializeComponent();
            Loaded += Window_loaded;
        }


        #region 串口设置

        private void MetroWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)//关闭窗口closing
        {
            MessageBoxResult result = MessageBox.Show("确认是否要退出？", "退出", MessageBoxButton.YesNo);//显示确认窗口
            if (result == MessageBoxResult.No)
            {
                e.Cancel = true;//取消操作
            }
        }

        private void MetroWindow_Closed(object sender, EventArgs e)//确认关闭后
        {
            Application.Current.Shutdown();//先停止线程,然后终止进程.
            Environment.Exit(0);//直接终止进程.
        }

        private void Window_loaded(object sender, RoutedEventArgs e)//主窗口初始化
        {
            //可用串口下拉控件
            ports = SerialPort.GetPortNames();//获取可用串口
            if (ports.Length > 0)
            {
                for (int i = 0; i < ports.Length; i++)
                {
                    comList.Add(new Customer() { com = ports[i] });//下拉控件里添加可用串口

                }
                AvailableComCbobox.ItemsSource = comList;//资源路径
                AvailableComCbobox.DisplayMemberPath = "com";//显示路径
                AvailableComCbobox.SelectedValuePath = "com";//值路径
                AvailableComCbobox.SelectedValue = ports[0];//默认选择第一个


            }
            else
            {
                // MessageBox.Show("无可用串口");
                MessageBox.Show("无可用串口", "提示");
            }

            //波特率下拉控件
            List<Customer> rateList = new List<Customer>
            {

                new Customer() { BaudRate = "1200" },
                new Customer() { BaudRate = "2400" },
                new Customer() { BaudRate = "4800" },
                new Customer() { BaudRate = "9600" },
                new Customer() { BaudRate = "14400" },
                new Customer() { BaudRate = "19200" },
                new Customer() { BaudRate = "28800" },
                new Customer() { BaudRate = "38400" },
                new Customer() { BaudRate = "57600" },
                new Customer() { BaudRate = "74880" },
                new Customer() { BaudRate = "115200" }
            };//可用波特率集合
            RateListCbobox.ItemsSource = rateList;
            RateListCbobox.DisplayMemberPath = "BaudRate";//显示出来的值
            RateListCbobox.SelectedValuePath = "BaudRate";//选中后获取的实际结果



            //数据位下拉控件
            List<Customer> dataBits = new List<Customer>
            {
                new Customer() { Dbits = "8" },
                new Customer() { Dbits = "7" },
                new Customer() { Dbits = "6" }
            };//数据位集合
            DataBitsCbobox.ItemsSource = dataBits;
            DataBitsCbobox.SelectedValuePath = "Dbits";
            DataBitsCbobox.DisplayMemberPath = "Dbits";

            //停止位下拉控件
            List<Customer> stopBits = new List<Customer>
            {
                new Customer() { Sbits = "1" },
                new Customer() { Sbits = "1.5" },
                new Customer() { Sbits = "2" }
            };//停止位集合
            StopBitsCbobox.ItemsSource = stopBits;
            StopBitsCbobox.SelectedValuePath = "Sbits";
            StopBitsCbobox.DisplayMemberPath = "Sbits";


            //校检位下拉控件
            List<Customer> comParity = new List<Customer>
            {
                new Customer() { Parity = "None", ParityValue = "0" },
                new Customer() { Parity = "Odd", ParityValue = "1" },
                new Customer() { Parity = "Even", ParityValue = "2" },
                new Customer() { Parity = "Mark", ParityValue = "3" },
                new Customer() { Parity = "Space", ParityValue = "4" }
            };//可用校验位集合
            ParityComCbobox.ItemsSource = comParity;
            ParityComCbobox.DisplayMemberPath = "Parity";
            ParityComCbobox.SelectedValuePath = "ParityValue";


            //默认值设置
            RateListCbobox.SelectedValue = "115200";//波特率默认设置9600
            ParityComCbobox.SelectedValue = "0";//校验位默认设置值为0，对应NONE
            DataBitsCbobox.SelectedValue = "8";//数据位默认设置8位
            StopBitsCbobox.SelectedValue = "1";//停止位默认设置1
            //RecUniodeComCbobox.SelectedValue = "UTF-8";//接收默认字符为UTF-8
            //SendUniodeComCbobox.SelectedValue = "UTF-8";//发送默认字符为UTF-8

            ComPort.ReadTimeout = 8000;//串口读超时8秒
            ComPort.WriteTimeout = 8000;//串口写超时8秒，在1ms自动发送数据时拔掉串口，写超时5秒后，会自动停止发送，如果无超时设定，这时程序假死
            ComPort.ReadBufferSize = 1024;//数据读缓存
            ComPort.WriteBufferSize = 1024;//数据写缓存
            //sendBtn.IsEnabled = false;//发送按钮初始化为不可用状态
            //sendModeCheck.IsChecked = false;//发送模式默认为未选中状态
            //recModeCheck.IsChecked = false;//接收模式默认为未选中状态
            UpButton.IsEnabled = false;
            DownButton.IsEnabled = false;
            LeftButton.IsEnabled = false;
            RightButton.IsEnabled = false;
            Number1.IsEnabled = false;
            Number2.IsEnabled = false;
            Number3.IsEnabled = false;
            PushButton.IsEnabled = false;
            SendButton.IsEnabled = false;
            Now.IsEnabled = false;
            //↑↑↑↑↑↑↑↑↑默认设置↑↑↑↑↑↑↑↑↑
            ComPort.DataReceived += new SerialDataReceivedEventHandler(ComReceive);//串口接收中断
            //autoSendTick.Tick += new EventHandler(autoSend);//定时发送中断

        }

        private void Button_Open(object sender, RoutedEventArgs e)
        {
            if (AvailableComCbobox.SelectedValue == null || AvailableComCbobox.SelectedValue == null)
            {
                MessageBox.Show("无法打开串口", "提示");
                return;
            }
            #region 打开串口
            if (ComPortIsOpen == false)
            {
                try//尝试打开串口
                {
                    ComPort.PortName = AvailableComCbobox.SelectedValue.ToString();//设置要打开的串口

                    ComPort.BaudRate = Convert.ToInt32(RateListCbobox.SelectedValue);//设置当前波特率
                    ComPort.Parity = (Parity)Convert.ToInt32(ParityComCbobox.SelectedValue);//设置当前校验位
                    ComPort.DataBits = Convert.ToInt32(DataBitsCbobox.SelectedValue);//设置当前数据位
                    ComPort.StopBits = (StopBits)Convert.ToDouble(StopBitsCbobox.SelectedValue);//设置当前停止位                    
                    ComPort.Open();//打开串口

                }
                catch//如果串口被其他占用，则无法打开
                {
                    MessageBox.Show("无法打开串口,请检测此串口是否有效或被其他占用！", "提示");
                    // GetPort();//刷新当前可用串口
                    return;//无法打开串口，提示后直接返回
                }

                //成功打开串口后的设置
                openBtn.Content = "关闭串口";//按钮显示改为“关闭按钮” 
                ComPortIsOpen = true;//串口打开状态字改为true
                WaitClose = false;//等待关闭串口状态改为false                
                //sendBtn.IsEnabled = true;//使能“发送数据”按钮
                defaultSet.IsEnabled = false;//打开串口后失能重置功能
                AvailableComCbobox.IsEnabled = false;//失能可用串口控件
                RateListCbobox.IsEnabled = false;//失能可用波特率控件
                ParityComCbobox.IsEnabled = false;//失能可用校验位控件
                DataBitsCbobox.IsEnabled = false;//失能可用数据位控件
                StopBitsCbobox.IsEnabled = false;//失能可用停止位控件               
                UpButton.IsEnabled = true;
                DownButton.IsEnabled = true;
                LeftButton.IsEnabled = true;
                RightButton.IsEnabled = true;                
                PushButton.IsEnabled = true;
                SendButton.IsEnabled = true;
                Now.IsEnabled = true;
                Number1.IsEnabled = true;
                Number2.IsEnabled = true;
                Number3.IsEnabled = true;

                /*
                if (autoSendCheck.IsChecked == true)//如果打开前，自动发送控件就被选中，则打开串口后自动开始发送数据
                {
                    autoSendTick.Interval = TimeSpan.FromMilliseconds(Convert.ToInt32(Time.Text));//设置自动发送间隔
                    autoSendTick.Start();//开启自动发送
                }
                */
            }
            #endregion
            #region 关闭串口
            else//ComPortIsOpen == true,当前串口为打开状态，按钮事件为关闭串口
            {
                try//尝试关闭串口
                {
                    autoSendTick.Stop();//停止自动发送
                    //autoSendCheck.IsChecked = false;//停止自动发送控件改为未选中状态
                    ComPort.DiscardOutBuffer();//清发送缓存
                    ComPort.DiscardInBuffer();//清接收缓存
                    WaitClose = true;//激活正在关闭状态字，用于在串口接收方法的invoke里判断是否正在关闭串口
                    while (Listening)//判断invoke是否结束
                    {
                        DispatcherHelper.DoEvents(); //循环时，仍进行等待事件中的进程，该方法为winform中的方法，WPF里面没有，这里在后面自己实现
                    }
                    ComPort.Close();//关闭串口
                    WaitClose = false;//关闭正在关闭状态字，用于在串口接收方法的invoke里判断是否正在关闭串口
                    SetAfterClose();//成功关闭串口或串口丢失后的设置
                }

                catch//如果在未关闭串口前，串口就已丢失，这时关闭串口会出现异常
                {
                    if (ComPort.IsOpen == false)//判断当前串口状态，如果ComPort.IsOpen==false，说明串口已丢失
                    {
                        SetComLose();
                    }
                    else//未知原因，无法关闭串口
                    {
                        MessageBox.Show("无法关闭串口，原因未知！", "提示");
                        return;//无法关闭串口，提示后直接返回
                    }
                }
                UpButton.IsEnabled = false;
                DownButton.IsEnabled = false;
                LeftButton.IsEnabled = false;
                RightButton.IsEnabled = false;
                Number1.IsEnabled = false;
                Number2.IsEnabled = false;
                Number3.IsEnabled = false;
                PushButton.IsEnabled = false;
                SendButton.IsEnabled = false;
                Now.IsEnabled = false;
                No1Text.Text = "";
                No2Text.Text = "";
                No3Text.Text = "";
            }
            #endregion

        }
        /*
        private void SendBtn_Click(object sender, RoutedEventArgs e)
        {
            ComSend();
        }

        void autoSend(object sender, EventArgs e)//自动发送
        {
            ComSend();//调用发送方法
        }
        */


        //模拟 Winfrom 中 Application.DoEvents() 详见 http://www.silverlightchina.net/html/study/WPF/2010/1216/4186.html?1292685167
        public static class DispatcherHelper
        {
            [SecurityPermissionAttribute(SecurityAction.Demand, Flags = SecurityPermissionFlag.UnmanagedCode)]
            public static void DoEvents()
            {
                DispatcherFrame frame = new DispatcherFrame();
                Dispatcher.CurrentDispatcher.BeginInvoke(DispatcherPriority.Background, new DispatcherOperationCallback(ExitFrames), frame);
                try { Dispatcher.PushFrame(frame); }
                catch (InvalidOperationException) { }
            }
            private static object ExitFrames(object frame)
            {
                ((DispatcherFrame)frame).Continue = false;
                return null;
            }
        }

        private void SetAfterClose()//成功关闭串口或串口丢失后的设置
        {
            openBtn.Content = "打开串口";//按钮显示为“打开串口”
            ComPortIsOpen = false;//串口状态设置为关闭状态
            //sendBtn.IsEnabled = false;//失能发送数据按钮
            defaultSet.IsEnabled = true;//打开串口后使能重置功能
            AvailableComCbobox.IsEnabled = true;//使能可用串口控件
            RateListCbobox.IsEnabled = true;//使能可用波特率下拉控件
            ParityComCbobox.IsEnabled = true;//使能可用校验位下拉控件
            DataBitsCbobox.IsEnabled = true;//使能数据位下拉控件
            StopBitsCbobox.IsEnabled = true;//使能停止位下拉控件
        }

        private void SetComLose()//成功关闭串口或串口丢失后的设置
        {
            autoSendTick.Stop();//串口丢失后要关闭自动发送
            //autoSendCheck.IsChecked = false;//自动发送改为未选中
            WaitClose = true;//;//激活正在关闭状态字，用于在串口接收方法的invoke里判断是否正在关闭串口
            while (Listening)//判断invoke是否结束
            {
                DispatcherHelper.DoEvents(); //循环时，仍进行等待事件中的进程，该方法为winform中的方法，WPF里面没有，这里在后面自己实现
            }
            MessageBox.Show("串口已丢失", "提示");
            WaitClose = false;//关闭正在关闭状态字，用于在串口接收方法的invoke里判断是否正在关闭串口
            GetPort();//刷新可用串口
            SetAfterClose();//成功关闭串口或串口丢失后的设置
        }

        private void GetPort()//刷新可用串口
        {

            comList.Clear();//情况控件链接资源
            AvailableComCbobox.DisplayMemberPath = "com1";
            AvailableComCbobox.SelectedValuePath = null;//路径都指为空，清空下拉控件显示，下面重新添加

            ports = new string[SerialPort.GetPortNames().Length];//重新定义可用串口数组长度
            ports = SerialPort.GetPortNames();//获取可用串口
            if (ports.Length > 0)//有可用串口
            {
                for (int i = 0; i < ports.Length; i++)
                {
                    comList.Add(new Customer() { com = ports[i] });//下拉控件里添加可用串口
                }
                AvailableComCbobox.ItemsSource = comList;//可用串口下拉控件资源路径
                AvailableComCbobox.DisplayMemberPath = "com";//可用串口下拉控件显示路径
                AvailableComCbobox.SelectedValuePath = "com";//可用串口下拉控件值路径

            }
        }

        private void Button1_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("测试");
        }



        private void CountClear_Click(object sender, RoutedEventArgs e)
        {
            sendCount.Text = "0";
            recCount.Text = "0";
        }


        private void DefaultSet_Click(object sender, RoutedEventArgs e)
        {
            RateListCbobox.SelectedValue = "115200";//波特率默认设置9600
            ParityComCbobox.SelectedValue = "0";//校验位默认设置值为0，对应NONE
            DataBitsCbobox.SelectedValue = "8";//数据位默认设置8位
            StopBitsCbobox.SelectedValue = "1";//停止位默认设置1
            GetPort();
        }

        private void tb_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            Regex re = new Regex("[^0-9.\\-]+");
            e.Handled = re.IsMatch(e.Text);
        }

        void TextBoxEx_TextChanged(object sender, TextChangedEventArgs e)
        {
            //屏蔽中文输入和非法字符粘贴输入
            var textBox = sender as TextBox;
            var change = new TextChange[e.Changes.Count];
            e.Changes.CopyTo(change, 0);


            int offset = change[0].Offset;
            if (change[0].AddedLength > 0)
            {
                double num;
                if (textBox != null && !Double.TryParse(textBox.Text, out num))
                {
                    textBox.Text = textBox.Text.Remove(offset, change[0].AddedLength);
                    textBox.Select(offset, 0);
                }
            }
        }

        void TextBoxEx_KeyDown(object sender, KeyEventArgs e)
        {
            var txt = sender as TextBox;
            //屏蔽非法按键
            if ((e.Key >= Key.NumPad0 && e.Key <= Key.NumPad9) || e.Key == Key.Decimal)
            {
                if (txt != null && (txt.Text.Contains(".") && e.Key == Key.Decimal))
                {
                    e.Handled = true;
                    return;
                }
                e.Handled = false;
            }
            else if (((e.Key >= Key.D0 && e.Key <= Key.D9) || e.Key == Key.OemPeriod) && e.KeyboardDevice.Modifiers != ModifierKeys.Shift)
            {
                if (txt != null && (txt.Text.Contains(".") && e.Key == Key.OemPeriod))
                {
                    e.Handled = true;
                    return;
                }
                e.Handled = false;
            }
            else
            {
                e.Handled = true;
            }
        }

        #endregion

        #region 收发服务函数

        private void ComSend(string Morder)//发送数据 普通方法，发送数据过程中UI会失去响应
        {

            byte[] sendBuffer = null;//发送数据缓冲区
            string sendData = Morder;//复制发送数据，以免发送过程中数据被手动改变           
                                     // sendBuffer = System.Text.Encoding.Default.GetBytes(sendData);//转码
            sendBuffer = Encoding.UTF8.GetBytes(sendData);

            try//尝试发送数据
            {//如果发送字节数大于1000，则每1000字节发送一次
                int sendTimes = (sendBuffer.Length / 1000);//发送次数
                for (int i = 0; i < sendTimes; i++)//每次发生1000Bytes
                {
                    ComPort.Write(sendBuffer, i * 1000, 1000);//发送sendBuffer中从第i * 1000字节开始的1000Bytes
                    sendCount.Text = (Convert.ToInt32(sendCount.Text) + 1000).ToString();//刷新发送字节数
                }
                if (sendBuffer.Length % 1000 != 0)
                {
                    ComPort.Write(sendBuffer, sendTimes * 1000, sendBuffer.Length % 1000);//发送字节小于1000Bytes或上面发送剩余的数据
                    sendCount.Text = (Convert.ToInt32(sendCount.Text) + sendBuffer.Length % 1000).ToString();//刷新发送字节数
                }
            }
            catch//如果无法发送，产生异常
            {
                if (ComPort.IsOpen == false)//如果ComPort.IsOpen == false，说明串口已丢失
                {
                    SetComLose();//串口丢失后相关设置
                }
                else
                {
                    MessageBox.Show("无法发送数据，原因未知！", "提示");
                }
            }
            //sendScrol.ScrollToBottom();//发送数据区滚动到底部

        }

        private void ComReceive(object sender, SerialDataReceivedEventArgs e)//接收数据 数据在接收中断里面处理
        {
            if (WaitClose) return;//如果正在关闭串口，则直接返回
            if (recStaus)//如果已经开启接收
            {

                try
                {
                    Listening = true;////设置标记，说明我已经开始处理数据，一会儿要使用系统UI的。
                    Thread.Sleep(10);//发送和接收均为文本时，接收中为加入判断是否为文字的算法，发送你（C4E3），接收可能识别为C4,E3，可用在这里加延时解决
                    byte[] recBuffer = new byte[ComPort.BytesToRead];//接收数据缓存
                    ComPort.Read(recBuffer, 0, recBuffer.Length);//读取数据
                                                                 //string recData = System.Text.Encoding.Default.GetString(recBuffer);//转码

                    recTBox.Dispatcher.Invoke(//WPF为单线程，此接收中断线程不能对WPF进行操作，用如下方法才可操作
                    new Action(
                         delegate
                         {
                             recCount.Text = (Convert.ToInt32(recCount.Text) + recBuffer.Length).ToString();//接收数据字节数
                             string recData = Encoding.UTF8.GetString(recBuffer);
                             frequency = recData.Substring(1, recData.Length - 1);

                             if(recData.Contains("OK"))
                             {
                                 if (recData.Contains("AOK"))
                                 {
                                     No1Text.Text = "设备初始化完成！";

                                 }
                                 else if (recData.Contains("BOK"))
                                 {
                                     No2Text.Text = "设备初始化完成！";

                                 }
                                 else if (recData.Contains("COK"))
                                 {
                                     No3Text.Text = "设备初始化完成！";
                                 }
                             }
                             
                             else
                             {
                                 string[] Datams = frequency.Replace("\r\n", "").Split('E');

                                 if (recData[0] == 'A')
                                     No1Text.Text = "当前角度" + "\r\n" +
                                         "垂直" + Datams[0] + "\r\n"
                                         + "水平" + Datams[1] + "\r\n";
                                 else if (recData[0] == 'B')
                                     No2Text.Text = "当前角度" + "\r\n" +
                                         "垂直" + Datams[0] + "\r\n"
                                         + "水平" + Datams[1] + "\r\n";
                                 else if (recData[0] == 'C')
                                     No3Text.Text = "当前角度" + "\r\n" +
                                         "垂直" + Datams[0] + "\r\n"
                                         + "水平" + Datams[1] + "\r\n";
                                 else
                                 {
                                     No1Text.Text = "返回值错误！";
                                     No2Text.Text = "返回值错误！";
                                     No3Text.Text = "返回值错误！";
                                 }
                             }
                             
                         }
                    )
                    );

                }
                finally
                {
                    Listening = false;//UI使用结束，用于关闭串口时判断，避免自动发送时拔掉串口，陷入死循环
                }

            }
            else//暂停接收
            {
                ComPort.DiscardInBuffer();//清接收缓存
            }
        }

        string FlagDefine( )
        {
            if (this.Number1.IsChecked == true)
                Mid = "A";
            else if (this.Number2.IsChecked == true)
                Mid = "B";
            else if (this.Number3.IsChecked == true)
                Mid = "C";

            return Mid;
        }

        #endregion

        #region 特殊控件
        private void LeftBtn_Click(object sender, RoutedEventArgs e)
        {
            string head = FlagDefine();

            if (LeftButton.Content.ToString() == "左")
            {
                ComSend(head+"MR");
                ComSend("\r\n");
                LeftButton.Content = "停";
            }
            else
            {
                ComSend(head+"YG");
                ComSend("\r\n");
                LeftButton.Content = "左";
            }
          //  recTBox.Text = head + "Now";
        }

        private void UpBtn_Click(object sender, RoutedEventArgs e)
        {
            string head = FlagDefine();

            if (UpButton.Content.ToString() == "仰")
            {
                ComSend(head+"MD");
                ComSend("\r\n");
                UpButton.Content = "停";
            }
            else
            {
                ComSend(head+"YG");
                ComSend("\r\n");
                UpButton.Content = "仰";
            }
        }

        private void DownBtn_Click(object sender, RoutedEventArgs e)
        {
            string head = FlagDefine();

            if (DownButton.Content.ToString() == "俯")
            {
                ComSend(head+"MU");
                ComSend("\r\n");
                DownButton.Content = "停";
            }
            else
            {
                ComSend(head+"YG");
                ComSend("\r\n");
                DownButton.Content = "俯";
            }
        }

        private void RightBtn_Click(object sender, RoutedEventArgs e)
        {
            string head = FlagDefine();

            if (RightButton.Content.ToString() == "右")
            {
                ComSend(head+"ML");
                ComSend("\r\n");
                RightButton.Content = "停";
            }
            else
            {
                ComSend(head+"YG");
                ComSend("\r\n");
                RightButton.Content = "右";
            }
        }

        private void NowBtn_Click(object sender, RoutedEventArgs e)
        {            
            string head = FlagDefine();
            ComSend(head+"CA");
            ComSend("\r\n");            
        }

        private void PBtn_Click(object sender, RoutedEventArgs e)
        {
            string head = FlagDefine();
            ComSend(head+"YR");
            ComSend("\r\n");
        }

        private void SBtn_Click(object sender, RoutedEventArgs e)
        {
            string head = FlagDefine();
            string text1 = "A";
            string text2 = Vsend.Text;
            string text3 = Hsend.Text;
            string text4 = "\r\n";
            if (Vsend.Text == "" && Hsend.Text == "")
                ComSend(head+"AAAAA\r\n");

            else if (Vsend.Text == "" && Hsend.Text != "")
                ComSend(head+text1 + "AA" + text3 + text4);


            else if (Vsend.Text != "" && Hsend.Text == "")
                ComSend(head+text1 + text2 + "AA" + text4);

            else
                ComSend(head+text1 + text2 + text3 + text4);

            Vsend.Text = "";
            Hsend.Text = "";

            if (head == "A")
                No1Text.Text = "预设角度" +"\r\n"+
                    "垂直" + text2+"\r\n"
                    + "水平" + text3+"\r\n";
            else if(head=="B")
                No2Text.Text = "预设角度" + "\r\n" +
                    "垂直" + text2 + "\r\n"
                    + "水平" + text3 + "\r\n";
            else if(head=="C")
                No3Text.Text = "预设角度" + "\r\n" +
                    "垂直" + text2 + "\r\n"
                    + "水平" + text3 + "\r\n";
            else
            {
                No1Text.Text = "错误！\r\n 请先作出选择";
                No2Text.Text = "错误！\r\n 请先作出选择";
                No3Text.Text = "错误！\r\n 请先作出选择";
            }
        }


        #endregion
    }
}