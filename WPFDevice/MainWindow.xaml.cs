using Microsoft.Azure.Devices.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Data.SqlClient;
using Dapper;
using System.Data;
using System.Net.Http;
using System.Net.Http.Json;
using Microsoft.Azure.Devices.Shared;
using Newtonsoft.Json;
using WPFDevice.Models;

namespace WPFDevice
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private readonly string _connect_url = "https://iot-function-app-v73.azurewebsites.net/api/devices/connect?";
        private readonly string _connectionString = "Data Source=(LocalDB)\\MSSQLLocalDB;AttachDbFilename=C:\\Users\\Henri\\source\\repos\\WPFDevice\\WPFDevice\\Data\\device_db.mdf;Integrated Security=True;Connect Timeout=30";
        private DeviceClient _deviceClient;
        private string _deviceName = "intelliFAN";
        private string _deviceType = "fan";
        private string _owner = "Christoffer Korell";
        private string _location = "kitchen";
        private bool _state = false;
        private bool _prevState = false;
        private string _deviceId;
        private int _interval = 1000;
        private bool _connected = false;
        public MainWindow()
        {
            InitializeComponent();
            SetupAsync().ConfigureAwait(false);
            SendMessageAsync().ConfigureAwait(false);
        }

        private async Task SetupAsync()
        {
            tbStateMessage.Text = "Initializing device. Please wait...";

            using IDbConnection conn = new SqlConnection(_connectionString);
            _deviceId = await conn.QueryFirstOrDefaultAsync<string>("SELECT DeviceId FROM DeviceInfo");
            if (string.IsNullOrEmpty(_deviceId))
            {
                tbStateMessage.Text = "Generating new DeviceID";
                _deviceId = Guid.NewGuid().ToString();
                await conn.ExecuteAsync("INSERT INTO DeviceInfo (DeviceId) VALUES (@DeviceId)", new { DeviceId = _deviceId });
            }

            var device_ConnectionString
                = await conn.QueryFirstOrDefaultAsync<string>
                ("SELECT ConnectionString FROM DeviceInfo WHERE DeviceId = @DeviceId",
                new { DeviceId = _deviceId });

            if (string.IsNullOrEmpty(device_ConnectionString))
            {
                tbStateMessage.Text = "Initializing connection string. Please wait...";
                using var http = new HttpClient();
                var result = await http.PostAsJsonAsync(_connect_url, new { _deviceId });
                device_ConnectionString = await result.Content.ReadAsStringAsync();
                await conn.ExecuteAsync("UPDATE DeviceInfo Set ConnectionString = @conn WHERE DeviceId = @DeviceId", new { conn = device_ConnectionString, DeviceId = _deviceId });
            }
            _deviceClient = DeviceClient.CreateFromConnectionString(device_ConnectionString);

            tbStateMessage.Text = "Updating properties. Please wait...";

            var twinCollection = new TwinCollection();
            twinCollection["deviceName"] = _deviceName;
            twinCollection["deviceType"] = _deviceType;
            twinCollection["owner"] = _owner;
            twinCollection["location"] = _location;

            await _deviceClient.UpdateReportedPropertiesAsync(twinCollection);

            _connected = true;

            tbStateMessage.Text = "Device connected.";

        }

        private async Task SendMessageAsync()
        {
            while (true)
            {
                if (_connected)
                {
                    if (_state != _prevState)
                    {
                        _prevState = _state;

                        var json = JsonConvert.SerializeObject(new { state = _state });

                        var message = new Message(Encoding.UTF8.GetBytes(json));
                        message.Properties.Add("deviceName", _deviceName);
                        message.Properties.Add("deviceType", _deviceType);
                        message.Properties.Add("owner", _owner);
                        message.Properties.Add("location", _location);

                        await _deviceClient.SendEventAsync(message);

                        tbStateMessage.Text = $"Message sent at {DateTime.Now}.";

                        var twinCollection = new TwinCollection();
                        twinCollection["state"] = _state;
                        await _deviceClient.UpdateReportedPropertiesAsync(twinCollection);
                    }
                }
                await Task.Delay(_interval);
            }

        }
        private void btnOnOff_Click(object sender, RoutedEventArgs e)
        {
            _state = !_state;

            if (_state)
                btnOnOff.Content = "Turn Off";
            else
                btnOnOff.Content = "Turn On";
        }
    }
}
