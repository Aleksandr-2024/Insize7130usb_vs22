using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Threading;
using static Device_Insize7130usb.DevInsize7130usb;
//using System.Data;
//using System.Diagnostics;
//using System.IO.Ports;
//using System.Linq;
//using System.Net.Configuration;
//using System.Text;

namespace Device_Insize7130usb
{
    /// <summary>
    /// Компонент для работы с датчиками Insize 7130 USB
    /// ------------------------------------------------
    /// Insize 7130 USB - виртуальный COM-port
    ///     Скорость передачи по умолчанию 9600
    ///     8 бит, нет четности, один стоп бит.
    /// ------------------------------------------------
    /// Режимы работы:
    ///     По запросу - в датчик отправляется код 0x10, в ответ возвращается 
    ///                 текущее значение.
    ///     Потоком - После получения команды начала "потоковой передачи",
    ///         Данные начинают поступать непрерывно до поступления коменды
    ///         "остановить потоковую передачу"
    /// ------------------------------------------------
    /// Формат ответа: ASCII (9 байт)
    ///     "+dd.dddd\0x10"
    ///             +dd.dddd - Значение
    ///             \0x10 - байт завершения посылки
    ///     
    /// Команды:
    ///     - Все команды состоят из одного байта
    ///     
    ///     0x99 - установить положение 0. Датчик запоминает в энергонезависимой
    ///             памяти текущее положение как нулевое.
    ///             !!! Ответ на эту команду - значение датчика перед обнулением. !!!
    ///     0xDD - Начать потоковую передачу
    ///         !!! Если отключить датчик от питания и подключить снова, он продолжает 
    ///             передавать данные после нового подключения 
    ///             (параметр сохраняется в энергонезависимой памяти)
    ///         !!! УТОЧНИТЬ !!! Кажется при резком движении датчика данные временно не 
    ///             передаются !!! 
    ///                 - Защита от недостоверности?
    ///                 - не успевает обрабатывать изменения и передавать?
    ///                 - Показалось??? проверить!!!
    ///     0xDE - Остановить потоковую передачу
    ///     !!! должны быть команды изменения скорости передачи, но о них нет информации
    ///     
    ///     0x10 - Получить текущее значение при отключенной потоковой передаче.
    ///     * Эксперементально установлено, что отправка любого кода, который
    ///             не перечислен выше, обрабатывается как команда 0x10.
    ///             !!! Уточнить !!!
    /// ------------------------------------------------
    ///     ** доп информация по похожим датчикам. Для датчиков RS-485:
    ///         - для запроса данных вместо кода 0x10 должен быть отправлен 
    ///             адрес датчика (должен быть выгравирован на датчике, но не нашел где)
    ///         - ответ в формате "NN+dd.dddd\0x10"
    ///             NN - номер датчика
    ///             +dd.dddd - Значение
    ///             \0x10 - байт завершения посылки
    /// </summary>
    public partial class DevInsize7130usb : Component
    {
        #region Инициализация

        public DevInsize7130usb()
        {
            InitializeComponent();
        }

        public DevInsize7130usb(IContainer container)
        {
            container.Add(this);

            InitializeComponent();
        }
        #endregion Инициализация 




        #region Методы

        #region Start/Stop Connection

        /// <summary>
        /// Запуск процедуры подключения с портом по умолчанию. 
        /// </summary>
        /// <returns>Результат выполнения</returns>
        public StatusCodes StartConnection()
        {
            return StartConnection(_serialPortNumber);
        }

