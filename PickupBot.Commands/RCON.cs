using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace PickupBot.Commands
{
    // ReSharper disable once InconsistentNaming
    public static class RCON
    {
        public static async Task<string> SendCommand(string rconCommand, string host, string password, int gameServerPort)
        {
            //connecting to server
            using var client = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            client.Connect(host, gameServerPort);

            var command = $"rcon {password} {rconCommand}{Environment.NewLine}{Environment.NewLine}";

            //pad first 4 bytes for later use
            var buffer = Encoding.ASCII.GetBytes($"xxxx{command}");

            //intial 4 characters as per standard
            buffer[0] = byte.Parse("255");
            buffer[1] = byte.Parse("255");
            buffer[2] = byte.Parse("255");
            buffer[3] = byte.Parse("255");

            //send rcon command and get response
            await client.SendAsync(buffer, SocketFlags.None);

            //big enough to receive response
            var bufferRec = new byte[65000];
            client.Receive(bufferRec);
            return Encoding.ASCII.GetString(bufferRec)?
                .Replace(password, "[PASSWORD]")
                .Replace("????print\n", "")
                .Replace("\0", "");
        }
        
        // ReSharper disable once InconsistentNaming
        public static async Task<string> UDPSendCommand(string rconCommand, string host, string password, int gameServerPort, bool skipReceive = false)
        {
            //connecting to server
            using var client = new UdpClient();
            client.Connect(host, gameServerPort);

            var command = $"rcon {password} {rconCommand}{Environment.NewLine}{Environment.NewLine}";

            //pad first 4 bytes for later use
            var buffer = Encoding.ASCII.GetBytes($"xxxx{command}");

            //intial 4 characters as per standard
            buffer[0] = byte.Parse("255");
            buffer[1] = byte.Parse("255");
            buffer[2] = byte.Parse("255");
            buffer[3] = byte.Parse("255");

            //send rcon command and get response
            await client.SendAsync(buffer, buffer.Length);

            if (skipReceive) return "";
            
            //big enough to receive response
            var bufferRec = await client.ReceiveAsync();

            client.Close();

            return Encoding.ASCII.GetString(bufferRec.Buffer)?
                .Replace(password, "[PASSWORD]")
                .Replace("????print\n", "")
                .Replace("\0", "");
        }
    }
}
