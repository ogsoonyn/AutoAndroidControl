using GalaSoft.MvvmLight;
using Reactive.Bindings;
using SharpAdbClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace AutoAndroidControl.ViewModel
{
    /// <summary>
    /// This class contains properties that the main View can data bind to.
    /// <para>
    /// Use the <strong>mvvminpc</strong> snippet to add bindable properties to this ViewModel.
    /// </para>
    /// <para>
    /// You can also use Blend to data bind with the tool's support.
    /// </para>
    /// <para>
    /// See http://www.galasoft.ch/mvvm
    /// </para>
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        /// <summary>
        /// Initializes a new instance of the MainViewModel class.
        /// </summary>
        public MainViewModel()
        {
            ////if (IsInDesignMode)
            ////{
            ////    // Code runs in Blend --> create design time data.
            ////}
            ////else
            ////{
            ////    // Code runs "for real"
            ////}
            ///
            RunAutomaticCommand = new ReactiveCommand();
            RunAutomaticCommand.Subscribe(_ => {
                if (IsRunning.Value)
                {
                    ShouldStop.Value = true;
                }
                else
                {
                    Execute();

                }
            });

            TapOnceCommand = new ReactiveCommand();
            TapOnceCommand.Subscribe(_ => TapOnce());

            StartButtonText = IsRunning.Select(x => (x ? "Stop" : "Start")).ToReactiveProperty();

            SearchAdbExec();
            AdbInit();
            SearchDevices();
        }

        //private DeviceData _android = null;
        private string ADBPath = @".\adb\adb.exe";
        //public List<DeviceData> DeviceList { get; set; } = new List<DeviceData>();

        #region ADB

        /// <summary>
        /// adb.exe���C���X�g�[������Ă��邩�𒲂ׂ�B
        /// �C���X�g�[������Ă���΁AADBPath���X�V����
        /// </summary>
        private void SearchAdbExec()
        {
            // where�R�}���h�Ŏ��s�t�@�C���̃p�X�����o
            string path = ExecExCommand("where.exe", "adb.exe");
            path = path.Replace(Environment.NewLine, "");

            if (File.Exists(path))
            {
                //LogMessages.AddT("adb.exe�̃C���X�g�[�������o���܂���");
                ADBPath = path;
            }
            else
            {
                //LogMessages.AddT(EBookCommon.LogLevel.Warning, @"adb.exe���C���X�g�[������Ă��܂���");
            }
            //LogMessages.AddT("adb: " + ADBPath);
        }
        private String ExecExCommand(string execFileName, string argument)
        {
            String ret = "";

            Process p = new Process();
            p.StartInfo.FileName = execFileName;

            p.StartInfo.UseShellExecute = false;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = false;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.Arguments = argument;

            p.Start();

            ret = p.StandardOutput.ReadToEnd();

            p.WaitForExit();
            p.Close();

            return ret;
        }

        /// <summary>
        /// �g�p����Android�f�o�C�X
        /// </summary>
        /*
        public DeviceData AndroidDevice
        {
            get { return _android; }
            set
            {
                if (value != null && _android != value)
                {
                    _android = value;
                    //LogMessages.AddT("�f�o�C�X �u" + AndroidDevice.Model + "�v ���g�p���܂�");
                    GetAndroidDisplaySize();
                    RaisePropertyChanged(() => AndroidDevice);
                }
            }
        }
        */

        /// <summary>
        /// ADB�T�[�o�̏�����
        /// </summary>
        public void AdbInit()
        {
            AdbServer adbServer = new AdbServer();
            adbServer.StartServer(ADBPath, false);
        }

        /// <summary>
        /// AndroidDevice���ݒ肳��Ă���΁A���̃f�o�C�X���ڑ�����Ă��邩���ׂ�
        /// AndroidDevice���ݒ肳��Ă��Ȃ���΁A�f�o�C�X��T�����Đݒ肷��
        /// </summary>
        /// <returns>AndroidDevice�����݂��邩</returns>
        private bool SearchDevices()
        {
            var devices = AdbClient.Instance.GetDevices();
            DeviceList.Value = new List<DeviceData>();

            // devices����Ȃ�AAndroidDevice��null��
            if (devices.Count == 0)
            {
                AndroidDevice.Value = null;
                //if (showLog) LogMessages.AddT(EBookCommon.LogLevel.Warning, "Android�f�o�C�X��������܂���");
                //RaisePropertyChanged(() => DeviceList);
                return false;
            }

            int index = 0;
            foreach (DeviceData dev in devices)
            {
                //if (showLog) LogMessages.AddT("[Device " + index + "] Model: " + dev.Model + ", Serial: " + dev.Serial);

                if (dev.State == DeviceState.Unauthorized)
                {
                    //if (showLog) LogMessages.AddT(EBookCommon.LogLevel.Error, "Device unauthorized. USB�f�o�b�O�������Ă��������I");
                }
                else
                {
                    DeviceList.Value.Add(dev);
                }

                index++;
            }

            // Unauthorized���������f�o�C�X���X�g����A���ݐݒ肵�Ă���f�o�C�X���L�邩���ׂ�
            bool found = false;
            foreach (DeviceData dev in DeviceList.Value)
            {
                if (AndroidDevice.Value != null)
                {
                    if (dev.Serial == AndroidDevice.Value.Serial)
                    {
                        AndroidDevice.Value = dev;
                        found = true;
                        break;
                    }
                }
            }

            // AndroidDevice��������Ȃ���΁A���������f�o�C�X�̍ŏ��̂ЂƂ�ݒ肷��B
            if (!found && DeviceList.Value.Count > 0)
            {
                AndroidDevice.Value = DeviceList.Value.First();
            }

            //RaisePropertyChanged(() => DeviceList);
            return true;
        }

        /// <summary>
        /// ADB�R�}���h�����s
        /// </summary>
        /// <param name="command">�R�}���h��</param>
        /// <returns>�R�}���h�o�͕�����</returns>
        private String AdbShell(String command)
        {
            if (AndroidDevice.Value != null)
            {
                var receiver = new ConsoleOutputReceiver();
                try
                {
                    AdbClient.Instance.ExecuteRemoteCommand(command, AndroidDevice.Value, receiver);
                }
                catch (SharpAdbClient.Exceptions.AdbException e)
                {
                    Debug.Print(e.ToString());
                }
                return receiver.ToString();
            }

            return "";
        }

        /// <summary>
        /// ADB Pull�����s
        /// </summary>
        /// <param name="from">Android�f�o�C�X���̃t�@�C���p�X</param>
        /// <param name="to">�R�s�[��̃t�@�C���p�X</param>
        private void AdbPull(string from, string to)
        {
            if (AndroidDevice.Value != null)
            {
                try
                {
                    using (SyncService service = new SyncService(new AdbSocket(new IPEndPoint(IPAddress.Loopback, AdbClient.AdbServerPort)), AndroidDevice.Value))
                    using (Stream stream = File.OpenWrite(to))
                    {
                        service.Pull(from, stream, null, CancellationToken.None);
                    }
                }
                catch (SharpAdbClient.Exceptions.AdbException e)
                {
                    Debug.Print(e.ToString());
                }
            }
        }

        /// <summary>
        /// Android�f�o�C�X�̉𑜓x�������擾
        /// </summary>
        private void GetAndroidDisplaySize()
        {
            String wmSize = AdbShell("wm size");

            if (wmSize.Length > 0)
            {
                MatchCollection matchedObj = Regex.Matches(wmSize, "[0123456789]+");

                if (matchedObj.Count >= 2)
                {
                    int first = Int32.Parse(matchedObj[0].Value);
                    int second = Int32.Parse(matchedObj[1].Value);

                    if (first > second)
                    {
                        DeviceHeight.Value = first;
                        DeviceWidth.Value = second;
                    }
                    else
                    {
                        DeviceWidth.Value = first;
                        DeviceHeight.Value = second;
                    }
//                    LogMessages.AddT("�𑜓x�����o�F" + _deviceWidth + "x" + _deviceHeight);
                }
            }
        }
        #endregion

        private void Execute()
        {
            ShouldStop.Value = false;
            IsRunning.Value = true;

            Task.Run(() => { 
                while (!ShouldStop.Value)
                {
                    // adb shell input tap X Y
                    TapOnce();

                    // sleep
                    Progress.Value = 0;
                    for(int i=0; i<Interval.Value*10; i++)
                    {
                        if (ShouldStop.Value) break;
                        Thread.Sleep(100);
                        Progress.Value += 10.0 / Interval.Value;
                    }
                }
                Progress.Value = 0;
                IsRunning.Value = false;
            });
        }

        private void TapOnce()
        {
            AdbShell("input tap " + XCoord.Value + " " + YCoord.Value);
        }

        public ReactiveProperty<int> XCoord { get; set; } = new ReactiveProperty<int>(250);
        public ReactiveProperty<int> YCoord { get; set; } = new ReactiveProperty<int>(1850);
        public ReactiveProperty<int> Interval { get; set; } = new ReactiveProperty<int>(10);
        public ReactiveProperty<double> Progress { get; private set; } = new ReactiveProperty<double>(0);

        public ReactiveProperty<bool> ShouldStop { get; private set; } = new ReactiveProperty<bool>(false);
        public ReactiveProperty<bool> IsRunning { get; private set; } = new ReactiveProperty<bool>(false);

        public ReactiveProperty<string> StartButtonText { get; }


        public ReactiveProperty<List<DeviceData>> DeviceList { get; private set; } = new ReactiveProperty<List<DeviceData>>();
        public ReactiveProperty<DeviceData> AndroidDevice { get; private set; } = new ReactiveProperty<DeviceData>();
        public ReactiveProperty<int> DeviceHeight { get; private set; } = new ReactiveProperty<int>(1920);
        public ReactiveProperty<int> DeviceWidth { get; private set; } = new ReactiveProperty<int>(1080);

        public ReactiveCommand RunAutomaticCommand { get; private set; }

        public ReactiveCommand TapOnceCommand { get; private set; }

    }
}