        /// <summary>
        /// Запуск процедуры подключения. 
        ///     - Проверка подключен датчик или нет
        ///     - Ожидание подключения датчика
        /// </summary>
        /// <returns>Результат выполнения</returns>
        public StatusCodes StartConnection( uint portNumber)
        {
            // Порт не может быть равен нулю
            if( _serialPortNumber == 0 ) 
                { return StatusCodes.Error_IllegalPortNumber; } 
            // Уже подключено
            if(_isConnected) 
                { return StatusCodes.Error_If_Connected; }
            // Идет подключение, нельзя менять правила
            if (_isConnecting) 
                { return StatusCodes.Error_If_Connecting; }
            if( portNumber != _serialPortNumber )
                { _serialPortNumber = portNumber; }
            // Препятствий нет - запускаем подключение

            _isConnecting = true;
            // Настраиваем порт
            if( serialPort1.IsOpen )
                { serialPort1.Close(); } // Такой ситуации не должно быть, пока решаем так.
            serialPort1.PortName = "COM" + _serialPortNumber.ToString();
            serialPort1.BaudRate = 9600;
            serialPort1.WriteTimeout = 100;
            serialPort1.ReadTimeout = 100;
            // Запускаем поток подключения в отдельном потоке

            _tokenTaskConnection = new CancellationTokenSource();
            //_taskConnection = Task.Run(TaskConnection, _tokenTaskConnection.Token);
            Task.Run(TaskConnection, _tokenTaskConnection.Token);




            //Connected.Invoke(); // DEBUG
            return StatusCodes.Success; // подключение успешно запущено
        }

        
        //private Task _taskConnection;
        private CancellationTokenSource _tokenTaskConnection;
        private const int _timeoutTaskConnection = 1000; // 1 раз в секунду

        /// <summary>
        ///  Задача подключения
        ///  Отслеживает заданный порт, Устанавливает подключение
        /// </summary>
        private void TaskConnection()
        {
            StatusConnectionChanged.Invoke(ConnectionStates.Connecting, ChangeStatusReasons.StartConnecting);

            _queueMeasuredData.Clear(); // очищаем очередь перед подключением
            ConnectingStates stateOfConnection = ConnectingStates.Start;

            while (!_tokenTaskConnection.IsCancellationRequested)
            {
                switch (stateOfConnection)
                {
                    case ConnectingStates.Start:
                        // пробуем подключиться к порту.
                        try
                        {
                            serialPort1.Open();
                        }
                        catch (Exception ex)
                        {
                            if(-2146232800 == ex.HResult)
                            {
                                // Порт не существует (не подключен)
                                // Ситуация не аварийная, ждем когда будет подключен
                                break;
                            }
                            if (-2147024891 == ex.HResult)
                            {
                                // Доступ к порту закрыт, порт занят. 
                                // Завершаем аварийно
                                // Ситуация не аварийная, ждем когда будет подключен
                                _isConnecting = false;
                                StatusConnectionChanged.Invoke(ConnectionStates.NoConnected, ChangeStatusReasons.PortBusy);
                                return;
                            }

                            throw; // оставляем для определения других ситуаций
                        }
                        if( serialPort1.IsOpen)
                        {
                            // Подключились, переходим к проверке.
                            serialPort1.DiscardInBuffer();
                            stateOfConnection = ConnectingStates.CheckAuto;
                        }


                        break;

                    case ConnectingStates.CheckAuto:
                        // Прошла 1 секунда, буфер должен быть заполнен данными
                        if( _queueMeasuredData.Count > 2 )
                        { //Буфер заполнился - автоматический режим работает. Успешное подключение.
                            // Чистим буфер. 
                            _queueMeasuredData.Clear();
                            _isConnecting = false;
                            _isConnected = true;
                            StatusConnectionChanged.Invoke(ConnectionStates.Connected, ChangeStatusReasons.SuccessConnection);
                            Connected.Invoke();
                            return;
                        }
                        // перходим к следующему шагу
                        stateOfConnection = ConnectingStates.End;
                        break;

                    case ConnectingStates.End: 
                        // Пока тут зацикливаем...
                        break;
                    default:
                        // Неопределенное состояние - прерываем подключение.
                        _isConnecting = false;
                        StatusConnectionChanged.Invoke(ConnectionStates.NoConnected, ChangeStatusReasons.IllegalState);
                        return;
                }


                Thread.Sleep(_timeoutTaskConnection);
            }
            // Подключение прервано
            _isConnecting = false;
            StatusConnectionChanged.Invoke(ConnectionStates.NoConnected, ChangeStatusReasons.CancellConnecting);
        }

        private enum ConnectingStates
        {
            Start = 0,  // Начальное состояние
            CheckAuto,  // Проверка автоматической передачи данных (потоком)
            End,        // Завершено.
            Step1, Step2, Step3, Step4, Step5, Step6, Step7, Step8, Step9, Step10, Step11, Step12,
        }

