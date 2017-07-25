using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Linq;

namespace TPLink.SmartHome
{
    /// <summary>
    /// Provides TP-Link Smart Home client for common functionality.
    /// </summary>
    public class Client
    {
        protected delegate Task<string> ExecuteAsyncHandler(string command);

        private const int RemotePort = 9999;
        private readonly static TimeSpan DiscoveryTimeout = TimeSpan.FromSeconds(5);
        private readonly static TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(5);
        private readonly static IDictionary<int, TimeZoneInfo> TZDictionary = new Dictionary<int, TimeZoneInfo>();

        private readonly string hostname;
        private readonly ExecuteAsyncHandler ExecuteAutoAsync;

        public Client(IPAddress address, ProtocolType protocolType = ProtocolType.Udp)
        {
            this.hostname = address.ToString();
            switch (protocolType)
            {
                case ProtocolType.Udp:
                    this.ExecuteAutoAsync = this.ExecuteUDPAsync;
                    break;
                case ProtocolType.Tcp:
                    this.ExecuteAutoAsync = this.ExecuteTCPAsync;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(protocolType));
            }
        }

        public Client(string hostname, ProtocolType protocolType = ProtocolType.Udp)
        {
            this.hostname = hostname;
            switch (protocolType)
            {
                case ProtocolType.Udp:
                    this.ExecuteAutoAsync = this.ExecuteUDPAsync;
                    break;
                case ProtocolType.Tcp:
                    this.ExecuteAutoAsync = this.ExecuteTCPAsync;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(protocolType));
            }
        }

        private static void EncodeMessage(string message, IList<byte> messageHeaderBuffer, IList<byte> messageBodyBuffer)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            int messageLength = message.Length;

            if (messageBodyBuffer == null)
            {
                throw new ArgumentNullException(nameof(messageBodyBuffer));
            }

            if (messageBodyBuffer.Count < messageLength)
            {
                throw new ArgumentException("The buffer is too small.", nameof(messageBodyBuffer));
            }

            if (messageHeaderBuffer != null)
            {
                if (messageHeaderBuffer.Count < 4)
                {
                    throw new ArgumentException("The buffer is too small.", nameof(messageHeaderBuffer));
                }

                messageHeaderBuffer[0] = (byte)(messageLength >> 24);
                messageHeaderBuffer[1] = (byte)(messageLength >> 16);
                messageHeaderBuffer[2] = (byte)(messageLength >> 8);
                messageHeaderBuffer[3] = (byte)(messageLength);
            }

            int key = 171;
            int i = 0;
            foreach (char val in message)
            {
                key = messageBodyBuffer[i++] = (byte)(key ^ val);
            }
        }

        private static string DecodeMessage(IList<byte> messageHeader, IList<byte> messageBody)
        {
            if (messageBody == null)
            {
                throw new ArgumentNullException(nameof(messageBody));
            }

            int messageBodyLength = messageBody.Count;
            if (messageBodyLength == 0)
            {
                return string.Empty;
            }

            int key = 171;
            char[] result = new char[messageBodyLength];
            for (int i = 0; i < messageBodyLength; i++)
            {
                result[i] = (char)(key ^ messageBody[i]);
                key = messageBody[i];
            }

            return new string(result);
        }

        private static JToken ParseResponse(string response, string command)
        {
            JToken responseToken = JObject.Parse(response);
            JToken resultToken = responseToken.SelectToken(command);
            if (resultToken == null)
            {
                throw new System.IO.InvalidDataException("The device has sent an invalid response.");
            }

            JToken errCodeToken = resultToken.SelectToken("err_code");
            if (errCodeToken != null)
            {
                int errCode = errCodeToken.Value<int>();
                if (errCode != 0)
                {
                    JToken errMsgToken = resultToken.SelectToken("err_msg");
                    throw new Exception(string.Format("The device has reported error {0}: {1}.", errCode, errMsgToken?.Value<string>()));
                }
            }

            return resultToken;
        }

        public async Task<JToken> ExecuteAsync(string @object, string member, params JToken[] parameters)
        {
            JContainer request = new JObject(
                new JProperty(@object, new JObject(
                    new JProperty(member, new JObject(parameters))
                ))
            );

            return ParseResponse(
                await this.ExecuteAutoAsync(request.ToString(Formatting.None)),
                @object + "." + member);
        }

        private static SystemInfo ParseSystemInformation(JToken resultToken)
        {
            SystemType type = SystemType.Unknown;
            switch (resultToken.SelectToken("type").Value<string>())
            {
                case "IOT.SMARTPLUGSWITCH":
                    type = SystemType.Plug;
                    if (resultToken.SelectToken("feature").Value<string>().Contains("ENE"))
                    {
                        type = SystemType.PlugWithEnergyMeter;
                    }
                    break;
            }

            return new SystemInfo(
                resultToken.SelectToken("alias").Value<string>(),
                resultToken.SelectToken("deviceId").Value<string>(),
                type,
                resultToken.SelectToken("model").Value<string>(),
                resultToken.SelectToken("sw_ver").Value<string>(),
                resultToken.SelectToken("latitude").Value<decimal>(),
                resultToken.SelectToken("longitude").Value<decimal>());
        }

        private async Task<string> ExecuteTCPAsync(string command)
        {
            // We need to use a new TcpClient for each request, as the device terminates the connection
            using (TcpClient tcpClient = new TcpClient())
            {
                tcpClient.NoDelay = true;
                await tcpClient.ConnectAsync(this.hostname, RemotePort);
                using (NetworkStream tcpStream = tcpClient.GetStream())
                {
                    // Write encrypted request
                    byte[] requestMessage = new byte[4 + command.Length];
                    Client.EncodeMessage(command, new ArraySegment<byte>(requestMessage, 0, 4), new ArraySegment<byte>(requestMessage, 4, command.Length));
                    await tcpStream.WriteAsync(requestMessage, 0, requestMessage.Length);

                    // Read response header
                    byte[] responseMessageHeader = new byte[4];
                    if (await tcpStream.ReadBlockAsync(responseMessageHeader, 0, 4) < 4)
                    {
                        throw new Exception("The stream was closed by remote party");
                    }

                    // Read response body
                    int responseMessageBodyLength = (responseMessageHeader[0] << 24) + (responseMessageHeader[1] << 16) + (responseMessageHeader[2] << 8) + responseMessageHeader[3];
                    byte[] responseMessageBody = new byte[responseMessageBodyLength];
                    if (await tcpStream.ReadBlockAsync(responseMessageBody, 0, responseMessageBodyLength) < responseMessageBodyLength)
                    {
                        throw new Exception("The stream was closed by remote party");
                    }

                    // Return decrypted response
                    return Client.DecodeMessage(responseMessageHeader, responseMessageBody);
                }
            }
        }

        private async Task<string> ExecuteUDPAsync(string command)
        {
            using (UdpClient udpClient = new UdpClient())
            {
                // Send the request
                byte[] requestMessage = new byte[command.Length];
                Client.EncodeMessage(command, null, requestMessage);
                await udpClient.SendAsync(requestMessage, requestMessage.Length, this.hostname, RemotePort);

                // Wait for the response
                Task<UdpReceiveResult> udpReceiveResultTask = udpClient.ReceiveAsync();
                await Task.WhenAny(udpReceiveResultTask, Task.Delay(ReceiveTimeout));
                if (udpReceiveResultTask.IsCompleted)
                {
                    return DecodeMessage(null, udpReceiveResultTask.Result.Buffer);
                }

                // Resend and wait for the response
                await udpClient.SendAsync(requestMessage, requestMessage.Length, this.hostname, RemotePort);
                await Task.WhenAny(udpReceiveResultTask, Task.Delay(ReceiveTimeout));
                if (udpReceiveResultTask.IsCompleted)
                {
                    return DecodeMessage(null, udpReceiveResultTask.Result.Buffer);
                }

                throw new TimeoutException();
            }
        }

        public virtual async Task<SystemInfo> GetSystemInfoAsync()
        {
            return ParseSystemInformation(await this.ExecuteAsync("system", "get_sysinfo"));
        }

        public Task SetNameAsync(string name)
        {
            return this.ExecuteAsync("system", "set_dev_alias",
                new JProperty("alias", name));
        }

        public async Task SetWirelessCredentialsAsync(string ssid, string password)
        {
            int keyType = -1;
            JToken resultToken = await this.ExecuteAsync("netif", "get_scaninfo", new JProperty("refresh", 1));
            foreach (JToken wirelessInfo in resultToken.SelectTokens("ap_list[*]"))
            {
                if (string.Compare(wirelessInfo.SelectToken("ssid").Value<string>(), ssid, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    ssid = wirelessInfo.SelectToken("ssid").Value<string>();
                    keyType = wirelessInfo.SelectToken("key_type").Value<int>();
                    break;
                }
            }

            if (keyType == -1)
            {
                throw new KeyNotFoundException("The device is not in range of the specified wireless network.");
            }

            resultToken = await this.ExecuteAsync("netif", "set_stainfo",
                new JProperty("ssid", ssid),
                new JProperty("password", password),
                new JProperty("key_type", keyType));
        }

        public async Task FlashFirmwareAsync(Uri firmwareLocation)
        {
            // Check system status
            JToken resultToken = await this.ExecuteAsync("system", "get_sysinfo");
            if (resultToken.SelectToken("updating").Value<int>() != 0)
            {
                throw new InvalidOperationException("The device is already updating.");
            }

            // Instruct the device to download the firmware
            await this.ExecuteAsync("system", "download_firmware",
                new JProperty("url", firmwareLocation.ToString()));

            // Wait for the device to download the firmware
            while (true)
            {
                await Task.Delay(1000);

                resultToken = await this.ExecuteAsync("system", "get_download_state");
                if (resultToken.SelectToken("ratio").Value<int>() == 100)
                {
                    break;
                }
            }

            // Flash the new firmware
            await this.ExecuteAsync("system", "flash_firmware");
        }

        public Task RebootAsync()
        {
            return this.ExecuteAsync("system", "reboot",
                new JProperty("delay", 1));
        }

        public async Task<DateTimeOffset> GetTimeAsync()
        {
            JToken resultToken = await this.ExecuteAsync("time", "get_timezone");
            int index = resultToken.SelectToken("index").Value<int>();
            TimeZoneInfo timeZoneInfo;
            if (!TZDictionary.TryGetValue(index, out timeZoneInfo))
            {
                timeZoneInfo = TimeZoneInformationExtensions.FromPOSIXString(resultToken.SelectToken("tz_str").Value<string>());
                TZDictionary.Add(index, timeZoneInfo);
            }

            resultToken = await this.ExecuteAsync("time", "get_time");
            DateTime dateTime = new DateTime(
                resultToken.SelectToken("year").Value<int>(), resultToken.SelectToken("month").Value<int>(), resultToken.SelectToken("mday").Value<int>(),
                resultToken.SelectToken("hour").Value<int>(), resultToken.SelectToken("min").Value<int>(), resultToken.SelectToken("sec").Value<int>());
            return new DateTimeOffset(
                dateTime.Year, dateTime.Month, dateTime.Day,
                dateTime.Hour, dateTime.Minute, dateTime.Second,
                timeZoneInfo.GetUtcOffset(dateTime));
        }

        public async Task SetTimeAsync(DateTime time)
        {
            if ((time.Kind != DateTimeKind.Utc) && (time.Kind != DateTimeKind.Local))
            {
                throw new ArgumentException("DateTime.Kind must be either Utc or Local.", nameof(time));
            }

            JToken resultToken = await this.ExecuteAsync("time", "get_timezone");
            int index = resultToken.SelectToken("index").Value<int>();
            TimeZoneInfo timeZoneInfo;
            if (!TZDictionary.TryGetValue(index, out timeZoneInfo))
            {
                timeZoneInfo = TimeZoneInformationExtensions.FromPOSIXString(resultToken.SelectToken("tz_str").Value<string>());
                TZDictionary.Add(index, timeZoneInfo);
            }

            //time = time.ToUniversalTime();
            time = TimeZoneInfo.ConvertTime(time, timeZoneInfo);

            await this.ExecuteAsync("time", "set_timezone",
                new JProperty("year", time.Year),
                new JProperty("month", time.Month),
                new JProperty("mday", time.Day),
                new JProperty("hour", time.Hour),
                new JProperty("min", time.Minute),
                new JProperty("sec", time.Second),
                new JProperty("index", index));
        }

        public async Task<CloudInfo> GetCloudInfoAsync()
        {
            JToken resultToken = await this.ExecuteAsync("cnCloud", "get_info");
            return new CloudInfo(
                resultToken.SelectToken("binded").Value<int>() == 1 ? resultToken.SelectToken("username").Value<string>() : null
            );
        }

        public async Task SetCloudCredentialsAsync(string username, string password)
        {
            await this.ExecuteAsync("cnCloud", "bind",
                new JProperty("username", username),
                new JProperty("password", password));
        }

        public static Task Discover(Action<IPAddress, SystemInfo> onDeviceFound)
        {
            return Discover(new IPAddress[] { IPAddress.Broadcast }, onDeviceFound, DiscoveryTimeout);
        }

        public static Task Discover(Action<IPAddress, SystemInfo> onDeviceFound, TimeSpan timeout)
        {
            return Discover(new IPAddress[] { IPAddress.Broadcast }, onDeviceFound, timeout);
        }

        public static Task Discover(IPNetwork networkAddress, Action<IPAddress, SystemInfo> onDeviceFound)
        {
            return Discover(new IPAddress[] { networkAddress.BroadcastAddress }, onDeviceFound, DiscoveryTimeout);
        }

        public static Task Discover(IPNetwork networkAddress, Action<IPAddress, SystemInfo> onDeviceFound, TimeSpan timeout)
        {
            return Discover(new IPAddress[] { networkAddress.BroadcastAddress }, onDeviceFound, timeout);
        }

        public static Task Discover(IEnumerable<IPNetwork> networkAddressList, Action<IPAddress, SystemInfo> onDeviceFound)
        {
            return Discover(networkAddressList.Select(network => network.BroadcastAddress), onDeviceFound, DiscoveryTimeout);
        }

        public static Task Discover(IEnumerable<IPNetwork> networkAddressList, Action<IPAddress, SystemInfo> onDeviceFound, TimeSpan timeout)
        {
            return Discover(networkAddressList.Select(network => network.BroadcastAddress), onDeviceFound, timeout);
        }

        private static async Task Discover(IEnumerable<IPAddress> broadcastAddressList, Action<IPAddress, SystemInfo> onDeviceFound, TimeSpan timeout)
        {
            using (UdpClient client = new UdpClient())
            {
                // Send the discovery message
                client.EnableBroadcast = true;
                string requestContent = "{\"system\":{\"get_sysinfo\":{}}}";
                byte[] requestMessage = new byte[requestContent.Length];
                Client.EncodeMessage(requestContent, null, requestMessage);
                foreach (IPAddress broadcastAddress in broadcastAddressList)
                {
#pragma warning disable 4014
                    client.SendAsync(requestMessage, requestMessage.Length, new IPEndPoint(broadcastAddress, RemotePort)).ConfigureAwait(false);
#pragma warning restore 4014
                }

                Task timeoutTask = Task.Delay(timeout);
                while (true)
                {
                    Task<UdpReceiveResult> udpReceiveResultTask = client.ReceiveAsync();
                    await Task.WhenAny(timeoutTask, udpReceiveResultTask, Task.Delay(2000));

                    if (timeoutTask.IsCompleted)
                    {
                        break;
                    }

                    if (udpReceiveResultTask.IsCompleted)
                    {
                        UdpReceiveResult udpReceiveResult = udpReceiveResultTask.Result;
                        string response = DecodeMessage(null, udpReceiveResult.Buffer);
                        onDeviceFound(udpReceiveResult.RemoteEndPoint.Address, ParseSystemInformation(ParseResponse(response, "system.get_sysinfo")));
                    }
                    else
                    {
                        // Nothing received for 2s, retransmit the discovery packet
                        foreach (IPAddress broadcastAddress in broadcastAddressList)
                        {
#pragma warning disable 4014
                            client.SendAsync(requestMessage, requestMessage.Length, new IPEndPoint(broadcastAddress, RemotePort)).ConfigureAwait(false);
#pragma warning restore 4014
                        }
                    }
                }
            }
        }
    }

    internal static class StreamExtensions
    {
        public static int ReadBlock(this System.IO.Stream stream, byte[] buffer, int offset, int count)
        {
            int bufferLength = 0;
            while (bufferLength < count)
            {
                int readLength = stream.Read(buffer, offset + bufferLength, count - bufferLength);
                if (readLength == 0)
                {
                    break;
                }
                bufferLength += readLength;
            }

            return bufferLength;
        }

        public static async Task<int> ReadBlockAsync(this System.IO.Stream stream, byte[] buffer, int offset, int count)
        {
            int bufferLength = 0;
            while (bufferLength < count)
            {
                int readLength = await stream.ReadAsync(buffer, offset + bufferLength, count - bufferLength);
                if (readLength == 0)
                {
                    break;
                }
                bufferLength += readLength;
            }

            return bufferLength;
        }
    }

    internal static class TimeZoneInformationExtensions
    {
        private readonly static Regex tzRegex = new Regex(@"^(?<StandardName>[A-Z]{3,})(?<OffsetHour>[+-]?\d{1,2})(?::(?<OffsetMinute>\d{2}(?::(?<OffsetSecond>\d{2}))?))?(?<DaylightName>[A-Z]{3,})(?:,)(?:M)(?<StartMonth>\d{1,2})(?:.)(?<StartWeek>\d)(?:.)(?<StartDay>\d)(?:/(?<StartHour>\d{1,2})(?::(?<StartMinute>\d{2}(?::(?<StartSecond>\d{2}))?))?)?(?:,)(?:M)(?<EndMonth>\d{1,2})(?:.)(?<EndWeek>\d)(?:.)(?<EndDay>\d)(?:/(?<EndHour>\d{1,2})(?::(?<EndMinute>\d{2}(?::(?<EndSecond>\d{2}))?))?)?$", RegexOptions.Compiled);

        public static TimeZoneInfo FromPOSIXString(string tz)
        {
            return TimeZoneInfo.Local;
            //Match match = tzRegex.Match(tz);
            //if (!match.Success)
            //{
            //    throw new FormatException(string.Format("The string '{0}' could not be parsed as a valid POSIX timezone.", tz));
            //}

            //TimeSpan offset = new TimeSpan(
            //    match.Groups["OffsetHour"].Success ? int.Parse(match.Groups["OffsetHour"].Value) : 0,
            //    match.Groups["OffsetMinute"].Success ? int.Parse(match.Groups["OffsetMinute"].Value) : 0,
            //    match.Groups["OffsetSecond"].Success ? int.Parse(match.Groups["OffsetSecond"].Value) : 0);

            //TimeZoneInfo.TransitionTime startTransition = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
            //    new DateTime(1, 1, 1,
            //        match.Groups["StartHour"].Success ? int.Parse(match.Groups["StartHour"].Value) : 2,
            //        match.Groups["StartMinute"].Success ? int.Parse(match.Groups["StartMinute"].Value) : 0,
            //        match.Groups["StartSecond"].Success ? int.Parse(match.Groups["StartSecond"].Value) : 0),
            //    int.Parse(match.Groups["StartMonth"].Value),
            //    int.Parse(match.Groups["StartWeek"].Value),
            //    (DayOfWeek)int.Parse(match.Groups["StartDay"].Value));

            //TimeZoneInfo.TransitionTime endTransition = TimeZoneInfo.TransitionTime.CreateFloatingDateRule(
            //    new DateTime(1, 1, 1,
            //        match.Groups["EndHour"].Success ? int.Parse(match.Groups["EndHour"].Value) : 2,
            //        match.Groups["EndMinute"].Success ? int.Parse(match.Groups["EndMinute"].Value) : 0,
            //        match.Groups["EndSecond"].Success ? int.Parse(match.Groups["EndSecond"].Value) : 0),
            //    int.Parse(match.Groups["EndMonth"].Value),
            //    int.Parse(match.Groups["EndWeek"].Value),
            //    (DayOfWeek)int.Parse(match.Groups["EndDay"].Value));

            //TimeZoneInfo.AdjustmentRule[] adjustments = {
            //    TimeZoneInfo.AdjustmentRule.CreateAdjustmentRule(
            //        new DateTime(1999, 10, 1),
            //        DateTime.MaxValue.Date,
            //        TimeSpan.FromHours(1),
            //        startTransition,
            //        endTransition)
            // };

            //return TimeZoneInfo.CreateCustomTimeZone(tz, -offset,
            //    match.Groups["StandardName"].Value + " / " + match.Groups["DaylightName"].Value,
            //    match.Groups["StandardName"].Value,
            //    match.Groups["DaylightName"].Value,
            //    adjustments);
        }
    }
}
