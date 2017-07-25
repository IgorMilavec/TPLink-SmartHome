// https://github.com/aspnet/BasicMiddleware/blob/master/src/Microsoft.AspNetCore.HttpOverrides/IPNetwork.cs
// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

// This class was taken from ASP.NET Core source repo and amended for my purposes.
// ToDo: Cleanup and submit PR in original repo, propose move to CoreFX

using System;
using System.Net;
using System.Net.Sockets;

namespace TPLink.SmartHome
{
    public class IPNetwork
    {
        public IPNetwork(IPAddress prefix, int prefixLength)
        {
            Prefix = prefix;
            PrefixLength = prefixLength;
            PrefixBytes = Prefix.GetAddressBytes();
            Mask = CreateMask();
        }

        public IPAddress Prefix { get; }

        private byte[] PrefixBytes { get; }

        /// <summary>
        /// The CIDR notation of the subnet mask 
        /// </summary>
        public int PrefixLength { get; }

        private byte[] Mask { get; }

        public IPAddress BroadcastAddress
        {
            get
            {
                byte[] bytes = Prefix.GetAddressBytes();
                for (int i = 0; i < PrefixBytes.Length; i++)
                {
                    bytes[i] |= (byte)~Mask[i];
                }

                return new IPAddress(bytes);
            }
        }

        public bool Contains(IPAddress address)
        {
            if (Prefix.AddressFamily != address.AddressFamily)
            {
                return false;
            }

            var addressBytes = address.GetAddressBytes();
            for (int i = 0; i < PrefixBytes.Length && Mask[i] != 0; i++)
            {
                if (PrefixBytes[i] != (addressBytes[i] & Mask[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private byte[] CreateMask()
        {
            var mask = new byte[PrefixBytes.Length];
            int remainingBits = PrefixLength;
            int i = 0;
            while (remainingBits >= 8)
            {
                mask[i] = 0xFF;
                i++;
                remainingBits -= 8;
            }
            if (remainingBits > 0)
            {
                mask[i] = (byte)(0xFF << (8 - remainingBits));
            }

            return mask;
        }

        public static IPNetwork Parse(string network)
        {
            if (string.IsNullOrEmpty(network))
            {
                throw new ArgumentException();
            }

            string[] networkParts = network.Split('/');
            if (networkParts.Length > 2)
            {
                throw new FormatException();
            }

            IPAddress networkAddress = IPAddress.Parse(networkParts[0]);
            int prefixlength;
            if (networkParts.Length > 1)
            {
                prefixlength = int.Parse(networkParts[1]);
            }
            else
            {
                switch (networkAddress.AddressFamily)
                {
                    case AddressFamily.InterNetwork:
                        prefixlength = 32;
                        break;

                    case AddressFamily.InterNetworkV6:
                        prefixlength = 128;
                        break;

                    default:
                        throw new NotSupportedException();
                }
            }

            return new IPNetwork(networkAddress, prefixlength);
        }
    }
}