        /// <summary>
        /// Остановка подключения.
        ///     Может потребоваться для смены порта датчика 
        ///         - Во время подключения смена порта недопустима, 
        /// </summary>
        public StatusCodes StopConnection()
        {
            // Проверяем состояние...
            if ( _isConnected ) 
            { // подключено
                // отключаем...
                //serialPort1.Close();
                _isConnected = false;  // TODO: DEBUG
            }
            else if( _isConnecting )
            {   // процесс подключения - отключаемся...
                _tokenTaskConnection.Cancel();
                //_isConnecting = false; // TODO: DEBUG
            }
    
            /// отключение будет из команды
            //Disconnected.Invoke();
            return StatusCodes.Success;
        }
        #endregion end Start/Stop Connection

        #endregion end Методы

        #region Свойства

        /// <summary>
        /// Номер последовательного порта. (если =0 - порт не выбран)
        /// Используется вместо символического имени типа "COM1" для того чтобы 
        /// не заморачиватся возможными ошибками с символическими именами.
        /// 
        /// * Не может быть изменен, если устройство подключено
        /// 
        /// </summary>
        public uint SerialPortNumber
        {
            get => _serialPortNumber;
            set
            {
                if ( _isConnected )
                {
                    throw new MyException("Нельзя изменить номер порта при подключенном устройстве");
                }
                if (_isConnecting)
                {
                    throw new MyException("Нельзя изменить номер порта во время подключения к усройству");
                }
                _serialPortNumber=value;
            }
        }
        private uint _serialPortNumber = 0;
        private bool _isConnected = false;
        private bool _isConnecting = false;

        public bool IsConnected { get => _isConnected; }

        #endregion end Свойства 

        #region События 
        // События
        //  1. Подключено
        //  2. Отключено
        //  3. Поступили новые данные


        /// <summary>
        /// Подключение устройства успешно
        /// </summary>
        public delegate void deviceConnected();
        /// <summary>
        /// Подключение устройства успешно
        /// </summary>
        public event deviceConnected Connected;

        /// <summary>
        ///  Подключение устройства не удалось - не поддерживает команды
        ///  Ошибка подключения - Устроство не поддерживается портом
        /// </summary>
        public delegate void deviceNotSupported(); 

        /// <summary>
        /// отключение устройства в процессе работы
        /// </summary>
        public delegate void deviceDisconnected(); 

        /// <summary>
        /// Поступили новые данные
        /// </summary>
        public delegate void newDataAvailable();

        /// <summary>
        /// Изменился статус подключения
        /// </summary>
        public delegate void statusConnectionChanged(ConnectionStates newStatus, ChangeStatusReasons reason);

        public event deviceDisconnected Disconnected;
        public event deviceNotSupported NotSupported;
        public event newDataAvailable NewDataAvailable;
        public event statusConnectionChanged StatusConnectionChanged;


        #endregion end События

        #region Разное

        [global::System.Serializable]
        public class MyException : Exception
        {
            public MyException() { }
            public MyException(string message) : base(message) { }
            public MyException(string message, Exception inner) : base(message, inner) { }
            protected MyException(
              System.Runtime.Serialization.SerializationInfo info,
              System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
        }

        [global::System.Serializable]
        public enum StatusCodes
        {
            Success = 0,
            OK = 0,
            Error_If_Connecting,   // Нельзя менять если Запущен процесс подключения
            Error_If_Connected,    // Нельзя менять если Подключено
            Error_If_Disconnected,    // Нельзя менять если отключено
            Error_IllegalPortNumber,    // Неправильный номер порта

        }

        [global::System.Serializable]
        /// <summary>
        ///  Коды команд, отправляемые датчику
        /// </summary>
        private enum CommandCodes
        {
            ReadSingle  = 0x10, // Считать единичное значение
            SetZero     = 0x99, // Установить ноль в текущем положении
            StreamStart = 0xDD, // Запустить непрерывную передачу значений.
            StreamStop  = 0xDE, // Остановить непрерывную передачу значений.
            // * По идее еще должны быть команды смены скорости передачи. но они
            // нигде не описаны (сильно не искал, пока нет необходимости)
            // ** Для RS-485 вместо ReadSingle - передается адрес
            // *** Как при использшвании RS-485 будут передаваться сразу несколько потоковых
            //      данных - ВОПРОС!!! Возможно по RS485 этот режим не работает

        }

