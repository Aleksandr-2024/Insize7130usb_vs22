using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net.Configuration;
using System.Text;
using System.Threading.Tasks;

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
            // Препятствий нет - запускаем подключение

            _isConnecting = true;
            // Запускаем поток подключения





            //Connected.Invoke(); // DEBUG
            return StatusCodes.Success; // подключение успешно запущено
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

                _isConnected = false;  // TODO: DEBUG
            }
            else if( _isConnecting )
            {   // процесс подключения - отключаемся...

                _isConnecting = false; // TODO: DEBUG
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


        public event deviceDisconnected Disconnected;
        public event deviceNotSupported NotSupported;
        public event newDataAvailable NewDataAvailable;


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








        #endregion serialport


    }
}
