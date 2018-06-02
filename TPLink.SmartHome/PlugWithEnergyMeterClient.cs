using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;

namespace TPLink.SmartHome
{
    /// <summary>
    /// Provides TP-Link Smart Home client for Power Plug with Energy Meter functionality.
    /// </summary>
    public class PlugWithEnergyMeterClient : PlugClient
    {
        public PlugWithEnergyMeterClient(IPAddress address, ProtocolType protocolType = ProtocolType.Udp) :
            base(address, protocolType)
        {
        }

        public PlugWithEnergyMeterClient(string hostname, ProtocolType protocolType = ProtocolType.Udp) :
            base(hostname, protocolType)
        {
        }

        public async Task<CalibrationInfo> GetCalibrationAsync()
        {
            JToken resultToken = await this.ExecuteAsync("emeter", "get_vgain_igain").ConfigureAwait(false);
            return new CalibrationInfo(
                resultToken.SelectToken("vgain").Value<int>(),
                resultToken.SelectToken("igain").Value<int>());
        }

        public CalibrationInfo GetCalibration()
        {
            return this.GetCalibrationAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public Task SetCalibrationAsync(CalibrationInfo calibrationInfo)
        {
            return this.ExecuteAsync("emeter", "set_vgain_igain",
                new JProperty("vgain", calibrationInfo.VoltageGain),
                new JProperty("igain", calibrationInfo.CurrentGain));
        }

        public void SetCalibration(CalibrationInfo calibrationInfo)
        {
            this.SetCalibrationAsync(calibrationInfo).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public async Task<ConsumptionInfo> GetConsumptionAsync()
        {
            JToken resultToken = await this.ExecuteAsync("emeter", "get_realtime").ConfigureAwait(false);
            return new ConsumptionInfo(
                resultToken.SelectToken("power").Value<decimal>(),
                resultToken.SelectToken("voltage").Value<decimal>(),
                resultToken.SelectToken("current").Value<decimal>());
        }

        public ConsumptionInfo GetConsumption()
        {
            return this.GetConsumptionAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        }
    }
}