        [global::System.Serializable]
        /// <summary>
        ///  Коды команд, отправляемые датчику
        /// </summary>
        public enum ConnectionStates
        {
            NoConnected = 0,    // Нет подключения
            Connecting = 1,     // Подключение
            Connected = 2,      // Подключено
        }

        [global::System.Serializable]
        /// <summary>
        ///  Причины смены статуса
        /// </summary>
        public enum ChangeStatusReasons
        {
            StartConnecting = 0,    // Начало подключения
            CancellConnecting,      // Подключение прервано - Отмена подключения
            SuccessConnection,      // Подключение завершено успешно
            DeviceNoSupported,      // Подключение завершено - Устройство не поддерживается
            IllegalState,           // В процессе подключение попяли в неопределенное состояние
            PortBusy,               // Полрт занят
        }

        /// <summary>
        /// Структура данных полученных от датчика
        /// Время - что-бы знать когда данные получены, пригодятся для проверки:
        ///         !!! УТОЧНИТЬ !!! Кажется при резком движении датчика данные временно не 
        ///             передаются !!!
        ///       * Время получения данных с порта. Если буфер не опрашивался и заполнен,
        ///         То данные будут отмечены одинаковым временем в момент считывания из буфера.
        /// Значение - результат измерения
        /// </summary>
        public struct MeasuredData
        {
            public DateTime Time;
            public double Value;
            public MeasuredData(DateTime time, double value) {  Time = time; Value = value; }
        }

        private readonly Queue<MeasuredData> _queueMeasuredData = new Queue<MeasuredData>();
        public Queue<MeasuredData> QueueMeasuredData { get { return _queueMeasuredData; } }

        #endregion Разное

        #region serialport 

        /// Работа с object SerialPort
        ///  Input (параметр - номер "COM" порта)
        ///     - port number
        ///  Output 
        ///     States
        ///         - Connected
        ///         - connecting
        ///     New values
        ///         - данные о новом положении
        ///  

        private void Sp()
        {

        }






        private string _tempString = ""; // Строка для получения значения от датчика

        /// <summary>
        /// Обработчик поступащих данных serialPort
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SerialPort1_DataReceived(object sender, System.IO.Ports.SerialDataReceivedEventArgs e)
        {
            //int bytesToRead = serialPort1.BytesToRead;

            while (serialPort1.BytesToRead > 0) 
            { 
                int dataByte = serialPort1.ReadByte();
                // Проверяем полученный байт
                if( dataByte == '-' || dataByte == '+' )
                { // первый байт в посылке
                    _tempString = ((char)dataByte).ToString();
                    continue;
                }

                if (dataByte == 10 )
                { // Последний байт в посылке
                    // байт не добавляем
                    // Делаем проверку, и если все ОК - добавляем в очередь принятых данных

                    // Срока должна быть формата "+99.9999"
                    if( _tempString.Length != 8 ) 
                        { _tempString = ""; continue; } // ошибка, 
                    // МЕняем заточку на запятую
                    string[] strArray = _tempString.Split('.');
                    _tempString = strArray[0]+','+ strArray[1];

                    if ( Double.TryParse(_tempString, out double result))
                    {
                        // Преобразование успешное
                        // добавляем в очередь данных
                        //_queueMeasuredData.Enqueue(new MeasuredData( ));
                        _queueMeasuredData.Enqueue(new MeasuredData( System.DateTime.Now, result));
                        if( _isConnected ) 
                            { NewDataAvailable.Invoke(); }

                        continue;
                    }
                    else
                    {
                        // ошибка преобразования
                        _tempString = ""; continue;
                    }
                }

                if( dataByte == '.' )
                { // разделитель целой и дробной части
                    _tempString += ((char)dataByte).ToString();
                    continue;
                }
                
                if (dataByte >= '0' && dataByte <= '9')
                { // цифры
                    _tempString += ((char)dataByte).ToString();
                    continue;
                }
                
                // ошибочный байт
                _tempString = "";
            }


        }



        #endregion serialport

    }
}
