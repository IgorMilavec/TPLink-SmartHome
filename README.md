# TP-Link Smart Home Library for .NET

This library provides the client for TP-Link Smart Home devices. The TP-Link Smart Home protocol is proprietary; this code is based on the article [Reverse Engineering the TP-Link HS110](https://www.softscheck.com/en/reverse-engineering-tp-link-hs110/) and controbutors' own experiments.

The library is built for .NET Standard 1.3.

## Supported devices
 * Smart Plugs
   * HS100 Wi-Fi Smart Plug
   * HS110 Wi-Fi Smart Plug with Energy Monitoring

## Transport protocol
The library supports both UDP (default) and TCP transports. Please note that I was unable to send multiple consecutive commands over a single TCP connection, so usage of TCP protocol at this time only brings additional overhead. One advantage of TCP would be support for larger payloads, however I have not yet observed a protocol command or response that would not fit into a single UDP packet.

If you wish to discover devices in remote networks, please note that you need to configure directed broadcasts for target network.

# License

This library is licensed under the [GNU Lesser General Public License v3.0](https://github.com/IgorMilavec/TPLink-SmartHome/blob/master/LICENSE).

This library uses external libraries that have their own respective licenses:
 * [Json.NET](https://github.com/JamesNK/Newtonsoft.Json)
