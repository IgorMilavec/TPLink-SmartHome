namespace TPLink.SmartHome
{
    /// <summary>
    /// Holds information about device's cloud binding.
    /// </summary>
    public sealed class CloudInfo
    {
        internal CloudInfo(string username)
        {
            this.Username = username;
        }

        public string Username { get; }
    }
}
