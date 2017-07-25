namespace TPLink.SmartHome
{
    /// <summary>
    /// Describes the device type
    /// </summary>
    public enum SystemType
    {
        Unknown,
        Plug,
        PlugWithEnergyMeter,
        Bulb
    }

    /// <summary>
    /// Holds system information about the device.
    /// </summary>
    public class SystemInfo
    {
        protected internal SystemInfo(string name, string id, SystemType type, string model, string firmwareVersion, decimal locationLat, decimal locationLon)
        {
            this.Name = name;
            this.Id = id;
            this.Type = type;
            this.Model = model;
            this.FirmwareVersion = firmwareVersion;
            this.LocationLat = locationLat;
            this.LocationLon = locationLon;
        }

        public string Name { get; }

        public string Id { get; }

        public SystemType Type { get; }

        public string Model { get; }

        public string FirmwareVersion { get; }

        public decimal LocationLat { get; }

        public decimal LocationLon { get; }

        public override string ToString()
        {
            return Type.ToString() + " " + Name;
        }
    }
}
